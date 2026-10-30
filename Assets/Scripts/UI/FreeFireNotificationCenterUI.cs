using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Social;
using AetherSerpent.Account;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Free Fire-style Tactical Notification Center & Social Quality Overlay.
    /// Provides:
    /// - Interactive sliding in-game banner toasts with 1-click Accept/Decline
    /// - Notification Center history (Friend requests, Squad invites, Whispers, System)
    /// - Recent Players roster with Quick Add Friend, Squad Invite, Profile, Report
    /// - Online Presence & Do Not Disturb (DND) controls
    /// - Quick Lobby Reactions / Emotes floating animation bar
    /// - Copy Player ID button
    /// Toggleable via 'N' or header bell button.
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeFireNotificationCenterUI : MonoBehaviour
    {
        public static FreeFireNotificationCenterUI Instance { get; private set; }

        public bool IsVisible { get; private set; } = false;

        private enum CenterTab
        {
            Notifications,
            RecentPlayers,
            StatusPrivacy
        }

        private CenterTab currentTab = CenterTab.Notifications;
        private Vector2 notifScrollPos;
        private Vector2 recentScrollPos;

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
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.nKey.wasPressedThisFrame)
                {
                    ToggleVisibility();
                }
                else if (IsVisible && kb.escapeKey.wasPressedThisFrame)
                {
                    IsVisible = false;
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.N))
            {
                ToggleVisibility();
            }
            else if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                IsVisible = false;
            }
#endif
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            if (IsVisible && NotificationManager.Instance != null)
            {
                _ = NotificationManager.Instance.FetchNotificationsAsync();
            }
        }

        // =========================================================
        // ONGUI RENDERING
        // =========================================================

        private void OnGUI()
        {
            var mgr = NotificationManager.Instance;
            if (mgr == null) return;

            // 1. Draw In-Game Active Toast Banners
            DrawActiveToastBanners(mgr);

            // 2. Draw Floating Lobby Reactions
            DrawFloatingReactions(mgr);

            // 3. Draw Header Bell Indicator & Quick Emote Bar
            DrawHeaderBellPill(mgr);
            DrawQuickEmoteBar(mgr);

            // 4. Draw Main Notification Center Modal
            if (IsVisible)
            {
                DrawNotificationCenterModal(mgr);
            }
        }

        // ---------------------------------------------------------
        // HEADER BELL PILL
        // ---------------------------------------------------------

        private void DrawHeaderBellPill(NotificationManager mgr)
        {
            // Positioned near top-right
            Rect bellRect = new Rect(Screen.width - 95, 10, 85, 28);

            string unreadBadge = mgr.UnreadCount > 0 ?
                $"<color=#ff1744><b>🔔 ({mgr.UnreadCount})</b></color>" :
                "🔔 [N]";

            if (GUI.Button(bellRect, unreadBadge))
            {
                ToggleVisibility();
            }
        }

        // ---------------------------------------------------------
        // ACTIVE IN-GAME TOAST BANNERS (WITH QUICK ACTION BUTTONS)
        // ---------------------------------------------------------

        private void DrawActiveToastBanners(NotificationManager mgr)
        {
            if (mgr.ActiveToasts.Count == 0 || mgr.IsDndActive) return;

            float startY = 55;
            for (int i = 0; i < mgr.ActiveToasts.Count; i++)
            {
                var toast = mgr.ActiveToasts[i];
                float toastWidth = Mathf.Min(480, Screen.width - 40);
                Rect toastRect = new Rect(Screen.width / 2f - toastWidth / 2f, startY + (i * 62), toastWidth, 54);

                // Toast background
                GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
                GUI.Box(toastRect, "");

                // Accent border color based on type
                Color accentColor = toast.type switch
                {
                    NotificationType.SquadInvite => new Color(0f, 0.9f, 1f),
                    NotificationType.FriendRequest => new Color(0.2f, 1f, 0.4f),
                    NotificationType.Whisper => new Color(1f, 0.4f, 0.7f),
                    _ => new Color(1f, 0.8f, 0.2f)
                };

                GUI.color = accentColor;
                GUI.Box(new Rect(toastRect.x, toastRect.y, 4, toastRect.height), "");
                GUI.color = Color.white;

                GUILayout.BeginArea(new Rect(toastRect.x + 12, toastRect.y + 4, toastRect.width - 20, toastRect.height - 8));
                GUILayout.BeginHorizontal();

                // Icon and text
                string typePrefix = toast.type switch
                {
                    NotificationType.SquadInvite => "<color=#00e5ff>[SQUAD INVITE]</color>",
                    NotificationType.FriendRequest => "<color=#00e676>[FRIEND REQUEST]</color>",
                    NotificationType.Whisper => "<color=#ff4081>[WHISPER]</color>",
                    _ => "<color=#ffd600>[SYSTEM]</color>"
                };

                GUILayout.Label($"<b>{typePrefix} {toast.title}</b>\n<size=11>{toast.message}</size>", GUILayout.Width(280));

                // Quick Action Buttons directly on toast!
                if (toast.type == NotificationType.SquadInvite)
                {
                    GUI.color = new Color(0.2f, 1f, 0.4f);
                    if (GUILayout.Button("<b>ACCEPT</b>", GUILayout.Width(70), GUILayout.Height(36)))
                    {
                        ExecuteSquadInviteAction(toast, true);
                        mgr.DismissToast(toast);
                    }
                    GUI.color = new Color(1f, 0.3f, 0.3f);
                    if (GUILayout.Button("✕", GUILayout.Width(30), GUILayout.Height(36)))
                    {
                        ExecuteSquadInviteAction(toast, false);
                        mgr.DismissToast(toast);
                    }
                    GUI.color = Color.white;
                }
                else if (toast.type == NotificationType.FriendRequest)
                {
                    GUI.color = new Color(0.2f, 1f, 0.4f);
                    if (GUILayout.Button("<b>ACCEPT</b>", GUILayout.Width(70), GUILayout.Height(36)))
                    {
                        if (SocialGroupingManager.Instance != null && !string.IsNullOrEmpty(toast.senderId))
                        {
                            _ = SocialGroupingManager.Instance.AddFriendAsync(toast.senderId);
                        }
                        mgr.DismissToast(toast);
                    }
                    GUI.color = Color.white;
                    if (GUILayout.Button("✕", GUILayout.Width(30), GUILayout.Height(36)))
                    {
                        mgr.DismissToast(toast);
                    }
                }
                else if (toast.type == NotificationType.Whisper)
                {
                    if (GUILayout.Button("<b>REPLY</b>", GUILayout.Width(65), GUILayout.Height(36)))
                    {
                        if (FreeFireChatUI.Instance != null)
                        {
                            FreeFireChatUI.Instance.StartWhisper(toast.senderId, toast.senderName, toast.senderNumericId);
                        }
                        mgr.DismissToast(toast);
                    }
                    if (GUILayout.Button("✕", GUILayout.Width(30), GUILayout.Height(36)))
                    {
                        mgr.DismissToast(toast);
                    }
                }
                else
                {
                    if (GUILayout.Button("DISMISS", GUILayout.Width(80), GUILayout.Height(36)))
                    {
                        mgr.DismissToast(toast);
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private void ExecuteSquadInviteAction(NotificationItem toast, bool accept)
        {
            if (string.IsNullOrEmpty(toast.dataJson)) return;

            try
            {
                // Simple JSON field parsing
                int invIdx = toast.dataJson.IndexOf("\"inviteId\":");
                if (invIdx > 0)
                {
                    string sub = toast.dataJson.Substring(invIdx + 11);
                    int end = sub.IndexOfAny(new[] { ',', '}' });
                    if (end > 0 && int.TryParse(sub.Substring(0, end).Trim(), out int invId))
                    {
                        if (SocialGroupingManager.Instance != null)
                        {
                            _ = SocialGroupingManager.Instance.RespondSquadInviteAsync(invId, accept);
                        }
                    }
                }
            }
            catch { }
        }

        // ---------------------------------------------------------
        // QUICK LOBBY EMOTE / REACTION BAR
        // ---------------------------------------------------------

        private void DrawQuickEmoteBar(NotificationManager mgr)
        {
            // Positioned at bottom center of screen
            Rect barRect = new Rect(Screen.width / 2f - 180, Screen.height - 42, 360, 34);

            GUI.color = new Color(0.08f, 0.1f, 0.15f, 0.88f);
            GUI.Box(barRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(barRect.x + 6, barRect.y + 4, barRect.width - 12, barRect.height - 8));
            GUILayout.BeginHorizontal();

            GUILayout.Label("<color=#00e5ff><b>EMOTES:</b></color>", GUILayout.Width(60));

            if (GUILayout.Button("🔥", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.Fire);
            if (GUILayout.Button("🐍", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.Serpent);
            if (GUILayout.Button("👑", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.Crown);
            if (GUILayout.Button("👏", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.GG);
            if (GUILayout.Button("❤️", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.Heart);
            if (GUILayout.Button("⚡", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.Lightning);
            if (GUILayout.Button("💀", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.RIP);
            if (GUILayout.Button("🛡️", GUILayout.Width(32), GUILayout.Height(24))) mgr.TriggerReaction(LobbyReaction.Shield);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawFloatingReactions(NotificationManager mgr)
        {
            if (mgr.ActiveReactions.Count == 0) return;

            foreach (var r in mgr.ActiveReactions)
            {
                float alpha = Mathf.Clamp01(r.timer / 0.5f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                Rect rRect = new Rect(r.screenPos.x - 40, r.screenPos.y, 80, 40);
                GUI.Label(rRect, $"<size=28>{r.emoji}</size>\n<size=10><color=#00e5ff>{r.senderName}</color></size>");
            }
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------
        // MAIN NOTIFICATION CENTER MODAL
        // ---------------------------------------------------------

        private void DrawNotificationCenterModal(NotificationManager mgr)
        {
            float modalWidth = Mathf.Min(620, Screen.width - 40);
            float modalHeight = Mathf.Min(520, Screen.height - 80);
            Rect modalRect = new Rect(Screen.width / 2f - modalWidth / 2f, Screen.height / 2f - modalHeight / 2f, modalWidth, modalHeight);

            // Modal Background
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);
            GUI.Box(modalRect, "");
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(modalRect.x + 16, modalRect.y + 14, modalRect.width - 32, modalRect.height - 28));

            // Header Banner
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=17><color=#00e5ff>AETHERSERPENT // NOTIFICATION & SOCIAL CENTER</color></size></b>");
            if (GUILayout.Button("<b>✕ CLOSE</b>", GUILayout.Width(80), GUILayout.Height(26)))
            {
                IsVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Tab Navigation
            GUILayout.BeginHorizontal();
            GUI.color = currentTab == CenterTab.Notifications ? new Color(0f, 0.9f, 1f) : Color.gray;
            string notifTabLabel = mgr.UnreadCount > 0 ?
                $"NOTIFICATIONS ({mgr.UnreadCount})" : "NOTIFICATIONS";
            if (GUILayout.Button(notifTabLabel, GUILayout.Height(30))) currentTab = CenterTab.Notifications;

            GUI.color = currentTab == CenterTab.RecentPlayers ? new Color(0f, 0.9f, 1f) : Color.gray;
            if (GUILayout.Button($"RECENT PLAYERS ({mgr.RecentPlayers.Count})", GUILayout.Height(30))) currentTab = CenterTab.RecentPlayers;

            GUI.color = currentTab == CenterTab.StatusPrivacy ? new Color(0f, 0.9f, 1f) : Color.gray;
            if (GUILayout.Button("STATUS & PRIVACY", GUILayout.Height(30))) currentTab = CenterTab.StatusPrivacy;
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Tab Contents
            switch (currentTab)
            {
                case CenterTab.Notifications:
                    DrawNotificationsTab(mgr);
                    break;
                case CenterTab.RecentPlayers:
                    DrawRecentPlayersTab(mgr);
                    break;
                case CenterTab.StatusPrivacy:
                    DrawStatusPrivacyTab(mgr);
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawNotificationsTab(NotificationManager mgr)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>All Notifications ({mgr.Notifications.Count})</b>");
            if (GUILayout.Button("Mark All as Read", GUILayout.Width(130), GUILayout.Height(24)))
            {
                _ = mgr.MarkAsReadAsync(0, true);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            notifScrollPos = GUILayout.BeginScrollView(notifScrollPos);

            if (mgr.Notifications.Count == 0)
            {
                GUILayout.Space(30);
                GUILayout.Label("<color=#888888><i>You have no notifications yet. Friend requests and squad invites will appear here!</i></color>");
            }
            else
            {
                foreach (var item in mgr.Notifications)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();

                    string readIcon = item.isRead ? "<color=#888888>○</color>" : "<color=#00e5ff>●</color>";
                    string titleColor = item.type switch
                    {
                        NotificationType.SquadInvite => "<color=#00e5ff>",
                        NotificationType.FriendRequest => "<color=#00e676>",
                        NotificationType.Whisper => "<color=#ff4081>",
                        _ => "<color=#ffd600>"
                    };

                    GUILayout.Label($"{readIcon} {titleColor}<b>{item.title}</b></color>\n<size=11>{item.message}</size>", GUILayout.Width(350));

                    // Quick Actions
                    if (item.type == NotificationType.SquadInvite)
                    {
                        GUI.color = new Color(0.2f, 1f, 0.4f);
                        if (GUILayout.Button("ACCEPT", GUILayout.Width(65), GUILayout.Height(28)))
                        {
                            ExecuteSquadInviteAction(item, true);
                            _ = mgr.MarkAsReadAsync(item.id);
                        }
                        GUI.color = Color.white;
                    }
                    else if (item.type == NotificationType.FriendRequest)
                    {
                        GUI.color = new Color(0.2f, 1f, 0.4f);
                        if (GUILayout.Button("ACCEPT", GUILayout.Width(65), GUILayout.Height(28)))
                        {
                            if (SocialGroupingManager.Instance != null && !string.IsNullOrEmpty(item.senderId))
                            {
                                _ = SocialGroupingManager.Instance.AddFriendAsync(item.senderId);
                            }
                            _ = mgr.MarkAsReadAsync(item.id);
                        }
                        GUI.color = Color.white;
                    }
                    else if (item.type == NotificationType.Whisper)
                    {
                        if (GUILayout.Button("REPLY", GUILayout.Width(65), GUILayout.Height(28)))
                        {
                            if (FreeFireChatUI.Instance != null)
                            {
                                FreeFireChatUI.Instance.StartWhisper(item.senderId, item.senderName, item.senderNumericId);
                                IsVisible = false;
                            }
                            _ = mgr.MarkAsReadAsync(item.id);
                        }
                    }

                    if (!item.isRead && GUILayout.Button("READ", GUILayout.Width(50), GUILayout.Height(28)))
                    {
                        _ = mgr.MarkAsReadAsync(item.id);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawRecentPlayersTab(NotificationManager mgr)
        {
            GUILayout.Label("<b>PLAYERS MET IN RECENT MATCHES</b>");
            GUILayout.Space(6);

            recentScrollPos = GUILayout.BeginScrollView(recentScrollPos);

            if (mgr.RecentPlayers.Count == 0)
            {
                GUILayout.Space(30);
                GUILayout.Label("<color=#888888><i>No recent players met yet. Complete co-op matches to track teammates and opponents!</i></color>");
            }
            else
            {
                foreach (var p in mgr.RecentPlayers)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();

                    GUILayout.Label($"<b>{p.username}</b> (UID: {p.numericId})\n<size=11><color=#888888>Mode: {p.matchMode} | Role: {p.role}</color></size>", GUILayout.Width(240));

                    // 1. Add Friend
                    if (GUILayout.Button("+ FRIEND", GUILayout.Width(75), GUILayout.Height(28)))
                    {
                        if (SocialGroupingManager.Instance != null)
                        {
                            _ = SocialGroupingManager.Instance.AddFriendAsync(p.playerId);
                        }
                    }

                    // 2. Invite to Squad
                    if (GUILayout.Button("INVITE", GUILayout.Width(65), GUILayout.Height(28)))
                    {
                        if (SocialGroupingManager.Instance != null)
                        {
                            _ = SocialGroupingManager.Instance.InviteFriendToSquadAsync(p.playerId);
                        }
                    }

                    // 3. Copy UID
                    if (GUILayout.Button("COPY UID", GUILayout.Width(70), GUILayout.Height(28)))
                    {
                        mgr.CopyPlayerIdToClipboard(p.numericId);
                    }

                    // 4. Report
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("REPORT", GUILayout.Width(65), GUILayout.Height(28)))
                    {
                        if (FreeFireVoiceUI.Instance != null)
                        {
                            FreeFireVoiceUI.Instance.OpenReportModal(p.playerId, p.username, p.numericId);
                        }
                    }
                    GUI.color = Color.white;

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawStatusPrivacyTab(NotificationManager mgr)
        {
            GUILayout.Label("<b>ONLINE STATUS & NOTIFICATION PREFERENCES</b>");
            GUILayout.Space(8);

            // 1. Online Presence Status Selector
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Your Online Presence:</b>");
            GUILayout.BeginHorizontal();

            GUI.color = mgr.CurrentPresence == PresenceStatus.Online ? new Color(0.2f, 1f, 0.4f) : Color.gray;
            if (GUILayout.Button("🟢 ONLINE", GUILayout.Height(28))) mgr.SetPresenceStatus(PresenceStatus.Online);

            GUI.color = mgr.CurrentPresence == PresenceStatus.Away ? new Color(1f, 0.8f, 0.2f) : Color.gray;
            if (GUILayout.Button("🟡 AWAY", GUILayout.Height(28))) mgr.SetPresenceStatus(PresenceStatus.Away);

            GUI.color = mgr.CurrentPresence == PresenceStatus.DoNotDisturb ? new Color(1f, 0.2f, 0.2f) : Color.gray;
            if (GUILayout.Button("🔴 DO NOT DISTURB", GUILayout.Height(28))) mgr.SetDndMode(true);

            GUI.color = mgr.CurrentPresence == PresenceStatus.Invisible ? new Color(0.7f, 0.7f, 0.7f) : Color.gray;
            if (GUILayout.Button("⚪ INVISIBLE", GUILayout.Height(28))) mgr.SetPresenceStatus(PresenceStatus.Invisible);

            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // 2. Do Not Disturb Mode Details
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>DO NOT DISTURB (DND) MODE</b>");

            bool newDnd = GUILayout.Toggle(mgr.IsDndActive, " <b>Enable Do Not Disturb (DND) Mode</b>");
            if (newDnd != mgr.IsDndActive)
            {
                mgr.SetDndMode(newDnd);
            }
            GUILayout.Label("<size=11><color=#888888>When DND is active, in-game notification toasts and chimes are automatically suppressed. All invites and messages are saved silently to your Notification Center history.</color></size>");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // 3. Copy My Player ID
            var acc = AccountManager.Instance;
            if (acc != null && acc.NumericId > 0)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("<b>YOUR PLAYER IDENTITY</b>");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Username: <b>{acc.Username}</b>  |  Player ID: <b><color=#00e5ff>{acc.NumericId}</color></b>", GUILayout.Width(350));
                if (GUILayout.Button("<b>COPY PLAYER ID</b>", GUILayout.Width(130), GUILayout.Height(26)))
                {
                    mgr.CopyPlayerIdToClipboard(acc.NumericId);
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }
    }
}

