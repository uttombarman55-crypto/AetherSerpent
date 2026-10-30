using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Core;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Governs the Main Menu user interface and interactive workflows.
    /// Supports Keyboard/Mouse, Gamepad navigation, and Touch taps.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject controlsPanel;

        [Header("Action Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button closeControlsButton;
        [SerializeField] private Button quitButton;

        private void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
            if (closeControlsButton != null) closeControlsButton.onClick.AddListener(CloseControls);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.MainMenu);
            }

            // Ensure AccountManager and FreeFireProfileUI exist even if started directly in MainMenu
            if (AetherSerpent.Account.AccountManager.Instance == null)
            {
                GameObject accObj = new GameObject("[AccountManager]");
                accObj.AddComponent<AetherSerpent.Account.AccountManager>();
            }
            if (FreeFireProfileUI.Instance == null)
            {
                GameObject profObj = new GameObject("[FreeFireProfileUI]");
                profObj.AddComponent<FreeFireProfileUI>();
            }
            if (AetherSerpent.Social.SocialGroupingManager.Instance == null)
            {
                GameObject sgObj = new GameObject("[SocialGroupingManager]");
                sgObj.AddComponent<AetherSerpent.Social.SocialGroupingManager>();
            }
            if (FreeFireSocialUI.Instance == null)
            {
                GameObject sUiObj = new GameObject("[FreeFireSocialUI]");
                sUiObj.AddComponent<FreeFireSocialUI>();
            }
            if (AetherSerpent.Social.ChatManager.Instance == null)
            {
                GameObject chatObj = new GameObject("[ChatManager]");
                chatObj.AddComponent<AetherSerpent.Social.ChatManager>();
            }
            if (FreeFireChatUI.Instance == null)
            {
                GameObject cUiObj = new GameObject("[FreeFireChatUI]");
                cUiObj.AddComponent<FreeFireChatUI>();
            }
            if (AetherSerpent.Voice.VoiceEngine.Instance == null)
            {
                GameObject vObj = new GameObject("[VoiceEngine]");
                vObj.AddComponent<AetherSerpent.Voice.VoiceEngine>();
            }
            if (FreeFireVoiceUI.Instance == null)
            {
                GameObject vUiObj = new GameObject("[FreeFireVoiceUI]");
                vUiObj.AddComponent<FreeFireVoiceUI>();
            }
            if (AetherSerpent.Social.NotificationManager.Instance == null)
            {
                GameObject nObj = new GameObject("[NotificationManager]");
                nObj.AddComponent<AetherSerpent.Social.NotificationManager>();
            }
            if (FreeFireNotificationCenterUI.Instance == null)
            {
                GameObject nUiObj = new GameObject("[FreeFireNotificationCenterUI]");
                nUiObj.AddComponent<FreeFireNotificationCenterUI>();
            }
        }

        public void OnPlayClicked()
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("Gameplay");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
            }
        }

        public void OpenControls()
        {
            if (controlsPanel != null) controlsPanel.SetActive(true);
        }

        public void CloseControls()
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }

        public void OnQuitClicked()
        {
            Debug.Log("[MainMenuUI] Application quit triggered.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            // Free Fire Style Profile & Identity Quick Pill in Top-Right
            var acc = AetherSerpent.Account.AccountManager.Instance;
            int unread = AetherSerpent.Social.NotificationManager.Instance != null ?
                AetherSerpent.Social.NotificationManager.Instance.UnreadCount : 0;

            Rect notifPillRect = new Rect(Screen.width - 335, 15, 85, 48);
            string notifIconLabel = unread > 0 ? $"🔔 <b>{unread}</b>\n<size=10>ALERTS</size>" : "🔔\n<size=10>ALERTS</size>";
            if (GUI.Button(notifPillRect, notifIconLabel))
            {
                FreeFireNotificationCenterUI.Instance?.ToggleVisibility();
            }

            if (acc != null && acc.Profile != null)
            {
                Rect profilePillRect = new Rect(Screen.width - 240, 15, 225, 48);
                GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);
                GUI.Box(profilePillRect, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(new Rect(profilePillRect.x + 8, profilePillRect.y + 4, profilePillRect.width - 16, profilePillRect.height - 8));
                GUILayout.BeginHorizontal();
                string displayName = AetherSerpent.Social.SocialGroupingManager.Instance != null ?
                    AetherSerpent.Social.SocialGroupingManager.Instance.GetFormattedPlayerTag(acc.Username) : acc.Username;

                GUILayout.Label($"<b>{displayName}</b>\n<color=#00e5ff>UID: {acc.NumericId}</color>", GUILayout.Width(130));
                if (GUILayout.Button("PROFILE", GUILayout.Width(75), GUILayout.Height(36)))
                {
                    FreeFireProfileUI.Instance?.ToggleVisibility();
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            // Immediate GUI fallback for testing and easy navigation
            if (playButton == null)
            {
                Rect menuRect = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 200, 320, 400);
                GUI.Box(menuRect, "<b>AETHERSERPENT // MAIN MENU</b>");
                GUILayout.BeginArea(new Rect(menuRect.x + 20, menuRect.y + 35, menuRect.width - 40, menuRect.height - 45));

                if (GUILayout.Button("<b>PLAY GAME (SANCTUARY)</b>", GUILayout.Height(40)))
                {
                    OnPlayClicked();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("PROFILE & IDENTITY [P]", GUILayout.Height(30)))
                {
                    FreeFireProfileUI.Instance?.ToggleVisibility();
                }

                GUILayout.Space(6);
                if (GUILayout.Button($"NOTIFICATIONS & ALERTS [N] {(unread > 0 ? $"({unread})" : "")}", GUILayout.Height(30)))
                {
                    FreeFireNotificationCenterUI.Instance?.ToggleVisibility();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("FRIENDS • SQUAD • CLAN [L]", GUILayout.Height(32)))
                {
                    if (FreeFireSocialUI.Instance != null)
                    {
                        FreeFireSocialUI.Instance.ToggleVisibility();
                    }
                    else if (SocialAndLeaderboardUI.Instance != null)
                    {
                        SocialAndLeaderboardUI.Instance.ToggleVisibility();
                    }
                }

                GUILayout.Space(6);
                if (GUILayout.Button("TEXT CHAT [C / Enter]", GUILayout.Height(30)))
                {
                    FreeFireChatUI.Instance?.ToggleVisibility();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("VOICE COMMS [V]", GUILayout.Height(30)))
                {
                    FreeFireVoiceUI.Instance?.ToggleVisibility();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("GLOBAL LEADERBOARDS", GUILayout.Height(28)))
                {
                    SocialAndLeaderboardUI.Instance?.ToggleVisibility();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("CONTROLS GUIDE", GUILayout.Height(28)))
                {
                    OpenControls();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("QUIT", GUILayout.Height(26)))
                {
                    OnQuitClicked();
                }

                GUILayout.EndArea();
            }
        }
    }
}

