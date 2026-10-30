using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Core;
using AetherSerpent.Serpent;
using AetherSerpent.Environment;
using AetherSerpent.Input;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Master AAA Gameplay HUD.
    /// Manages:
    /// - Biological Vitals (Health, Hunger/Metabolism, Energy/Stamina)
    /// - Living Serpent Telemetry (Length, Speedometer, Aether Harvest)
    /// - Evolution Progress & Path Status (Titan, Celestial, Abyssal)
    /// - Active Class Ability Cooldowns (Seismic Slam, Solar Burst, Void Warp)
    /// - Real-time World Telemetry (Time-of-Day, Active Biome Banner)
    /// - Evolution Prompt Trigger [TAB]
    /// - In-game Pause & Comprehensive Settings Menu
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Vitals Gauges (Sliders)")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider hungerSlider;
        [SerializeField] private Slider energySlider;

        [Header("Telemetry Texts")]
        [SerializeField] private Text lengthText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text fpsText;
        [SerializeField] private Text worldStatusText; // Time & Biome
        [SerializeField] private Text abilityStatusText;

        [Header("Evolution Prompt Button")]
        [SerializeField] private Button evolveButton;
        [SerializeField] private Text evolveButtonText;

        [Header("Pause & Settings Menu")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Dropdown qualityDropdown;

        private SerpentController serpentController;
        private SerpentGrowth serpentGrowth;
        private SerpentEvolution serpentEvolution;

        private float fpsTimer = 0f;
        private int frameCount = 0;
        private int currentFps = 0;

        private void Start()
        {
            FindSerpent();

            if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            if (evolveButton != null) evolveButton.onClick.AddListener(OpenEvolutionScreen);

            if (qualityDropdown != null)
            {
                qualityDropdown.value = QualitySettings.GetQualityLevel();
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            if (pausePanel != null) pausePanel.SetActive(false);

            if (InputReader.Instance != null)
            {
                InputReader.Instance.OnPauseEvent += TogglePause;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.Gameplay);
            }

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
                    serpentEvolution = serpentController.GetComponent<SerpentEvolution>();
                }
            }
        }

        private void Update()
        {
            if (serpentController == null) FindSerpent();

            // Check hotkey [Tab] to open evolution modal directly
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame) OpenEvolutionScreen();
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) OpenEvolutionScreen();
#endif

            UpdateVitalsUI();
            UpdateTelemetryUI();
            UpdateWorldAndAbilityUI();
            UpdateFPSCounter();
        }

        private void UpdateVitalsUI()
        {
            if (SerpentVitals.Instance != null)
            {
                if (healthSlider != null) healthSlider.value = SerpentVitals.Instance.HealthPercent;
                if (hungerSlider != null) hungerSlider.value = SerpentVitals.Instance.HungerPercent;
                if (energySlider != null) energySlider.value = SerpentVitals.Instance.EnergyPercent;
            }
            else if (serpentController != null && energySlider != null)
            {
                energySlider.value = serpentController.BoostEnergyPercent;
            }
        }

        private void UpdateTelemetryUI()
        {
            if (serpentController != null && speedText != null)
            {
                speedText.text = $"SPEED: {serpentController.CurrentSpeed * 3.6f:0.0} km/h";
            }

            if (serpentGrowth != null && lengthText != null)
            {
                int count = serpentGrowth.CurrentSegmentCount;
                string formName = (serpentEvolution != null && serpentEvolution.CurrentPath != EvolutionPath.Primal)
                    ? serpentEvolution.CurrentPath.ToString().ToUpper()
                    : ((count < 90) ? "JUVENILE SERPENT" : (count < 160 ? "ELDER DRAKE" : "CELESTIAL LEVIATHAN"));

                lengthText.text = $"LENGTH: {count} SEGMENTS\n<size=18><color=#60E0FF>{formName}</color></size>";
            }

            if (scoreText != null && GameManager.Instance != null)
            {
                scoreText.text = $"AETHER: {GameManager.Instance.CurrentScore}";
            }
        }

        private void UpdateWorldAndAbilityUI()
        {
            // World status: Biome & Time-of-Day
            if (worldStatusText != null)
            {
                string biome = (BiomeManager.Instance != null) ? BiomeManager.Instance.BiomeDisplayName : "CELESTIAL SANCTUM";
                string time = (DayNightCycle.Instance != null) ? DayNightCycle.Instance.TimeDescription : "MIDDAY";
                worldStatusText.text = $"{biome} | {time}";
            }

            // Ability status
            if (abilityStatusText != null && serpentEvolution != null)
            {
                if (serpentEvolution.CurrentPath == EvolutionPath.Primal)
                {
                    abilityStatusText.text = "CLASS ABILITY: <color=#888888>LOCKED (EVOLVE TO UNLOCK)</color>";
                }
                else
                {
                    string abilityName = (serpentEvolution.CurrentPath == EvolutionPath.Titan) ? "SEISMIC SLAM" :
                                         (serpentEvolution.CurrentPath == EvolutionPath.Celestial) ? "SOLAR BURST" : "VOID WARP";

                    if (serpentEvolution.IsAbilityReady)
                    {
                        abilityStatusText.text = $"ABILITY [Q / R / Gamepad X]: <color=#60FFA0>{abilityName} READY</color>";
                    }
                    else
                    {
                        float cd = 6f * (1f - serpentEvolution.AbilityCooldownNormalized);
                        abilityStatusText.text = $"ABILITY: <color=#FFA040>{abilityName} ({cd:0.0}s)</color>";
                    }
                }
            }

            // Evolve Button availability
            if (evolveButton != null && serpentEvolution != null)
            {
                float maxEssence = Mathf.Max(serpentEvolution.TitanEssence, serpentEvolution.CelestialEssence, serpentEvolution.AbyssalEssence);
                bool canEvolve = (serpentEvolution.CurrentPath == EvolutionPath.Primal) && (maxEssence >= serpentEvolution.EssenceThreshold);

                evolveButton.gameObject.SetActive(canEvolve);
                if (canEvolve && evolveButtonText != null)
                {
                    evolveButtonText.text = "METAMORPHOSIS READY [TAB]";
                }
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

        public void OpenEvolutionScreen()
        {
            if (EvolutionSelectionUI.Instance != null)
            {
                EvolutionSelectionUI.Instance.OpenEvolutionPrompt();
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
        }

        public void RestartGame()
        {
            Time.timeScale = 1.0f;
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("Gameplay");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
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

        public void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
        }

        private void OnGUI()
        {
            // HUD Profile & Social quick access buttons in top-right
            if ((FreeFireProfileUI.Instance == null || !FreeFireProfileUI.Instance.IsVisible) &&
                (FreeFireSocialUI.Instance == null || !FreeFireSocialUI.Instance.IsVisible) &&
                (FreeFireChatUI.Instance == null || !FreeFireChatUI.Instance.IsVisible))
            {
                var acc = AetherSerpent.Account.AccountManager.Instance;
                string uidStr = acc != null && acc.NumericId > 0 ? $"UID: {acc.NumericId}" : "PROFILE";
                Rect profileRect = new Rect(Screen.width - 390, 10, 120, 28);
                if (GUI.Button(profileRect, $"<b>{uidStr} [P]</b>"))
                {
                    FreeFireProfileUI.Instance?.ToggleVisibility();
                }

                Rect socialRect = new Rect(Screen.width - 260, 10, 120, 28);
                if (GUI.Button(socialRect, "<b>SOCIAL [L]</b>"))
                {
                    FreeFireSocialUI.Instance?.ToggleVisibility();
                }

                Rect chatRect = new Rect(Screen.width - 130, 10, 120, 28);
                if (GUI.Button(chatRect, "<b>CHAT [C]</b>"))
                {
                    FreeFireChatUI.Instance?.ToggleVisibility();
                }
            }
        }
    }
}
