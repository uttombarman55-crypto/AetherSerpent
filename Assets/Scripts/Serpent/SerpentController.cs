using UnityEngine;
using AetherSerpent.Input;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// Master Serpent Controller.
    /// Manages 3D flight and slithering movement, turning momentum,
    /// banking into turns, and high-velocity Aether boost dynamics.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SerpentController : MonoBehaviour
    {
        [Header("Locomotion Speeds")]
        [SerializeField] private float cruiseSpeed = 10.0f;
        [SerializeField] private float sprintSpeed = 18.0f;
        [SerializeField] private float acceleration = 12.0f;

        [Header("Turning & Agility")]
        [SerializeField] private float turnSpeed = 120.0f;
        [SerializeField] private float verticalPitchSpeed = 80.0f;
        [SerializeField] private float maxBankAngle = 35.0f;
        [SerializeField] private float bankSpeed = 5.0f;

        [Header("Boost Energy System")]
        [SerializeField] private float maxBoostEnergy = 100f;
        [SerializeField] private float boostConsumptionRate = 35f;
        [SerializeField] private float boostRechargeRate = 20f;
        [SerializeField] private float boostRechargeDelay = 1.0f;

        public float CurrentSpeed { get; private set; }
        public bool IsBoosting { get; private set; }
        public float BoostEnergyPercent => currentBoostEnergy / maxBoostEnergy;

        private CharacterController characterController;
        private float currentBoostEnergy;
        private float lastBoostTime;
        private float currentBank = 0f;
        private float targetPitch = 0f;
        private float currentPitch = 0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            currentBoostEnergy = maxBoostEnergy;
            CurrentSpeed = cruiseSpeed;
        }

        private void Update()
        {
            HandleBoostEnergy();
            ProcessMovement();
        }

        private void HandleBoostEnergy()
        {
            bool wantsSprint = InputReader.Instance != null && InputReader.Instance.IsSprinting;

            if (wantsSprint && currentBoostEnergy > 5f)
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

        private void ProcessMovement()
        {
            Vector2 moveInput = Vector2.zero;
            float verticalInput = 0f;

            if (InputReader.Instance != null)
            {
                moveInput = InputReader.Instance.MoveInput;
                verticalInput = InputReader.Instance.AscendDescendInput;
            }

            // Target speed
            float targetSpeed = IsBoosting ? sprintSpeed : cruiseSpeed;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, acceleration * Time.deltaTime);

            // Turn steering (Yaw)
            float yawDelta = moveInput.x * turnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yawDelta, Space.World);

            // Pitch steering (Pitch up/down)
            targetPitch = Mathf.Clamp(targetPitch + (verticalInput * verticalPitchSpeed * Time.deltaTime), -65f, 65f);
            // Center pitch smoothly when no vertical input
            if (Mathf.Abs(verticalInput) < 0.05f)
            {
                targetPitch = Mathf.MoveTowards(targetPitch, 0f, 30f * Time.deltaTime);
            }
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 6f);

            // Bank roll into turns
            float targetBank = -moveInput.x * maxBankAngle;
            currentBank = Mathf.Lerp(currentBank, targetBank, Time.deltaTime * bankSpeed);

            // Apply combined pitch and bank roll
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
