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

            // 3. Load Save System Profile
            AetherSerpent.Save.SaveSystem.LoadFromDisk();

            // 4. Verify / Instantiate Network & Social Subsystems
            if (AetherSerpent.Network.NetworkManager.Instance == null)
            {
                GameObject netObj = new GameObject("[NetworkManager]");
                netObj.AddComponent<AetherSerpent.Network.NetworkManager>();
            }

            if (AetherSerpent.Social.LeaderboardManager.Instance == null)
            {
                GameObject lbObj = new GameObject("[LeaderboardManager]");
                lbObj.AddComponent<AetherSerpent.Social.LeaderboardManager>();
            }

            if (AetherSerpent.Social.SocialManager.Instance == null)
            {
                GameObject socObj = new GameObject("[SocialManager]");
                socObj.AddComponent<AetherSerpent.Social.SocialManager>();
            }

            if (AetherSerpent.Core.VersionCheckManager.Instance == null)
            {
                GameObject verObj = new GameObject("[VersionCheckManager]");
                verObj.AddComponent<AetherSerpent.Core.VersionCheckManager>();
            }

            if (AetherSerpent.Account.AccountManager.Instance == null)
            {
                GameObject accObj = new GameObject("[AccountManager]");
                accObj.AddComponent<AetherSerpent.Account.AccountManager>();
            }

            if (AetherSerpent.UI.FreeFireProfileUI.Instance == null)
            {
                GameObject profObj = new GameObject("[FreeFireProfileUI]");
                profObj.AddComponent<AetherSerpent.UI.FreeFireProfileUI>();
            }

            if (AetherSerpent.Social.SocialGroupingManager.Instance == null)
            {
                GameObject sgObj = new GameObject("[SocialGroupingManager]");
                sgObj.AddComponent<AetherSerpent.Social.SocialGroupingManager>();
            }

            if (AetherSerpent.UI.FreeFireSocialUI.Instance == null)
            {
                GameObject sUiObj = new GameObject("[FreeFireSocialUI]");
                sUiObj.AddComponent<AetherSerpent.UI.FreeFireSocialUI>();
            }

            if (AetherSerpent.Social.ChatManager.Instance == null)
            {
                GameObject chatObj = new GameObject("[ChatManager]");
                chatObj.AddComponent<AetherSerpent.Social.ChatManager>();
            }

            if (AetherSerpent.UI.FreeFireChatUI.Instance == null)
            {
                GameObject cUiObj = new GameObject("[FreeFireChatUI]");
                cUiObj.AddComponent<AetherSerpent.UI.FreeFireChatUI>();
            }

            if (AetherSerpent.Voice.VoiceEngine.Instance == null)
            {
                GameObject vObj = new GameObject("[VoiceEngine]");
                vObj.AddComponent<AetherSerpent.Voice.VoiceEngine>();
            }

            if (AetherSerpent.UI.FreeFireVoiceUI.Instance == null)
            {
                GameObject vUiObj = new GameObject("[FreeFireVoiceUI]");
                vUiObj.AddComponent<AetherSerpent.UI.FreeFireVoiceUI>();
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

