using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Serpent;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Governs the cinematic Evolution Selection Screen.
    /// Presents the 3 distinct metamorphosis paths:
    /// - TITAN (Colossal, Armored, Seismic Slam)
    /// - CELESTIAL (Winged, Luminous, Solar Burst & Flight)
    /// - ABYSSAL (Toxic Crest, Void Warp & Miasma)
    /// </summary>
    public class EvolutionSelectionUI : MonoBehaviour
    {
        public static EvolutionSelectionUI Instance { get; private set; }

        [Header("Modal Root")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private CanvasGroup modalCanvasGroup;

        [Header("Evolution Path Buttons")]
        [SerializeField] private Button evolveTitanButton;
        [SerializeField] private Button evolveCelestialButton;
        [SerializeField] private Button evolveAbyssalButton;
        [SerializeField] private Button closePromptButton;

        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (modalRoot != null) modalRoot.SetActive(false);

            if (evolveTitanButton != null) evolveTitanButton.onClick.AddListener(() => SelectPath(EvolutionPath.Titan));
            if (evolveCelestialButton != null) evolveCelestialButton.onClick.AddListener(() => SelectPath(EvolutionPath.Celestial));
            if (evolveAbyssalButton != null) evolveAbyssalButton.onClick.AddListener(() => SelectPath(EvolutionPath.Abyssal));
            if (closePromptButton != null) closePromptButton.onClick.AddListener(CloseModal);
        }

        public void OpenEvolutionPrompt()
        {
            if (modalRoot == null) return;
            modalRoot.SetActive(true);
            Time.timeScale = 0.05f; // Deep cinematic time slowdown
        }

        public void SelectPath(EvolutionPath path)
        {
            CloseModal();

            if (SerpentEvolution.Instance != null)
            {
                SerpentEvolution.Instance.Evolve(path);
            }
        }

        public void CloseModal()
        {
            Time.timeScale = 1.0f;
            if (modalRoot != null) modalRoot.SetActive(false);
        }
    }
}

