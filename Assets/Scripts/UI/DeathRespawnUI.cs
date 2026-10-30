using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Core;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Governs the Death and Reincarnation flow.
    /// Triggers when SerpentVitals reaches zero health.
    /// Provides reincarnation statistics and seamless respawn back into gameplay.
    /// </summary>
    public class DeathRespawnUI : MonoBehaviour
    {
        [Header("Modal Root")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private Text deathSummaryText;
        [SerializeField] private Button reincarnateButton;
        [SerializeField] private Button returnMainMenuButton;

        private void Start()
        {
            if (deathPanel != null) deathPanel.SetActive(false);

            if (reincarnateButton != null) reincarnateButton.onClick.AddListener(OnReincarnateClicked);
            if (returnMainMenuButton != null) returnMainMenuButton.onClick.AddListener(OnReturnMainMenuClicked);

            if (SerpentVitals.Instance != null)
            {
                SerpentVitals.Instance.OnDeathEvent += ShowDeathScreen;
            }
        }

        private void OnDestroy()
        {
            if (SerpentVitals.Instance != null)
            {
                SerpentVitals.Instance.OnDeathEvent -= ShowDeathScreen;
            }
        }

        private void ShowDeathScreen()
        {
            if (deathPanel == null) return;
            deathPanel.SetActive(true);

            int score = (GameManager.Instance != null) ? GameManager.Instance.CurrentScore : 0;
            if (deathSummaryText != null)
            {
                deathSummaryText.text = $"YOUR PHYSICAL COIL HAS DISSOLVED\n\nAether Reaped: {score}\n\nThe soul of the Serpent is eternal. Reincarnate to begin anew.";
            }
        }

        private void OnReincarnateClicked()
        {
            if (deathPanel != null) deathPanel.SetActive(false);

            if (SerpentVitals.Instance != null)
            {
                SerpentVitals.Instance.Respawn();
            }
        }

        private void OnReturnMainMenuClicked()
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

