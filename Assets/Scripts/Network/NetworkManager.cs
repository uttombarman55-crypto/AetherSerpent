using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AetherSerpent.Network
{
    /// <summary>
    /// Master Network Manager for AetherSerpent.
    /// Governs:
    /// - Cloudflare Turnstile token handling & JWT Authentication
    /// - Matchmaking & Room Assignment
    /// - WebSocket Lifecycle & Real-time message routing
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Cloudflare Backend Configuration")]
        [SerializeField] private string backendHttpUrl = "https://aetherserpent-backend.uttombarman55.workers.dev";
        [SerializeField] private string turnstileToken = "test-token"; // Validated by Cloudflare Turnstile
        [SerializeField] private bool autoConnectOnStart = false;

        [Header("Session Info")]
        [SerializeField] private string currentRoomId = "";
        [SerializeField] private NetworkRole currentRole = NetworkRole.Solo;
        [SerializeField] private string playerId = "";
        [SerializeField] private string username = "AetherDrifter";
        [SerializeField] private string accessToken = "";
        [SerializeField] private string refreshToken = "";

        public string BackendHttpUrl => backendHttpUrl;
        public string CurrentRoomId => currentRoomId;
        public NetworkRole CurrentRole => currentRole;
        public string PlayerId => playerId;
        public string Username => username;
        public string AccessToken => accessToken;
        public bool IsConnected => webSocketClient != null && webSocketClient.State == WebSocketClient.ConnectionState.Connected;
        public int PingMs => webSocketClient != null ? webSocketClient.PingMs : 0;
        public WebSocketClient.ConnectionState ConnectionState => webSocketClient != null ? webSocketClient.State : WebSocketClient.ConnectionState.Disconnected;

        /// <summary>
        /// Allows AccountManager to synchronize credentials from Free Fire Account & Identity system.
        /// </summary>
        public void SetAuthSession(string token, string refresh, string pId, string uName)
        {
            accessToken = token;
            refreshToken = refresh;
            playerId = pId;
            username = uName;
            Debug.Log($"[NetworkManager] Session synchronized for {username} (PlayerId: {playerId})");
        }

        // Events
        public event Action<WebSocketClient.ConnectionState> OnConnectionStateChanged;
        public event Action<RoomJoinedPayload> OnRoomJoined;
        public event Action<NetworkSerpentState> OnStateSyncReceived;
        public event Action<PreyConsumedPayload> OnPreyConsumedReceived;
        public event Action<int> OnPingUpdated;

        private WebSocketClient webSocketClient;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            webSocketClient = new WebSocketClient();
            webSocketClient.OnConnected += () => OnConnectionStateChanged?.Invoke(WebSocketClient.ConnectionState.Connected);
            webSocketClient.OnDisconnected += () => OnConnectionStateChanged?.Invoke(WebSocketClient.ConnectionState.Disconnected);
            webSocketClient.OnPingUpdated += (ping) => OnPingUpdated?.Invoke(ping);
            webSocketClient.OnMessageReceived += HandleIncomingWebSocketMessage;
        }

        private async void Start()
        {
            if (autoConnectOnStart)
            {
                await JoinOrCreateRoomAsync();
            }
        }

        private void Update()
        {
            webSocketClient?.DispatchIncomingMessages();
        }

        private void OnDestroy()
        {
            _ = webSocketClient?.DisconnectAsync();
        }

        /// <summary>
        /// Authenticates with Cloudflare Workers backend via Turnstile + JWT.
        /// </summary>
        public async Task<bool> AuthenticateAsync(string customUsername = null)
        {
            if (!string.IsNullOrEmpty(customUsername)) username = customUsername;

            string endpoint = $"{backendHttpUrl}/api/auth/login";
            string jsonBody = $"{{\"username\":\"{username}\",\"turnstileToken\":\"{turnstileToken}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Turnstile-Token", turnstileToken);

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string responseText = req.downloadHandler.text;
                    AuthResponsePayload authData = JsonUtility.FromJson<AuthResponsePayload>(responseText);

                    if (authData != null && authData.success)
                    {
                        accessToken = authData.accessToken;
                        refreshToken = authData.refreshToken;
                        playerId = authData.playerId;
                        username = authData.username;
                        Debug.Log($"[NetworkManager] Authenticated successfully as {username} (ID: {playerId})");
                        return true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] Authentication failed: {req.error}");
                }
            }

            return false;
        }

        /// <summary>
        /// Joins or creates a multiplayer room with requested role (Solo, CoopHead, or CoopTail).
        /// </summary>
        public async Task<bool> JoinOrCreateRoomAsync(string preferredRoom = "", NetworkRole role = NetworkRole.Solo)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                bool authSuccess = await AuthenticateAsync();
                if (!authSuccess) return false;
            }

            string roleString = (role == NetworkRole.CoopHead) ? "coop_head" :
                               (role == NetworkRole.CoopTail) ? "coop_tail" : "solo";

            string endpoint = $"{backendHttpUrl}/api/matchmaking/find-or-create";
            string jsonBody = $"{{\"preferredRoomId\":\"{preferredRoom}\",\"requestedRole\":\"{roleString}\"}}";

            using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                var asyncOp = req.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string responseText = req.downloadHandler.text;
                    MatchmakingResponsePayload matchData = JsonUtility.FromJson<MatchmakingResponsePayload>(responseText);

                    if (matchData != null && matchData.success)
                    {
                        currentRoomId = matchData.roomId;
                        currentRole = (matchData.assignedRole == "coop_head") ? NetworkRole.CoopHead :
                                      (matchData.assignedRole == "coop_tail") ? NetworkRole.CoopTail : NetworkRole.Solo;

                        // Transform HTTP URL to WS URL
                        string wsBase = backendHttpUrl.Replace("http://", "ws://").Replace("https://", "wss://");
                        string fullWsUrl = $"{wsBase}{matchData.wsUrl}";

                        await webSocketClient.ConnectAsync(fullWsUrl);
                        return true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] Matchmaking failed: {req.error}");
                }
            }

            return false;
        }

        public async Task SendInputUpdateAsync(ClientInputPayload input)
        {
            if (!IsConnected) return;

            string payloadJson = JsonUtility.ToJson(input);
            string messageJson = $"{{\"type\":\"INPUT_UPDATE\",\"payload\":{payloadJson},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";

            await webSocketClient.SendAsync(messageJson);
        }

        public async Task SendPreyConsumptionAsync(string preyId, int preyType, Vector3 position)
        {
            if (!IsConnected) return;

            string payloadJson = $"{{\"preyId\":\"{preyId}\",\"preyType\":{preyType},\"claimedPosition\":{{\"x\":{position.x},\"y\":{position.y},\"z\":{position.z}}},\"clientTimestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
            string messageJson = $"{{\"type\":\"EAT_PREY_REQUEST\",\"payload\":{payloadJson},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";

            await webSocketClient.SendAsync(messageJson);
        }

        private void HandleIncomingWebSocketMessage(string messageJson)
        {
            try
            {
                if (messageJson.Contains("\"type\":\"ROOM_JOINED\""))
                {
                    // Parse room joined packet
                    int payloadStart = messageJson.IndexOf("\"payload\":") + 10;
                    int payloadEnd = messageJson.LastIndexOf(",\"timestamp\"");
                    if (payloadStart > 9 && payloadEnd > payloadStart)
                    {
                        string payloadStr = messageJson.Substring(payloadStart, payloadEnd - payloadStart);
                        RoomJoinedPayload roomData = JsonUtility.FromJson<RoomJoinedPayload>(payloadStr);
                        if (roomData != null)
                        {
                            OnRoomJoined?.Invoke(roomData);
                        }
                    }
                }
                else if (messageJson.Contains("\"type\":\"TICK_SYNC\""))
                {
                    int payloadStart = messageJson.IndexOf("\"payload\":") + 10;
                    int payloadEnd = messageJson.LastIndexOf(",\"timestamp\"");
                    if (payloadStart > 9 && payloadEnd > payloadStart)
                    {
                        string payloadStr = messageJson.Substring(payloadStart, payloadEnd - payloadStart);
                        NetworkSerpentState state = JsonUtility.FromJson<NetworkSerpentState>(payloadStr);
                        if (state != null)
                        {
                            OnStateSyncReceived?.Invoke(state);
                        }
                    }
                }
                else if (messageJson.Contains("\"type\":\"PREY_CONSUMED\""))
                {
                    int payloadStart = messageJson.IndexOf("\"payload\":") + 10;
                    int payloadEnd = messageJson.LastIndexOf(",\"timestamp\"");
                    if (payloadStart > 9 && payloadEnd > payloadStart)
                    {
                        string payloadStr = messageJson.Substring(payloadStart, payloadEnd - payloadStart);
                        PreyConsumedPayload preyData = JsonUtility.FromJson<PreyConsumedPayload>(payloadStr);
                        if (preyData != null)
                        {
                            OnPreyConsumedReceived?.Invoke(preyData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkManager] Failed parsing packet: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (webSocketClient != null)
            {
                await webSocketClient.DisconnectAsync();
            }
            currentRoomId = "";
        }
    }
}
