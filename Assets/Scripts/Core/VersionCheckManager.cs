using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AetherSerpent.Network;

namespace AetherSerpent.Core
{
    [Serializable]
    public class VersionCheckResult
    {
        public string clientVersion;
        public string minSupportedVersion;
        public string latestVersion;
        public bool isUpdateRequired;
        public bool isMaintenanceActive;
        public string maintenanceMessage;
    }

    /// <summary>
    /// Checks for client version compatibility and server maintenance windows.
    /// Fails safely: offline single-player remains 100% accessible if servers cannot be reached.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public class VersionCheckManager : MonoBehaviour
    {
        public static VersionCheckManager Instance { get; private set; }

        public const string CurrentClientVersion = "1.0.0";

        public bool IsCheckComplete { get; private set; } = false;
        public bool IsUpdateRequired { get; private set; } = false;
        public bool IsMaintenanceActive { get; private set; } = false;
        public string MaintenanceMessage { get; private set; } = "";

        public event Action<bool, string> OnMaintenanceStatusChanged;

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

        private async void Start()
        {
            await RunVersionAndMaintenanceCheckAsync();
        }

        public async Task<bool> RunVersionAndMaintenanceCheckAsync()
        {
            string baseUrl = NetworkManager.Instance != null ? NetworkManager.Instance.BackendHttpUrl : "http://127.0.0.1:8787";
            string url = $"{baseUrl}/api/system/version-check?clientVersion={CurrentClientVersion}";

            try
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = 5; // Fast 5s timeout
                    var asyncOp = req.SendWebRequest();
                    while (!asyncOp.isDone) await Task.Yield();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        VersionCheckResult res = JsonUtility.FromJson<VersionCheckResult>(req.downloadHandler.text);
                        if (res != null)
                        {
                            IsUpdateRequired = res.isUpdateRequired;
                            IsMaintenanceActive = res.isMaintenanceActive;
                            MaintenanceMessage = res.maintenanceMessage ?? "";

                            if (IsMaintenanceActive)
                            {
                                Debug.LogWarning($"[VersionCheckManager] Maintenance mode active: {MaintenanceMessage}");
                                OnMaintenanceStatusChanged?.Invoke(true, MaintenanceMessage);
                            }
                            else
                            {
                                Debug.Log("[VersionCheckManager] Version validated. All systems nominal.");
                            }

                            IsCheckComplete = true;
                            return true;
                        }
                    }
                    else
                    {
                        Debug.Log($"[VersionCheckManager] Offline or server unreachable ({req.error}). Falling back cleanly to offline mode.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[VersionCheckManager] Version check skipped ({ex.Message}). Operating in standalone mode.");
            }

            IsCheckComplete = true;
            return false;
        }

        private void OnGUI()
        {
            if (IsMaintenanceActive)
            {
                GUI.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                GUI.Box(new Rect(10, Screen.height - 40, Screen.width - 20, 30), $"<b>MAINTENANCE NOTICE:</b> {MaintenanceMessage} (Single-player remains active)");
                GUI.color = Color.white;
            }
        }
    }
}

