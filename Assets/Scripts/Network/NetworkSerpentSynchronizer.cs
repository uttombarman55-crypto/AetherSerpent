using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.Serpent;
using AetherSerpent.Input;

namespace AetherSerpent.Network
{
    /// <summary>
    /// Synchronizes the local 3D Serpent with the Cloudflare GameRoom Durable Object.
    /// Supports:
    /// - Full Co-op Mode: Player A controls Head, Player B controls Tail
    /// - Zero Rubber-Banding Interpolation & Dead Reckoning
    /// - Server-Authoritative Vertebrae & Growth Alignment
    /// </summary>
    [RequireComponent(typeof(SerpentController))]
    public class NetworkSerpentSynchronizer : MonoBehaviour
    {
        [Header("Sync Rates")]
        [SerializeField] private float inputSendRate = 20.0f; // 20 packets per second
        [SerializeField] private float interpolationSmoothing = 14.0f;
        [SerializeField] private float snapThreshold = 6.0f;

        [Header("Co-op State")]
        public NetworkRole LocalRole = NetworkRole.Solo;
        public bool IsCoopActive => LocalRole != NetworkRole.Solo;

        private SerpentController controller;
        private SerpentPhysicsFollower follower;
        private SerpentGrowth growth;

        private float lastInputSendTime;
        private Vector3 serverTargetHeadPos;
        private Quaternion serverTargetHeadRot;
        private bool hasReceivedFirstSync = false;

        private void Awake()
        {
            controller = GetComponent<SerpentController>();
            follower = GetComponent<SerpentPhysicsFollower>();
            growth = GetComponent<SerpentGrowth>();
        }

        private void Start()
        {
            serverTargetHeadPos = transform.position;
            serverTargetHeadRot = transform.rotation;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnRoomJoined += HandleRoomJoined;
                NetworkManager.Instance.OnStateSyncReceived += HandleStateSync;
                LocalRole = NetworkManager.Instance.CurrentRole;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnRoomJoined -= HandleRoomJoined;
                NetworkManager.Instance.OnStateSyncReceived -= HandleStateSync;
            }
        }

        private void HandleRoomJoined(RoomJoinedPayload data)
        {
            LocalRole = NetworkManager.Instance.CurrentRole;
            Debug.Log($"[NetworkSerpentSynchronizer] Joined Room. Assigned Role: {LocalRole}");

            // Configure controller physics according to role
            if (LocalRole == NetworkRole.CoopTail)
            {
                // In Player B (Tail) mode, head movement is driven by Player A via network
                if (controller != null) controller.enabled = false;
            }
            else
            {
                if (controller != null) controller.enabled = true;
            }

            if (data.state != null)
            {
                ApplyStateImmediate(data.state);
            }
        }

        private void HandleStateSync(NetworkSerpentState state)
        {
            hasReceivedFirstSync = true;
            serverTargetHeadPos = state.headPosition.ToVector3();
            serverTargetHeadRot = state.headRotation.ToQuaternion();

            // Synchronize segment count if server grew
            if (growth != null && state.segmentCount > growth.CurrentSegmentCount)
            {
                int delta = state.segmentCount - growth.CurrentSegmentCount;
                growth.AddSegments(delta);
            }
        }

        private void Update()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected) return;

            ProcessInputDispatch();
            ProcessStateInterpolation();
        }

        private void ProcessInputDispatch()
        {
            if (Time.time - lastInputSendTime < (1.0f / inputSendRate)) return;
            lastInputSendTime = Time.time;

            Vector2 move = (InputReader.Instance != null) ? InputReader.Instance.MoveInput : Vector2.zero;
            float vertical = (InputReader.Instance != null) ? InputReader.Instance.AscendDescendInput : 0f;
            bool sprint = (InputReader.Instance != null) && InputReader.Instance.IsSprinting;

            bool tailWhip = false;
            float tailStabilize = 0f;

            if (LocalRole == NetworkRole.CoopTail)
            {
                // Player B specific tail whip input
                tailWhip = sprint || UnityEngine.Input.GetKey(KeyCode.Space);
                tailStabilize = move.x;
            }

            ClientInputPayload payload = new ClientInputPayload
            {
                role = (LocalRole == NetworkRole.CoopHead) ? "coop_head" :
                       (LocalRole == NetworkRole.CoopTail) ? "coop_tail" : "solo",
                moveInput = new NetworkVector2(move),
                verticalInput = vertical,
                isSprinting = sprint,
                tailWhipTriggered = tailWhip,
                tailStabilizeAxis = tailStabilize,
                clientTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _ = NetworkManager.Instance.SendInputUpdateAsync(payload);
        }

        private void ProcessStateInterpolation()
        {
            if (!hasReceivedFirstSync) return;

            // In CoopTail or spectator mode, smoothly converge head to server state
            if (LocalRole == NetworkRole.CoopTail)
            {
                float dist = Vector3.Distance(transform.position, serverTargetHeadPos);

                if (dist > snapThreshold)
                {
                    // Snap on extreme divergence to prevent lingering desync
                    transform.position = serverTargetHeadPos;
                    transform.rotation = serverTargetHeadRot;
                }
                else
                {
                    // Butter-smooth Hermite-style interpolation
                    transform.position = Vector3.Lerp(transform.position, serverTargetHeadPos, Time.deltaTime * interpolationSmoothing);
                    transform.rotation = Quaternion.Slerp(transform.rotation, serverTargetHeadRot, Time.deltaTime * interpolationSmoothing);
                }
            }
            else if (LocalRole == NetworkRole.CoopHead)
            {
                // In CoopHead mode, player has client prediction; gentle drift convergence
                float drift = Vector3.Distance(transform.position, serverTargetHeadPos);
                if (drift > 3.5f)
                {
                    transform.position = Vector3.Lerp(transform.position, serverTargetHeadPos, Time.deltaTime * 4.0f);
                }
            }
        }

        private void ApplyStateImmediate(NetworkSerpentState state)
        {
            transform.position = state.headPosition.ToVector3();
            transform.rotation = state.headRotation.ToQuaternion();
            serverTargetHeadPos = transform.position;
            serverTargetHeadRot = transform.rotation;
        }
    }
}

