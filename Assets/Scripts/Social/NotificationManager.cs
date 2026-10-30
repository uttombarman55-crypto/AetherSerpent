using System;
using System.Collections;
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
    /// Master Notification & Social Quality Subsystem.
    /// Manages:
    /// - Real-time incoming notification dispatch and history caching
    /// - In-game animated toast banner queue with actionable buttons (Accept/Decline/Reply)
    /// - Do Not Disturb (DND) mode
    /// - Online presence status & privacy (Online, Away, DND, Invisible)
    /// - Recent Players tracker (matches met, quick add friend, squad invite, report)
    /// - Lobby quick reactions & animated floating emotes
    /// - One-click Player ID clipboard copy
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-68)]
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        private const string PREF_DND = "aether_dnd_active";
        private const string PREF_PRESENCE = "aether_presence_status";
        private const string PREF_RECENT_PLAYERS = "aether_recent_players_json";
        private const int MAX_RECENT_PLAYERS = 30;

        // Notification Collections
        public List<NotificationItem> Notifications { get; private set; } = new List<NotificationItem>();
        public int UnreadCount { get; private set; } = 0;

        // Active Banner Toast Queue
        public List<NotificationItem> ActiveToasts { get; private set; } = new List<NotificationItem>();

        // Status & Privacy
        public bool IsDndActive { get; private set; } = false;
        public PresenceStatus CurrentPresence { get; private set; } = PresenceStatus.Online;
        public bool ShowOnlineStatus { get; private set; } = true;

        // Recent Players
        public List<RecentPlayer> RecentPlayers { get; private set; } = new List<RecentPlayer>();

        // Lobby Reactions
        public List<ActiveLobbyReaction> ActiveReactions { get; private set; } = new List<ActiveLobbyReaction>();

        // Events
        public event Action<NotificationItem> OnNotificationReceived;
        public event Action<int> OnUnreadCountChanged;
        public event Action<PresenceStatus> OnPresenceChanged;
        public event Action<ActiveLobbyReaction> OnLobbyReactionTriggered;

        // Reaction Emojis
        private static readonly Dictionary<LobbyReaction, string> ReactionEmojis = new Dictionary<LobbyReaction, string>
        {
            { LobbyReaction.Fire, "🔥" },
            { LobbyReaction.Serpent, "🐍" },
            { LobbyReaction.Crown, "👑" },
            { LobbyReaction.GG, "👏" },
            { LobbyReaction.Heart, "❤️" },
            { LobbyReaction.Lightning, "⚡" },
            { LobbyReaction.RIP, "💀" },
            { LobbyReaction.Shield, "🛡️" }
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

            LoadPreferences();
        }

        private void Start()
        {
            StartCoroutine(PeriodicSyncRoutine());
        }

        private void Update()
        {
            // Update Active Toast Banners
            for (int i = ActiveToasts.Count - 1; i >= 0; i--)
            {
                ActiveToasts[i].displayTimer -= Time.unscaledDeltaTime;
                if (ActiveToasts[i].displayTimer <= 0f)
                {
                    ActiveToasts.RemoveAt(i);
                }
            }

            // Update Floating Reactions
            for (int i = ActiveReactions.Count - 1; i >= 0; i--)
            {
                ActiveReactions[i].timer -= Time.unscaledDeltaTime;
                ActiveReactions[i].screenPos.y -= 40f * Time.unscaledDeltaTime; // Float upwards
                if (ActiveReactions[i].timer <= 0f)
                {
                    ActiveReactions.RemoveAt(i);
                }
            }
        }

        private IEnumerator PeriodicSyncRoutine()
        {
            WaitForSeconds wait15s = new WaitForSeconds(15f);

            while (true)
            {
                yield return wait15s;

                if (AccountManager.Instance != null && AccountManager.Instance.IsAuthenticated)
                {
                    _ = FetchNotificationsAsync();
                }
            }
        }

        // =========================================================
        // NOTIFICATIONS API & DISPATCH
        // =========================================================

        public async Task<List<NotificationItem>> FetchNotificationsAsync()
        {
            string token = GetToken();
            if (string.IsNullOrEmpty(token)) return Notifications;

            string url = $"{GetBaseUrl()}/api/notifications/list?limit=40";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {token}");
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<NotificationsListResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.notifications != null)
                    {
                        Notifications.Clear();
                        foreach (var dto in res.notifications)
                        {
                            Notifications.Add(new NotificationItem
                            {
                                id = dto.id,
                                type = ParseNotificationType(dto.type),
                                title = dto.title,
                                message = dto.message,
                                senderId = dto.senderId,
                                senderName = dto.senderName,
                                senderNumericId = dto.senderNumericId,
                                dataJson = dto.dataJson,
                                isRead = dto.isRead,
                                createdAt = dto.createdAt
                            });
                        }

                        UnreadCount = res.unreadCount;
                        OnUnreadCountChanged?.Invoke(UnreadCount);
                    }
                }
            }

            return Notifications;
        }

        public void ReceiveNotification(NotificationItem item)
        {
            if (item == null) return;

            Notifications.Insert(0, item);
            UnreadCount++;
            OnUnreadCountChanged?.Invoke(UnreadCount);
            OnNotificationReceived?.Invoke(item);

            // Display floating toast banner if Do Not Disturb is NOT active
            if (!IsDndActive)
            {
                item.displayTimer = 5.0f;
                ActiveToasts.Insert(0, item);
                while (ActiveToasts.Count > 3) ActiveToasts.RemoveAt(ActiveToasts.Count - 1);
            }
        }

        public async Task MarkAsReadAsync(int notificationId = 0, bool markAll = false)
        {
            string token = GetToken();
            if (string.IsNullOrEmpty(token)) return;

            string url = $"{GetBaseUrl()}/api/notifications/read";
            string json = markAll ? "{\"all\":true}" : $"{{\"notificationId\":{notificationId}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (markAll)
                {
                    foreach (var n in Notifications) n.isRead = true;
                    UnreadCount = 0;
                }
                else
                {
                    var found = Notifications.Find(n => n.id == notificationId);
                    if (found != null && !found.isRead)
                    {
                        found.isRead = true;
                        UnreadCount = Mathf.Max(0, UnreadCount - 1);
                    }
                }
                OnUnreadCountChanged?.Invoke(UnreadCount);
            }
        }

        public void DismissToast(NotificationItem toast)
        {
            ActiveToasts.Remove(toast);
        }

        // =========================================================
        // DO NOT DISTURB & PRESENCE STATUS
        // =========================================================

        public void SetDndMode(bool active)
        {
            IsDndActive = active;
            PlayerPrefs.SetInt(PREF_DND, active ? 1 : 0);
            PlayerPrefs.Save();

            if (active)
            {
                ActiveToasts.Clear();
                SetPresenceStatus(PresenceStatus.DoNotDisturb);
            }
            else
            {
                SetPresenceStatus(PresenceStatus.Online);
            }
        }

        public void SetPresenceStatus(PresenceStatus status, bool showStatus = true)
        {
            CurrentPresence = status;
            ShowOnlineStatus = showStatus;
            PlayerPrefs.SetInt(PREF_PRESENCE, (int)status);
            PlayerPrefs.Save();

            OnPresenceChanged?.Invoke(CurrentPresence);
            _ = SyncPresenceToBackendAsync(status, showStatus);
        }

        private async Task SyncPresenceToBackendAsync(PresenceStatus status, bool showOnline)
        {
            string token = GetToken();
            if (string.IsNullOrEmpty(token)) return;

            string statusStr = status switch
            {
                PresenceStatus.Away => "away",
                PresenceStatus.InMatch => "in_match",
                PresenceStatus.DoNotDisturb => "dnd",
                PresenceStatus.Invisible => "invisible",
                _ => "online"
            };

            string url = $"{GetBaseUrl()}/api/presence/update";
            string bodyJson = $"{{\"status\":\"{statusStr}\",\"showOnlineStatus\":{(showOnline ? "true" : "false")}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
            }
        }

        // =========================================================
        // RECENT PLAYERS SYSTEM
        // =========================================================

        public void RecordRecentPlayer(string playerId, string username, int numericId, string role = "Co-op Partner", string matchMode = "Sanctuary")
        {
            if (string.IsNullOrEmpty(playerId) || playerId == GetMyPlayerId()) return;

            // Remove existing duplicate entry
            RecentPlayers.RemoveAll(p => p.playerId == playerId);

            RecentPlayer recent = new RecentPlayer
            {
                playerId = playerId,
                username = username,
                numericId = numericId,
                role = role,
                matchMode = matchMode,
                matchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            RecentPlayers.Insert(0, recent);
            while (RecentPlayers.Count > MAX_RECENT_PLAYERS)
            {
                RecentPlayers.RemoveAt(RecentPlayers.Count - 1);
            }

            SaveRecentPlayers();
        }

        private void LoadRecentPlayers()
        {
            string raw = PlayerPrefs.GetString(PREF_RECENT_PLAYERS, "");
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<RecentPlayersWrapper>(raw);
                    if (wrapper != null && wrapper.players != null)
                    {
                        RecentPlayers = wrapper.players;
                    }
                }
                catch { }
            }
        }

        private void SaveRecentPlayers()
        {
            var wrapper = new RecentPlayersWrapper { players = RecentPlayers };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(PREF_RECENT_PLAYERS, json);
            PlayerPrefs.Save();
        }

        [Serializable]
        private class RecentPlayersWrapper
        {
            public List<RecentPlayer> players;
        }

        // =========================================================
        // LOBBY QUICK REACTIONS & EMOTES
        // =========================================================

        public void TriggerReaction(LobbyReaction reaction, string senderName = "You", Vector2? customPos = null)
        {
            string emoji = ReactionEmojis.ContainsKey(reaction) ? ReactionEmojis[reaction] : "🔥";
            Vector2 pos = customPos ?? new Vector2(UnityEngine.Random.Range(Screen.width * 0.3f, Screen.width * 0.7f), Screen.height * 0.75f);

            var active = new ActiveLobbyReaction
            {
                senderName = senderName,
                reaction = reaction,
                emoji = emoji,
                timer = 2.5f,
                screenPos = pos
            };

            ActiveReactions.Add(active);
            OnLobbyReactionTriggered?.Invoke(active);

            // Broadcast to squad/lobby via backend
            _ = SendReactionToBackendAsync(reaction);
        }

        private async Task SendReactionToBackendAsync(LobbyReaction reaction)
        {
            string token = GetToken();
            if (string.IsNullOrEmpty(token)) return;

            string reactionStr = reaction.ToString().ToLower();
            string url = $"{GetBaseUrl()}/api/social/reaction";
            string bodyJson = $"{{\"reaction\":\"{reactionStr}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
            }
        }

        // =========================================================
        // ONE-CLICK COPY PLAYER ID
        // =========================================================

        public void CopyPlayerIdToClipboard(int numericId)
        {
            if (numericId <= 0) return;
            GUIUtility.systemCopyBuffer = numericId.ToString();
            ReceiveNotification(new NotificationItem
            {
                type = NotificationType.System,
                title = "Player ID Copied",
                message = $"UID {numericId} copied to clipboard!",
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        // =========================================================
        // PREFERENCES & HELPERS
        // =========================================================

        private void LoadPreferences()
        {
            IsDndActive = PlayerPrefs.GetInt(PREF_DND, 0) == 1;
            CurrentPresence = (PresenceStatus)PlayerPrefs.GetInt(PREF_PRESENCE, 0);
            LoadRecentPlayers();
        }

        private string GetToken()
        {
            return AccountManager.Instance != null ? AccountManager.Instance.AccessToken : "";
        }

        private string GetMyPlayerId()
        {
            return AccountManager.Instance != null && AccountManager.Instance.Profile != null ?
                AccountManager.Instance.Profile.playerId : "";
        }

        private string GetBaseUrl()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "https://aetherserpent-backend.uttombarman55.workers.dev";
        }

        private static NotificationType ParseNotificationType(string raw)
        {
            return (raw?.ToLower()) switch
            {
                "squad_invite" => NotificationType.SquadInvite,
                "clan_invite" => NotificationType.ClanInvite,
                "whisper" => NotificationType.Whisper,
                "system" => NotificationType.System,
                _ => NotificationType.FriendRequest
            };
        }
    }
}

