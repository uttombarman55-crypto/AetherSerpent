using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Core;
using AetherSerpent.Serpent;
using AetherSerpent.Input;

namespace AetherSerpent.UI
{
    /// <summary>
    /// AAA Gameplay HUD displaying dynamic telemetry:
    /// - Speedometer & Boost Energy gauge
    /// - Living Serpent length counter & score
    /// - Real-time FPS Performance Metric
    /// - Comprehensive Controls guide overlay
    /// - In-game Pause menu
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Telemetry Texts")]
        [SerializeField] private Text lengthText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text fpsText;

        [Header("Energy & Gauges")]
        [SerializeField] private Slider boostEnergySlider;

        [Header("Pause Menu")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;

        private SerpentController serpentController;
        private SerpentGrowth serpentGrowth;

        private float fpsTimer = 0f;
        private int frameCount = 0;
        private int currentFps = 0;

        private void Start()
        {
            FindSerpent();

            if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            if (pausePanel != null) pausePanel.SetActive(false);

            if (InputReader.Instance != null)
            {
                InputReader.Instance.OnPauseEvent += TogglePause;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.Gameplay);
            }
        }

        private void OnDestroy()
        {
            if (InputReader.Instance != null)
            {
                InputReader.Instance.OnPauseEvent -= TogglePause;
            }
        }

        private void FindSerpent()
        {
            if (serpentController == null)
            {
                serpentController = FindFirstObjectByType<SerpentController>();
                if (serpentController != null)
                {
                    serpentGrowth = serpentController.GetComponent<SerpentGrowth>();
                }
            }
        }

        private void Update()
        {
            if (serpentController == null) FindSerpent();

            UpdateTelemetry();
            UpdateFPSCounter();
        }

        private void UpdateTelemetry()
        {
            if (serpentController != null)
            {
                if (speedText != null)
                {
                    speedText.text = $"SPEED: {serpentController.CurrentSpeed * 3.6f:0.0} km/h";
                }

                if (boostEnergySlider != null)
                {
                    boostEnergySlider.value = serpentController.BoostEnergyPercent;
                }
            }

            if (serpentGrowth != null && lengthText != null)
            {
                lengthText.text = $"LENGTH: {serpentGrowth.CurrentSegmentCount} SEGMENTS";
            }

            if (scoreText != null && GameManager.Instance != null)
            {
                scoreText.text = $"AETHER: {GameManager.Instance.CurrentScore}";
            }
        }

        private void UpdateFPSCounter()
        {
            frameCount++;
            fpsTimer += Time.unscaledDeltaTime;

            if (fpsTimer >= 0.5f)
            {
                currentFps = Mathf.RoundToInt(frameCount / fpsTimer);
                frameCount = 0;
                fpsTimer = 0f;

                if (fpsText != null)
                {
                    fpsText.text = $"{currentFps} FPS | URP 17+ HDR";
                }
            }
        }

        public void TogglePause()
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.CurrentState == GameManager.GameState.Gameplay)
            {
                GameManager.Instance.SetState(GameManager.GameState.Paused);
                if (pausePanel != null) pausePanel.SetActive(true);
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.Paused)
            {
                ResumeGame();
            }
        }

        public void ResumeGame()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.Gameplay);
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1.0f;
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("MainMenu");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
