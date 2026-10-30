using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Core;
using AetherSerpent.Camera;
using AetherSerpent.Save;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Pause Menu & Settings Controller.
    /// Governs:
    /// - Gameplay pause toggling via ESC / Gamepad Start
    /// - Audio volume controls (Master, Music, SFX)
    /// - Graphics fidelity presets (Ultra, High, Balanced)
    /// - Direct Photo Mode launch
    /// - Return to Main Menu and Quit flows
    /// </summary>
    public class SettingsAndPauseUI : MonoBehaviour
    {
        public static SettingsAndPauseUI Instance { get; private set; }

        public bool IsPaused { get; private set; } = false;

        [Header("Canvas References (Optional)")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        private float masterVolume = 1.0f;
        private float musicVolume = 0.8f;
        private float sfxVolume = 0.9f;
        private int currentQualityLevel = 2; // 0=Balanced, 1=High, 2=Ultra
        private readonly string[] qualityNames = { "Balanced (Performance)", "High Fidelity", "Ultra (URP Complete)" };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (pausePanel != null) pausePanel.SetActive(false);

            // Load saved settings
            SaveSystem.LoadFromDisk();
            masterVolume = SaveSystem.CurrentData.masterVolume;
            musicVolume = SaveSystem.CurrentData.musicVolume;
            sfxVolume = SaveSystem.CurrentData.sfxVolume;
            currentQualityLevel = Mathf.Clamp(SaveSystem.CurrentData.graphicsQualityLevel, 0, 2);

            ApplyAudioSettings();
            ApplyGraphicsSettings();
        }

        private void Update()
        {
            // Do not pause if Photo Mode is handling inputs
            if (PhotoModeController.Instance != null && PhotoModeController.Instance.IsPhotoModeActive) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
#endif
        }

        public void TogglePause()
        {
            SetPause(!IsPaused);
        }

        public void SetPause(bool pause)
        {
            IsPaused = pause;
            Time.timeScale = IsPaused ? 0.0f : 1.0f;

            if (pausePanel != null)
            {
                pausePanel.SetActive(IsPaused);
            }

            Cursor.visible = IsPaused;
            Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Confined;
        }

        public void Resume()
        {
            SetPause(false);
        }

        public void OpenPhotoMode()
        {
            SetPause(false);
            if (PhotoModeController.Instance != null)
            {
                PhotoModeController.Instance.EnterPhotoMode();
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1.0f;
            SaveSystem.SaveToDisk();
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("MainMenu");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }

        private void ApplyAudioSettings()
        {
            AudioListener.volume = masterVolume;
            SaveSystem.CurrentData.masterVolume = masterVolume;
            SaveSystem.CurrentData.musicVolume = musicVolume;
            SaveSystem.CurrentData.sfxVolume = sfxVolume;
        }

        private void ApplyGraphicsSettings()
        {
            QualitySettings.SetQualityLevel(currentQualityLevel, true);
            SaveSystem.CurrentData.graphicsQualityLevel = currentQualityLevel;
        }

        private void OnGUI()
        {
            if (!IsPaused) return;

            // Semi-transparent backdrop
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Pause Dialog
            Rect menuRect = new Rect(Screen.width / 2f - 200, Screen.height / 2f - 240, 400, 480);
            GUI.Box(menuRect, "<b>AETHERSERPENT // PAUSE & SETTINGS</b>");

            GUILayout.BeginArea(new Rect(menuRect.x + 20, menuRect.y + 40, menuRect.width - 40, menuRect.height - 50));

            // Resume Button
            if (GUILayout.Button("RESUME GAME [ESC]", GUILayout.Height(36)))
            {
                Resume();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("ENTER PHOTO MODE [P]", GUILayout.Height(32)))
            {
                OpenPhotoMode();
            }

            GUILayout.Space(12);
            GUILayout.Label("<b>--- AUDIO CONFIGURATION ---</b>");
            GUILayout.Label($"Master Volume: {Mathf.RoundToInt(masterVolume * 100)}%");
            float newMaster = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);
            if (Mathf.Abs(newMaster - masterVolume) > 0.01f)
            {
                masterVolume = newMaster;
                ApplyAudioSettings();
            }

            GUILayout.Label($"Music Volume: {Mathf.RoundToInt(musicVolume * 100)}%");
            musicVolume = GUILayout.HorizontalSlider(musicVolume, 0f, 1f);

            GUILayout.Label($"SFX Volume: {Mathf.RoundToInt(sfxVolume * 100)}%");
            sfxVolume = GUILayout.HorizontalSlider(sfxVolume, 0f, 1f);

            GUILayout.Space(12);
            GUILayout.Label("<b>--- GRAPHICS FIDELITY ---</b>");
            GUILayout.Label($"Preset: <b>{qualityNames[currentQualityLevel]}</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Balanced")) { currentQualityLevel = 0; ApplyGraphicsSettings(); }
            if (GUILayout.Button("High")) { currentQualityLevel = 1; ApplyGraphicsSettings(); }
            if (GUILayout.Button("Ultra")) { currentQualityLevel = 2; ApplyGraphicsSettings(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            if (GUILayout.Button("SAVE & RETURN TO MAIN MENU", GUILayout.Height(30)))
            {
                ReturnToMainMenu();
            }

            if (GUILayout.Button("QUIT TO DESKTOP", GUILayout.Height(26)))
            {
                SaveSystem.SaveToDisk();
                Application.Quit();
            }

            GUILayout.EndArea();
        }
    }
}
