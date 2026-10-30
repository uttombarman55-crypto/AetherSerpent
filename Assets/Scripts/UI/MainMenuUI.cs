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
            if (acc != null && acc.Profile != null)
            {
                Rect profilePillRect = new Rect(Screen.width - 240, 15, 225, 48);
                GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);
                GUI.Box(profilePillRect, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(new Rect(profilePillRect.x + 8, profilePillRect.y + 4, profilePillRect.width - 16, profilePillRect.height - 8));
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{acc.Username}</b>\n<color=#00e5ff>UID: {acc.NumericId}</color>", GUILayout.Width(130));
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
                Rect menuRect = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 160, 320, 320);
                GUI.Box(menuRect, "<b>AETHERSERPENT // MAIN MENU</b>");
                GUILayout.BeginArea(new Rect(menuRect.x + 20, menuRect.y + 35, menuRect.width - 40, menuRect.height - 45));

                if (GUILayout.Button("<b>PLAY GAME (SANCTUARY)</b>", GUILayout.Height(40)))
                {
                    OnPlayClicked();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("PROFILE & IDENTITY [P]", GUILayout.Height(32)))
                {
                    FreeFireProfileUI.Instance?.ToggleVisibility();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("LEADERBOARDS & SOCIAL [L]", GUILayout.Height(32)))
                {
                    if (SocialAndLeaderboardUI.Instance != null)
                    {
                        SocialAndLeaderboardUI.Instance.ToggleVisibility();
                    }
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

