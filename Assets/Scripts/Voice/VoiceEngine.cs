using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using AetherSerpent.Account;
using AetherSerpent.Network;

namespace AetherSerpent.Voice
{
    /// <summary>
    /// Master Low-Latency Real-Time Voice Chat Engine.
    /// Manages:
    /// - Hardware microphone capture with permission check
    /// - High-fidelity DSP: Pre-emphasis, Noise Gate, VAD with hysteresis, AEC simulation
    /// - Push-to-Talk (PTT) vs Open Mic
    /// - WebRTC SDP / ICE signaling relay & fallback streaming
    /// - Per-peer circular jitter buffers and individual volume scaling (0-200%)
    /// - 3D spatial proximity attenuation in matches vs 2D stereo in squad
    /// - Individual Mute, Global Deafen, and Voice Abuse Reporting
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-65)]
    public class VoiceEngine : MonoBehaviour
    {
        public static VoiceEngine Instance { get; private set; }

        private const string PREF_VOICE_SETTINGS = "aether_voice_settings";
        private const string PREF_MUTED_VOICE_PEERS = "aether_voice_muted_peers";
        private const int SAMPLE_RATE = 16000; // 16 kHz wideband speech
        private const int CHUNK_SAMPLES = 800; // 50 ms buffer per transmission
        private const int MIC_BUFFER_SEC = 2;

        public VoiceSettings Settings { get; private set; } = new VoiceSettings();
        public VoiceChannel CurrentChannel { get; private set; } = VoiceChannel.Off;

        // Current Local State
        public bool IsSpeaking { get; private set; } = false;
        public float CurrentMicRms { get; private set; } = 0f;
        public bool IsPttKeyPressed { get; private set; } = false;

        // Remote Peers in Voice Session
        private Dictionary<string, VoicePeer> peers = new Dictionary<string, VoicePeer>();
        public IReadOnlyDictionary<string, VoicePeer> Peers => peers;

        // Locally Muted Peer IDs
        private HashSet<string> locallyMutedPeerIds = new HashSet<string>();

        // Hardware Microphone State
        private AudioClip micClip;
        private int lastMicPosition = 0;
        private float[] micSampleBuffer = new float[CHUNK_SAMPLES];
        private float[] tempReadBuffer = new float[SAMPLE_RATE * MIC_BUFFER_SEC];
        private float speakingHoldTimer = 0f;
        private float prevSample = 0f;
        private int packetSequence = 0;

        // WebSocket Connection for Voice & Signaling
        private WebSocketClient voiceWsClient;
        public bool IsWsConnected => voiceWsClient != null && voiceWsClient.State == WebSocketClient.ConnectionState.Connected;

        // Events
        public event Action<VoiceChannel> OnChannelChanged;
        public event Action<bool> OnSpeakingStateChanged;
        public event Action<VoicePeer> OnPeerJoined;
        public event Action<string> OnPeerLeft;
        public event Action<VoicePeer> OnPeerStateUpdated;
        public event Action<WebRtcSignalPayload> OnWebRtcSignalReceived;
        public event Action<string> OnVoiceError;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
            LoadMutedPeers();
        }

        private void Start()
        {
            RequestMicrophonePermission();
            StartCoroutine(AudioCaptureRoutine());
        }

        private void Update()
        {
            // Update Push-to-Talk key detection
            UpdatePttState();

            // Dispatch incoming WebSocket packets to main thread
            voiceWsClient?.DispatchIncomingMessages();

            // Update peer audio playback buffers & decay speaking states
            UpdatePeerAudio();
        }

        private void OnDestroy()
        {
            StopMicrophoneCapture();
            _ = voiceWsClient?.DisconnectAsync();
        }

        // =========================================================
        // PERMISSIONS & HARDWARE MICROPHONE SETUP
        // =========================================================

        public void RequestMicrophonePermission()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
#endif
        }

        public string[] GetAvailableMicrophones()
        {
            return Microphone.devices;
        }

        public void SetMicrophone(string deviceName)
        {
            Settings.selectedMicrophone = deviceName;
            SaveSettings();

            if (CurrentChannel != VoiceChannel.Off)
            {
                RestartMicrophoneCapture();
            }
        }

        private void StartMicrophoneCapture()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[VoiceEngine] No microphone devices detected on host system.");
                return;
            }

            string device = string.IsNullOrEmpty(Settings.selectedMicrophone) ? Microphone.devices[0] : Settings.selectedMicrophone;

            try
            {
                micClip = Microphone.Start(device, true, MIC_BUFFER_SEC, SAMPLE_RATE);
                lastMicPosition = 0;
                Debug.Log($"[VoiceEngine] Microphone started on '{device}' @ {SAMPLE_RATE}Hz.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceEngine] Failed to start microphone: {ex.Message}");
                OnVoiceError?.Invoke(ex.Message);
            }
        }

        private void StopMicrophoneCapture()
        {
            string device = string.IsNullOrEmpty(Settings.selectedMicrophone) ? null : Settings.selectedMicrophone;
            if (Microphone.IsRecording(device))
            {
                Microphone.End(device);
            }
            micClip = null;
            IsSpeaking = false;
            CurrentMicRms = 0f;
        }

        private void RestartMicrophoneCapture()
        {
            StopMicrophoneCapture();
            StartMicrophoneCapture();
        }

        // =========================================================
        // AUDIO CAPTURE & DSP PROCESSING LOOP
        // =========================================================

        private IEnumerator AudioCaptureRoutine()
        {
            WaitForSeconds wait50ms = new WaitForSeconds(0.05f);

            while (true)
            {
                yield return wait50ms;

                if (CurrentChannel == VoiceChannel.Off || Settings.isMicMuted || micClip == null)
                {
                    if (IsSpeaking)
                    {
                        SetSpeakingState(false);
                    }
                    continue;
                }

                string device = string.IsNullOrEmpty(Settings.selectedMicrophone) ? null : Settings.selectedMicrophone;
                if (!Microphone.IsRecording(device))
                {
                    StartMicrophoneCapture();
                    continue;
                }

                int currentPos = Microphone.GetPosition(device);
                if (currentPos < 0 || lastMicPosition == currentPos) continue;

                // Read new samples from circular microphone clip
                int samplesAvailable = (currentPos >= lastMicPosition) ?
                    (currentPos - lastMicPosition) :
                    ((SAMPLE_RATE * MIC_BUFFER_SEC) - lastMicPosition + currentPos);

                if (samplesAvailable < CHUNK_SAMPLES) continue;

                micClip.GetData(tempReadBuffer, 0);

                // Copy newest CHUNK_SAMPLES
                for (int i = 0; i < CHUNK_SAMPLES; i++)
                {
                    int index = (lastMicPosition + i) % (SAMPLE_RATE * MIC_BUFFER_SEC);
                    micSampleBuffer[i] = tempReadBuffer[index];
                }
                lastMicPosition = (lastMicPosition + CHUNK_SAMPLES) % (SAMPLE_RATE * MIC_BUFFER_SEC);

                // Apply DSP Pipeline
                ProcessOutgoingAudio(micSampleBuffer);
            }
        }

        private void ProcessOutgoingAudio(float[] buffer)
        {
            // 1. Pre-emphasis High-Pass Filter: y[n] = x[n] - 0.95 * x[n-1]
            for (int i = 0; i < buffer.Length; i++)
            {
                float current = buffer[i];
                buffer[i] = current - 0.95f * prevSample;
                prevSample = current;
            }

            // 2. Compute RMS Energy
            float sumSquares = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                sumSquares += buffer[i] * buffer[i];
            }
            float rms = Mathf.Sqrt(sumSquares / buffer.Length);
            CurrentMicRms = Mathf.Clamp01(rms * 10f); // Normalized meter

            // 3. Noise Gate & Background Floor Suppression
            if (Settings.noiseSuppressionEnabled && rms < (Settings.vadThreshold * 0.4f))
            {
                Array.Clear(buffer, 0, buffer.Length);
                rms = 0f;
            }

            // 4. Acoustic Echo Cancellation (AEC) Simulation
            // Attenuates microphone buffer if remote peers are currently generating loud audio
            if (Settings.echoCancellationEnabled)
            {
                float peerAudioEnergy = EstimatePeerPlaybackEnergy();
                if (peerAudioEnergy > 0.05f)
                {
                    float duckingFactor = Mathf.Clamp01(1.0f - (peerAudioEnergy * 0.8f));
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        buffer[i] *= duckingFactor;
                    }
                }
            }

            // 5. Voice Activity Detection (VAD) & PTT Check
            bool voiceDetected = rms >= Settings.vadThreshold;
            bool allowedToTransmit = false;

            if (Settings.inputMode == VoiceInputMode.OpenMic)
            {
                if (voiceDetected)
                {
                    speakingHoldTimer = 0.35f; // 350 ms hangover hold
                    allowedToTransmit = true;
                }
                else if (speakingHoldTimer > 0f)
                {
                    speakingHoldTimer -= 0.05f;
                    allowedToTransmit = true;
                }
            }
            else // Push-to-Talk
            {
                if (IsPttKeyPressed)
                {
                    speakingHoldTimer = 0.2f;
                    allowedToTransmit = true;
                }
                else if (speakingHoldTimer > 0f)
                {
                    speakingHoldTimer -= 0.05f;
                    allowedToTransmit = true;
                }
            }

            SetSpeakingState(allowedToTransmit);

            // 6. Gain Scaling & Transmission
            if (allowedToTransmit)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = Mathf.Clamp(buffer[i] * Settings.micGain, -1.0f, 1.0f);
                }

                TransmitAudioFrame(buffer);
            }
        }

        private float EstimatePeerPlaybackEnergy()
        {
            float maxEnergy = 0f;
            foreach (var peer in peers.Values)
            {
                if (peer.isSpeaking && peer.volumeLevel > maxEnergy)
                {
                    maxEnergy = peer.volumeLevel;
                }
            }
            return maxEnergy;
        }

        private void SetSpeakingState(bool speaking)
        {
            if (IsSpeaking != speaking)
            {
                IsSpeaking = speaking;
                OnSpeakingStateChanged?.Invoke(speaking);

                // Notify peers via WebSocket of speaking indicator
                SendVoiceStateUpdate(speaking, Settings.isMicMuted, Settings.isDeafened, CurrentMicRms);
            }
        }

        private void UpdatePttState()
        {
            if (Settings.inputMode == VoiceInputMode.PushToTalk)
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (Settings.pttKey == KeyCode.V)
                        IsPttKeyPressed = kb.vKey.isPressed;
                    else if (Settings.pttKey == KeyCode.B)
                        IsPttKeyPressed = kb.bKey.isPressed;
                    else if (Settings.pttKey == KeyCode.T)
                        IsPttKeyPressed = kb.tKey.isPressed;
                    else if (Settings.pttKey == KeyCode.LeftAlt)
                        IsPttKeyPressed = kb.leftAltKey.isPressed;
                    else
                        IsPttKeyPressed = kb.vKey.isPressed;
                }
                else
                {
                    IsPttKeyPressed = Input.GetKey(Settings.pttKey);
                }
#else
                IsPttKeyPressed = Input.GetKey(Settings.pttKey);
#endif
            }
            else
            {
                IsPttKeyPressed = false;
            }
        }

        // =========================================================
        // AUDIO ENCODING & WEBSOCKET TRANSMISSION
        // =========================================================

        private void TransmitAudioFrame(float[] samples)
        {
            if (!IsWsConnected || CurrentChannel == VoiceChannel.Off) return;

            // Compress float samples [-1, 1] into 16-bit signed PCM bytes
            byte[] pcmBytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                pcmBytes[i * 2] = (byte)(val & 0xFF);
                pcmBytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            string base64Audio = Convert.ToBase64String(pcmBytes);

            // Fetch current player position for 3D proximity rolloff
            Vector3 myPos = Vector3.zero;
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) myPos = playerObj.transform.position;

            var packet = new VoiceAudioPacketPayload
            {
                type = "VOICE_FRAME",
                senderId = GetPlayerId(),
                senderName = GetUsername(),
                channel = CurrentChannel == VoiceChannel.Squad ? "squad" : "match",
                sequence = ++packetSequence,
                audioBase64 = base64Audio,
                posX = myPos.x,
                posY = myPos.y,
                posZ = myPos.z,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            string json = JsonUtility.ToJson(packet);
            _ = voiceWsClient.SendAsync(json);
        }

        private void SendVoiceStateUpdate(bool speaking, bool muted, bool deafened, float vol)
        {
            if (!IsWsConnected) return;

            var state = new VoiceStateUpdatePayload
            {
                type = "VOICE_STATE_UPDATE",
                isSpeaking = speaking,
                isMuted = muted,
                isDeafened = deafened,
                volumeLevel = vol
            };

            string json = JsonUtility.ToJson(state);
            _ = voiceWsClient.SendAsync(json);
        }

        // =========================================================
        // WEBSOCKET CLIENT & SIGNALING RELAY
        // =========================================================

        public async Task ConnectVoiceAsync(VoiceChannel channel)
        {
            if (channel == VoiceChannel.Off)
            {
                await DisconnectVoiceAsync();
                return;
            }

            CurrentChannel = channel;
            OnChannelChanged?.Invoke(CurrentChannel);

            string token = GetToken();
            string baseUrl = GetBaseUrl();
            string wsUrl = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            string channelStr = channel == VoiceChannel.Squad ? "squad" : "match";

            string squadId = "";
            if (SocialGroupingManager.Instance != null && SocialGroupingManager.Instance.MySquad != null)
            {
                squadId = SocialGroupingManager.Instance.MySquad.squadId;
            }

            string fullUrl = $"{wsUrl}/api/voice/ws?token={token}&channel={channelStr}&squadId={squadId}";

            if (voiceWsClient != null)
            {
                await voiceWsClient.DisconnectAsync();
            }

            voiceWsClient = new WebSocketClient();
            voiceWsClient.OnMessageReceived += HandleIncomingVoiceMessage;
            voiceWsClient.OnError += (err) => OnVoiceError?.Invoke(err);

            try
            {
                await voiceWsClient.ConnectAsync(fullUrl);
                Debug.Log($"[VoiceEngine] Connected to Voice WebSocket on channel '{channelStr}'.");
                StartMicrophoneCapture();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VoiceEngine] Voice WebSocket connection failed: {ex.Message}");
                OnVoiceError?.Invoke(ex.Message);
            }
        }

        public async Task DisconnectVoiceAsync()
        {
            CurrentChannel = VoiceChannel.Off;
            StopMicrophoneCapture();

            if (voiceWsClient != null)
            {
                await voiceWsClient.DisconnectAsync();
                voiceWsClient = null;
            }

            // Cleanup peers and audio sources
            foreach (var peer in peers.Values)
            {
                if (peer.sourceObj != null) Destroy(peer.sourceObj);
            }
            peers.Clear();

            OnChannelChanged?.Invoke(VoiceChannel.Off);
            Debug.Log("[VoiceEngine] Disconnected from Voice Chat.");
        }

        public async void SetVoiceChannel(VoiceChannel channel)
        {
            if (channel == CurrentChannel) return;

            if (channel == VoiceChannel.Off)
            {
                await DisconnectVoiceAsync();
            }
            else
            {
                await ConnectVoiceAsync(channel);
            }
        }

        private void HandleIncomingVoiceMessage(string json)
        {
            try
            {
                if (json.Contains("\"type\":\"VOICE_FRAME\""))
                {
                    HandleIncomingAudioFrame(json);
                }
                else if (json.Contains("\"type\":\"VOICE_STATE_SYNC\""))
                {
                    HandlePeerStateSync(json);
                }
                else if (json.Contains("\"type\":\"VOICE_ROSTER\""))
                {
                    HandleVoiceRoster(json);
                }
                else if (json.Contains("\"type\":\"VOICE_JOIN\""))
                {
                    HandlePeerJoin(json);
                }
                else if (json.Contains("\"type\":\"VOICE_LEAVE\""))
                {
                    HandlePeerLeave(json);
                }
                else if (json.Contains("\"type\":\"VOICE_SIGNAL\""))
                {
                    HandleWebRtcSignal(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VoiceEngine] Error handling voice message: {ex.Message}");
            }
        }

        private void HandleIncomingAudioFrame(string json)
        {
            if (Settings.isDeafened) return;

            var packet = JsonUtility.FromJson<VoiceAudioPacketPayload>(json);
            if (packet == null || string.IsNullOrEmpty(packet.senderId)) return;
            if (packet.senderId == GetPlayerId()) return;

            // Check if sender is locally muted
            if (IsPeerMuted(packet.senderId)) return;

            VoicePeer peer = GetOrCreatePeer(packet.senderId, packet.senderName);
            peer.worldPosition = new Vector3(packet.posX, packet.posY, packet.posZ);
            peer.isSpeaking = true;
            peer.lastPacketTime = Time.time;

            // Decode 16-bit PCM Base64 to float samples
            byte[] pcmBytes = Convert.FromBase64String(packet.audioBase64);
            int sampleCount = pcmBytes.Length / 2;

            float volumeMultiplier = peer.userVolumeScale * Settings.masterVoiceVolume;

            lock (peer.sampleQueue)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short val = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
                    float floatSample = (val / 32768f) * volumeMultiplier;
                    peer.sampleQueue.Enqueue(floatSample);
                }

                // Prevent excessive buffer backlog (keep max 0.25 sec jitter buffer)
                while (peer.sampleQueue.Count > SAMPLE_RATE / 4)
                {
                    peer.sampleQueue.Dequeue();
                }
            }
        }

        private void HandlePeerStateSync(string json)
        {
            int uStart = json.IndexOf("\"userState\":");
            if (uStart > 0)
            {
                string sub = json.Substring(uStart + 12).TrimEnd('}');
                // Simple parsing
                string pId = ExtractJsonString(sub, "playerId");
                if (string.IsNullOrEmpty(pId) || pId == GetPlayerId()) return;

                VoicePeer peer = GetOrCreatePeer(pId, ExtractJsonString(sub, "username"));
                peer.isSpeaking = json.Contains("\"isSpeaking\":true");
                peer.isMuted = json.Contains("\"isMuted\":true");
                peer.isDeafened = json.Contains("\"isDeafened\":true");
                OnPeerStateUpdated?.Invoke(peer);
            }
        }

        private void HandleVoiceRoster(string json)
        {
            // Reset existing peers
            foreach (var peer in peers.Values)
            {
                if (peer.sourceObj != null) Destroy(peer.sourceObj);
            }
            peers.Clear();

            // Extract peers array elements
            int pIndex = json.IndexOf("\"peers\":[");
            if (pIndex > 0)
            {
                string rawArray = json.Substring(pIndex + 8);
                int endArray = rawArray.IndexOf("]");
                if (endArray > 0)
                {
                    string inner = rawArray.Substring(0, endArray + 1);
                    string[] items = inner.Split(new[] { "},{" }, StringSplitOptions.None);
                    foreach (var item in items)
                    {
                        string pId = ExtractJsonString(item, "playerId");
                        string uName = ExtractJsonString(item, "username");
                        if (!string.IsNullOrEmpty(pId) && pId != GetPlayerId())
                        {
                            VoicePeer p = GetOrCreatePeer(pId, uName);
                            p.numericId = ExtractJsonInt(item, "numericId");
                            OnPeerJoined?.Invoke(p);
                        }
                    }
                }
            }
        }

        private void HandlePeerJoin(string json)
        {
            string pId = ExtractJsonString(json, "playerId");
            string uName = ExtractJsonString(json, "username");
            if (!string.IsNullOrEmpty(pId) && pId != GetPlayerId())
            {
                VoicePeer peer = GetOrCreatePeer(pId, uName);
                peer.numericId = ExtractJsonInt(json, "numericId");
                OnPeerJoined?.Invoke(peer);
            }
        }

        private void HandlePeerLeave(string json)
        {
            string pId = ExtractJsonString(json, "playerId");
            if (!string.IsNullOrEmpty(pId) && peers.TryGetValue(pId, out VoicePeer peer))
            {
                if (peer.sourceObj != null) Destroy(peer.sourceObj);
                peers.Remove(pId);
                OnPeerLeft?.Invoke(pId);
            }
        }

        private void HandleWebRtcSignal(string json)
        {
            var signal = JsonUtility.FromJson<WebRtcSignalPayload>(json);
            if (signal != null)
            {
                OnWebRtcSignalReceived?.Invoke(signal);
            }
        }

        // =========================================================
        // PROCEDURAL AUDIO PLAYBACK & 3D PROXIMITY
        // =========================================================

        private VoicePeer GetOrCreatePeer(string playerId, string username)
        {
            if (peers.TryGetValue(playerId, out VoicePeer existing))
            {
                if (!string.IsNullOrEmpty(username)) existing.username = username;
                return existing;
            }

            VoicePeer peer = new VoicePeer
            {
                playerId = playerId,
                username = string.IsNullOrEmpty(username) ? "Drifter" : username,
                channel = CurrentChannel,
                userVolumeScale = 1.0f,
                isLocallyMuted = locallyMutedPeerIds.Contains(playerId)
            };

            // Create AudioSource for procedural streaming
            GameObject audioObj = new GameObject($"[VoiceSource_{playerId}]");
            audioObj.transform.SetParent(transform);
            AudioSource aSrc = audioObj.AddComponent<AudioSource>();
            aSrc.playOnAwake = false;
            aSrc.loop = true;
            aSrc.spatialBlend = (CurrentChannel == VoiceChannel.Match && Settings.spatialProximityEnabled) ? 1.0f : 0.0f;
            aSrc.rolloffMode = AudioRolloffMode.Logarithmic;
            aSrc.minDistance = 5.0f;
            aSrc.maxDistance = 60.0f;
            aSrc.volume = Settings.masterVoiceVolume;

            // Generate procedural continuous streaming clip
            AudioClip clip = AudioClip.Create($"Clip_{playerId}", SAMPLE_RATE * 2, 1, SAMPLE_RATE, false);
            aSrc.clip = clip;
            aSrc.Play();

            peer.audioSource = aSrc;
            peer.sourceObj = audioObj;

            peers[playerId] = peer;
            return peer;
        }

        private void UpdatePeerAudio()
        {
            float now = Time.time;
            float[] fillBuffer = new float[CHUNK_SAMPLES];

            foreach (var peer in peers.Values)
            {
                // Check if peer stopped speaking (> 300ms since last audio frame)
                if (peer.isSpeaking && (now - peer.lastPacketTime > 0.35f))
                {
                    peer.isSpeaking = false;
                    peer.volumeLevel = 0f;
                    OnPeerStateUpdated?.Invoke(peer);
                }

                if (peer.audioSource == null) continue;

                // Position update for 3D spatial proximity
                if (CurrentChannel == VoiceChannel.Match && Settings.spatialProximityEnabled)
                {
                    peer.sourceObj.transform.position = peer.worldPosition;
                    peer.audioSource.spatialBlend = 1.0f;
                }
                else
                {
                    peer.audioSource.spatialBlend = 0.0f;
                }

                // If muted or deafened, silence
                if (peer.isLocallyMuted || Settings.isDeafened)
                {
                    peer.audioSource.volume = 0f;
                    continue;
                }

                peer.audioSource.volume = Mathf.Clamp(peer.userVolumeScale * Settings.masterVoiceVolume, 0f, 2f);

                // Dequeue audio samples into AudioSource procedural buffer
                lock (peer.sampleQueue)
                {
                    if (peer.sampleQueue.Count >= CHUNK_SAMPLES)
                    {
                        float sum = 0f;
                        for (int i = 0; i < CHUNK_SAMPLES; i++)
                        {
                            float s = peer.sampleQueue.Dequeue();
                            fillBuffer[i] = s;
                            sum += s * s;
                        }
                        peer.volumeLevel = Mathf.Clamp01(Mathf.Sqrt(sum / CHUNK_SAMPLES) * 8f);

                        // Feed into clip circular buffer
                        int writePos = peer.audioSource.timeSamples;
                        peer.audioSource.clip.SetData(fillBuffer, writePos % peer.audioSource.clip.samples);
                    }
                }
            }
        }

        // =========================================================
        // USER CONTROLS, MUTES, DEAFEN & REPORTING
        // =========================================================

        public void ToggleMicMute()
        {
            Settings.isMicMuted = !Settings.isMicMuted;
            SaveSettings();

            if (Settings.isMicMuted && IsSpeaking)
            {
                SetSpeakingState(false);
            }

            SendVoiceStateUpdate(IsSpeaking, Settings.isMicMuted, Settings.isDeafened, CurrentMicRms);
        }

        public void ToggleDeafen()
        {
            Settings.isDeafened = !Settings.isDeafened;
            SaveSettings();

            SendVoiceStateUpdate(IsSpeaking, Settings.isMicMuted, Settings.isDeafened, CurrentMicRms);
        }

        public void SetInputMode(VoiceInputMode mode)
        {
            Settings.inputMode = mode;
            SaveSettings();
        }

        public void SetPttKey(KeyCode key)
        {
            Settings.pttKey = key;
            SaveSettings();
        }

        public void SetVadThreshold(float threshold)
        {
            Settings.vadThreshold = Mathf.Clamp(threshold, 0.002f, 0.1f);
            SaveSettings();
        }

        public void SetMicGain(float gain)
        {
            Settings.micGain = Mathf.Clamp(gain, 0.2f, 3.0f);
            SaveSettings();
        }

        public void SetMasterVoiceVolume(float volume)
        {
            Settings.masterVoiceVolume = Mathf.Clamp(volume, 0.0f, 2.0f);
            SaveSettings();
        }

        public void SetPeerVolume(string peerId, float volume)
        {
            if (peers.TryGetValue(peerId, out VoicePeer peer))
            {
                peer.userVolumeScale = Mathf.Clamp(volume, 0.0f, 2.0f);
            }
        }

        public void SetPeerMute(string peerId, bool mute)
        {
            if (mute)
            {
                locallyMutedPeerIds.Add(peerId);
            }
            else
            {
                locallyMutedPeerIds.Remove(peerId);
            }
            SaveMutedPeers();

            if (peers.TryGetValue(peerId, out VoicePeer peer))
            {
                peer.isLocallyMuted = mute;
            }
        }

        public bool IsPeerMuted(string peerId)
        {
            return locallyMutedPeerIds.Contains(peerId);
        }

        public void ReportPlayerVoice(string targetPlayerId, string reason, string details)
        {
            if (string.IsNullOrEmpty(targetPlayerId)) return;

            // 1. Send via active Voice WebSocket if connected
            if (IsWsConnected)
            {
                var report = new VoiceReportPayload
                {
                    type = "VOICE_REPORT",
                    targetId = targetPlayerId,
                    reason = reason,
                    details = details
                };
                _ = voiceWsClient.SendAsync(JsonUtility.ToJson(report));
            }

            // 2. Also send REST backup
            _ = SendRestVoiceReportAsync(targetPlayerId, reason, details);
            Debug.Log($"[VoiceEngine] Reported player '{targetPlayerId}' for voice violation: {reason}");
        }

        private async Task SendRestVoiceReportAsync(string targetId, string reason, string details)
        {
            try
            {
                string token = GetToken();
                string url = $"{GetBaseUrl()}/api/voice/report";

                string bodyJson = $"{{\"targetId\":\"{targetId}\",\"reason\":\"{reason}\",\"details\":\"{details}\"}}";
                using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                    req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                    var op = req.SendWebRequest();
                    while (!op.isDone) await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VoiceEngine] REST report failed: {ex.Message}");
            }
        }

        // =========================================================
        // SETTINGS & STORAGE PERSISTENCE
        // =========================================================

        private void LoadSettings()
        {
            string json = PlayerPrefs.GetString(PREF_VOICE_SETTINGS, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json, Settings);
                }
                catch { }
            }
        }

        public void SaveSettings()
        {
            string json = JsonUtility.ToJson(Settings);
            PlayerPrefs.SetString(PREF_VOICE_SETTINGS, json);
            PlayerPrefs.Save();
        }

        private void LoadMutedPeers()
        {
            string raw = PlayerPrefs.GetString(PREF_MUTED_VOICE_PEERS, "");
            if (!string.IsNullOrEmpty(raw))
            {
                string[] ids = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var id in ids) locallyMutedPeerIds.Add(id);
            }
        }

        private void SaveMutedPeers()
        {
            string joined = string.Join(";", locallyMutedPeerIds);
            PlayerPrefs.SetString(PREF_MUTED_VOICE_PEERS, joined);
            PlayerPrefs.Save();
        }

        private string GetPlayerId()
        {
            return AccountManager.Instance != null && AccountManager.Instance.Profile != null ?
                AccountManager.Instance.Profile.playerId : "usr_local";
        }

        private string GetUsername()
        {
            return AccountManager.Instance != null ? AccountManager.Instance.Username : "Drifter";
        }

        private string GetToken()
        {
            return AccountManager.Instance != null ? AccountManager.Instance.AccessToken : "";
        }

        private string GetBaseUrl()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "https://aetherserpent-backend.uttombarman55.workers.dev";
        }

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\":\"";
            int start = json.IndexOf(pattern);
            if (start < 0) return "";
            start += pattern.Length;
            int end = json.IndexOf("\"", start);
            return end > start ? json.Substring(start, end - start) : "";
        }

        private static int ExtractJsonInt(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return 0;
            start += pattern.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            string val = json.Substring(start, end - start);
            int.TryParse(val, out int res);
            return res;
        }
    }
}

