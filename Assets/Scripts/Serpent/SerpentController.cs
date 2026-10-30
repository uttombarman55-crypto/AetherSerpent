using UnityEngine;
using AetherSerpent.Input;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// AAA Master Serpent Locomotion Controller.
    /// Features:
    /// - Heavy Physical Inertia with Acceleration & Deceleration Drag
    /// - Length-Dependent Turning Radius (Longer snake = Wider, heavier turning circle)
    /// - Surface Normal Alignment & Slope Climbing Conformance
    /// - Dynamic 3D Banking & Pitch with Organic Gyroscopic Momentum
    /// - High-Velocity Aether Boost with Smooth Energy Dynamics
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-30)]
    public class SerpentController : MonoBehaviour
    {
        [Header("Locomotion Speeds & Inertia")]
        [SerializeField] private float cruiseSpeed = 10.0f;
        [SerializeField] private float sprintSpeed = 18.0f;
        [SerializeField] private float acceleration = 8.0f;
        [SerializeField] private float deceleration = 6.0f;

        [Header("Length-Dependent Agility")]
        [SerializeField] private float baseTurnSpeed = 110.0f;
        [SerializeField] private float minTurnSpeed = 35.0f;
        [SerializeField] private float turnDampingPerSegment = 0.012f;
        [SerializeField] private float verticalPitchSpeed = 75.0f;

        [Header("Banking & Gyroscopic Roll")]
        [SerializeField] private float maxBankAngle = 38.0f;
        [SerializeField] private float bankSpeed = 4.5f;

        [Header("Slope & Ground Conformance")]
        [SerializeField] private bool conformToTerrain = true;
        [SerializeField] private LayerMask terrainMask = ~0;
        [SerializeField] private float groundRayDistance = 3.5f;
        [SerializeField] private float desiredGroundClearance = 0.75f;
        [SerializeField] private float slopeClimbAssist = 5.0f;

        [Header("Boost Energy System")]
        [SerializeField] private float maxBoostEnergy = 100f;
        [SerializeField] private float boostConsumptionRate = 30f;
        [SerializeField] private float boostRechargeRate = 22f;
        [SerializeField] private float boostRechargeDelay = 0.8f;

        public float CurrentSpeed { get; private set; }
        public bool IsBoosting { get; private set; }
        public float BoostEnergyPercent => currentBoostEnergy / maxBoostEnergy;
        public float EffectiveTurnSpeed { get; private set; }

        private CharacterController characterController;
        private SerpentGrowth serpentGrowth;
        private float currentBoostEnergy;
        private float lastBoostTime;
        private float currentBank = 0f;
        private float targetPitch = 0f;
        private float currentPitch = 0f;
        private Vector3 groundNormal = Vector3.up;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            serpentGrowth = GetComponent<SerpentGrowth>();
            currentBoostEnergy = maxBoostEnergy;
            CurrentSpeed = cruiseSpeed;
            EffectiveTurnSpeed = baseTurnSpeed;
        }

        private void Update()
        {
            HandleBoostEnergy();
            CalculateDynamicAgility();
            ProcessTerrainProbing();
            ProcessLocomotion();
        }

        private void HandleBoostEnergy()
        {
            bool wantsSprint = InputReader.Instance != null && InputReader.Instance.IsSprinting;

            if (wantsSprint && currentBoostEnergy > 4f)
            {
                IsBoosting = true;
                currentBoostEnergy = Mathf.Max(0f, currentBoostEnergy - (boostConsumptionRate * Time.deltaTime));
                lastBoostTime = Time.time;
            }
            else
            {
                IsBoosting = false;
                if (Time.time > lastBoostTime + boostRechargeDelay)
                {
                    currentBoostEnergy = Mathf.Min(maxBoostEnergy, currentBoostEnergy + (boostRechargeRate * Time.deltaTime));
                }
            }
        }

        private void CalculateDynamicAgility()
        {
            // As the serpent grows longer, its rotational inertia increases significantly,
            // producing a wide, majestic, heavy turning radius suitable for a massive leviathan
            int segmentCount = (serpentGrowth != null) ? serpentGrowth.CurrentSegmentCount : 50;
            float inertiaFactor = 1.0f + (segmentCount * turnDampingPerSegment);
            EffectiveTurnSpeed = Mathf.Max(minTurnSpeed, baseTurnSpeed / inertiaFactor);
        }

        private void ProcessTerrainProbing()
        {
            if (!conformToTerrain) return;

            // Probe terrain underneath and slightly forward of the head
            Vector3 probeOrigin = transform.position + (transform.forward * 1.0f) + (Vector3.up * 1.5f);
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, groundRayDistance + 2f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                groundNormal = Vector3.Slerp(groundNormal, hit.normal, Time.deltaTime * 8f);

                // Detect upcoming slope incline
                float heightDiff = hit.point.y + desiredGroundClearance - transform.position.y;
                if (heightDiff > 0.05f)
                {
                    // Slope climb assist lifts the head smoothly over ramps/stairs
                    targetPitch = Mathf.Lerp(targetPitch, 20f * Mathf.Clamp01(heightDiff), Time.deltaTime * slopeClimbAssist);
                }
            }
            else
            {
                groundNormal = Vector3.Slerp(groundNormal, Vector3.up, Time.deltaTime * 3f);
            }
        }

        private void ProcessLocomotion()
        {
            Vector2 moveInput = Vector2.zero;
            float verticalInput = 0f;

            if (InputReader.Instance != null)
            {
                moveInput = InputReader.Instance.MoveInput;
                verticalInput = InputReader.Instance.AscendDescendInput;
            }

            // Target speed with realistic acceleration/deceleration curves
            float targetSpeed = IsBoosting ? sprintSpeed : cruiseSpeed;
            float rate = (targetSpeed > CurrentSpeed) ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);

            // Turn steering (Yaw) with length-scaled inertia
            float yawDelta = moveInput.x * EffectiveTurnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yawDelta, Space.World);

            // Pitch steering (climb / dive)
            float pitchRate = verticalPitchSpeed * Time.deltaTime;
            targetPitch = Mathf.Clamp(targetPitch + (verticalInput * pitchRate), -60f, 60f);

            // Auto-level pitch towards terrain slope when vertical input is neutral
            if (Mathf.Abs(verticalInput) < 0.05f)
            {
                float slopePitch = Vector3.SignedAngle(Vector3.up, groundNormal, transform.right);
                targetPitch = Mathf.MoveTowards(targetPitch, slopePitch * 0.4f, 25f * Time.deltaTime);
            }
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 6f);

            // Bank roll into turns (heavier banking at higher speeds)
            float speedFactor = CurrentSpeed / cruiseSpeed;
            float targetBank = -moveInput.x * maxBankAngle * speedFactor;
            currentBank = Mathf.Lerp(currentBank, targetBank, Time.deltaTime * bankSpeed);

            // Orient head with pitch, yaw, and banking roll
            Vector3 forwardEuler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(currentPitch, forwardEuler.y, currentBank);

            // Forward locomotion
            Vector3 forwardVelocity = transform.forward * CurrentSpeed;

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(forwardVelocity * Time.deltaTime);
            }
            else
            {
                transform.position += forwardVelocity * Time.deltaTime;
            }
        }
    }
}
