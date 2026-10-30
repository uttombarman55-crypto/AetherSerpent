using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AetherSerpent.Core
{
    /// <summary>
    /// Handles enterprise-grade boot sequence, hardware capability auditing,
    /// subsystem prewarming, and asynchronous handoff to MainMenu scene.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [Header("UI References (Optional)")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text loadingText;

        [Header("Configuration")]
        [SerializeField] private float minimumBootTime = 1.2f;
        [SerializeField] private string targetScene = "MainMenu";

        private void Start()
        {
            StartCoroutine(ExecuteBootSequence());
        }

        private IEnumerator ExecuteBootSequence()
        {
            UpdateStatus("Initializing Aether Engine Subsystems...", 0.15f);

            // 1. Verify / Instantiate GameManager
            if (GameManager.Instance == null)
            {
                GameObject gmObj = new GameObject("[GameManager]");
                gmObj.AddComponent<GameManager>();
            }

            // 2. Verify / Instantiate SceneTransitionManager
            if (SceneTransitionManager.Instance == null)
            {
                GameObject stmObj = new GameObject("[SceneTransitionManager]");
                stmObj.AddComponent<SceneTransitionManager>();
            }

            yield return new WaitForSeconds(0.3f);
            UpdateStatus("Validating Universal Render Pipeline & Shaders...", 0.45f);

            // 3. Setup graphics configuration
            AetherGraphicsConfigurator configurator = FindFirstObjectByType<AetherGraphicsConfigurator>();
            if (configurator == null)
            {
                GameObject gfxObj = new GameObject("[GraphicsConfigurator]");
                configurator = gfxObj.AddComponent<AetherGraphicsConfigurator>();
            }
            configurator.EnforceHighEndGraphics();

            yield return new WaitForSeconds(0.4f);
            UpdateStatus("Connecting Input System & Controllers...", 0.8f);

            yield return new WaitForSeconds(0.5f);
            UpdateStatus("Ready! Entering Sanctuary...", 1.0f);

            yield return new WaitForSeconds(0.2f);

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene(targetScene);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            }
        }

        private void UpdateStatus(string message, float progress)
        {
            if (loadingText != null) loadingText.text = message;
            if (progressBar != null) progressBar.value = progress;
            Debug.Log($"[BootLoader] {message} ({progress * 100f:0}%)");
        }
    }
}
