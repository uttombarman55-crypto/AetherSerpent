using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AetherSerpent.Account;
using AetherSerpent.Network;

namespace AetherSerpent.Social
{
    /// <summary>
    /// Master Real-Time Text Chat Engine.
    /// Manages:
    /// - Channels: Global (with slow mode), Squad, Clan, Match/Room, Private Whisper, and System
    /// - Real-time WebSocket delivery and HTTP REST fallback
    /// - Message history caching & edge synchronization
    /// - Native Unicode emoji shortcuts
    /// - @Mention detection and notification
    /// - Local player muting & spam protection
    /// - 3D In-World chat bubble dispatches
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public class ChatManager : MonoBehaviour
    {
        public static ChatManager Instance { get; private set; }

        private const string PREF_MUTED_PLAYERS = "aether_muted_players";

        // Message Collections
        public List<ChatMessageItem> AllMessages { get; private set; } = new List<ChatMessageItem>();
        public List<ChatMessageItem> GlobalMessages { get; private set; } = new List<ChatMessageItem>();
        public List<ChatMessageItem> SquadMessages { get; private set; } = new List<ChatMessageItem>();
        public List<ChatMessageItem> ClanMessages { get; private set; } = new List<ChatMessageItem>();
        public List<ChatMessageItem> MatchMessages { get; private set; } = new List<ChatMessageItem>();
        public List<ChatMessageItem> WhisperMessages { get; private set; } = new List<ChatMessageItem>();

        // Local Muted Players
        private HashSet<string> mutedPlayerIds = new HashSet<string>();

        // Rate Limiting & Slow Mode
        public float GlobalSlowModeCooldown { get; private set; } = 0f;
        private float chatCooldown = 0f;

        // WebSocket State
        private WebSocketClient chatWsClient;
        public bool IsWsConnected => chatWsClient != null && chatWsClient.State == WebSocketClient.ConnectionState.Connected;

        // Settings
        public bool ShowChatBubbles { get; set; } = true;

        // Events
        public event Action<ChatMessageItem> OnMessageReceived;
        public event Action<ChatChannel> OnChannelUpdated;
        public event Action<ChatMessageItem> OnMentionReceived;
        public event Action<ChatMessageItem> OnChatBubbleTriggered;

        // Emoji Dictionary
        private static readonly Dictionary<string, string> EmojiShortcuts = new Dictionary<string, string>
        {
            { ":fire:", "🔥" },
            { ":snake:", "🐍" },
            { ":serpent:", "🐍" },
            { ":gg:", "🏆" },
            { ":crown:", "👑" },
            { ":heart:", "❤️" },
            { ":love:", "❤️" },
            { ":trophy:", "🏆" },
            { ":skull:", "💀" },
            { ":dead:", "💀" },
            { ":lightning:", "⚡" },
            { ":volt:", "⚡" },
            { ":shield:", "🛡️" },
            { ":star:", "⭐" },
            { ":dragon:", "🐉" },
            { ":gem:", "💎" },
            { ":diamond:", "💎" },
            { ":sword:", "⚔️" },
            { ":battle:", "⚔️" },
            { ":target:", "🎯" },
            { ":cool:", "😎" },
            { ":smile:", "😊" },
            { ":cry:", "😭" },
            { ":angry:", "😡" },
            { ":thumbsup:", "👍" },
            { ":clap:", "👏" },
            { ":party:", "🎉" },
            { ":sparkles:", "✨" },
            { ":aether:", "🌌" },
            { ":void:", "🕳️" }
        };

        private static readonly string[] BadWords = {
            "fuck", "shit", "bitch", "cunt", "whore", "slut", "nigger", "nigga",
            "faggot", "retard", "asshole", "dumbass", "dickhead", "bastard", "motherfucker", "kys"
        };

        public static string FilterClientProfanity(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string sanitized = text;
            foreach (var w in BadWords)
            {
                sanitized = System.Text.RegularExpressions.Regex.Replace(
                    sanitized,
                    $@"\b{w}\b",
                    new string('*', w.Length),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
            return sanitized;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadMutedPlayers();
        }

        private async void Start()
        {
            await Task.Delay(600);
            await InitializeChatWebSocketAsync();
            await FetchAllChannelHistoriesAsync();

            // Listen to GameRoom WebSocket packets for match chat
            if (NetworkManager.Instance != null)
            {
                // NetworkManager room events hook
            }
        }

        private void Update()
        {
            if (GlobalSlowModeCooldown > 0f)
            {
                GlobalSlowModeCooldown -= Time.unscaledDeltaTime;
                if (GlobalSlowModeCooldown < 0f) GlobalSlowModeCooldown = 0f;
            }

            if (chatCooldown > 0f)
            {
                chatCooldown -= Time.unscaledDeltaTime;
                if (chatCooldown < 0f) chatCooldown = 0f;
            }

            chatWsClient?.DispatchIncomingMessages();
        }

        private void OnDestroy()
        {
            _ = chatWsClient?.DisconnectAsync();
        }

        private string GetBaseUrl()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "https://aetherserpent-backend.uttombarman55.workers.dev";
        }

        private string GetToken()
        {
            return AccountManager.Instance != null ? AccountManager.Instance.AccessToken : "";
        }

        private void LoadMutedPlayers()
        {
            string raw = PlayerPrefs.GetString(PREF_MUTED_PLAYERS, "");
            if (!string.IsNullOrEmpty(raw))
            {
                string[] ids = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var id in ids) mutedPlayerIds.Add(id);
            }
        }

        private void SaveMutedPlayers()
        {
            string joined = string.Join(";", mutedPlayerIds);
            PlayerPrefs.SetString(PREF_MUTED_PLAYERS, joined);
            PlayerPrefs.Save();
        }

        public void MutePlayer(string pId)
        {
            if (string.IsNullOrEmpty(pId)) return;
            mutedPlayerIds.Add(pId);
            SaveMutedPlayers();
            FilterMutedMessages();
        }

        public void UnmutePlayer(string pId)
        {
            if (string.IsNullOrEmpty(pId)) return;
            mutedPlayerIds.Remove(pId);
            SaveMutedPlayers();
        }

        public bool IsMuted(string pId)
        {
            return !string.IsNullOrEmpty(pId) && mutedPlayerIds.Contains(pId);
        }

        private void FilterMutedMessages()
        {
            AllMessages.RemoveAll(m => IsMuted(m.senderId));
            GlobalMessages.RemoveAll(m => IsMuted(m.senderId));
            SquadMessages.RemoveAll(m => IsMuted(m.senderId));
            ClanMessages.RemoveAll(m => IsMuted(m.senderId));
            MatchMessages.RemoveAll(m => IsMuted(m.senderId));
            WhisperMessages.RemoveAll(m => IsMuted(m.senderId));
        }

        // =========================================================
        // REAL-TIME WEBSOCKET CONNECTION
        // =========================================================

        public async Task InitializeChatWebSocketAsync()
        {
            string token = GetToken();
            if (string.IsNullOrEmpty(token)) return;

            string httpUrl = GetBaseUrl();
            string wsUrl = httpUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            string fullUrl = $"{wsUrl}/api/chat/ws?token={token}";

            chatWsClient = new WebSocketClient();
            chatWsClient.OnMessageReceived += HandleIncomingWsMessage;

            try
            {
                await chatWsClient.ConnectAsync(fullUrl);
                Debug.Log("[ChatManager] Connected to Real-Time Chat WebSocket.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChatManager] WebSocket connection fallback to REST: {ex.Message}");
            }
        }

        private void HandleIncomingWsMessage(string json)
        {
            try
            {
                if (json.Contains("\"type\":\"CHAT_BROADCAST\""))
                {
                    int pStart = json.IndexOf("\"payload\":") + 10;
                    int pEnd = json.LastIndexOf(",\"timestamp\"");
                    if (pStart > 9 && pEnd > pStart)
                    {
                        string sub = json.Substring(pStart, pEnd - pStart);
                        ChatMessageItem item = JsonUtility.FromJson<ChatMessageItem>(sub);
                        if (item != null)
                        {
                            ReceiveMessage(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChatManager] Parse error on incoming chat: {ex.Message}");
            }
        }

        // =========================================================
        // MESSAGE DISPATCH & INGESTION
        // =========================================================

        public void ReceiveMessage(ChatMessageItem item)
        {
            if (item == null || IsMuted(item.senderId)) return;

            // Check if already in list (deduplication)
            if (item.id > 0 && AllMessages.Exists(m => m.id == item.id)) return;

            // Emoji parsing & profanity filter
            item.message = FilterClientProfanity(ReplaceEmojiShortcuts(item.message));

            // Mention detection
            string myName = AccountManager.Instance != null ? AccountManager.Instance.Username : "";
            int myNumId = AccountManager.Instance != null ? AccountManager.Instance.NumericId : 0;

            if (!string.IsNullOrEmpty(myName) &&
                (item.message.IndexOf($"@{myName}", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (myNumId > 0 && item.message.Contains($"@{myNumId}"))))
            {
                item.isMention = true;
                OnMentionReceived?.Invoke(item);
            }

            // Route to channel lists
            AllMessages.Add(item);
            if (AllMessages.Count > 100) AllMessages.RemoveAt(0);

            switch (item.GetChannelEnum())
            {
                case ChatChannel.Global:
                    GlobalMessages.Add(item);
                    if (GlobalMessages.Count > 50) GlobalMessages.RemoveAt(0);
                    break;
                case ChatChannel.Squad:
                    SquadMessages.Add(item);
                    if (SquadMessages.Count > 50) SquadMessages.RemoveAt(0);
                    break;
                case ChatChannel.Clan:
                    ClanMessages.Add(item);
                    if (ClanMessages.Count > 50) ClanMessages.RemoveAt(0);
                    break;
                case ChatChannel.Match:
                    MatchMessages.Add(item);
                    if (MatchMessages.Count > 50) MatchMessages.RemoveAt(0);
                    break;
                case ChatChannel.Whisper:
                    WhisperMessages.Add(item);
                    if (WhisperMessages.Count > 50) WhisperMessages.RemoveAt(0);
                    break;
            }

            // Trigger In-World Chat Bubble for Match/Squad messages
            if (ShowChatBubbles && (item.GetChannelEnum() == ChatChannel.Match || item.GetChannelEnum() == ChatChannel.Squad))
            {
                OnChatBubbleTriggered?.Invoke(item);
            }

            OnMessageReceived?.Invoke(item);
            OnChannelUpdated?.Invoke(item.GetChannelEnum());
        }

        public async Task<(bool success, string error)> SendMessageAsync(
            ChatChannel channel,
            string message,
            string recipientId = null,
            int recipientNumericId = 0,
            string recipientName = null
        ) {
            if (string.IsNullOrWhiteSpace(message)) return (false, "Message cannot be empty.");

            // Slow mode check
            if (channel == ChatChannel.Global && GlobalSlowModeCooldown > 0f)
            {
                return (false, $"Global chat slow mode active. Wait {GlobalSlowModeCooldown:F1}s.");
            }

            if (chatCooldown > 0f)
            {
                return (false, "Sending messages too fast.");
            }

            string cleanMsg = FilterClientProfanity(ReplaceEmojiShortcuts(message.Trim()));
            string channelStr = channel.ToString().ToLowerInvariant();

            SendChatPayload payload = new SendChatPayload
            {
                channel = channelStr,
                message = cleanMsg,
                recipientId = recipientId ?? "",
                recipientNumericId = recipientNumericId,
                recipientName = recipientName ?? "",
                roomId = NetworkManager.Instance != null ? NetworkManager.Instance.CurrentRoomId : "",
                squadId = SocialGroupingManager.Instance != null && SocialGroupingManager.Instance.CurrentSquad != null ? SocialGroupingManager.Instance.CurrentSquad.squadId : "",
                clanId = SocialGroupingManager.Instance != null && SocialGroupingManager.Instance.CurrentClan != null ? SocialGroupingManager.Instance.CurrentClan.clanId : ""
            };

            // Attempt via active WebSocket if available
            if (IsWsConnected)
            {
                string wsJson = $"{{\"type\":\"CHAT_SEND\",\"payload\":{JsonUtility.ToJson(payload)},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
                await chatWsClient.SendAsync(wsJson);
            }

            // Also post via REST API for reliability & persistence
            string url = $"{GetBaseUrl()}/api/chat/send";
            string bodyJson = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ChatSendApiResponse res = JsonUtility.FromJson<ChatSendApiResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.message != null)
                    {
                        ReceiveMessage(res.message);

                        if (channel == ChatChannel.Global) GlobalSlowModeCooldown = 5.0f;
                        chatCooldown = 0.8f;

                        return (true, null);
                    }
                    return (false, res?.error ?? "Failed to send chat.");
                }

                return (false, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        // =========================================================
        // HISTORY FETCHING
        // =========================================================

        public async Task FetchAllChannelHistoriesAsync()
        {
            await FetchHistoryAsync(ChatChannel.Global);
            await FetchHistoryAsync(ChatChannel.Squad);
            await FetchHistoryAsync(ChatChannel.Clan);
            await FetchHistoryAsync(ChatChannel.Match);
        }

        public async Task<List<ChatMessageItem>> FetchHistoryAsync(ChatChannel channel, string targetId = null, string peerId = null)
        {
            string chStr = channel.ToString().ToLowerInvariant();
            string query = $"channel={chStr}&limit=50";
            if (!string.IsNullOrEmpty(targetId)) query += $"&targetId={UnityWebRequest.EscapeURL(targetId)}";
            if (!string.IsNullOrEmpty(peerId)) query += $"&peerId={UnityWebRequest.EscapeURL(peerId)}";

            string url = $"{GetBaseUrl()}/api/chat/history?{query}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ChatHistoryApiResponse res = JsonUtility.FromJson<ChatHistoryApiResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.messages != null)
                    {
                        foreach (var msg in res.messages)
                        {
                            ReceiveMessage(msg);
                        }
                        return res.messages;
                    }
                }
            }
            return new List<ChatMessageItem>();
        }

        // =========================================================
        // UTILITIES
        // =========================================================

        public static string ReplaceEmojiShortcuts(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string result = input;
            foreach (var kvp in EmojiShortcuts)
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }
            return result;
        }

        private static string TryParseError(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                int errIdx = json.IndexOf("\"error\":");
                if (errIdx >= 0)
                {
                    int startQuote = json.IndexOf('"', errIdx + 8);
                    if (startQuote >= 0)
                    {
                        int endQuote = json.IndexOf('"', startQuote + 1);
                        if (endQuote > startQuote)
                        {
                            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }
                }
            }
            catch {}
            return null;
        }
    }
}

