using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AetherSerpent.Network;

namespace AetherSerpent.Social
{
    [Serializable]
    public class LeaderboardEntryData
    {
        public string playerId;
        public string username;
        public int score;
        public int length;
        public int evolutionPath;
        public long submittedAt;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public bool success;
        public int count;
        public int year;
        public int week;
        public List<LeaderboardEntryData> entries = new List<LeaderboardEntryData>();
        public string error;
    }

    [Serializable]
    public class ScoreSubmissionRequest
    {
        public int score;
        public int length;
        public int evolutionPath;
    }

    /// <summary>
    /// Governs Global & Weekly Leaderboard submissions and queries.
    /// Operates against the Cloudflare SQLite-backed SocialHubDO.
    /// </summary>
    [DisallowMultipleComponent]
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        public List<LeaderboardEntryData> CachedGlobalEntries { get; private set; } = new List<LeaderboardEntryData>();
        public List<LeaderboardEntryData> CachedWeeklyEntries { get; private set; } = new List<LeaderboardEntryData>();

        public event Action<List<LeaderboardEntryData>> OnGlobalLeaderboardLoaded;
        public event Action<List<LeaderboardEntryData>> OnWeeklyLeaderboardLoaded;
        public event Action<bool> OnScoreSubmitted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private string GetBaseUrl()
        {
            return NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "http://127.0.0.1:8787";
        }

        /// <summary>
        /// Fetches the top global high scores.
        /// </summary>
        public async Task<List<LeaderboardEntryData>> FetchGlobalLeaderboardAsync(int limit = 20, int offset = 0)
        {
            string url = $"{GetBaseUrl()}/api/leaderboard/global?limit={limit}&offset={offset}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    LeaderboardResponse res = JsonUtility.FromJson<LeaderboardResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        CachedGlobalEntries = res.entries ?? new List<LeaderboardEntryData>();
                        OnGlobalLeaderboardLoaded?.Invoke(CachedGlobalEntries);
                        return CachedGlobalEntries;
                    }
                }
                else
                {
                    Debug.LogWarning($"[LeaderboardManager] Failed loading global leaderboard: {req.error}");
                }
            }

            return CachedGlobalEntries;
        }

        /// <summary>
        /// Fetches the current week's leaderboards (calculated by server UTC Monday boundary).
        /// </summary>
        public async Task<List<LeaderboardEntryData>> FetchWeeklyLeaderboardAsync(int limit = 20, int offset = 0)
        {
            string url = $"{GetBaseUrl()}/api/leaderboard/weekly?limit={limit}&offset={offset}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    LeaderboardResponse res = JsonUtility.FromJson<LeaderboardResponse>(req.downloadHandler.text);
                    if (res != null && res.success)
                    {
                        CachedWeeklyEntries = res.entries ?? new List<LeaderboardEntryData>();
                        OnWeeklyLeaderboardLoaded?.Invoke(CachedWeeklyEntries);
                        return CachedWeeklyEntries;
                    }
                }
                else
                {
                    Debug.LogWarning($"[LeaderboardManager] Failed loading weekly leaderboard: {req.error}");
                }
            }

            return CachedWeeklyEntries;
        }

        /// <summary>
        /// Submits the final score to the Cloudflare SQLite backend with server-authoritative validation.
        /// </summary>
        public async Task<bool> SubmitScoreAsync(int score, int length, int evolutionPath)
        {
            if (NetworkManager.Instance == null) return false;

            string url = $"{GetBaseUrl()}/api/leaderboard/submit";
            ScoreSubmissionRequest payload = new ScoreSubmissionRequest
            {
                score = score,
                length = length,
                evolutionPath = evolutionPath
            };

            string json = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                // Include player JWT
                if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.PlayerId))
                {
                    req.SetRequestHeader("Authorization", $"Bearer {NetworkManager.Instance.AccessToken}");
                }

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                bool success = (req.result == UnityWebRequest.Result.Success);
                if (success)
                {
                    Debug.Log($"[LeaderboardManager] Score submitted successfully: {score} pts ({length} segments)");
                }
                else
                {
                    Debug.LogWarning($"[LeaderboardManager] Score submission failed: {req.error}");
                }

                OnScoreSubmitted?.Invoke(success);
                return success;
            }
        }
    }
}

