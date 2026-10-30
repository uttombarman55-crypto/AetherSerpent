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
    }
}

