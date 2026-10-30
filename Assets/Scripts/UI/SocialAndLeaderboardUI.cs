using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Social;
using AetherSerpent.Core;

namespace AetherSerpent.UI
{
    /// <summary>
    /// In-Game Hub for Global & Weekly Leaderboards, Friends List, and Co-op Party assembly.
    /// Toggleable via 'L' key.
    /// </summary>
    public class SocialAndLeaderboardUI : MonoBehaviour
    {
        public static SocialAndLeaderboardUI Instance { get; private set; }

        public bool IsVisible { get; private set; } = false;

        private enum Tab { GlobalLeaderboard, WeeklyLeaderboard, Friends, Party }
        private Tab currentTab = Tab.GlobalLeaderboard;

        private Vector2 scrollPos;
        private string friendIdInput = "";
        private string partyCodeInput = "";
        private string statusMessage = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.lKey.wasPressedThisFrame)
            {
                ToggleVisibility();
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.L))
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
                RefreshCurrentTabData();
            }
        }

        private void RefreshCurrentTabData()
        {
            statusMessage = "Loading...";
            if (currentTab == Tab.GlobalLeaderboard && LeaderboardManager.Instance != null)
            {
                _ = LeaderboardManager.Instance.FetchGlobalLeaderboardAsync().ContinueWith(t => statusMessage = "");
            }
            else if (currentTab == Tab.WeeklyLeaderboard && LeaderboardManager.Instance != null)
            {
                _ = LeaderboardManager.Instance.FetchWeeklyLeaderboardAsync().ContinueWith(t => statusMessage = "");
            }
            else if (currentTab == Tab.Friends && SocialManager.Instance != null)
            {
                _ = SocialManager.Instance.RefreshFriendsListAsync().ContinueWith(t => statusMessage = "");
            }
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            Rect winRect = new Rect(Screen.width / 2f - 260, Screen.height / 2f - 240, 520, 480);
            GUI.Box(winRect, "<b>AETHERSERPENT // CELESTIAL SOCIAL & LEADERBOARD</b>");

            GUILayout.BeginArea(new Rect(winRect.x + 15, winRect.y + 35, winRect.width - 30, winRect.height - 45));

            // Tab Bar
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Global Ranks", currentTab == Tab.GlobalLeaderboard ? GUI.skin.box : GUI.skin.button))
            {
                currentTab = Tab.GlobalLeaderboard;
                RefreshCurrentTabData();
            }
            if (GUILayout.Button("Weekly Ranks", currentTab == Tab.WeeklyLeaderboard ? GUI.skin.box : GUI.skin.button))
            {
                currentTab = Tab.WeeklyLeaderboard;
                RefreshCurrentTabData();
            }
            if (GUILayout.Button("Friends", currentTab == Tab.Friends ? GUI.skin.box : GUI.skin.button))
            {
                currentTab = Tab.Friends;
                RefreshCurrentTabData();
            }
            if (GUILayout.Button("Party", currentTab == Tab.Party ? GUI.skin.box : GUI.skin.button))
            {
                currentTab = Tab.Party;
                RefreshCurrentTabData();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Label($"<i>{statusMessage}</i>");
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(320));

            if (currentTab == Tab.GlobalLeaderboard)
            {
                DrawLeaderboardList(LeaderboardManager.Instance != null ? LeaderboardManager.Instance.CachedGlobalEntries : null, "ALL-TIME GLOBAL TOP DRIFTERS");
            }
            else if (currentTab == Tab.WeeklyLeaderboard)
            {
                DrawLeaderboardList(LeaderboardManager.Instance != null ? LeaderboardManager.Instance.CachedWeeklyEntries : null, "CURRENT WEEK'S TOP DRIFTERS");
            }
            else if (currentTab == Tab.Friends)
            {
                DrawFriendsTab();
            }
            else if (currentTab == Tab.Party)
            {
                DrawPartyTab();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Submit Current Score", GUILayout.Height(28)))
            {
                int currentScore = (GameManager.Instance != null) ? GameManager.Instance.Score : 100;
                int currentLength = 50;
                var growth = FindFirstObjectByType<AetherSerpent.Serpent.SerpentGrowth>();
                if (growth != null) currentLength = growth.CurrentSegmentCount;

                if (LeaderboardManager.Instance != null)
                {
                    statusMessage = "Submitting score...";
                    _ = LeaderboardManager.Instance.SubmitScoreAsync(currentScore, currentLength, 0).ContinueWith(t =>
                    {
                        statusMessage = t.Result ? "Score submitted to Cloudflare!" : "Submission failed.";
                        RefreshCurrentTabData();
                    });
                }
            }

            if (GUILayout.Button("Close [L]", GUILayout.Height(28), GUILayout.Width(100)))
            {
                IsVisible = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawLeaderboardList(List<LeaderboardEntryData> entries, string title)
        {
            GUILayout.Label($"<b>--- {title} ---</b>");
            if (entries == null || entries.Count == 0)
            {
                GUILayout.Label("<i>No entries recorded yet. Be the first to claim glory!</i>");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>#</b>", GUILayout.Width(25));
            GUILayout.Label("<b>Player</b>", GUILayout.Width(160));
            GUILayout.Label("<b>Score</b>", GUILayout.Width(90));
            GUILayout.Label("<b>Length</b>", GUILayout.Width(70));
            GUILayout.Label("<b>Evolution</b>");
            GUILayout.EndHorizontal();

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                string rankColor = (i == 0) ? "#FFD700" : (i == 1 ? "#C0C0C0" : (i == 2 ? "#CD7F32" : "#FFFFFF"));
                string evoName = (e.evolutionPath == 1) ? "Titan" : (e.evolutionPath == 2 ? "Celestial" : (e.evolutionPath == 3 ? "Abyssal" : "Primal"));

                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color={rankColor}>{i + 1}</color>", GUILayout.Width(25));
                GUILayout.Label(e.username, GUILayout.Width(160));
                GUILayout.Label($"<b>{e.score}</b>", GUILayout.Width(90));
                GUILayout.Label($"{e.length} seg", GUILayout.Width(70));
                GUILayout.Label(evoName);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFriendsTab()
        {
            GUILayout.Label("<b>--- CELESTIAL FRIENDS ---</b>");
            var friends = SocialManager.Instance != null ? SocialManager.Instance.Friends : null;

            if (friends != null && friends.Count > 0)
            {
                foreach (var f in friends)
                {
                    GUILayout.BeginHorizontal();
                    string status = f.isOnline ? "<color=#40FF60>ONLINE</color>" : "<color=#888888>OFFLINE</color>";
                    GUILayout.Label($"• <b>{f.username}</b> ({status})", GUILayout.Width(220));
                    if (GUILayout.Button("Invite to Party", GUILayout.Height(20)))
                    {
                        statusMessage = $"Invite sent to {f.username}!";
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("<i>No friends added yet.</i>");
            }

            GUILayout.Space(12);
            GUILayout.Label("<b>Add Friend by Player ID:</b>");
            GUILayout.BeginHorizontal();
            friendIdInput = GUILayout.TextField(friendIdInput, 30);
            if (GUILayout.Button("Add", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(friendIdInput) && SocialManager.Instance != null)
                {
                    _ = SocialManager.Instance.AddFriendAsync(friendIdInput.Trim(), $"Serpent_{friendIdInput.Substring(0, Math.Min(4, friendIdInput.Length))}");
                    friendIdInput = "";
                    RefreshCurrentTabData();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPartyTab()
        {
            GUILayout.Label("<b>--- CO-OP PARTY ASSEMBLY ---</b>");
            var party = SocialManager.Instance != null ? SocialManager.Instance.CurrentParty : null;

            if (party != null)
            {
                GUILayout.Label($"Party Code: <b><color=#40D0FF>{party.partyId}</color></b>");
                GUILayout.Label($"Leader: <b>{party.leaderName}</b>");
                GUILayout.Space(6);
                GUILayout.Label("<b>Members:</b>");
                foreach (var m in party.members)
                {
                    string leaderTag = m.isLeader ? " [LEADER]" : "";
                    GUILayout.Label($"  - {m.username}{leaderTag}");
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Leave Party", GUILayout.Height(26)))
                {
                    SocialManager.Instance.LeaveParty();
                    RefreshCurrentTabData();
                }
            }
            else
            {
                GUILayout.Label("<i>You are not in a Co-op Party.</i>");
                GUILayout.Space(8);

                if (GUILayout.Button("Create New Party", GUILayout.Height(30)))
                {
                    if (SocialManager.Instance != null)
                    {
                        _ = SocialManager.Instance.CreatePartyAsync().ContinueWith(t => RefreshCurrentTabData());
                    }
                }

                GUILayout.Space(12);
                GUILayout.Label("<b>Join Party by Code:</b>");
                GUILayout.BeginHorizontal();
                partyCodeInput = GUILayout.TextField(partyCodeInput, 20);
                if (GUILayout.Button("Join", GUILayout.Width(80)))
                {
                    if (!string.IsNullOrEmpty(partyCodeInput) && SocialManager.Instance != null)
                    {
                        _ = SocialManager.Instance.JoinPartyAsync(partyCodeInput.Trim()).ContinueWith(t => RefreshCurrentTabData());
                    }
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}

