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
            // Immediate GUI fallback for testing and easy navigation
            if (playButton == null)
            {
                Rect menuRect = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 140, 320, 280);
                GUI.Box(menuRect, "<b>AETHERSERPENT // MAIN MENU</b>");
                GUILayout.BeginArea(new Rect(menuRect.x + 20, menuRect.y + 40, menuRect.width - 40, menuRect.height - 50));

                if (GUILayout.Button("<b>PLAY GAME (SANCTUARY)</b>", GUILayout.Height(40)))
                {
                    OnPlayClicked();
                }

                GUILayout.Space(8);
                if (GUILayout.Button("LEADERBOARDS & SOCIAL [L]", GUILayout.Height(32)))
                {
                    if (SocialAndLeaderboardUI.Instance != null)
                    {
                        SocialAndLeaderboardUI.Instance.ToggleVisibility();
                    }
                }

                GUILayout.Space(8);
                if (GUILayout.Button("CONTROLS GUIDE", GUILayout.Height(30)))
                {
                    OpenControls();
                }

                GUILayout.Space(8);
                if (GUILayout.Button("QUIT", GUILayout.Height(26)))
                {
                    OnQuitClicked();
                }

                GUILayout.EndArea();
            }
        }
    }
}

