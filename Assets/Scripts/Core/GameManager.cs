using UnityEngine;
using UnityEngine.SceneManagement;

namespace AetherSerpent.Core
{
    /// <summary>
    /// Master Singleton governing application lifecycle, game states, and global services.
    /// Follows AAA architectural standards with strict persistent lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Boot,
            MainMenu,
            Gameplay,
            Paused,
            GameOver
        }

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Boot;
        public GameState CurrentState => currentState;

        [Header("Gameplay Data")]
        [SerializeField] private int currentScore = 0;
        [SerializeField] private int serpentsFed = 0;
        public int CurrentScore => currentScore;
        public int SerpentsFed => serpentsFed;

        public delegate void StateChangeHandler(GameState newState);
        public static event StateChangeHandler OnGameStateChanged;

        public delegate void ScoreChangeHandler(int newScore);
        public static event ScoreChangeHandler OnScoreChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeApp();
        }

        private void InitializeApp()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1.0f;
            Debug.Log("[GameManager] Initialized AetherSerpent_EternalCoil runtime systems.");
        }

        public void SetState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            Debug.Log($"[GameManager] Game State transitioned to: {newState}");

            if (newState == GameState.Paused)
            {
                Time.timeScale = 0.0f;
            }
            else
            {
                Time.timeScale = 1.0f;
            }

            OnGameStateChanged?.Invoke(newState);
        }

        public void AddScore(int points)
        {
            currentScore += points;
            serpentsFed++;
            OnScoreChanged?.Invoke(currentScore);
        }

        public void ResetScore()
        {
            currentScore = 0;
            serpentsFed = 0;
            OnScoreChanged?.Invoke(currentScore);
        }

        public void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
