using UnityEngine;
using AetherSerpent.Serpent;
using AetherSerpent.Input;

namespace AetherSerpent.CameraSystem
{
    /// <summary>
    /// AAA Dynamic Third-Person Camera System.
    /// Features:
    /// - Dynamic distance & height adaptation based on serpent length & segment count
    /// - Dynamic high-speed pullback & FOV widening during boost
    /// - Jitter-free spherical position and rotation damping
    /// - Integrated 6-DOF Perlin trauma camera shake
    /// - Manual orbit control (Gamepad Right Stick / Mouse / Touch Drag)
    /// </summary>
    public class AetherCameraController : MonoBehaviour
    {
        [Header("Target & Focus")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Distance Scaling (Length-Adaptive)")]
        [SerializeField] private float baseDistance = 8.0f;
        [SerializeField] private float distancePerSegment = 0.28f;
        [SerializeField] private float maxDistance = 35.0f;
        [SerializeField] private float boostDistancePullback = 2.5f;

        [Header("Height & Elevation")]
        [SerializeField] private float baseHeight = 3.5f;
        [SerializeField] private float heightPerSegment = 0.12f;

        [Header("Orbit Angles")]
        [SerializeField] private float currentPitch = 18f;
        [SerializeField] private float currentYaw = 0f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float orbitSensitivity = 2.0f;

        [Header("Smooth Damping (No-Jitter)")]
        [SerializeField] private float followDamping = 8.0f;
        [SerializeField] private float rotationDamping = 10.0f;

        [Header("FOV Dynamics")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float baseFOV = 60f;
        [SerializeField] private float boostFOV = 72f;

        private Vector3 currentVelocity;
        private float currentDistance;
        private float targetDistance;
        private SerpentGrowth serpentGrowth;
        private SerpentController serpentController;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) targetCamera = Camera.main;
            }

            currentDistance = baseDistance;
            targetDistance = baseDistance;
        }

        private void Start()
        {
            FindTargetSerpent();
        }

        public void FindTargetSerpent()
        {
            if (target == null)
            {
                var controller = FindFirstObjectByType<SerpentController>();
                if (controller != null)
                {
                    target = controller.transform;
                    serpentController = controller;
                    serpentGrowth = controller.GetComponent<SerpentGrowth>();
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                FindTargetSerpent();
                if (target == null) return;
            }

            HandleInputOrbit();
            CalculateDynamicFraming();
            UpdateCameraTransform();
            UpdateFOVDynamics();
        }

        private void HandleInputOrbit()
        {
            if (InputReader.Instance != null)
            {
                Vector2 look = InputReader.Instance.LookInput;
                currentYaw += look.x * orbitSensitivity;
                currentPitch = Mathf.Clamp(currentPitch - (look.y * orbitSensitivity), minPitch, maxPitch);

                float zoom = InputReader.Instance.ZoomInput;
                if (Mathf.Abs(zoom) > 0.01f)
                {
                    baseDistance = Mathf.Clamp(baseDistance - (zoom * 2.0f), 4f, 25f);
                }
            }
        }

        private void CalculateDynamicFraming()
        {
            int segmentCount = (serpentGrowth != null) ? serpentGrowth.CurrentSegmentCount : 12;
            bool isBoosting = (serpentController != null) && serpentController.IsBoosting;

            // Dynamically scale distance and height based on total snake length
            float lengthFactor = segmentCount * distancePerSegment;
            float boostFactor = isBoosting ? boostDistancePullback : 0f;

            targetDistance = Mathf.Clamp(baseDistance + lengthFactor + boostFactor, baseDistance, maxDistance);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * 3.5f);
        }

        private void UpdateCameraTransform()
        {
            Vector3 targetPivot = target.position + targetOffset;

            // Compute ideal rotation
            Quaternion desiredRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

            // Compute ideal position behind target pivot
            float dynamicHeight = baseHeight + ((serpentGrowth != null ? serpentGrowth.CurrentSegmentCount : 12) * heightPerSegment);
            Vector3 offsetVector = desiredRotation * new Vector3(0f, dynamicHeight, -currentDistance);
            Vector3 desiredPosition = targetPivot + offsetVector;

            // Apply smooth jitter-free position damping
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followDamping);

            // Camera look rotation
            Quaternion targetLookRotation = Quaternion.LookRotation(targetPivot - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetLookRotation, Time.deltaTime * rotationDamping);

            // Apply Trauma Shake from CameraShakeManager
            if (CameraShakeManager.Instance != null)
            {
                transform.position += CameraShakeManager.Instance.ShakeOffsetPosition;
                transform.rotation = transform.rotation * Quaternion.Euler(CameraShakeManager.Instance.ShakeOffsetRotation);
            }
        }

        private void UpdateFOVDynamics()
        {
            if (targetCamera == null) return;

            bool isBoosting = (serpentController != null) && serpentController.IsBoosting;
            float targetFOVVal = isBoosting ? boostFOV : baseFOV;
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOVVal, Time.deltaTime * 4.0f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                serpentController = target.GetComponent<SerpentController>();
                serpentGrowth = target.GetComponent<SerpentGrowth>();
            }
        }
    }
}
