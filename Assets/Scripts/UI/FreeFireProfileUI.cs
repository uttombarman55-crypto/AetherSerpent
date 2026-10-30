using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Account;
using AetherSerpent.Social;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Free Fire-style Tactical Esport Account & Identity Interface.
    /// Provides:
    /// - Esport Profile Banner, Avatar & Frame presentation
    /// - Permanent 9-digit Player ID with 1-click clipboard copy
    /// - Career stats, level & EXP progress, evolution showcase
    /// - Cosmetic Locker customization (Avatars, Frames, Banners, Bio, Squad Tag)
    /// - Esport Privacy controls & Block list management
    /// - Guest Account upgrade & Emergency Recovery Code management
    /// - Searchable Player Inspector with 9-digit ID lookup and Report modal
    /// Toggleable via 'P' hotkey or in-game button.
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeFireProfileUI : MonoBehaviour
    {
        public static FreeFireProfileUI Instance { get; private set; }

        public bool IsVisible { get; private set; } = false;

        private enum ProfileTab
        {
            Overview,
            Customization,
            Privacy,
            AccountManagement,
            InspectSearch
        }

        private ProfileTab currentTab = ProfileTab.Overview;

        // Customization Edit State
        private int editAvatarId = 1;
        private int editFrameId = 1;
        private int editBannerId = 1;
        private string editBio = "";
        private string editSquad = "";

        // Privacy Edit State
        private int editAllowInvites = 0;
        private int editAllowMessages = 0;
        private bool editShowOnline = true;

        // Account Form States
        private string regUsernameInput = "";
        private string regPasswordInput = "";
        private string changeUsernameInput = "";
        private string recoverTargetInput = "";
        private string recoverCodeInput = "";
        private string recoverNewPassInput = "";

        // Search & Inspect State
        private string searchInput = "";
        private AccountProfile inspectedProfile = null;
        private bool isSearching = false;

        // Report Modal State
        private bool showReportModal = false;
        private string reportReason = "Cheating / Third-Party Tools";
        private string reportDetails = "";

        // Notifications & Feedback
        private string statusFeedback = "";
        private float feedbackTimer = 0f;
        private Vector2 scrollPos;
        private Vector2 blockScrollPos;

        // Visual Colors & Cosmetic Catalog
        private static readonly string[] AvatarNames = {
            "Neon Serpent", "Void Leviathan", "Solar Drake", "Abyssal Wyrm", "Celestial Dragon", "Cyber Ouroboros"
        };
        private static readonly string[] FrameNames = {
            "Standard Bronze", "Silver Fang", "Royal Gold", "Aether Prismatic"
        };
        private static readonly string[] BannerNames = {
            "Cosmic Void", "Solar Magma", "Celestial Aurora", "Emerald Abyss"
        };
        private static readonly string[] InviteOptions = { "Everyone", "Friends Only", "Nobody" };
        private static readonly string[] ReportReasons = {
            "Cheating / Speed Hack / Modding",
            "Toxicity / Harassment",
            "Voice Abuse / Audio Spam",
            "Inappropriate Name or Tag",
            "Griefing / Match Sabotage",
            "Other Violation"
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
            if (AccountManager.Instance != null)
            {
                AccountManager.Instance.OnProfileUpdated += SyncFromProfile;
                AccountManager.Instance.OnAccountStateChanged += (loggedIn, prof) => SyncFromProfile(prof);
                AccountManager.Instance.OnPrivacyUpdated += SyncFromPrivacy;
                SyncFromProfile(AccountManager.Instance.Profile);
                SyncFromPrivacy(AccountManager.Instance.PrivacySettings);
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
            if (kb != null)
            {
                if (kb.pKey.wasPressedThisFrame || kb.f2Key.wasPressedThisFrame)
                {
                    ToggleVisibility();
                }
                else if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (showReportModal) showReportModal = false;
                    else if (IsVisible) IsVisible = false;
                }
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.P) || UnityEngine.Input.GetKeyDown(KeyCode.F2))
            {
                ToggleVisibility();
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (showReportModal) showReportModal = false;
                else if (IsVisible) IsVisible = false;
            }
#endif
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            if (IsVisible)
            {
                if (AccountManager.Instance != null)
                {
                    SyncFromProfile(AccountManager.Instance.Profile);
                    SyncFromPrivacy(AccountManager.Instance.PrivacySettings);
                    _ = AccountManager.Instance.FetchPrivacySettingsAsync();
                    _ = AccountManager.Instance.FetchBlocklistAsync();
                }
            }
        }

        private void SyncFromProfile(AccountProfile prof)
        {
            if (prof == null) return;
            editAvatarId = Mathf.Clamp(prof.avatarId, 1, 6);
            editFrameId = Mathf.Clamp(prof.frameId, 1, 4);
            editBannerId = Mathf.Clamp(prof.bannerId, 1, 4);
            editBio = prof.bio ?? "";
            editSquad = prof.squadName ?? "";
        }

        private void SyncFromPrivacy(PrivacySettingsData priv)
        {
            if (priv == null) return;
            editAllowInvites = Mathf.Clamp(priv.allowInvites, 0, 2);
            editAllowMessages = Mathf.Clamp(priv.allowMessages, 0, 2);
            editShowOnline = priv.showOnlineStatus;
        }

        private void ShowFeedback(string msg, float duration = 3.5f)
        {
            statusFeedback = msg;
            feedbackTimer = duration;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            // Free Fire Dark Obsidian / Tactical HUD Theme
            GUI.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 0.96f);

            float w = Mathf.Min(680, Screen.width - 40);
            float h = Mathf.Min(600, Screen.height - 40);
            Rect mainRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.Box(mainRect, "");

            GUILayout.BeginArea(new Rect(mainRect.x + 15, mainRect.y + 12, mainRect.width - 30, mainRect.height - 24));

            DrawProfileHeader();
            GUILayout.Space(8);

            DrawTabBar();
            GUILayout.Space(8);

            scrollPos = GUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case ProfileTab.Overview:
                    DrawOverviewTab();
                    break;
                case ProfileTab.Customization:
                    DrawCustomizationTab();
                    break;
                case ProfileTab.Privacy:
                    DrawPrivacyTab();
                    break;
                case ProfileTab.AccountManagement:
                    DrawAccountTab();
                    break;
                case ProfileTab.InspectSearch:
                    DrawInspectSearchTab();
                    break;
            }

            GUILayout.EndScrollView();

            // Status message toast
            if (!string.IsNullOrEmpty(statusFeedback))
            {
                GUILayout.Space(4);
                GUI.color = Color.yellow;
                GUILayout.Label($"<b>[SYSTEM] {statusFeedback}</b>", GUILayout.Height(22));
                GUI.color = Color.white;
            }

            if (showReportModal)
            {
                DrawReportModalOverlay(mainRect);
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Renders Free Fire Signature Banner Header (Banner, Frame, Avatar, ID, Level, Bio)
        /// </summary>
        private void DrawProfileHeader()
        {
            AccountProfile prof = AccountManager.Instance != null ? AccountManager.Instance.Profile : new AccountProfile();
            int bId = Mathf.Clamp(prof.bannerId, 1, 4);
            int fId = Mathf.Clamp(prof.frameId, 1, 4);
            int aId = Mathf.Clamp(prof.avatarId, 1, 6);

            // Banner color accent
            Color bannerColor = (bId == 1) ? new Color(0.2f, 0.1f, 0.4f) :
                               (bId == 2) ? new Color(0.45f, 0.15f, 0.05f) :
                               (bId == 3) ? new Color(0.05f, 0.35f, 0.4f) :
                                            new Color(0.05f, 0.35f, 0.15f);

            Color frameColor = (fId == 1) ? new Color(0.75f, 0.45f, 0.2f) :
                              (fId == 2) ? new Color(0.8f, 0.85f, 0.95f) :
                              (fId == 3) ? new Color(1.0f, 0.84f, 0.0f) :
                                           new Color(0.2f, 0.95f, 1.0f);

            // Header Banner Container
            GUI.color = bannerColor;
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();

            // Avatar & Frame Box
            GUI.color = frameColor;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(72), GUILayout.Height(72));
            GUI.color = Color.white;
            GUILayout.Label($"<b>AVATAR</b>\n#{aId}", GUILayout.Width(64), GUILayout.Height(36));
            GUILayout.Label($"<size=9>{AvatarNames[aId - 1]}</size>", GUILayout.Width(64));
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Identity Info
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            string clanTag = SocialGroupingManager.Instance != null && SocialGroupingManager.Instance.InClan ? $"<color=#ffcc00>[{SocialGroupingManager.Instance.CurrentClan.tag}]</color> " : "";
            string squadTag = SocialGroupingManager.Instance != null && SocialGroupingManager.Instance.InSquad ? $"<color=#00e5ff>[{SocialGroupingManager.Instance.CurrentSquad.tag}]</color> " :
                             (!string.IsNullOrEmpty(prof.squadName) ? $"<color=#00e5ff>[{prof.squadName}]</color> " : "");
            GUILayout.Label($"<b><size=15>{clanTag}{squadTag}{prof.username}</size></b>", GUILayout.ExpandWidth(true));

            // Close button
            if (GUILayout.Button("✕ CLOSE", GUILayout.Width(75), GUILayout.Height(24)))
            {
                IsVisible = false;
            }
            GUILayout.EndHorizontal();

            // 9-digit Player ID + Copy Button
            GUILayout.BeginHorizontal();
            GUI.color = Color.cyan;
            GUILayout.Label($"<b>UID: {prof.numericId}</b>", GUILayout.Width(130));
            GUI.color = Color.white;

            if (GUILayout.Button("📋 COPY ID", GUILayout.Width(80), GUILayout.Height(20)))
            {
                GUIUtility.systemCopyBuffer = prof.numericId.ToString();
                ShowFeedback($"Player ID '{prof.numericId}' copied to clipboard!");
            }

            // Account status badge
            if (prof.isGuest)
            {
                GUI.color = new Color(1f, 0.6f, 0.1f);
                GUILayout.Label("• GUEST ACCOUNT", GUILayout.Width(130));
                GUI.color = Color.white;
                if (GUILayout.Button("UPGRADE", GUILayout.Width(75), GUILayout.Height(20)))
                {
                    currentTab = ProfileTab.AccountManagement;
                }
            }
            else
            {
                GUI.color = Color.green;
                GUILayout.Label("✔ OFFICIAL ACCOUNT", GUILayout.Width(140));
                GUI.color = Color.white;
            }

            GUILayout.EndHorizontal();

            // Level & EXP Progress
            int nextExp = prof.level * 500;
            float progress = Mathf.Clamp01((float)prof.exp / Mathf.Max(1, nextExp));
            GUILayout.BeginHorizontal();
            GUI.color = Color.yellow;
            GUILayout.Label($"<b>LV. {prof.level}</b>", GUILayout.Width(50));
            GUI.color = Color.white;
            GUILayout.Label($"EXP: {prof.exp} / {nextExp} ({Mathf.RoundToInt(progress * 100)}%)", GUILayout.Width(160));
            GUILayout.Label($"<i>\"{prof.bio}\"</i>", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Free Fire-style Tab bar
        /// </summary>
        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();

            if (DrawTabButton("Overview", currentTab == ProfileTab.Overview)) currentTab = ProfileTab.Overview;
            if (DrawTabButton("Locker Customization", currentTab == ProfileTab.Customization)) currentTab = ProfileTab.Customization;
            if (DrawTabButton("Privacy & Blocked", currentTab == ProfileTab.Privacy)) currentTab = ProfileTab.Privacy;
            if (DrawTabButton("Account & Recovery", currentTab == ProfileTab.AccountManagement)) currentTab = ProfileTab.AccountManagement;
            if (DrawTabButton("🔍 Find Player", currentTab == ProfileTab.InspectSearch)) currentTab = ProfileTab.InspectSearch;

            GUILayout.EndHorizontal();
        }

        private bool DrawTabButton(string label, bool active)
        {
            if (active) GUI.color = Color.cyan;
            bool clicked = GUILayout.Button(label, GUILayout.Height(28));
            GUI.color = Color.white;
            return clicked;
        }

        /// <summary>
        /// TAB 1: Overview & Career stats
        /// </summary>
        private void DrawOverviewTab()
        {
            AccountProfile prof = AccountManager.Instance != null ? AccountManager.Instance.Profile : new AccountProfile();

            GUILayout.Label("<b>BATTLE CAREER STATS</b>");
            GUILayout.BeginHorizontal(GUI.skin.box);
            DrawStatBlock("Matches Played", prof.matchesPlayed.ToString());
            DrawStatBlock("Total Preys Consumed", prof.totalKills.ToString());
            DrawStatBlock("Highest Score", prof.highestScore.ToString());
            DrawStatBlock("Longest Serpent Length", $"{prof.highestLength}m");
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("<b>FAVORITE EVOLUTION FORM</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Active Evolution: <b><color=#00e5ff>{prof.GetEvolutionName()}</color></b>");
            GUILayout.Label("Mastery Perks: Enhanced Kinetic Mass, Extended Celestial Orbit, Void Resilience.");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            GUILayout.Label("<b>IDENTITY BADGES & COSMETICS EQUIPPED</b>");
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"Avatar: <b>{AvatarNames[Mathf.Clamp(prof.avatarId, 1, 6) - 1]}</b>");
            GUILayout.Label($"Name Frame: <b>{FrameNames[Mathf.Clamp(prof.frameId, 1, 4) - 1]}</b>");
            GUILayout.Label($"Banner: <b>{BannerNames[Mathf.Clamp(prof.bannerId, 1, 4) - 1]}</b>");
            GUILayout.EndHorizontal();
        }

        private void DrawStatBlock(string title, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
            GUILayout.Label($"<size=10>{title}</size>");
            GUI.color = Color.yellow;
            GUILayout.Label($"<b><size=14>{value}</size></b>");
            GUI.color = Color.white;
            GUILayout.EndVertical();
        }

        /// <summary>
        /// TAB 2: Cosmetic Locker Customization
        /// </summary>
        private void DrawCustomizationTab()
        {
            GUILayout.Label("<b>COSMETIC VAULT & IDENTITY CUSTOMIZATION</b>");

            // Avatar selection
            GUILayout.Label("<b>Select Avatar:</b>");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= AvatarNames.Length; i++)
            {
                bool isSelected = (editAvatarId == i);
                if (isSelected) GUI.color = Color.cyan;
                if (GUILayout.Button($"#{i} {AvatarNames[i - 1]}", GUILayout.Height(30)))
                {
                    editAvatarId = i;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Frame selection
            GUILayout.Label("<b>Select Name Frame:</b>");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= FrameNames.Length; i++)
            {
                bool isSelected = (editFrameId == i);
                if (isSelected) GUI.color = Color.cyan;
                if (GUILayout.Button($"#{i} {FrameNames[i - 1]}", GUILayout.Height(30)))
                {
                    editFrameId = i;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Banner selection
            GUILayout.Label("<b>Select Header Banner:</b>");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= BannerNames.Length; i++)
            {
                bool isSelected = (editBannerId == i);
                if (isSelected) GUI.color = Color.cyan;
                if (GUILayout.Button($"#{i} {BannerNames[i - 1]}", GUILayout.Height(30)))
                {
                    editBannerId = i;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Bio & Squad Inputs
            GUILayout.Label("<b>Squad Tag (Max 24 chars):</b>");
            editSquad = GUILayout.TextField(editSquad, 24);

            GUILayout.Space(6);
            GUILayout.Label("<b>Signature Bio Quote (Max 120 chars):</b>");
            editBio = GUILayout.TextField(editBio, 120);

            GUILayout.Space(14);

            GUI.color = Color.green;
            if (GUILayout.Button("<b>SAVE PROFILE CUSTOMIZATIONS</b>", GUILayout.Height(36)))
            {
                SaveCustomizations();
            }
            GUI.color = Color.white;
        }

        private async void SaveCustomizations()
        {
            if (AccountManager.Instance == null) return;
            ShowFeedback("Saving customizations to Cloudflare edge...");
            var (success, error) = await AccountManager.Instance.UpdateProfileAsync(editAvatarId, editFrameId, editBannerId, editBio, editSquad);
            if (success)
            {
                ShowFeedback("Profile customizations saved successfully!");
            }
            else
            {
                ShowFeedback($"Failed to save: {error}");
            }
        }

        /// <summary>
        /// TAB 3: Privacy & Block list
        /// </summary>
        private void DrawPrivacyTab()
        {
            GUILayout.Label("<b>ESPORT PRIVACY & SOCIAL PREFERENCES</b>");

            GUILayout.BeginVertical(GUI.skin.box);

            // Who can invite
            GUILayout.BeginHorizontal();
            GUILayout.Label("Who can send Party / Co-op Invites:", GUILayout.Width(240));
            for (int i = 0; i < InviteOptions.Length; i++)
            {
                if (editAllowInvites == i) GUI.color = Color.cyan;
                if (GUILayout.Button(InviteOptions[i], GUILayout.Width(90), GUILayout.Height(24)))
                {
                    editAllowInvites = i;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Who can message
            GUILayout.BeginHorizontal();
            GUILayout.Label("Who can send Direct Messages:", GUILayout.Width(240));
            for (int i = 0; i < InviteOptions.Length; i++)
            {
                if (editAllowMessages == i) GUI.color = Color.cyan;
                if (GUILayout.Button(InviteOptions[i], GUILayout.Width(90), GUILayout.Height(24)))
                {
                    editAllowMessages = i;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Online visibility
            GUILayout.BeginHorizontal();
            GUILayout.Label("Online Status Visibility:", GUILayout.Width(240));
            if (editShowOnline) GUI.color = Color.green;
            if (GUILayout.Button(editShowOnline ? "ONLINE (Public)" : "OFFLINE (Invisible)", GUILayout.Width(180), GUILayout.Height(24)))
            {
                editShowOnline = !editShowOnline;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("<b>APPLY PRIVACY SETTINGS</b>", GUILayout.Height(30)))
            {
                SavePrivacy();
            }

            GUILayout.EndVertical();

            GUILayout.Space(14);

            // Block list section
            GUILayout.Label("<b>BLOCKED PLAYERS LIST</b>");
            var blockList = AccountManager.Instance != null ? AccountManager.Instance.BlockedUsers : new List<BlockedUserData>();

            if (blockList.Count == 0)
            {
                GUILayout.Label("<i>No blocked players.</i>");
            }
            else
            {
                blockScrollPos = GUILayout.BeginScrollView(blockScrollPos, GUILayout.Height(120));
                foreach (var blocked in blockList)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label($"<b>{blocked.username}</b> (UID: {blocked.numericId})", GUILayout.ExpandWidth(true));
                    GUI.color = Color.red;
                    if (GUILayout.Button("UNBLOCK", GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        UnblockPlayer(blocked.blockedId);
                    }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
        }

        private async void SavePrivacy()
        {
            if (AccountManager.Instance == null) return;
            ShowFeedback("Updating privacy preferences...");
            bool ok = await AccountManager.Instance.UpdatePrivacySettingsAsync(editAllowInvites, editAllowMessages, editShowOnline);
            if (ok) ShowFeedback("Privacy preferences saved.");
            else ShowFeedback("Failed to update privacy settings.");
        }

        private async void UnblockPlayer(string targetId)
        {
            if (AccountManager.Instance == null) return;
            bool ok = await AccountManager.Instance.UnblockPlayerAsync(targetId);
            if (ok) ShowFeedback("Player unblocked.");
        }

        /// <summary>
        /// TAB 4: Account Management & Emergency Recovery Code
        /// </summary>
        private void DrawAccountTab()
        {
            AccountProfile prof = AccountManager.Instance != null ? AccountManager.Instance.Profile : new AccountProfile();

            GUILayout.Label("<b>ACCOUNT CREDENTIALS & RECOVERY</b>");

            // Recovery Code Box (Critical for Free Fire recovery requirement)
            string recCode = AccountManager.Instance != null ? AccountManager.Instance.RecoveryCode : "";
            if (!string.IsNullOrEmpty(recCode))
            {
                GUI.color = new Color(1f, 0.85f, 0.2f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.color = Color.white;
                GUILayout.Label("<b>EMERGENCY ACCOUNT RECOVERY CODE:</b>");
                GUILayout.BeginHorizontal();
                GUI.color = Color.yellow;
                GUILayout.Label($"<size=18><b>{recCode}</b></size>", GUILayout.Width(160));
                GUI.color = Color.white;
                if (GUILayout.Button("📋 COPY RECOVERY CODE", GUILayout.Width(180), GUILayout.Height(26)))
                {
                    GUIUtility.systemCopyBuffer = recCode;
                    ShowFeedback("Recovery code copied to clipboard!");
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("<size=10>Store this code safely. It is required to restore your account if device or credentials are lost.</size>");
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            // If Guest: Show Upgrade Form
            if (prof.isGuest)
            {
                GUI.color = new Color(0.1f, 0.45f, 0.6f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.color = Color.white;
                GUILayout.Label("<b>BIND / UPGRADE GUEST ACCOUNT TO OFFICIAL ACCOUNT:</b>");
                GUILayout.Label("<size=10>Binding keeps your 9-digit Player ID, statistics, cosmetics, and unlocked evolutions permanently.</size>");

                GUILayout.Space(4);
                GUILayout.Label("Choose Permanent Username:");
                regUsernameInput = GUILayout.TextField(regUsernameInput, 20);

                GUILayout.Label("Choose Secure Password (min 6 chars):");
                regPasswordInput = GUILayout.PasswordField(regPasswordInput, '*', 32);

                GUILayout.Space(6);
                GUI.color = Color.green;
                if (GUILayout.Button("<b>CONFIRM & UPGRADE ACCOUNT</b>", GUILayout.Height(30)))
                {
                    UpgradeAccount();
                }
                GUI.color = Color.white;
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }
            else
            {
                // Official account: Change username
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>CHANGE USERNAME</b> (Remaining changes: <b>{prof.usernameChangesLeft}/3</b>)");
                if (prof.usernameChangesLeft > 0)
                {
                    GUILayout.BeginHorizontal();
                    changeUsernameInput = GUILayout.TextField(changeUsernameInput, 20, GUILayout.Width(220));
                    if (GUILayout.Button("UPDATE USERNAME", GUILayout.Width(140), GUILayout.Height(24)))
                    {
                        ChangeUsername();
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label("<i>No username changes remaining for this account.</i>");
                }
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            // Account Recovery Modal / Form
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>RECOVER LOST ACCOUNT VIA RECOVERY CODE:</b>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Username or UID:", GUILayout.Width(120));
            recoverTargetInput = GUILayout.TextField(recoverTargetInput, 24);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Recovery Code:", GUILayout.Width(120));
            recoverCodeInput = GUILayout.TextField(recoverCodeInput, 16);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("New Password:", GUILayout.Width(120));
            recoverNewPassInput = GUILayout.PasswordField(recoverNewPassInput, '*', 32);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("SUBMIT ACCOUNT RECOVERY", GUILayout.Height(26)))
            {
                RecoverAccount();
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Switch account / Logout
            GUI.color = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("LOGOUT / SWITCH ACCOUNT", GUILayout.Height(28)))
            {
                if (AccountManager.Instance != null)
                {
                    AccountManager.Instance.Logout();
                    ShowFeedback("Logged out. Guest account re-initialized.");
                }
            }
            GUI.color = Color.white;
        }

        private async void UpgradeAccount()
        {
            if (string.IsNullOrWhiteSpace(regUsernameInput) || string.IsNullOrWhiteSpace(regPasswordInput))
            {
                ShowFeedback("Please provide both username and password.");
                return;
            }

            ShowFeedback("Upgrading account on Cloudflare edge...");
            var (success, error) = await AccountManager.Instance.UpgradeGuestAsync(regUsernameInput.Trim(), regPasswordInput.Trim());
            if (success)
            {
                ShowFeedback("Account successfully upgraded to Official Account!");
                regUsernameInput = "";
                regPasswordInput = "";
            }
            else
            {
                ShowFeedback($"Upgrade failed: {error}");
            }
        }

        private async void ChangeUsername()
        {
            if (string.IsNullOrWhiteSpace(changeUsernameInput))
            {
                ShowFeedback("Enter a valid new username.");
                return;
            }

            ShowFeedback("Updating username...");
            var (success, error) = await AccountManager.Instance.ChangeUsernameAsync(changeUsernameInput.Trim());
            if (success)
            {
                ShowFeedback("Username updated successfully!");
                changeUsernameInput = "";
            }
            else
            {
                ShowFeedback($"Username change failed: {error}");
            }
        }

        private async void RecoverAccount()
        {
            if (string.IsNullOrWhiteSpace(recoverTargetInput) || string.IsNullOrWhiteSpace(recoverCodeInput) || string.IsNullOrWhiteSpace(recoverNewPassInput))
            {
                ShowFeedback("Please fill in target username/UID, recovery code, and new password.");
                return;
            }

            ShowFeedback("Validating recovery code with Cloudflare backend...");
            var (success, newCode, error) = await AccountManager.Instance.RecoverAccountAsync(recoverTargetInput.Trim(), recoverCodeInput.Trim(), recoverNewPassInput.Trim());
            if (success)
            {
                ShowFeedback($"Account recovered successfully! New recovery code: {newCode}");
                recoverTargetInput = "";
                recoverCodeInput = "";
                recoverNewPassInput = "";
            }
            else
            {
                ShowFeedback($"Recovery failed: {error}");
            }
        }

        /// <summary>
        /// TAB 5: Search & Inspect Player by 9-digit Player ID or Username
        /// </summary>
        private void DrawInspectSearchTab()
        {
            GUILayout.Label("<b>SEARCH PLAYER BY 9-DIGIT PLAYER ID OR USERNAME</b>");

            GUILayout.BeginHorizontal();
            searchInput = GUILayout.TextField(searchInput, 20, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            if (GUILayout.Button(isSearching ? "SEARCHING..." : "🔍 SEARCH", GUILayout.Width(110), GUILayout.Height(28)))
            {
                SearchPlayer();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (inspectedProfile != null)
            {
                GUILayout.Label("<b>INSPECTED PLAYER PROFILE CARD:</b>");
                GUILayout.BeginVertical(GUI.skin.box);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b><size=14>{inspectedProfile.username}</size></b>", GUILayout.Width(160));
                GUI.color = Color.cyan;
                GUILayout.Label($"UID: <b>{inspectedProfile.numericId}</b>", GUILayout.Width(120));
                GUI.color = Color.white;
                GUILayout.Label($"LV. {inspectedProfile.level}", GUILayout.Width(60));
                if (!string.IsNullOrEmpty(inspectedProfile.squadName))
                {
                    GUILayout.Label($"Squad: <b>[{inspectedProfile.squadName}]</b>", GUILayout.ExpandWidth(true));
                }
                GUILayout.EndHorizontal();

                GUILayout.Label($"<i>\"{inspectedProfile.bio}\"</i>");
                GUILayout.Label($"Cosmetics: Avatar #{inspectedProfile.avatarId} | Frame #{inspectedProfile.frameId} | Banner #{inspectedProfile.bannerId}");
                GUILayout.Label($"Evolution: <b>{inspectedProfile.GetEvolutionName()}</b> | High Score: {inspectedProfile.highestScore} | Length: {inspectedProfile.highestLength}m");

                GUILayout.Space(8);

                // Action Bar
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("📋 COPY UID", GUILayout.Height(24)))
                {
                    GUIUtility.systemCopyBuffer = inspectedProfile.numericId.ToString();
                    ShowFeedback($"UID {inspectedProfile.numericId} copied!");
                }

                if (GUILayout.Button("➕ ADD FRIEND", GUILayout.Height(24)))
                {
                    if (SocialManager.Instance != null)
                    {
                        _ = SocialManager.Instance.AddFriendAsync(inspectedProfile.playerId, inspectedProfile.username);
                        ShowFeedback($"Friend request sent to {inspectedProfile.username}!");
                    }
                }

                if (GUILayout.Button("⚡ SQUAD INVITE", GUILayout.Height(24)))
                {
                    if (SocialGroupingManager.Instance != null)
                    {
                        _ = SocialGroupingManager.Instance.InviteFriendToSquadAsync(inspectedProfile.playerId);
                        ShowFeedback($"Squad invite sent to {inspectedProfile.username}!");
                    }
                    else if (SocialManager.Instance != null)
                    {
                        _ = SocialManager.Instance.CreatePartyAsync();
                        ShowFeedback($"Co-op Party created! Share invite code with {inspectedProfile.username}.");
                    }
                }

                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🚫 BLOCK", GUILayout.Height(24)))
                {
                    if (AccountManager.Instance != null)
                    {
                        _ = AccountManager.Instance.BlockPlayerAsync(inspectedProfile.playerId);
                        ShowFeedback($"Blocked {inspectedProfile.username}.");
                    }
                }

                GUI.color = Color.red;
                if (GUILayout.Button("🚩 REPORT", GUILayout.Height(24)))
                {
                    showReportModal = true;
                }
                GUI.color = Color.white;

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Label("<i>Enter a 9-digit Player ID (e.g. 565333927) or exact username to inspect player stats.</i>");
            }
        }

        private async void SearchPlayer()
        {
            if (string.IsNullOrWhiteSpace(searchInput)) return;
            isSearching = true;

            int parsedNum = 0;
            int.TryParse(searchInput.Trim(), out parsedNum);

            AccountProfile target = null;
            if (parsedNum > 0)
            {
                target = await AccountManager.Instance.FetchProfileAsync(numericId: parsedNum);
            }
            else
            {
                target = await AccountManager.Instance.FetchProfileAsync(targetUsername: searchInput.Trim());
            }

            if (target != null)
            {
                inspectedProfile = target;
                ShowFeedback($"Loaded profile for {target.username} (UID: {target.numericId})");
            }
            else
            {
                ShowFeedback("No player found with that ID or username.");
            }

            isSearching = false;
        }

        /// <summary>
        /// Player Conduct & Anti-Cheat Report Modal
        /// </summary>
        private void DrawReportModalOverlay(Rect parentRect)
        {
            Rect modalRect = new Rect(parentRect.x + 40, parentRect.y + 60, parentRect.width - 80, parentRect.height - 120);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);
            GUI.Box(modalRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(modalRect.x + 20, modalRect.y + 20, modalRect.width - 40, modalRect.height - 40));

            GUI.color = Color.red;
            GUILayout.Label($"<b><size=15>REPORT PLAYER: {inspectedProfile?.username} (UID: {inspectedProfile?.numericId})</size></b>");
            GUI.color = Color.white;

            GUILayout.Space(6);
            GUILayout.Label("Select Violation Reason:");

            for (int i = 0; i < ReportReasons.Length; i++)
            {
                bool isReasonSelected = (reportReason == ReportReasons[i]);
                if (isReasonSelected) GUI.color = Color.yellow;
                if (GUILayout.Button(ReportReasons[i], GUILayout.Height(24)))
                {
                    reportReason = ReportReasons[i];
                }
                GUI.color = Color.white;
            }

            GUILayout.Space(6);
            GUILayout.Label("Additional Details (Optional):");
            reportDetails = GUILayout.TextField(reportDetails, 200, GUILayout.Height(30));

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            GUI.color = Color.red;
            if (GUILayout.Button("<b>SUBMIT REPORT</b>", GUILayout.Height(30)))
            {
                SubmitReport();
            }

            GUI.color = Color.gray;
            if (GUILayout.Button("CANCEL", GUILayout.Height(30)))
            {
                showReportModal = false;
            }
            GUI.color = Color.white;

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private async void SubmitReport()
        {
            if (inspectedProfile == null || AccountManager.Instance == null) return;
            showReportModal = false;
            ShowFeedback("Submitting conduct report to Cloudflare moderation queue...");

            var (ok, error) = await AccountManager.Instance.ReportPlayerAsync(inspectedProfile.playerId, reportReason, reportDetails);
            if (ok)
            {
                ShowFeedback($"Report submitted against {inspectedProfile.username}. Thank you for keeping AetherSerpent fair!");
                reportDetails = "";
            }
            else
            {
                ShowFeedback($"Failed to submit report: {error}");
            }
        }
    }
}
