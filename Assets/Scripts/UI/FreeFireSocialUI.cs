using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Social;
using AetherSerpent.Account;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Free Fire-style Tactical Esport Social Grouping Interface.
    /// Provides:
    /// - Complete Friends System: Online/In-Match/Offline presence, requests, last login, quick squad invite
    /// - Squad System (4-5 players): Lobby with Ready toggle, Squad Level/XP, Leader Kick/Promote, Squad Chat, and Match Transition
    /// - Clan / Guild System (up to 50 players): Guild search, ranks (Leader/Officer/Member), announcements, roster, clan chat
    /// - Formatted nametags: [CLAN] [SQUAD] Username
    /// Toggleable via 'L' key.
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeFireSocialUI : MonoBehaviour
    {
        public static FreeFireSocialUI Instance { get; private set; }

        public bool IsVisible { get; private set; } = false;

        private enum SocialTab
        {
            Friends,
            Squad,
            Clan
        }

        private SocialTab currentTab = SocialTab.Friends;

        // Friends tab states
        private enum FriendFilter { All, Online, InMatch, Requests }
        private FriendFilter currentFriendFilter = FriendFilter.All;
        private string addFriendInput = "";

        // Squad tab states
        private string createSquadName = "";
        private string createSquadTag = "AES";
        private string squadChatInput = "";

        // Clan tab states
        private string createClanName = "";
        private string createClanTag = "ETRN";
        private string createClanDesc = "Ascend the celestial ranks together.";
        private int createClanBadge = 1;
        private int createClanMinLevel = 1;
        private bool createClanIsOpen = true;
        private string clanSearchInput = "";
        private string clanChatInput = "";
        private string editAnnouncementInput = "";
        private bool showAnnouncementEditor = false;

        // UI Scroll positions & Toast feedback
        private Vector2 friendsScroll;
        private Vector2 requestsScroll;
        private Vector2 squadMembersScroll;
        private Vector2 squadChatScroll;
        private Vector2 clanBrowseScroll;
        private Vector2 clanRosterScroll;
        private Vector2 clanChatScroll;

        private string statusFeedback = "";
        private float feedbackTimer = 0f;

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
            if (SocialGroupingManager.Instance != null)
            {
                SocialGroupingManager.Instance.OnSquadMatchLaunched += (roomId) =>
                {
                    ShowFeedback($"Squad Match Launched! Joining room {roomId}...");
                    IsVisible = false;
                };
            }
        }

        private void Update()
        {
            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.unscaledDeltaTime;
                if (feedbackTimer <= 0f) statusFeedback = "";
            }

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.lKey.wasPressedThisFrame || kb.f3Key.wasPressedThisFrame))
            {
                ToggleVisibility();
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.L) || UnityEngine.Input.GetKeyDown(KeyCode.F3))
            {
                ToggleVisibility();
            }
#endif
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            if (IsVisible && SocialGroupingManager.Instance != null)
            {
                _ = SocialGroupingManager.Instance.RefreshAllSocialAsync();
            }
        }

        public void OpenSquadTab()
        {
            IsVisible = true;
            currentTab = SocialTab.Squad;
            _ = SocialGroupingManager.Instance?.RefreshMySquadAsync();
        }

        public void OpenClanTab()
        {
            IsVisible = true;
            currentTab = SocialTab.Clan;
            _ = SocialGroupingManager.Instance?.RefreshMyClanAsync();
        }

        private void ShowFeedback(string msg, float duration = 3.5f)
        {
            statusFeedback = msg;
            feedbackTimer = duration;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            GUI.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0.98f);

            float w = Mathf.Min(740, Screen.width - 40);
            float h = Mathf.Min(620, Screen.height - 40);
            Rect mainRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.Box(mainRect, "");

            GUILayout.BeginArea(new Rect(mainRect.x + 16, mainRect.y + 12, mainRect.width - 32, mainRect.height - 24));

            DrawHeader();
            GUILayout.Space(8);

            DrawTabBar();
            GUILayout.Space(8);

            switch (currentTab)
            {
                case SocialTab.Friends:
                    DrawFriendsTab();
                    break;
                case SocialTab.Squad:
                    DrawSquadTab();
                    break;
                case SocialTab.Clan:
                    DrawClanTab();
                    break;
            }

            if (!string.IsNullOrEmpty(statusFeedback))
            {
                GUILayout.Space(4);
                GUI.color = Color.yellow;
                GUILayout.Label($"<b>[SOCIAL] {statusFeedback}</b>", GUILayout.Height(20));
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        private void DrawHeader()
        {
            var mgr = SocialGroupingManager.Instance;
            var acc = AccountManager.Instance;

            GUI.color = new Color(0.15f, 0.2f, 0.3f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();

            string formattedName = mgr != null ? mgr.GetFormattedPlayerTag() : (acc != null ? acc.Username : "Drifter");
            string uidStr = acc != null && acc.NumericId > 0 ? $"<color=#00e5ff>UID: {acc.NumericId}</color>" : "";

            GUILayout.Label($"<b><size=15>FREE FIRE SOCIAL HUB</size></b>  •  {formattedName}  {uidStr}", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("✕ CLOSE [L]", GUILayout.Width(90), GUILayout.Height(24)))
            {
                IsVisible = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawTabBar()
        {
            var mgr = SocialGroupingManager.Instance;
            int reqCount = mgr != null ? mgr.FriendRequests.Count : 0;
            string reqBadge = reqCount > 0 ? $" <color=#ff3333>({reqCount})</color>" : "";

            int invCount = mgr != null ? mgr.SquadInvites.Count : 0;
            string invBadge = invCount > 0 ? $" <color=#00ff66>({invCount})</color>" : "";

            string squadTitle = (mgr != null && mgr.InSquad) ? $"Squad [{mgr.CurrentSquad.tag}]" : $"Squad Lobby{invBadge}";
            string clanTitle = (mgr != null && mgr.InClan) ? $"Clan [{mgr.CurrentClan.tag}]" : "Clan / Guild";

            GUILayout.BeginHorizontal();

            if (DrawTabBtn($"Friends{reqBadge}", currentTab == SocialTab.Friends)) currentTab = SocialTab.Friends;
            if (DrawTabBtn(squadTitle, currentTab == SocialTab.Squad)) currentTab = SocialTab.Squad;
            if (DrawTabBtn(clanTitle, currentTab == SocialTab.Clan)) currentTab = SocialTab.Clan;

            GUILayout.EndHorizontal();
        }

        private bool DrawTabBtn(string label, bool active)
        {
            if (active) GUI.color = Color.cyan;
            bool clicked = GUILayout.Button(label, GUILayout.Height(28));
            GUI.color = Color.white;
            return clicked;
        }

        // =========================================================
        // TAB 1: FRIENDS SYSTEM
        // =========================================================

        private void DrawFriendsTab()
        {
            var mgr = SocialGroupingManager.Instance;
            var friends = mgr != null ? mgr.FriendsList : new List<FriendItem>();
            var requests = mgr != null ? mgr.FriendRequests : new List<FriendRequestItem>();

            // Top Bar: Add Friend
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Add Friend by UID or Name:", GUILayout.Width(170));
            addFriendInput = GUILayout.TextField(addFriendInput, 24, GUILayout.Width(180), GUILayout.Height(24));
            if (GUILayout.Button("SEND REQUEST", GUILayout.Width(120), GUILayout.Height(24)))
            {
                SendFriendRequest();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Filter Tabs
            GUILayout.BeginHorizontal();
            if (DrawFilterBtn($"ALL ({friends.Count})", currentFriendFilter == FriendFilter.All)) currentFriendFilter = FriendFilter.All;
            int onlineCount = friends.FindAll(f => f.status == "online").Count;
            if (DrawFilterBtn($"ONLINE ({onlineCount})", currentFriendFilter == FriendFilter.Online)) currentFriendFilter = FriendFilter.Online;
            int inMatchCount = friends.FindAll(f => f.status == "in_match").Count;
            if (DrawFilterBtn($"IN MATCH ({inMatchCount})", currentFriendFilter == FriendFilter.InMatch)) currentFriendFilter = FriendFilter.InMatch;
            string reqLabel = requests.Count > 0 ? $"REQUESTS <color=#ff3333>({requests.Count})</color>" : $"REQUESTS ({requests.Count})";
            if (DrawFilterBtn(reqLabel, currentFriendFilter == FriendFilter.Requests)) currentFriendFilter = FriendFilter.Requests;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (currentFriendFilter == FriendFilter.Requests)
            {
                // Incoming Requests View
                GUILayout.Label("<b>PENDING FRIEND REQUESTS</b>");
                if (requests.Count == 0)
                {
                    GUILayout.Label("<i>No pending incoming friend requests.</i>");
                }
                else
                {
                    requestsScroll = GUILayout.BeginScrollView(requestsScroll);
                    foreach (var req in requests)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        GUILayout.Label($"<b>{req.senderName}</b> (UID: {req.senderNumericId})", GUILayout.ExpandWidth(true));
                        GUI.color = Color.green;
                        if (GUILayout.Button("ACCEPT", GUILayout.Width(75), GUILayout.Height(22)))
                        {
                            RespondFriendRequest(req.id, true);
                        }
                        GUI.color = Color.red;
                        if (GUILayout.Button("REJECT", GUILayout.Width(75), GUILayout.Height(22)))
                        {
                            RespondFriendRequest(req.id, false);
                        }
                        GUI.color = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                }
            }
            else
            {
                // Friends List View
                var displayList = friends;
                if (currentFriendFilter == FriendFilter.Online) displayList = friends.FindAll(f => f.status == "online");
                else if (currentFriendFilter == FriendFilter.InMatch) displayList = friends.FindAll(f => f.status == "in_match");

                friendsScroll = GUILayout.BeginScrollView(friendsScroll);
                if (displayList.Count == 0)
                {
                    GUILayout.Label("<i>No friends found in this category.</i>");
                }
                else
                {
                    foreach (var f in displayList)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box);

                        // Friend Info
                        GUILayout.BeginVertical(GUILayout.Width(280));
                        GUILayout.Label($"<b>{f.GetDisplayTag()}</b>  {f.GetStatusBadge()}");
                        GUILayout.Label($"<size=10><color=#00e5ff>UID: {f.numericId}</color>  •  Last active: {FormatTimeAgo(f.lastLoginAt)}</size>");
                        GUILayout.EndVertical();

                        // Quick Invite to Squad
                        if (GUILayout.Button("⚡ SQUAD INVITE", GUILayout.Width(110), GUILayout.Height(26)))
                        {
                            QuickInviteFriendToSquad(f.playerId, f.username);
                        }

                        // Remove button
                        GUI.color = new Color(1f, 0.4f, 0.4f);
                        if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(26)))
                        {
                            RemoveFriend(f.playerId, f.username);
                        }
                        GUI.color = Color.white;

                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();
            }
        }

        private bool DrawFilterBtn(string label, bool active)
        {
            if (active) GUI.color = Color.yellow;
            bool clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.color = Color.white;
            return clicked;
        }

        private async void SendFriendRequest()
        {
            if (string.IsNullOrWhiteSpace(addFriendInput)) return;
            string input = addFriendInput.Trim();
            ShowFeedback("Sending friend request...");

            int numId = 0;
            int.TryParse(input, out numId);

            var (ok, msg) = await SocialGroupingManager.Instance.SendFriendRequestAsync(
                targetNumericId: numId,
                targetUsername: numId == 0 ? input : null
            );

            ShowFeedback(msg);
            if (ok) addFriendInput = "";
        }

        private async void RespondFriendRequest(int reqId, bool accept)
        {
            ShowFeedback(accept ? "Accepting friend request..." : "Rejecting friend request...");
            bool ok = await SocialGroupingManager.Instance.RespondFriendRequestAsync(reqId, accept);
            if (ok) ShowFeedback(accept ? "Friend added to your list!" : "Request rejected.");
        }

        private async void RemoveFriend(string friendId, string name)
        {
            bool ok = await SocialGroupingManager.Instance.RemoveFriendAsync(friendId);
            if (ok) ShowFeedback($"Removed {name} from friends list.");
        }

        private async void QuickInviteFriendToSquad(string friendPlayerId, string name)
        {
            var mgr = SocialGroupingManager.Instance;
            if (mgr == null) return;

            if (!mgr.InSquad)
            {
                // Auto create squad if not in one!
                ShowFeedback("Creating squad lobby...");
                var (created, _) = await mgr.CreateSquadAsync("Aether Squad", "AES");
                if (!created)
                {
                    ShowFeedback("Failed to initialize squad lobby.");
                    return;
                }
            }

            var (ok, err) = await mgr.InviteFriendToSquadAsync(friendPlayerId);
            if (ok) ShowFeedback($"Squad invitation sent to {name}!");
            else ShowFeedback($"Invite failed: {err}");
        }

        // =========================================================
        // TAB 2: SQUAD SYSTEM (4-5 PLAYERS LOBBY & READY SYSTEM)
        // =========================================================

        private void DrawSquadTab()
        {
            var mgr = SocialGroupingManager.Instance;

            if (mgr != null && mgr.InSquad)
            {
                DrawActiveSquadLobby(mgr.CurrentSquad);
            }
            else
            {
                DrawSquadCreationAndInvites();
            }
        }

        private void DrawSquadCreationAndInvites()
        {
            var mgr = SocialGroupingManager.Instance;
            var invites = mgr != null ? mgr.SquadInvites : new List<SquadInviteItem>();

            // Create Squad Panel
            GUI.color = new Color(0.12f, 0.22f, 0.35f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            GUILayout.Label("<b>CREATE TACTICAL SQUAD (4-5 PLAYERS)</b>");
            GUILayout.Label("<size=10>Assemble a strike team, coordinate via Squad Chat, toggle READY, and launch into matches together.</size>");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Squad Name:", GUILayout.Width(90));
            createSquadName = GUILayout.TextField(createSquadName, 20, GUILayout.Width(160));

            GUILayout.Label("Tag (e.g. AES):", GUILayout.Width(100));
            createSquadTag = GUILayout.TextField(createSquadTag, 5, GUILayout.Width(60));

            GUI.color = Color.green;
            if (GUILayout.Button("<b>CREATE SQUAD</b>", GUILayout.Width(140), GUILayout.Height(24)))
            {
                CreateSquad();
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Incoming Squad Invites
            GUILayout.Label("<b>INCOMING SQUAD INVITATIONS</b>");
            if (invites.Count == 0)
            {
                GUILayout.Label("<i>No pending squad invites. Ask a friend to invite you or create your own squad above!</i>");
            }
            else
            {
                foreach (var inv in invites)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"<b>[{inv.squadTag}] {inv.squadName}</b>  •  Invited by: <b>{inv.inviterName}</b>", GUILayout.ExpandWidth(true));
                    GUI.color = Color.green;
                    if (GUILayout.Button("ACCEPT & JOIN", GUILayout.Width(130), GUILayout.Height(24)))
                    {
                        AcceptSquadInvite(inv.id);
                    }
                    GUI.color = Color.gray;
                    if (GUILayout.Button("DECLINE", GUILayout.Width(80), GUILayout.Height(24)))
                    {
                        DeclineSquadInvite(inv.id);
                    }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
            }
        }

        private async void CreateSquad()
        {
            string sName = string.IsNullOrWhiteSpace(createSquadName) ? "Aether Squad" : createSquadName.Trim();
            string sTag = string.IsNullOrWhiteSpace(createSquadTag) ? "AES" : createSquadTag.Trim();

            ShowFeedback("Establishing tactical squad on Cloudflare edge...");
            var (ok, err) = await SocialGroupingManager.Instance.CreateSquadAsync(sName, sTag);
            if (ok) ShowFeedback("Squad lobby established!");
            else ShowFeedback($"Failed: {err}");
        }

        private async void AcceptSquadInvite(int inviteId)
        {
            ShowFeedback("Joining squad lobby...");
            bool ok = await SocialGroupingManager.Instance.RespondSquadInviteAsync(inviteId, true);
            if (ok) ShowFeedback("Joined squad lobby!");
        }

        private async void DeclineSquadInvite(int inviteId)
        {
            await SocialGroupingManager.Instance.RespondSquadInviteAsync(inviteId, false);
            ShowFeedback("Squad invite declined.");
        }

        private void DrawActiveSquadLobby(SquadItem squad)
        {
            var mgr = SocialGroupingManager.Instance;
            string myId = AccountManager.Instance != null ? AccountManager.Instance.PlayerId : "";
            bool isLeader = mgr != null && mgr.IsSquadLeader;

            // Header Info Bar
            GUI.color = new Color(0.1f, 0.35f, 0.45f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><size=15>[{squad.tag}] {squad.name}</size></b>   <color=#ffcc00>LV. {squad.squadLevel} ({squad.squadXp} XP)</color>", GUILayout.ExpandWidth(true));

            int readyCount = squad.members.FindAll(m => m.isReady).Count;
            bool allReady = squad.AreAllMembersReady();
            GUI.color = allReady ? Color.green : Color.yellow;
            GUILayout.Label($"<b>{readyCount} / {squad.members.Count} READY</b>", GUILayout.Width(110));
            GUI.color = Color.white;

            if (GUILayout.Button("LEAVE SQUAD", GUILayout.Width(100), GUILayout.Height(22)))
            {
                _ = SocialGroupingManager.Instance.LeaveSquadAsync();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // 5-Player Member Slots Grid
            GUILayout.Label("<b>SQUAD LOBBY ROSTER (MAX 5 PLAYERS)</b>");
            squadMembersScroll = GUILayout.BeginScrollView(squadMembersScroll, GUILayout.Height(150));

            for (int i = 0; i < 5; i++)
            {
                if (i < squad.members.Count)
                {
                    var m = squad.members[i];
                    bool isMe = (m.playerId == myId);

                    GUILayout.BeginHorizontal(GUI.skin.box);

                    string roleBadge = m.role == "leader" ? "<color=#ffcc00>👑 LEADER</color>" : "MEMBER";
                    string readyBadge = m.isReady ? "<color=#00ff66>✅ READY</color>" : "<color=#ff9900>⏳ NOT READY</color>";

                    GUILayout.Label($"Slot #{i + 1}: <b>{m.username}</b> (UID: {m.numericId})  •  {roleBadge}  •  {readyBadge}", GUILayout.ExpandWidth(true));

                    // Leader action options on other members
                    if (isLeader && !isMe)
                    {
                        if (GUILayout.Button("PROMOTE", GUILayout.Width(75), GUILayout.Height(20)))
                        {
                            _ = SocialGroupingManager.Instance.PromoteSquadMemberAsync(m.playerId);
                        }
                        GUI.color = Color.red;
                        if (GUILayout.Button("KICK", GUILayout.Width(55), GUILayout.Height(20)))
                        {
                            _ = SocialGroupingManager.Instance.KickSquadMemberAsync(m.playerId);
                        }
                        GUI.color = Color.white;
                    }

                    GUILayout.EndHorizontal();
                }
                else
                {
                    // Empty slot
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"Slot #{i + 1}: <i>[Empty Slot]</i>", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("➕ INVITE FRIEND", GUILayout.Width(130), GUILayout.Height(20)))
                    {
                        currentTab = SocialTab.Friends;
                        currentFriendFilter = FriendFilter.Online;
                        ShowFeedback("Select a friend to invite to your squad!");
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // Squad Chat Stream
            GUILayout.Label("<b>SQUAD CHAT</b>");
            squadChatScroll = GUILayout.BeginScrollView(squadChatScroll, GUILayout.Height(100));
            var chatList = mgr != null ? mgr.SquadChat : new List<SquadChatItem>();
            if (chatList.Count == 0)
            {
                GUILayout.Label("<i>No chat messages yet. Plan your battle tactics here!</i>");
            }
            else
            {
                foreach (var msg in chatList)
                {
                    GUILayout.Label($"<b><color=#00e5ff>{msg.senderName}:</color></b> {msg.message}");
                }
            }
            GUILayout.EndScrollView();

            // Chat input row
            GUILayout.BeginHorizontal();
            squadChatInput = GUILayout.TextField(squadChatInput, 120, GUILayout.ExpandWidth(true), GUILayout.Height(24));
            if (GUILayout.Button("SEND", GUILayout.Width(75), GUILayout.Height(24)))
            {
                SendSquadChatMessage();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Bottom Lobby Control Bar
            GUILayout.BeginHorizontal();

            // Ready Toggle Button
            var myMember = squad.members.Find(m => m.playerId == myId);
            bool myReady = myMember != null && myMember.isReady;

            GUI.color = myReady ? Color.green : Color.yellow;
            if (GUILayout.Button(myReady ? "<b>✅ YOU ARE READY (CLICK TO CANCEL)</b>" : "<b>⏳ CLICK TO READY UP</b>", GUILayout.Height(36), GUILayout.Width(260)))
            {
                _ = SocialGroupingManager.Instance.ToggleSquadReadyAsync(!myReady);
            }
            GUI.color = Color.white;

            // Launch Match (Leader Only)
            if (isLeader)
            {
                bool canLaunch = squad.AreAllMembersReady();
                GUI.color = canLaunch ? Color.green : Color.gray;
                if (GUILayout.Button(canLaunch ? "<b>🚀 LAUNCH SQUAD MATCH</b>" : "<b>WAITING FOR ALL READY</b>", GUILayout.Height(36), GUILayout.ExpandWidth(true)))
                {
                    if (canLaunch)
                    {
                        LaunchSquadMatch();
                    }
                    else
                    {
                        ShowFeedback("All squad members must click READY before launching match!");
                    }
                }
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("<i>Waiting for Squad Leader to launch match...</i>", GUILayout.Height(36));
            }

            GUILayout.EndHorizontal();
        }

        private async void SendSquadChatMessage()
        {
            if (string.IsNullOrWhiteSpace(squadChatInput)) return;
            string text = squadChatInput.Trim();
            squadChatInput = "";
            await SocialGroupingManager.Instance.SendSquadChatAsync(text);
        }

        private async void LaunchSquadMatch()
        {
            ShowFeedback("Launching squad match on Cloudflare game room...");
            var (ok, roomId, err) = await SocialGroupingManager.Instance.StartSquadMatchAsync("squad");
            if (ok)
            {
                ShowFeedback($"Match launched! Joining room {roomId}...");
                IsVisible = false;
            }
            else
            {
                ShowFeedback($"Launch failed: {err}");
            }
        }

        // =========================================================
        // TAB 3: CLAN / GUILD SYSTEM (UP TO 50 PLAYERS)
        // =========================================================

        private void DrawClanTab()
        {
            var mgr = SocialGroupingManager.Instance;

            if (mgr != null && mgr.InClan)
            {
                DrawActiveClanHub(mgr.CurrentClan);
            }
            else
            {
                DrawClanBrowseAndCreation();
            }
        }

        private void DrawClanBrowseAndCreation()
        {
            var mgr = SocialGroupingManager.Instance;
            var clans = mgr != null ? mgr.PublicClans : new List<ClanItem>();

            // Create Clan Banner
            GUI.color = new Color(0.25f, 0.18f, 0.08f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            GUILayout.Label("<b>FOUND A CELESTIAL GUILD / CLAN (UP TO 50 PLAYERS)</b>");
            GUILayout.Label("<size=10>Build an esport empire, manage officers, post daily announcements, and compete on guild leaderboards.</size>");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Guild Name:", GUILayout.Width(75));
            createClanName = GUILayout.TextField(createClanName, 24, GUILayout.Width(140));

            GUILayout.Label("Tag (e.g. ETRN):", GUILayout.Width(100));
            createClanTag = GUILayout.TextField(createClanTag, 5, GUILayout.Width(55));

            GUILayout.Label("Min LV:", GUILayout.Width(50));
            int.TryParse(GUILayout.TextField(createClanMinLevel.ToString(), 2, GUILayout.Width(35)), out createClanMinLevel);

            GUI.color = Color.yellow;
            if (GUILayout.Button("<b>FOUND CLAN</b>", GUILayout.Width(120), GUILayout.Height(24)))
            {
                FoundClan();
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // Browse Clans List
            GUILayout.Label("<b>BROWSE & SEARCH PUBLIC GUILDS</b>");
            GUILayout.BeginHorizontal();
            clanSearchInput = GUILayout.TextField(clanSearchInput, 20, GUILayout.ExpandWidth(true), GUILayout.Height(24));
            if (GUILayout.Button("SEARCH", GUILayout.Width(80), GUILayout.Height(24)))
            {
                _ = SocialGroupingManager.Instance.SearchClansAsync(clanSearchInput);
            }
            if (GUILayout.Button("REFRESH", GUILayout.Width(80), GUILayout.Height(24)))
            {
                _ = SocialGroupingManager.Instance.SearchClansAsync("");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            clanBrowseScroll = GUILayout.BeginScrollView(clanBrowseScroll);
            if (clans.Count == 0)
            {
                GUILayout.Label("<i>No clans listed. Click SEARCH or REFRESH to explore celestial guilds.</i>");
            }
            else
            {
                foreach (var c in clans)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);

                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    GUILayout.Label($"<b>[{c.tag}] {c.name}</b>  <color=#ffcc00>LV. {c.clanLevel}</color>  •  Members: <b>{c.memberCount} / {c.maxMembers}</b>");
                    GUILayout.Label($"<size=10><i>\"{c.description}\"</i>  •  Min Level: {c.minLevel}  •  Leader: {c.leaderName}</size>");
                    GUILayout.EndVertical();

                    GUI.color = Color.green;
                    if (GUILayout.Button("JOIN GUILD", GUILayout.Width(100), GUILayout.Height(28)))
                    {
                        JoinClan(c.clanId);
                    }
                    GUI.color = Color.white;

                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        private async void FoundClan()
        {
            if (string.IsNullOrWhiteSpace(createClanName) || string.IsNullOrWhiteSpace(createClanTag))
            {
                ShowFeedback("Clan Name and Tag are required.");
                return;
            }

            ShowFeedback("Founding celestial guild on Cloudflare edge...");
            var (ok, err) = await SocialGroupingManager.Instance.CreateClanAsync(
                createClanName.Trim(),
                createClanTag.Trim(),
                createClanDesc,
                createClanBadge,
                createClanMinLevel,
                createClanIsOpen
            );

            if (ok) ShowFeedback("Guild created successfully!");
            else ShowFeedback($"Failed to create guild: {err}");
        }

        private async void JoinClan(string clanId)
        {
            ShowFeedback("Submitting guild join request...");
            var (ok, msg) = await SocialGroupingManager.Instance.JoinClanAsync(clanId);
            ShowFeedback(msg);
        }

        private void DrawActiveClanHub(ClanItem clan)
        {
            var mgr = SocialGroupingManager.Instance;
            bool isOfficerOrLeader = mgr != null && mgr.IsClanOfficerOrLeader;
            bool isLeader = mgr != null && mgr.IsClanLeader;
            string myId = AccountManager.Instance != null ? AccountManager.Instance.PlayerId : "";

            // Guild Header Banner
            GUI.color = new Color(0.3f, 0.2f, 0.05f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><size=16>[{clan.tag}] {clan.name}</size></b>   <color=#ffcc00>GUILD LV. {clan.clanLevel} ({clan.clanXp} XP)</color>", GUILayout.ExpandWidth(true));
            GUILayout.Label($"<b>{clan.onlineCount} / {clan.memberCount} ONLINE</b>", GUILayout.Width(120));

            if (GUILayout.Button("LEAVE GUILD", GUILayout.Width(100), GUILayout.Height(22)))
            {
                _ = SocialGroupingManager.Instance.LeaveClanAsync();
            }
            GUILayout.EndHorizontal();

            // Daily Announcement
            GUILayout.Space(4);
            GUILayout.Label($"<b>BULLETIN:</b> <color=#00e5ff>\"{clan.announcement}\"</color>");
            if (isOfficerOrLeader)
            {
                if (showAnnouncementEditor)
                {
                    GUILayout.BeginHorizontal();
                    editAnnouncementInput = GUILayout.TextField(editAnnouncementInput, 150, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("SAVE", GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        _ = SocialGroupingManager.Instance.UpdateClanAnnouncementAsync(editAnnouncementInput);
                        showAnnouncementEditor = false;
                    }
                    if (GUILayout.Button("CANCEL", GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        showAnnouncementEditor = false;
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    if (GUILayout.Button("EDIT ANNOUNCEMENT", GUILayout.Width(150), GUILayout.Height(18)))
                    {
                        editAnnouncementInput = clan.announcement;
                        showAnnouncementEditor = true;
                    }
                }
            }

            GUILayout.EndVertical();

            GUILayout.Space(6);

            // Member Roster (Up to 50 players)
            GUILayout.Label($"<b>GUILD ROSTER ({clan.memberCount} / {clan.maxMembers} PLAYERS)</b>");
            clanRosterScroll = GUILayout.BeginScrollView(clanRosterScroll, GUILayout.Height(130));

            if (clan.members != null)
            {
                foreach (var m in clan.members)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);

                    string rankBadge = m.rank == "leader" ? "<color=#ffcc00>👑 LEADER</color>" :
                                      m.rank == "officer" ? "<color=#00e5ff>⭐ OFFICER</color>" : "MEMBER";

                    string onlineBadge = m.isOnline ? "<color=#00ff66>● ONLINE</color>" : "<color=#888888>○ OFFLINE</color>";

                    GUILayout.Label($"<b>{m.username}</b> (UID: {m.numericId})  •  {rankBadge}  •  {onlineBadge}  •  Contr: {m.contribution} XP", GUILayout.ExpandWidth(true));

                    // Leader rank promotion/demotion
                    if (isLeader && m.playerId != myId)
                    {
                        if (m.rank == "member")
                        {
                            if (GUILayout.Button("PROMOTE", GUILayout.Width(75), GUILayout.Height(20)))
                            {
                                _ = SocialGroupingManager.Instance.SetClanRankAsync(m.playerId, "officer");
                            }
                        }
                        else if (m.rank == "officer")
                        {
                            if (GUILayout.Button("DEMOTE", GUILayout.Width(70), GUILayout.Height(20)))
                            {
                                _ = SocialGroupingManager.Instance.SetClanRankAsync(m.playerId, "member");
                            }
                        }
                    }

                    // Kick member
                    if (isOfficerOrLeader && m.playerId != myId && m.rank != "leader")
                    {
                        GUI.color = Color.red;
                        if (GUILayout.Button("KICK", GUILayout.Width(50), GUILayout.Height(20)))
                        {
                            _ = SocialGroupingManager.Instance.KickClanMemberAsync(m.playerId);
                        }
                        GUI.color = Color.white;
                    }

                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);

            // Clan Chat
            GUILayout.Label("<b>GUILD CHAT STREAM</b>");
            clanChatScroll = GUILayout.BeginScrollView(clanChatScroll, GUILayout.Height(95));
            var clanMessages = mgr != null ? mgr.ClanChat : new List<ClanChatItem>();
            if (clanMessages.Count == 0)
            {
                GUILayout.Label("<i>No guild messages yet. Greet your fellow guild members!</i>");
            }
            else
            {
                foreach (var msg in clanMessages)
                {
                    string rBadge = msg.senderRank == "leader" ? "<color=#ffcc00>[Leader]</color> " :
                                   msg.senderRank == "officer" ? "<color=#00e5ff>[Officer]</color> " : "";
                    GUILayout.Label($"<b>{rBadge}{msg.senderName}:</b> {msg.message}");
                }
            }
            GUILayout.EndScrollView();

            // Clan Chat Input Row
            GUILayout.BeginHorizontal();
            clanChatInput = GUILayout.TextField(clanChatInput, 140, GUILayout.ExpandWidth(true), GUILayout.Height(24));
            if (GUILayout.Button("SEND", GUILayout.Width(75), GUILayout.Height(24)))
            {
                SendClanChatMessage();
            }
            GUILayout.EndHorizontal();
        }

        private async void SendClanChatMessage()
        {
            if (string.IsNullOrWhiteSpace(clanChatInput)) return;
            string text = clanChatInput.Trim();
            clanChatInput = "";
            await SocialGroupingManager.Instance.SendClanChatAsync(text);
        }

        private string FormatTimeAgo(long timestamp)
        {
            if (timestamp <= 0) return "Just now";
            long diffSec = Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp) / 1000);
            if (diffSec < 60) return "Just now";
            if (diffSec < 3600) return $"{diffSec / 60}m ago";
            if (diffSec < 86400) return $"{diffSec / 3600}h ago";
            return $"{diffSec / 86400}d ago";
        }
    }
}

