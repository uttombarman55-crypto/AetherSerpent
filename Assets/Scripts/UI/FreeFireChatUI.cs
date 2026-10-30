using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Social;
using AetherSerpent.Account;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Free Fire-style Tactical Esport Real-Time Chat Interface.
    /// Provides:
    /// - Multi-channel tabs: All, Global (with slow mode), Squad, Clan, Match, Whisper
    /// - Color-coded message stream with timestamps, Clan & Squad tags
    /// - 1-Click Emoji tray (🔥, 🐍, ⚡, 👑, 💀, 🛡️, 💎, 🐉, ⚔️, 🏆, ❤️, ✨)
    /// - @Mention highlighting and notification badge
    /// - Sender action popup: Whisper, Mute/Unmute, Copy UID
    /// - In-world 3D floating chat speech bubbles above player characters
    /// - Mobile-friendly floating minimized preview bar with tap-to-expand
    /// Toggleable via Enter, 'C', or chat icon.
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeFireChatUI : MonoBehaviour
    {
        public static FreeFireChatUI Instance { get; private set; }

        public bool IsVisible { get; private set; } = false;

        private ChatChannel activeChannel = ChatChannel.All;

        private string chatInputText = "";
        private string whisperTargetName = "";
        private string whisperTargetId = "";
        private int whisperTargetNumericId = 0;

        private Vector2 messageScrollPos;
        private string toastFeedback = "";
        private float toastTimer = 0f;

        // Context Action Popup on Sender click
        private bool showUserActions = false;
        private string selectedSenderId = "";
        private string selectedSenderName = "";
        private int selectedSenderNumericId = 0;
        private Rect userActionPopupRect;

        // Unread mention notification counter
        private int unreadMentionCount = 0;

        // 3D In-World Chat Bubble System
        private class ActiveChatBubble
        {
            public string senderName;
            public string text;
            public Color channelColor;
            public float timeRemaining;
            public Vector3 worldPosition;
        }
        private List<ActiveChatBubble> activeBubbles = new List<ActiveChatBubble>();

        // Emoji list for quick picker
        private static readonly string[] QuickEmojis = {
            "🔥", "🐍", "⚡", "👑", "💀", "🛡️", "💎", "🐉", "⚔️", "🏆", "❤️", "✨"
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

        private void Start()
        {
            if (ChatManager.Instance != null)
            {
                ChatManager.Instance.OnMentionReceived += HandleMentionNotification;
                ChatManager.Instance.OnChatBubbleTriggered += HandleChatBubble;
            }
        }

        private void Update()
        {
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) toastFeedback = "";
            }

            // Update in-world chat bubbles
            for (int i = activeBubbles.Count - 1; i >= 0; i--)
            {
                activeBubbles[i].timeRemaining -= Time.unscaledDeltaTime;
                if (activeBubbles[i].timeRemaining <= 0f)
                {
                    activeBubbles.RemoveAt(i);
                }
            }

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    if (!IsVisible)
                    {
                        OpenChat();
                    }
                    else if (!string.IsNullOrWhiteSpace(chatInputText))
                    {
                        SendMessage();
                    }
                }
                else if (kb.cKey.wasPressedThisFrame && !IsVisible)
                {
                    ToggleVisibility();
                }
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!IsVisible)
                {
                    OpenChat();
                }
                else if (!string.IsNullOrWhiteSpace(chatInputText))
                {
                    SendMessage();
                }
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.C) && !IsVisible)
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
                unreadMentionCount = 0;
            }
        }

        public void OpenChat(ChatChannel channel = ChatChannel.All)
        {
            IsVisible = true;
            activeChannel = channel;
            unreadMentionCount = 0;
        }

        public void StartWhisper(string targetId, string targetName, int targetNumId = 0)
        {
            IsVisible = true;
            activeChannel = ChatChannel.Whisper;
            whisperTargetId = targetId;
            whisperTargetName = targetName;
            whisperTargetNumericId = targetNumId;
            ShowToast($"Whispering to {targetName}");
        }

        private void HandleMentionNotification(ChatMessageItem item)
        {
            if (!IsVisible)
            {
                unreadMentionCount++;
                ShowToast($"<b>[MENTION]</b> {item.senderName} mentioned you in {item.GetChannelPrefix()}!");
            }
        }

        private void HandleChatBubble(ChatMessageItem item)
        {
            if (ChatManager.Instance != null && !ChatManager.Instance.ShowChatBubbles) return;

            // Try to find serpent head or camera target
            Vector3 spawnPos = Vector3.up * 2.5f;
            var serpent = FindFirstObjectByType<AetherSerpent.Serpent.SerpentController>();
            if (serpent != null)
            {
                spawnPos = serpent.transform.position + Vector3.up * 2.2f;
            }

            Color cColor = Color.white;
            ColorUtility.TryParseHtmlString(item.GetChannelColorHex(), out cColor);

            activeBubbles.Add(new ActiveChatBubble
            {
                senderName = item.senderName,
                text = item.message,
                channelColor = cColor,
                timeRemaining = 4.0f,
                worldPosition = spawnPos
            });

            if (activeBubbles.Count > 4) activeBubbles.RemoveAt(0);
        }

        private void ShowToast(string msg, float duration = 3.0f)
        {
            toastFeedback = msg;
            toastTimer = duration;
        }

        private void OnGUI()
        {
            // 1. Draw In-World 3D Speech Bubbles
            DrawWorldChatBubbles();

            // 2. Draw Minimized Bottom-Left Preview Bar if chat is closed
            if (!IsVisible)
            {
                DrawMinimizedPreviewPill();
                return;
            }

            // 3. Draw Full Tactical Chat Panel
            DrawFullChatWindow();

            // 4. Draw User Action Modal if open
            if (showUserActions)
            {
                DrawUserActionPopup();
            }
        }

        // =========================================================
        // 1. IN-WORLD 3D CHAT BUBBLES
        // =========================================================

        private void DrawWorldChatBubbles()
        {
            if (Camera.main == null || activeBubbles.Count == 0) return;

            Camera cam = Camera.main;

            for (int i = 0; i < activeBubbles.Count; i++)
            {
                var bubble = activeBubbles[i];
                Vector3 screenPos = cam.WorldToScreenPoint(bubble.worldPosition + Vector3.up * (i * 0.4f));

                // If behind camera, skip
                if (screenPos.z <= 0f) continue;

                float guiY = Screen.height - screenPos.y;
                float bubbleW = Mathf.Clamp(bubble.text.Length * 9 + 40, 120, 280);
                float bubbleH = 34;

                Rect bRect = new Rect(screenPos.x - bubbleW * 0.5f, guiY - bubbleH, bubbleW, bubbleH);

                // Alpha fade out in last second
                float alpha = Mathf.Clamp01(bubble.timeRemaining);
                GUI.color = new Color(0.1f, 0.12f, 0.16f, 0.92f * alpha);
                GUI.Box(bRect, "");

                GUI.color = new Color(bubble.channelColor.r, bubble.channelColor.g, bubble.channelColor.b, alpha);
                GUILayout.BeginArea(new Rect(bRect.x + 6, bRect.y + 4, bRect.width - 12, bRect.height - 8));
                GUILayout.Label($"<b>{bubble.senderName}:</b> {bubble.text}");
                GUILayout.EndArea();
                GUI.color = Color.white;
            }
        }

        // =========================================================
        // 2. MINIMIZED MOBILE-FRIENDLY BOTTOM-LEFT PREVIEW PILL
        // =========================================================

        private void DrawMinimizedPreviewPill()
        {
            var chatMgr = ChatManager.Instance;
            var msgs = chatMgr != null ? chatMgr.AllMessages : new List<ChatMessageItem>();

            float pillW = Mathf.Min(340, Screen.width * 0.6f);
            float pillH = 50;
            Rect pillRect = new Rect(15, Screen.height - pillH - 15, pillW, pillH);

            GUI.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
            GUI.Box(pillRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(pillRect.x + 8, pillRect.y + 4, pillRect.width - 16, pillRect.height - 8));
            GUILayout.BeginHorizontal();

            // Latest message preview
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (msgs.Count > 0)
            {
                var lastMsg = msgs[msgs.Count - 1];
                string col = lastMsg.GetChannelColorHex();
                GUILayout.Label($"<size=10><color={col}><b>{lastMsg.GetChannelPrefix()} {lastMsg.senderName}:</b> {lastMsg.message}</color></size>");
            }
            else
            {
                GUILayout.Label("<size=10><i>Press Enter or tap to open chat...</i></size>");
            }
            GUILayout.EndVertical();

            // Chat Open Button with Mention Badge
            string btnLabel = "CHAT [C]";
            if (unreadMentionCount > 0)
            {
                GUI.color = Color.yellow;
                btnLabel = $"CHAT ({unreadMentionCount})";
            }
            if (GUILayout.Button(btnLabel, GUILayout.Width(75), GUILayout.Height(38)))
            {
                ToggleVisibility();
            }
            GUI.color = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // =========================================================
        // 3. FULL TACTICAL CHAT WINDOW
        // =========================================================

        private void DrawFullChatWindow()
        {
            GUI.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0.96f);

            float w = Mathf.Min(620, Screen.width - 30);
            float h = Mathf.Min(460, Screen.height - 40);
            float x = 15;
            float y = Screen.height - h - 15;
            Rect winRect = new Rect(x, y, w, h);

            GUI.Box(winRect, "");

            GUILayout.BeginArea(new Rect(winRect.x + 12, winRect.y + 8, winRect.width - 24, winRect.height - 16));

            // Top Header: Title, Bubbles toggle, Close button
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>AETHERSERPENT // REAL-TIME CHAT</b>", GUILayout.ExpandWidth(true));

            if (ChatManager.Instance != null)
            {
                bool bubbles = ChatManager.Instance.ShowChatBubbles;
                if (GUILayout.Button(bubbles ? "BUBBLES: ON" : "BUBBLES: OFF", GUILayout.Width(100), GUILayout.Height(20)))
                {
                    ChatManager.Instance.ShowChatBubbles = !bubbles;
                }
            }

            if (GUILayout.Button("✕ CLOSE", GUILayout.Width(65), GUILayout.Height(20)))
            {
                IsVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Channel Selector Tabs
            DrawChannelTabs();
            GUILayout.Space(6);

            // Message Stream Area
            DrawMessageStream(winRect.height - 170);
            GUILayout.Space(4);

            // Whisper Target Bar (if Whisper channel selected)
            if (activeChannel == ChatChannel.Whisper)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                string targetLabel = !string.IsNullOrEmpty(whisperTargetName) ? $"Whispering to: <b><color=#ff4081>{whisperTargetName}</color></b>" : "Enter recipient Name or UID:";
                GUILayout.Label(targetLabel, GUILayout.ExpandWidth(true));
                if (!string.IsNullOrEmpty(whisperTargetName))
                {
                    if (GUILayout.Button("CHANGE", GUILayout.Width(70), GUILayout.Height(20)))
                    {
                        whisperTargetName = "";
                        whisperTargetId = "";
                    }
                }
                else
                {
                    whisperTargetName = GUILayout.TextField(whisperTargetName, 20, GUILayout.Width(140), GUILayout.Height(20));
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // Quick Emoji Picker Bar
            DrawEmojiBar();
            GUILayout.Space(4);

            // Input Row: Text Field + Slow Mode indicator + Send Button
            DrawInputRow();

            // Status feedback toast
            if (!string.IsNullOrEmpty(toastFeedback))
            {
                GUILayout.Space(2);
                GUI.color = Color.yellow;
                GUILayout.Label($"<size=10><b>{toastFeedback}</b></size>", GUILayout.Height(16));
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        private void DrawChannelTabs()
        {
            GUILayout.BeginHorizontal();

            if (DrawTab("ALL", activeChannel == ChatChannel.All, "#ffffff")) activeChannel = ChatChannel.All;
            if (DrawTab("GLOBAL", activeChannel == ChatChannel.Global, "#ffb300")) activeChannel = ChatChannel.Global;
            if (DrawTab("SQUAD", activeChannel == ChatChannel.Squad, "#00e5ff")) activeChannel = ChatChannel.Squad;
            if (DrawTab("CLAN", activeChannel == ChatChannel.Clan, "#e040fb")) activeChannel = ChatChannel.Clan;
            if (DrawTab("MATCH", activeChannel == ChatChannel.Match, "#00e676")) activeChannel = ChatChannel.Match;
            if (DrawTab("WHISPER", activeChannel == ChatChannel.Whisper, "#ff4081")) activeChannel = ChatChannel.Whisper;

            GUILayout.EndHorizontal();
        }

        private bool DrawTab(string label, bool active, string hexColor)
        {
            Color tabColor;
            ColorUtility.TryParseHtmlString(hexColor, out tabColor);

            if (active) GUI.color = tabColor;
            bool clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.color = Color.white;
            return clicked;
        }

        private void DrawMessageStream(float height)
        {
            var chatMgr = ChatManager.Instance;
            List<ChatMessageItem> msgs = chatMgr != null ? chatMgr.AllMessages : new List<ChatMessageItem>();

            if (chatMgr != null)
            {
                switch (activeChannel)
                {
                    case ChatChannel.Global: msgs = chatMgr.GlobalMessages; break;
                    case ChatChannel.Squad: msgs = chatMgr.SquadMessages; break;
                    case ChatChannel.Clan: msgs = chatMgr.ClanMessages; break;
                    case ChatChannel.Match: msgs = chatMgr.MatchMessages; break;
                    case ChatChannel.Whisper: msgs = chatMgr.WhisperMessages; break;
                }
            }

            string myUsername = AccountManager.Instance != null ? AccountManager.Instance.Username : "";

            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            messageScrollPos = GUILayout.BeginScrollView(messageScrollPos, GUILayout.Height(height));

            if (msgs.Count == 0)
            {
                GUILayout.Label($"<i>No messages in {activeChannel} channel yet. Be the first to say something!</i>");
            }
            else
            {
                foreach (var msg in msgs)
                {
                    GUILayout.BeginHorizontal();

                    // Sender clickable button to open context menu
                    string sColor = msg.GetChannelColorHex();
                    if (GUILayout.Button($"<color={sColor}>[{msg.GetFormattedTimestamp()}] {msg.GetChannelPrefix()}</color>", GUI.skin.label, GUILayout.Width(110)))
                    {
                        OpenUserActions(msg.senderId, msg.senderName, msg.senderNumericId);
                    }

                    // Message text
                    string formattedLine = msg.GetFormattedLine(myUsername);
                    if (GUILayout.Button(formattedLine, GUI.skin.label, GUILayout.ExpandWidth(true)))
                    {
                        OpenUserActions(msg.senderId, msg.senderName, msg.senderNumericId);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawEmojiBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=10><b>EMOJIS:</b></size>", GUILayout.Width(50));
            foreach (var emoji in QuickEmojis)
            {
                if (GUILayout.Button(emoji, GUILayout.Width(28), GUILayout.Height(22)))
                {
                    chatInputText += emoji;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInputRow()
        {
            GUILayout.BeginHorizontal();

            // Input field
            chatInputText = GUILayout.TextField(chatInputText, 180, GUILayout.ExpandWidth(true), GUILayout.Height(28));

            // Slow mode indicator
            var chatMgr = ChatManager.Instance;
            if (activeChannel == ChatChannel.Global && chatMgr != null && chatMgr.GlobalSlowModeCooldown > 0f)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"⏳ {chatMgr.GlobalSlowModeCooldown:F1}s", GUILayout.Width(50), GUILayout.Height(28));
                GUI.color = Color.white;
            }

            // Send button
            GUI.color = Color.green;
            if (GUILayout.Button("<b>SEND</b>", GUILayout.Width(80), GUILayout.Height(28)))
            {
                SendMessage();
            }
            GUI.color = Color.white;

            GUILayout.EndHorizontal();
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(chatInputText)) return;
            string text = chatInputText.Trim();
            chatInputText = "";

            var chatMgr = ChatManager.Instance;
            if (chatMgr == null) return;

            // Check for whisper command: /w <name_or_uid> <message>
            if (text.StartsWith("/w ", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/whisper ", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = text.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    string target = parts[1];
                    string msg = parts[2];
                    int targetNum = 0;
                    int.TryParse(target, out targetNum);

                    var (wOk, wErr) = await chatMgr.SendMessageAsync(
                        ChatChannel.Whisper,
                        msg,
                        recipientNumericId: targetNum,
                        recipientName: targetNum == 0 ? target : null
                    );

                    if (!wOk) ShowToast($"Whisper failed: {wErr}");
                    return;
                }
            }

            // Send to active channel
            ChatChannel targetCh = activeChannel == ChatChannel.All ? ChatChannel.Global : activeChannel;

            var (ok, err) = await chatMgr.SendMessageAsync(
                targetCh,
                text,
                recipientId: whisperTargetId,
                recipientNumericId: whisperTargetNumericId,
                recipientName: whisperTargetName
            );

            if (!ok)
            {
                ShowToast(err);
            }
            else
            {
                // Auto scroll to bottom
                messageScrollPos.y = 99999f;
            }
        }

        // =========================================================
        // 4. USER ACTION POPUP (WHISPER, MUTE, COPY UID)
        // =========================================================

        private void OpenUserActions(string pId, string uName, int numId)
        {
            string myId = AccountManager.Instance != null ? AccountManager.Instance.PlayerId : "";
            if (pId == myId) return; // Don't popup on self

            selectedSenderId = pId;
            selectedSenderName = uName;
            selectedSenderNumericId = numId;
            showUserActions = true;
        }

        private void DrawUserActionPopup()
        {
            Rect popupRect = new Rect(Screen.width * 0.5f - 120, Screen.height * 0.5f - 90, 240, 180);
            GUI.color = new Color(0.12f, 0.14f, 0.2f, 0.98f);
            GUI.Box(popupRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(popupRect.x + 12, popupRect.y + 10, popupRect.width - 24, popupRect.height - 20));

            GUILayout.Label($"<b>PLAYER: {selectedSenderName}</b>");
            if (selectedSenderNumericId > 0)
            {
                GUILayout.Label($"<color=#00e5ff>UID: {selectedSenderNumericId}</color>");
            }

            GUILayout.Space(6);

            if (GUILayout.Button("💬 PRIVATE WHISPER", GUILayout.Height(24)))
            {
                StartWhisper(selectedSenderId, selectedSenderName, selectedSenderNumericId);
                showUserActions = false;
            }

            if (selectedSenderNumericId > 0)
            {
                if (GUILayout.Button("📋 COPY UID", GUILayout.Height(24)))
                {
                    GUIUtility.systemCopyBuffer = selectedSenderNumericId.ToString();
                    ShowToast($"Copied UID {selectedSenderNumericId} to clipboard!");
                    showUserActions = false;
                }
            }

            var chatMgr = ChatManager.Instance;
            bool isMuted = chatMgr != null && chatMgr.IsMuted(selectedSenderId);
            GUI.color = isMuted ? Color.green : new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button(isMuted ? "UNMUTE PLAYER" : "MUTE PLAYER", GUILayout.Height(24)))
            {
                if (isMuted) chatMgr.UnmutePlayer(selectedSenderId);
                else chatMgr.MutePlayer(selectedSenderId);
                ShowToast(isMuted ? $"Unmuted {selectedSenderName}." : $"Muted {selectedSenderName}. Messages hidden.");
                showUserActions = false;
            }
            GUI.color = Color.white;

            GUILayout.Space(4);
            if (GUILayout.Button("CANCEL", GUILayout.Height(20)))
            {
                showUserActions = false;
            }

            GUILayout.EndArea();
        }
    }
}

