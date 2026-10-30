using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Voice;
using AetherSerpent.Account;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Free Fire-style Tactical Esport Voice Chat Interface.
    /// Provides:
    /// - Always-accessible Quick Voice Bar (Mic Mute, Deafen, Channel, Speaking Waveform)
    /// - Tactical Voice Modal (Roster, Volume Sliders, Individual Mute, Report Abuse)
    /// - Audio Settings (Microphone selection, animated VU meter, VAD threshold, PTT keybind)
    /// - Report Voice Abuse popup
    /// Toggleable via 'V' or HUD click.
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeFireVoiceUI : MonoBehaviour
    {
        public static FreeFireVoiceUI Instance { get; private set; }

        public bool IsVisible { get; private set; } = false;

        private enum VoiceTab
        {
            Roster,
            Settings
        }

        private VoiceTab currentTab = VoiceTab.Roster;

        // Reporting State
        private bool isReportModalOpen = false;
        private string reportTargetId = "";
        private string reportTargetName = "";
        private int reportTargetNumericId = 0;
        private string reportReason = "Voice Harassment / Toxicity";
        private string reportDetails = "";
        private string toastMessage = "";
        private float toastTimer = 0f;

        private Vector2 rosterScrollPos;
        private Vector2 settingsScrollPos;

        private static readonly string[] ReportReasons = {
            "Voice Harassment / Toxicity",
            "Loud Noise / Screaming / Mic Spam",
            "Inappropriate Language",
            "Music / Audio Bot Spam"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) toastMessage = "";
            }

            // Hotkey 'V' toggles Voice Modal (unless PTT is active on 'V')
            var engine = VoiceEngine.Instance;
            bool isVKeyForPtt = engine != null &&
                               engine.Settings.inputMode == VoiceInputMode.PushToTalk &&
                               engine.Settings.pttKey == KeyCode.V;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                // If V is PTT, use 'B' or 'O' for overlay, else 'V'
                if (!isVKeyForPtt && kb.vKey.wasPressedThisFrame)
                {
                    ToggleVisibility();
                }
                else if (isVKeyForPtt && kb.bKey.wasPressedThisFrame)
                {
                    ToggleVisibility();
                }
            }
#else
            if (!isVKeyForPtt && Input.GetKeyDown(KeyCode.V))
            {
                ToggleVisibility();
            }
            else if (isVKeyForPtt && Input.GetKeyDown(KeyCode.B))
            {
                ToggleVisibility();
            }
#endif
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            if (IsVisible)
            {
                isReportModalOpen = false;
            }
        }

        public void OpenVoiceSettings()
        {
            IsVisible = true;
            currentTab = VoiceTab.Settings;
            isReportModalOpen = false;
        }

        public void OpenReportModal(string targetId, string targetName, int numericId)
        {
            reportTargetId = targetId;
            reportTargetName = targetName;
            reportTargetNumericId = numericId;
            reportDetails = "";
            isReportModalOpen = true;
        }

        private void ShowToast(string msg)
        {
            toastMessage = msg;
            toastTimer = 3.5f;
        }

        // =========================================================
        // ONGUI RENDERING
        // =========================================================

        private void OnGUI()
        {
            var engine = VoiceEngine.Instance;
            if (engine == null) return;

            DrawQuickVoiceBar(engine);

            if (IsVisible)
            {
                DrawVoiceModal(engine);
            }

            if (isReportModalOpen)
            {
                DrawReportModal(engine);
            }

            if (!string.IsNullOrEmpty(toastMessage) && toastTimer > 0f)
            {
                DrawToastNotification();
            }
        }

        // ---------------------------------------------------------
        // QUICK HUD VOICE BAR
        // ---------------------------------------------------------

        private void DrawQuickVoiceBar(VoiceEngine engine)
        {
            // Positioned at top-center or bottom-right
            Rect barRect = new Rect(Screen.width / 2f - 210, 10, 420, 36);

            GUI.color = new Color(0.08f, 0.1f, 0.15f, 0.92f);
            GUI.Box(barRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(barRect.x + 6, barRect.y + 4, barRect.width - 12, barRect.height - 8));
            GUILayout.BeginHorizontal();

            // Channel Selector Pill
            string channelLabel = engine.CurrentChannel switch
            {
                VoiceChannel.Squad => "<color=#00e5ff><b>[SQUAD]</b></color>",
                VoiceChannel.Match => "<color=#00e676><b>[MATCH]</b></color>",
                _ => "<color=#888888><b>[OFF]</b></color>"
            };

            if (GUILayout.Button(channelLabel, GUILayout.Width(75), GUILayout.Height(28)))
            {
                VoiceChannel nextChannel = engine.CurrentChannel switch
                {
                    VoiceChannel.Off => VoiceChannel.Squad,
                    VoiceChannel.Squad => VoiceChannel.Match,
                    _ => VoiceChannel.Off
                };
                engine.SetVoiceChannel(nextChannel);
                ShowToast($"Voice channel switched to {nextChannel}");
            }

            // Mic Mute Button
            string micLabel;
            if (engine.Settings.isMicMuted)
            {
                micLabel = "<color=#ff1744><b>MIC: MUTED</b></color>";
            }
            else if (engine.IsSpeaking)
            {
                micLabel = "<color=#00e676><b>MIC: TALKING ▃▅▇</b></color>";
            }
            else if (engine.Settings.inputMode == VoiceInputMode.PushToTalk)
            {
                micLabel = $"<color=#ffd600><b>PTT [{engine.Settings.pttKey}]</b></color>";
            }
            else
            {
                micLabel = "<color=#ffffff><b>MIC: OPEN</b></color>";
            }

            if (GUILayout.Button(micLabel, GUILayout.Width(130), GUILayout.Height(28)))
            {
                engine.ToggleMicMute();
                ShowToast(engine.Settings.isMicMuted ? "Microphone Muted" : "Microphone Active");
            }

            // Deafen Button
            string deafenLabel = engine.Settings.isDeafened ?
                "<color=#ff1744><b>DEAFENED</b></color>" :
                "<color=#e0e0e0>DEAFEN</color>";

            if (GUILayout.Button(deafenLabel, GUILayout.Width(85), GUILayout.Height(28)))
            {
                engine.ToggleDeafen();
                ShowToast(engine.Settings.isDeafened ? "Voice Deafen Enabled (All Muted)" : "Voice Deafen Disabled");
            }

            // Voice Modal / Settings Button
            if (GUILayout.Button("<b>VOICE [V]</b>", GUILayout.Width(80), GUILayout.Height(28)))
            {
                ToggleVisibility();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Speaking Banner if anyone is talking
            DrawActiveSpeakerBanner(engine);
        }

        private void DrawActiveSpeakerBanner(VoiceEngine engine)
        {
            string activeSpeakerName = "";
            bool isLocal = false;

            if (engine.IsSpeaking)
            {
                activeSpeakerName = "YOU";
                isLocal = true;
            }
            else
            {
                foreach (var peer in engine.Peers.Values)
                {
                    if (peer.isSpeaking)
                    {
                        activeSpeakerName = peer.username;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(activeSpeakerName))
            {
                Rect speakerRect = new Rect(Screen.width / 2f - 140, 50, 280, 26);
                GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.88f);
                GUI.Box(speakerRect, "");
                GUI.color = isLocal ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(0f, 0.9f, 1f, 1f);

                string wave = (Time.frameCount % 20 < 10) ? " ▃▅▇" : " ▇▅▃";
                GUI.Label(speakerRect, $"  🔊 <b>{activeSpeakerName} is speaking...{wave}</b>");
                GUI.color = Color.white;
            }
        }

        // ---------------------------------------------------------
        // MAIN TACTICAL VOICE MODAL
        // ---------------------------------------------------------

        private void DrawVoiceModal(VoiceEngine engine)
        {
            float modalWidth = Mathf.Min(600, Screen.width - 40);
            float modalHeight = Mathf.Min(500, Screen.height - 80);
            Rect modalRect = new Rect(Screen.width / 2f - modalWidth / 2f, Screen.height / 2f - modalHeight / 2f, modalWidth, modalHeight);

            // Modal Background
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.97f);
            GUI.Box(modalRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(modalRect.x + 14, modalRect.y + 12, modalRect.width - 28, modalRect.height - 24));

            // Header Banner
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=17><color=#00e5ff>AETHERSERPENT // TACTICAL VOICE COMMS</color></size></b>");
            if (GUILayout.Button("<b>✕ CLOSE</b>", GUILayout.Width(80), GUILayout.Height(26)))
            {
                IsVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Channel Selector Buttons
            GUILayout.BeginHorizontal();
            GUI.color = engine.CurrentChannel == VoiceChannel.Squad ? new Color(0f, 0.9f, 1f) : Color.white;
            if (GUILayout.Button("SQUAD VOICE (FIRETEAM)", GUILayout.Height(30)))
            {
                engine.SetVoiceChannel(VoiceChannel.Squad);
            }

            GUI.color = engine.CurrentChannel == VoiceChannel.Match ? new Color(0f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button("MATCH / PROXIMITY VOICE", GUILayout.Height(30)))
            {
                engine.SetVoiceChannel(VoiceChannel.Match);
            }

            GUI.color = engine.CurrentChannel == VoiceChannel.Off ? new Color(1f, 0.3f, 0.3f) : Color.white;
            if (GUILayout.Button("DISABLE VOICE", GUILayout.Height(30)))
            {
                engine.SetVoiceChannel(VoiceChannel.Off);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Navigation Tabs
            GUILayout.BeginHorizontal();
            GUI.color = currentTab == VoiceTab.Roster ? new Color(0f, 0.9f, 1f) : Color.gray;
            if (GUILayout.Button($"VOICE ROSTER ({engine.Peers.Count + 1})", GUILayout.Height(28)))
            {
                currentTab = VoiceTab.Roster;
            }

            GUI.color = currentTab == VoiceTab.Settings ? new Color(0f, 0.9f, 1f) : Color.gray;
            if (GUILayout.Button("AUDIO & DEVICE SETTINGS", GUILayout.Height(28)))
            {
                currentTab = VoiceTab.Settings;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Tab Content
            if (currentTab == VoiceTab.Roster)
            {
                DrawRosterTab(engine);
            }
            else
            {
                DrawSettingsTab(engine);
            }

            GUILayout.EndArea();
        }

        private void DrawRosterTab(VoiceEngine engine)
        {
            rosterScrollPos = GUILayout.BeginScrollView(rosterScrollPos);

            // 1. Local Player Item
            var acc = AccountManager.Instance;
            string localName = acc != null ? acc.Username : "You";
            int localId = acc != null ? acc.NumericId : 0;

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();

            string statusDot = engine.IsSpeaking ? "<color=#00e676>●</color>" : "<color=#888888>○</color>";
            string micStatus = engine.Settings.isMicMuted ? "<color=#ff1744>[MUTED]</color>" :
                (engine.IsSpeaking ? "<color=#00e676>[TALKING]</color>" : "<color=#ffffff>[IDLE]</color>");

            GUILayout.Label($"{statusDot} <b>{localName} (You)</b> - UID: {localId}  {micStatus}", GUILayout.Width(300));

            if (GUILayout.Button(engine.Settings.isMicMuted ? "UNMUTE" : "MUTE", GUILayout.Width(75), GUILayout.Height(24)))
            {
                engine.ToggleMicMute();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // 2. Remote Peers
            if (engine.Peers.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.Label("<color=#888888><i>No other players connected to this voice channel. Invite squad members to chat!</i></color>");
            }
            else
            {
                foreach (var peer in engine.Peers.Values)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();

                    string peerDot = peer.isSpeaking ? "<color=#00e676>●</color>" : "<color=#888888>○</color>";
                    string peerStatus = peer.isLocallyMuted ? "<color=#ff1744>[MUTED BY YOU]</color>" :
                        (peer.isSpeaking ? "<color=#00e676>[TALKING]</color>" : "<color=#888888>[LISTENING]</color>");

                    GUILayout.Label($"{peerDot} <b>{peer.username}</b> (UID: {peer.numericId}) {peerStatus}", GUILayout.Width(260));

                    // Individual Mute Toggle
                    if (GUILayout.Button(peer.isLocallyMuted ? "UNMUTE" : "MUTE", GUILayout.Width(70), GUILayout.Height(24)))
                    {
                        engine.SetPeerMute(peer.playerId, !peer.isLocallyMuted);
                    }

                    // Report Button
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("REPORT", GUILayout.Width(65), GUILayout.Height(24)))
                    {
                        OpenReportModal(peer.playerId, peer.username, peer.numericId);
                    }
                    GUI.color = Color.white;

                    GUILayout.EndHorizontal();

                    // Per-Player Volume Slider
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Player Volume: {Mathf.RoundToInt(peer.userVolumeScale * 100f)}%", GUILayout.Width(130));
                    float newVol = GUILayout.HorizontalSlider(peer.userVolumeScale, 0f, 2f);
                    if (Mathf.Abs(newVol - peer.userVolumeScale) > 0.01f)
                    {
                        engine.SetPeerVolume(peer.playerId, newVol);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawSettingsTab(VoiceEngine engine)
        {
            settingsScrollPos = GUILayout.BeginScrollView(settingsScrollPos);

            // 1. Microphone Hardware Selector
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>MICROPHONE HARDWARE INPUT</b>");

            string[] mics = engine.GetAvailableMicrophones();
            if (mics.Length == 0)
            {
                GUILayout.Label("<color=#ff1744>No microphones detected on system.</color>");
            }
            else
            {
                string currentMic = string.IsNullOrEmpty(engine.Settings.selectedMicrophone) ? mics[0] : engine.Settings.selectedMicrophone;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Device: <b>{currentMic}</b>", GUILayout.Width(320));

                if (GUILayout.Button("CHANGE DEVICE", GUILayout.Width(130), GUILayout.Height(26)))
                {
                    int currIdx = Array.IndexOf(mics, currentMic);
                    int nextIdx = (currIdx + 1) % mics.Length;
                    engine.SetMicrophone(mics[nextIdx]);
                    ShowToast($"Microphone switched to {mics[nextIdx]}");
                }
                GUILayout.EndHorizontal();
            }

            // Realtime VU Level Meter
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mic Input Level:", GUILayout.Width(110));
            int bars = Mathf.Clamp(Mathf.RoundToInt(engine.CurrentMicRms * 20), 0, 20);
            string vuMeter = new string('█', bars) + new string('░', 20 - bars);
            string vuColor = bars > 5 ? "<color=#00e676>" : "<color=#888888>";
            GUILayout.Label($"{vuColor}<b>[{vuMeter}]</b></color>");
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 2. Input Mode & Keybinds
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>VOICE INPUT MODE</b>");

            GUILayout.BeginHorizontal();
            bool isOpenMic = engine.Settings.inputMode == VoiceInputMode.OpenMic;
            if (GUILayout.Toggle(isOpenMic, " Open Mic (Voice Activity Detection)") != isOpenMic)
            {
                engine.SetInputMode(isOpenMic ? VoiceInputMode.PushToTalk : VoiceInputMode.OpenMic);
            }

            bool isPtt = engine.Settings.inputMode == VoiceInputMode.PushToTalk;
            if (GUILayout.Toggle(isPtt, $" Push-to-Talk [{engine.Settings.pttKey}]") != isPtt)
            {
                engine.SetInputMode(isPtt ? VoiceInputMode.OpenMic : VoiceInputMode.PushToTalk);
            }
            GUILayout.EndHorizontal();

            if (isPtt)
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("PTT Keybind:", GUILayout.Width(100));
                if (GUILayout.Button("V Key", GUILayout.Width(60))) engine.SetPttKey(KeyCode.V);
                if (GUILayout.Button("B Key", GUILayout.Width(60))) engine.SetPttKey(KeyCode.B);
                if (GUILayout.Button("T Key", GUILayout.Width(60))) engine.SetPttKey(KeyCode.T);
                if (GUILayout.Button("Alt Key", GUILayout.Width(60))) engine.SetPttKey(KeyCode.LeftAlt);
                GUILayout.EndHorizontal();
            }

            // VAD Sensitivity Slider
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Voice Sensitivity (VAD Threshold): {Mathf.RoundToInt((0.1f - engine.Settings.vadThreshold) * 1000f)}", GUILayout.Width(260));
            float newVad = GUILayout.HorizontalSlider(engine.Settings.vadThreshold, 0.002f, 0.08f);
            if (Mathf.Abs(newVad - engine.Settings.vadThreshold) > 0.001f)
            {
                engine.SetVadThreshold(newVad);
            }
            GUILayout.EndHorizontal();

            // Microphone Gain
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Microphone Gain: {Mathf.RoundToInt(engine.Settings.micGain * 100f)}%", GUILayout.Width(260));
            float newGain = GUILayout.HorizontalSlider(engine.Settings.micGain, 0.5f, 2.5f);
            if (Mathf.Abs(newGain - engine.Settings.micGain) > 0.02f)
            {
                engine.SetMicGain(newGain);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 3. Audio Output & DSP Processing
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>DSP FILTERS & MASTER AUDIO</b>");

            // Master Voice Volume
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Master Voice Volume: {Mathf.RoundToInt(engine.Settings.masterVoiceVolume * 100f)}%", GUILayout.Width(260));
            float newMaster = GUILayout.HorizontalSlider(engine.Settings.masterVoiceVolume, 0f, 2f);
            if (Mathf.Abs(newMaster - engine.Settings.masterVoiceVolume) > 0.02f)
            {
                engine.SetMasterVoiceVolume(newMaster);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            bool newNoise = GUILayout.Toggle(engine.Settings.noiseSuppressionEnabled, " Enable Hardware Noise Gate & Ambient Suppression");
            if (newNoise != engine.Settings.noiseSuppressionEnabled)
            {
                engine.Settings.noiseSuppressionEnabled = newNoise;
                engine.SaveSettings();
            }

            bool newEcho = GUILayout.Toggle(engine.Settings.echoCancellationEnabled, " Enable Acoustic Echo Cancellation (AEC)");
            if (newEcho != engine.Settings.echoCancellationEnabled)
            {
                engine.Settings.echoCancellationEnabled = newEcho;
                engine.SaveSettings();
            }

            bool newSpatial = GUILayout.Toggle(engine.Settings.spatialProximityEnabled, " Enable 3D Positional Proximity Rolloff (In Match)");
            if (newSpatial != engine.Settings.spatialProximityEnabled)
            {
                engine.Settings.spatialProximityEnabled = newSpatial;
                engine.SaveSettings();
            }

            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }

        // ---------------------------------------------------------
        // REPORT VOICE ABUSE MODAL
        // ---------------------------------------------------------

        private void DrawReportModal(VoiceEngine engine)
        {
            Rect rModal = new Rect(Screen.width / 2f - 220, Screen.height / 2f - 180, 440, 360);

            GUI.color = new Color(0.08f, 0.04f, 0.06f, 0.98f);
            GUI.Box(rModal, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(rModal.x + 16, rModal.y + 16, rModal.width - 32, rModal.height - 32));

            GUILayout.Label("<b><size=15><color=#ff1744>REPORT VOICE HARASSMENT / ABUSE</color></size></b>");
            GUILayout.Label($"Player: <b>{reportTargetName}</b> (UID: {reportTargetNumericId})");

            GUILayout.Space(10);
            GUILayout.Label("<b>Select Violation Category:</b>");
            foreach (var reason in ReportReasons)
            {
                if (GUILayout.Toggle(reportReason == reason, $" {reason}"))
                {
                    reportReason = reason;
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("Optional Details:");
            reportDetails = GUILayout.TextField(reportDetails, 150, GUILayout.Height(40));

            GUILayout.Space(14);
            GUILayout.BeginHorizontal();

            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button("SUBMIT REPORT", GUILayout.Height(32)))
            {
                engine.ReportPlayerVoice(reportTargetId, reportReason, reportDetails);
                engine.SetPeerMute(reportTargetId, true);
                isReportModalOpen = false;
                ShowToast($"Report filed against {reportTargetName}. Player automatically muted.");
            }
            GUI.color = Color.white;

            if (GUILayout.Button("CANCEL", GUILayout.Height(32)))
            {
                isReportModalOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawToastNotification()
        {
            Rect toastRect = new Rect(Screen.width / 2f - 180, Screen.height - 60, 360, 32);
            GUI.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            GUI.Box(toastRect, "");
            GUI.color = new Color(0f, 0.9f, 1f, 1f);
            GUI.Label(toastRect, $"  ⓘ {toastMessage}");
            GUI.color = Color.white;
        }
    }
}

