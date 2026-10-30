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
    /// Free Fire-Style Social Grouping Manager.
    /// Manages:
    /// - Friends list with Online/In-Match/Offline status and last login timestamps
    /// - Friend request dispatch, acceptance, and rejection
    /// - Tactical Squad system (4-5 players) with Lobby Ready states, chat, and match transition
    /// - Celestial Clan / Guild system (up to 50 players) with ranks, announcements, and clan chat
    /// - Nametag formatting: [CLAN] [SQUAD] Username
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public class SocialGroupingManager : MonoBehaviour
    {
        public static SocialGroupingManager Instance { get; private set; }

        [Header("State Information")]
        public List<FriendItem> FriendsList { get; private set; } = new List<FriendItem>();
        public List<FriendRequestItem> FriendRequests { get; private set; } = new List<FriendRequestItem>();

        public SquadItem CurrentSquad { get; private set; }
        public List<SquadInviteItem> SquadInvites { get; private set; } = new List<SquadInviteItem>();
        public List<SquadChatItem> SquadChat { get; private set; } = new List<SquadChatItem>();

        public ClanItem CurrentClan { get; private set; }
        public string MyClanRank { get; private set; } = "none";
        public List<ClanItem> PublicClans { get; private set; } = new List<ClanItem>();
        public List<ClanJoinRequestItem> ClanRequests { get; private set; } = new List<ClanJoinRequestItem>();
        public List<ClanChatItem> ClanChat { get; private set; } = new List<ClanChatItem>();

        public bool InSquad => CurrentSquad != null && !string.IsNullOrEmpty(CurrentSquad.squadId);
        public bool IsSquadLeader
        {
            get
            {
                if (!InSquad) return false;
                string myId = AccountManager.Instance != null ? AccountManager.Instance.PlayerId : "";
                return CurrentSquad.leaderId == myId;
            }
        }

        public bool InClan => CurrentClan != null && !string.IsNullOrEmpty(CurrentClan.clanId);
        public bool IsClanOfficerOrLeader => InClan && (MyClanRank == "leader" || MyClanRank == "officer");
        public bool IsClanLeader => InClan && MyClanRank == "leader";

        // Events
        public event Action<List<FriendItem>> OnFriendsChanged;
        public event Action<List<FriendRequestItem>> OnFriendRequestsChanged;
        public event Action<SquadItem> OnSquadChanged;
        public event Action<List<SquadChatItem>> OnSquadChatChanged;
        public event Action<ClanItem> OnClanChanged;
        public event Action<List<ClanChatItem>> OnClanChatChanged;
        public event Action<string> OnSquadMatchLaunched;

        private float presenceTimer = 0f;

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

        private async void Start()
        {
            await Task.Delay(500);
            await RefreshAllSocialAsync();
            await UpdatePresenceAsync("online");
        }

        private void Update()
        {
            presenceTimer += Time.unscaledDeltaTime;
            if (presenceTimer >= 45f)
            {
                presenceTimer = 0f;
                _ = UpdatePresenceAsync("online");
            }
        }

        private void OnApplicationQuit()
        {
            _ = UpdatePresenceAsync("offline");
        }

        private string GetBaseUrl()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "https://aetherserpent-backend.uttombarman55.workers.dev";
        }

        private string GetToken()
        {
            return AccountManager.Instance != null ? AccountManager.Instance.AccessToken : "";
        }

        private string GetPlayerId()
        {
            return AccountManager.Instance != null ? AccountManager.Instance.PlayerId : "";
        }

        public async Task RefreshAllSocialAsync()
        {
            await RefreshFriendsAsync();
            await RefreshFriendRequestsAsync();
            await RefreshMySquadAsync();
            await RefreshSquadInvitesAsync();
            await RefreshMyClanAsync();
        }

        // =========================================================
        // 1. FRIENDS & PRESENCE API
        // =========================================================

        public async Task<List<FriendItem>> RefreshFriendsAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/friends/list";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    FriendsListFullResponse res = JsonUtility.FromJson<FriendsListFullResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.friends != null)
                    {
                        FriendsList = res.friends;
                        OnFriendsChanged?.Invoke(FriendsList);
                        return FriendsList;
                    }
                }
            }
            return FriendsList;
        }

        public async Task<(bool success, string message)> SendFriendRequestAsync(string targetPlayerId = null, int targetNumericId = 0, string targetUsername = null)
        {
            string url = $"{GetBaseUrl()}/api/social/friends/request";
            string bodyJson = $"{{\"targetPlayerId\":\"{Escape(targetPlayerId)}\",\"targetNumericId\":{targetNumericId},\"targetUsername\":\"{Escape(targetUsername)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SocialActionResponse res = JsonUtility.FromJson<SocialActionResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        return (true, res.message ?? "Friend request sent!");
                    }
                    return (false, res?.error ?? "Failed to send request.");
                }
                return (false, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        public async Task<List<FriendRequestItem>> RefreshFriendRequestsAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/friends/requests";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    FriendRequestsResponse res = JsonUtility.FromJson<FriendRequestsResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.incoming != null)
                    {
                        FriendRequests = res.incoming;
                        OnFriendRequestsChanged?.Invoke(FriendRequests);
                        return FriendRequests;
                    }
                }
            }
            return FriendRequests;
        }

        public async Task<bool> RespondFriendRequestAsync(int requestId, bool accept)
        {
            string url = $"{GetBaseUrl()}/api/social/friends/respond";
            string bodyJson = $"{{\"requestId\":{requestId},\"action\":\"{(accept ? "accept" : "reject")}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshFriendsAsync();
                    await RefreshFriendRequestsAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> RemoveFriendAsync(string friendId)
        {
            string url = $"{GetBaseUrl()}/api/social/friends/remove";
            string bodyJson = $"{{\"friendId\":\"{Escape(friendId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshFriendsAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> UpdatePresenceAsync(string status, string matchRoomId = null)
        {
            string url = $"{GetBaseUrl()}/api/social/presence/update";
            string bodyJson = $"{{\"status\":\"{Escape(status)}\",\"matchRoomId\":\"{Escape(matchRoomId)}\"}}";

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

                return req.result == UnityWebRequest.Result.Success;
            }
        }

        // =========================================================
        // 2. SQUAD SYSTEM (4-5 PLAYERS LOBBY & READY SYSTEM)
        // =========================================================

        public async Task<(bool success, string error)> CreateSquadAsync(string name, string tag)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/create";
            string bodyJson = $"{{\"name\":\"{Escape(name)}\",\"tag\":\"{Escape(tag)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SquadResponse res = JsonUtility.FromJson<SquadResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.squad != null)
                    {
                        CurrentSquad = res.squad;
                        OnSquadChanged?.Invoke(CurrentSquad);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Failed to create squad.");
                }
                return (false, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        public async Task<SquadItem> RefreshMySquadAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/squad/my";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SquadResponse res = JsonUtility.FromJson<SquadResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        CurrentSquad = res.hasSquad ? res.squad : null;
                        OnSquadChanged?.Invoke(CurrentSquad);

                        // Record fellow squad members in RecentPlayers
                        if (CurrentSquad != null && CurrentSquad.members != null && NotificationManager.Instance != null)
                        {
                            string myId = GetPlayerId();
                            foreach (var mem in CurrentSquad.members)
                            {
                                if (mem.playerId != myId)
                                {
                                    NotificationManager.Instance.RecordRecentPlayer(
                                        mem.playerId,
                                        mem.username,
                                        mem.numericId,
                                        mem.isLeader ? "Squad Leader" : "Squad Member",
                                        "Squad Lobby"
                                    );
                                }
                            }
                        }

                        return CurrentSquad;
                    }
                }
            }
            return CurrentSquad;
        }

        public async Task<(bool success, string error)> InviteFriendToSquadAsync(string targetPlayerId)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/invite";
            string bodyJson = $"{{\"targetPlayerId\":\"{Escape(targetPlayerId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    return (true, null);
                }
                return (false, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        public async Task<List<SquadInviteItem>> RefreshSquadInvitesAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/squad/invites";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SquadInvitesResponse res = JsonUtility.FromJson<SquadInvitesResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.invites != null)
                    {
                        SquadInvites = res.invites;
                        return SquadInvites;
                    }
                }
            }
            return SquadInvites;
        }

        public async Task<bool> RespondSquadInviteAsync(int inviteId, bool accept)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/respond-invite";
            string bodyJson = $"{{\"inviteId\":{inviteId},\"action\":\"{(accept ? "accept" : "reject")}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMySquadAsync();
                    await RefreshSquadInvitesAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> ToggleSquadReadyAsync(bool isReady)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/ready";
            string bodyJson = $"{{\"isReady\":{(isReady ? "true" : "false")}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMySquadAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> LeaveSquadAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/squad/leave";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes("{}");
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                CurrentSquad = null;
                OnSquadChanged?.Invoke(null);
                return true;
            }
        }

        public async Task<bool> KickSquadMemberAsync(string targetPlayerId)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/kick";
            string bodyJson = $"{{\"targetPlayerId\":\"{Escape(targetPlayerId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMySquadAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> PromoteSquadMemberAsync(string targetPlayerId)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/promote";
            string bodyJson = $"{{\"targetPlayerId\":\"{Escape(targetPlayerId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMySquadAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<(bool success, string matchRoomId, string error)> StartSquadMatchAsync(string mode = "squad")
        {
            string url = $"{GetBaseUrl()}/api/social/squad/start-match";
            string bodyJson = $"{{\"mode\":\"{Escape(mode)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SquadMatchLaunchResponse res = JsonUtility.FromJson<SquadMatchLaunchResponse>(req.downloadHandler.text);
                    if (res != null && res.success && !string.IsNullOrEmpty(res.matchRoomId))
                    {
                        OnSquadMatchLaunched?.Invoke(res.matchRoomId);

                        // Direct connection via NetworkManager
                        if (NetworkManager.Instance != null)
                        {
                            _ = NetworkManager.Instance.JoinOrCreateRoomAsync(preferredRoom: res.matchRoomId, role: NetworkRole.Solo);
                        }

                        return (true, res.matchRoomId, null);
                    }
                    return (false, null, res?.error ?? "Match launch failed.");
                }
                return (false, null, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        public async Task<bool> SendSquadChatAsync(string message)
        {
            string url = $"{GetBaseUrl()}/api/social/squad/chat";
            string bodyJson = $"{{\"message\":\"{Escape(message)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshSquadChatAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<List<SquadChatItem>> RefreshSquadChatAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/squad/chat";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    SquadChatResponse res = JsonUtility.FromJson<SquadChatResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.messages != null)
                    {
                        SquadChat = res.messages;
                        OnSquadChatChanged?.Invoke(SquadChat);
                        return SquadChat;
                    }
                }
            }
            return SquadChat;
        }

        // =========================================================
        // 3. CLAN / GUILD SYSTEM (UP TO 50 PLAYERS)
        // =========================================================

        public async Task<(bool success, string error)> CreateClanAsync(string name, string tag, string description, int badgeId, int minLevel, bool isOpen)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/create";
            string bodyJson = $"{{\"name\":\"{Escape(name)}\",\"tag\":\"{Escape(tag)}\",\"description\":\"{Escape(description)}\",\"badgeId\":{badgeId},\"minLevel\":{minLevel},\"isOpen\":{(isOpen ? "true" : "false")}}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ClanResponse res = JsonUtility.FromJson<ClanResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.clan != null)
                    {
                        CurrentClan = res.clan;
                        MyClanRank = "leader";
                        OnClanChanged?.Invoke(CurrentClan);
                        return (true, null);
                    }
                    return (false, res?.error ?? "Failed to create clan.");
                }
                return (false, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        public async Task<ClanItem> RefreshMyClanAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/clan/my";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ClanResponse res = JsonUtility.FromJson<ClanResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        CurrentClan = res.hasClan ? res.clan : null;
                        MyClanRank = res.hasClan ? (res.myRank ?? "member") : "none";
                        OnClanChanged?.Invoke(CurrentClan);
                        return CurrentClan;
                    }
                }
            }
            return CurrentClan;
        }

        public async Task<List<ClanItem>> SearchClansAsync(string query = "")
        {
            string url = $"{GetBaseUrl()}/api/social/clan/list?search={UnityWebRequest.EscapeURL(query)}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", $"Bearer {token}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ClanListResponse res = JsonUtility.FromJson<ClanListResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.clans != null)
                    {
                        PublicClans = res.clans;
                        return PublicClans;
                    }
                }
            }
            return PublicClans;
        }

        public async Task<(bool success, string message)> JoinClanAsync(string clanId)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/join";
            string bodyJson = $"{{\"clanId\":\"{Escape(clanId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMyClanAsync();
                    return (true, "Clan join request processed.");
                }
                return (false, TryParseError(req.downloadHandler.text) ?? req.error);
            }
        }

        public async Task<List<ClanJoinRequestItem>> RefreshClanRequestsAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/clan/requests";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ClanRequestsResponse res = JsonUtility.FromJson<ClanRequestsResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.requests != null)
                    {
                        ClanRequests = res.requests;
                        return ClanRequests;
                    }
                }
            }
            return ClanRequests;
        }

        public async Task<bool> RespondClanRequestAsync(int requestId, bool accept)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/respond-request";
            string bodyJson = $"{{\"requestId\":{requestId},\"action\":\"{(accept ? "accept" : "reject")}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMyClanAsync();
                    await RefreshClanRequestsAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> LeaveClanAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/clan/leave";
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes("{}");
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                CurrentClan = null;
                MyClanRank = "none";
                OnClanChanged?.Invoke(null);
                return true;
            }
        }

        public async Task<bool> KickClanMemberAsync(string targetPlayerId)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/kick";
            string bodyJson = $"{{\"targetPlayerId\":\"{Escape(targetPlayerId)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMyClanAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> SetClanRankAsync(string targetPlayerId, string rank)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/set-rank";
            string bodyJson = $"{{\"targetPlayerId\":\"{Escape(targetPlayerId)}\",\"rank\":\"{Escape(rank)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshMyClanAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> UpdateClanAnnouncementAsync(string announcement)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/announcement";
            string bodyJson = $"{{\"announcement\":\"{Escape(announcement)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    if (CurrentClan != null) CurrentClan.announcement = announcement;
                    OnClanChanged?.Invoke(CurrentClan);
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> SendClanChatAsync(string message)
        {
            string url = $"{GetBaseUrl()}/api/social/clan/chat";
            string bodyJson = $"{{\"message\":\"{Escape(message)}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    await RefreshClanChatAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<List<ClanChatItem>> RefreshClanChatAsync()
        {
            string url = $"{GetBaseUrl()}/api/social/clan/chat";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {GetToken()}");

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    ClanChatResponse res = JsonUtility.FromJson<ClanChatResponse>(req.downloadHandler.text);
                    if (res != null && res.success && res.messages != null)
                    {
                        ClanChat = res.messages;
                        OnClanChatChanged?.Invoke(ClanChat);
                        return ClanChat;
                    }
                }
            }
            return ClanChat;
        }

        /// <summary>
        /// Formats player full display name with [CLAN] [SQUAD] tags.
        /// </summary>
        public string GetFormattedPlayerTag(string username = null)
        {
            string uName = username ?? (AccountManager.Instance != null ? AccountManager.Instance.Username : "Drifter");
            string clanTag = InClan ? CurrentClan.tag : "";
            string squadTag = InSquad ? CurrentSquad.tag : "";

            string full = "";
            if (!string.IsNullOrEmpty(clanTag)) full += $"<color=#ffcc00>[{clanTag}]</color> ";
            if (!string.IsNullOrEmpty(squadTag)) full += $"<color=#00e5ff>[{squadTag}]</color> ";
            full += uName;
            return full;
        }

        private static string Escape(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
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

