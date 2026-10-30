using UnityEngine;
using UnityEngine.UI;

namespace AetherSerpent.Network
{
    /// <summary>
    /// Connection Status & Co-op Matchmaking UI.
    /// Provides:
    /// - Live connection status indicator (Online/Offline/Reconnecting)
    /// - Ping telemetry & active Room Code display
    /// - Role badge (Player A Head vs Player B Tail)
    /// - Matchmaking modal & quick-join buttons
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("Status HUD Displays")]
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Text pingText;
        [SerializeField] private Text roomInfoText;
        [SerializeField] private Text roleBadgeText;

        [Header("Matchmaking Panel")]
        [SerializeField] private GameObject matchmakingModal;
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Button joinAsHeadButton;
        [SerializeField] private Button joinAsTailButton;
        [SerializeField] private Button joinSoloButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button toggleModalButton;

        private bool showFallbackModal = false;
        private string guiRoomCode = "COOP_ALPHA";

        private void Start()
        {
            if (joinAsHeadButton != null) joinAsHeadButton.onClick.AddListener(() => JoinMatch(NetworkRole.CoopHead));
            if (joinAsTailButton != null) joinAsTailButton.onClick.AddListener(() => JoinMatch(NetworkRole.CoopTail));
            if (joinSoloButton != null) joinSoloButton.onClick.AddListener(() => JoinMatch(NetworkRole.Solo));
            if (disconnectButton != null) disconnectButton.onClick.AddListener(Disconnect);
            if (toggleModalButton != null) toggleModalButton.onClick.AddListener(ToggleModal);

            if (matchmakingModal != null) matchmakingModal.SetActive(false);

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged += UpdateConnectionStateUI;
                NetworkManager.Instance.OnPingUpdated += UpdatePingUI;
                NetworkManager.Instance.OnRoomJoined += UpdateRoomJoinedUI;
            }

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionStateChanged -= UpdateConnectionStateUI;
                NetworkManager.Instance.OnPingUpdated -= UpdatePingUI;
                NetworkManager.Instance.OnRoomJoined -= UpdateRoomJoinedUI;
            }
        }

        private void Update()
        {
            // Toggle matchmaking modal with [M] or [F1]
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.mKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)) ToggleModal();
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.M) || UnityEngine.Input.GetKeyDown(KeyCode.F1)) ToggleModal();
#endif
        }

        public void ToggleModal()
        {
            if (matchmakingModal != null)
            {
                matchmakingModal.SetActive(!matchmakingModal.activeSelf);
            }
            else
            {
                showFallbackModal = !showFallbackModal;
            }
        }

        private async void JoinMatch(NetworkRole role, string overrideRoom = null)
        {
            string room = overrideRoom;
            if (string.IsNullOrEmpty(room))
            {
                room = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
            }
            if (string.IsNullOrEmpty(room)) room = "COOP_ALPHA";

            if (connectionStatusText != null)
            {
                connectionStatusText.text = "<color=#FFFF40>CONNECTING...</color>";
            }

            if (NetworkManager.Instance != null)
            {
                bool success = await NetworkManager.Instance.JoinOrCreateRoomAsync(room, role);
                if (success)
                {
                    if (matchmakingModal != null) matchmakingModal.SetActive(false);
                    showFallbackModal = false;
                }
            }
        }

        private void OnGUI()
        {
            // Immediate GUI overlay for playtesting and instant telemetry feedback
            if (connectionStatusText == null)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 310, 140), GUI.skin.box);
                GUILayout.Label("<b>--- AETHERSERPENT NETWORK ---</b>");
                bool isOnline = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
                string statusStr = isOnline ? "<color=#40FF60>ONLINE (Durable Object)</color>" : "<color=#FFAAAA>OFFLINE / LOCAL SOLO</color>";
                GUILayout.Label($"Status: {statusStr}");
                if (isOnline)
                {
                    GUILayout.Label($"Room: <b>{NetworkManager.Instance.CurrentRoomId}</b> | Ping: <b>{NetworkManager.Instance.PingMs} ms</b>");
                    GUILayout.Label($"Role: <b>{NetworkManager.Instance.CurrentRole}</b>");
                    if (GUILayout.Button("Disconnect", GUILayout.Height(24)))
                    {
                        Disconnect();
                    }
                }
                else
                {
                    if (GUILayout.Button("Open Co-op Matchmaking [M]", GUILayout.Height(26)))
                    {
                        ToggleModal();
                    }
                }
                GUILayout.EndArea();
            }

            if (showFallbackModal && matchmakingModal == null)
            {
                Rect modalRect = new Rect(Screen.width / 2f - 180, Screen.height / 2f - 140, 360, 270);
                GUI.Box(modalRect, "<b>AetherSerpent Matchmaking & Co-op</b>");
                GUILayout.BeginArea(new Rect(modalRect.x + 15, modalRect.y + 30, modalRect.width - 30, modalRect.height - 40));
                
                GUILayout.Label("Room Code:");
                guiRoomCode = GUILayout.TextField(guiRoomCode, 20);
                GUILayout.Space(6);

                if (GUILayout.Button("Join Co-op as HEAD (Player A)", GUILayout.Height(30)))
                {
                    JoinMatch(NetworkRole.CoopHead, guiRoomCode);
                }
                if (GUILayout.Button("Join Co-op as TAIL (Player B)", GUILayout.Height(30)))
                {
                    JoinMatch(NetworkRole.CoopTail, guiRoomCode);
                }
                if (GUILayout.Button("Join Solo Synchronized", GUILayout.Height(30)))
                {
                    JoinMatch(NetworkRole.Solo, guiRoomCode);
                }
                GUILayout.Space(6);
                if (GUILayout.Button("Close [M]", GUILayout.Height(24)))
                {
                    showFallbackModal = false;
                }

                GUILayout.EndArea();
            }
        }

        private async void Disconnect()
        {
            if (NetworkManager.Instance != null)
            {
                await NetworkManager.Instance.DisconnectAsync();
            }
            UpdateDisplay();
        }

        private void UpdateConnectionStateUI(WebSocketClient.ConnectionState state)
        {
            UpdateDisplay();
        }

        private void UpdatePingUI(int pingMs)
        {
            if (pingText != null)
            {
                string col = (pingMs < 60) ? "#60FFA0" : (pingMs < 120 ? "#FFFF40" : "#FF6060");
                pingText.text = $"PING: <color={col}>{pingMs} ms</color>";
            }
        }

        private void UpdateRoomJoinedUI(RoomJoinedPayload payload)
        {
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            bool isOnline = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;

            if (connectionStatusText != null)
            {
                if (isOnline)
                {
                    connectionStatusText.text = "NETWORK: <color=#60FFA0>ONLINE (CLOUDFLARE)</color>";
                }
                else
                {
                    connectionStatusText.text = "NETWORK: <color=#888888>LOCAL SOLO [PRESS M FOR CO-OP]</color>";
                }
            }

            if (roomInfoText != null)
            {
                string room = (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.CurrentRoomId))
                    ? NetworkManager.Instance.CurrentRoomId
                    : "OFFLINE";
                roomInfoText.text = $"ROOM: <color=#60D0FF>{room}</color>";
            }

            if (roleBadgeText != null && NetworkManager.Instance != null)
            {
                if (!isOnline)
                {
                    roleBadgeText.text = "ROLE: LOCAL SOLO";
                }
                else
                {
                    string roleStr = (NetworkManager.Instance.CurrentRole == NetworkRole.CoopHead) ? "<color=#60FFA0>CO-OP HEAD (PLAYER A)</color>" :
                                     (NetworkManager.Instance.CurrentRole == NetworkRole.CoopTail) ? "<color=#FFB040>CO-OP TAIL (PLAYER B)</color>" :
                                     "<color=#60D0FF>SOLO SYNCHRONIZED</color>";
                    roleBadgeText.text = $"ROLE: {roleStr}";
                }
            }
        }
    }
}
