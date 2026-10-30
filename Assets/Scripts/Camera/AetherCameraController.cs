using UnityEngine;
using AetherSerpent.Serpent;
using AetherSerpent.Input;

namespace AetherSerpent.CameraSystem
{
    /// <summary>
    /// AAA Dynamic Third-Person Camera System.
    /// Features:
    /// - Logarithmic Dynamic Framing scaling from 40 to 300+ segments seamlessly
    /// - Center-of-Mass Focus Blending (frames the entire majestic serpent body)
    /// - Dynamic high-speed pullback & FOV widening during boost
    /// - Jitter-free spherical position and rotation damping
    /// - Integrated 6-DOF Perlin trauma camera shake
    /// - Manual orbit control (Gamepad Right Stick / Mouse / Touch Drag)
    /// </summary>
    public class AetherCameraController : MonoBehaviour
    {
        [Header("Target & Focus")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        [Range(0f, 1f)]
        [SerializeField] private float midpointFocusBias = 0.35f; // Blends focus between head and body center

        [Header("Distance Scaling (Length-Adaptive)")]
        [SerializeField] private float baseDistance = 11.0f;
        [SerializeField] private float distancePowerScale = 1.35f;
        [SerializeField] private float maxDistance = 120.0f;
        [SerializeField] private float boostDistancePullback = 3.5f;

        [Header("Height & Elevation")]
        [SerializeField] private float baseHeight = 4.2f;
        [SerializeField] private float heightPowerScale = 0.55f;

        [Header("Orbit Angles")]
        [SerializeField] private float currentPitch = 20f;
        [SerializeField] private float currentYaw = 0f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float orbitSensitivity = 2.0f;

        [Header("Smooth Damping (No-Jitter)")]
        [SerializeField] private float followDamping = 7.0f;
        [SerializeField] private float rotationDamping = 9.0f;

        [Header("FOV Dynamics")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float baseFOV = 60f;
        [SerializeField] private float boostFOV = 74f;

        private float currentDistance;
        private float targetDistance;
        private SerpentGrowth serpentGrowth;
        private SerpentController serpentController;
        private SerpentPhysicsFollower serpentFollower;

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
                    serpentFollower = controller.GetComponent<SerpentPhysicsFollower>();
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
                    baseDistance = Mathf.Clamp(baseDistance - (zoom * 2.5f), 6f, 40f);
                }
            }
        }

        private void CalculateDynamicFraming()
        {
            int segmentCount = (serpentGrowth != null) ? serpentGrowth.CurrentSegmentCount : 50;
            bool isBoosting = (serpentController != null) && serpentController.IsBoosting;

            // Logarithmic/exponential growth curve: gently expands for giant serpents without losing framing
            float lengthFactor = Mathf.Pow(segmentCount, 0.75f) * distancePowerScale;
            float boostFactor = isBoosting ? boostDistancePullback : 0f;

            targetDistance = Mathf.Clamp(baseDistance + lengthFactor + boostFactor, baseDistance, maxDistance);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * 3.0f);
        }

        private void UpdateCameraTransform()
        {
            // Compute blended focal point between head and serpent midpoint
            Vector3 focalPoint = target.position + targetOffset;
            if (serpentFollower != null && midpointFocusBias > 0.01f)
            {
                Vector3 spineMid = serpentFollower.GetSpineMidpoint() + targetOffset;
                focalPoint = Vector3.Lerp(focalPoint, spineMid, midpointFocusBias);
            }

            // Ideal orbit rotation
            Quaternion desiredRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

            // Compute ideal camera position behind focal point
            int count = (serpentGrowth != null) ? serpentGrowth.CurrentSegmentCount : 50;
            float dynamicHeight = baseHeight + (Mathf.Pow(count, 0.65f) * heightPowerScale);
            Vector3 offsetVector = desiredRotation * new Vector3(0f, dynamicHeight, -currentDistance);
            Vector3 desiredPosition = focalPoint + offsetVector;

            // Apply smooth jitter-free position damping
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followDamping);

            // Camera look rotation
            Quaternion targetLookRotation = Quaternion.LookRotation(focalPoint - transform.position, Vector3.up);
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
                serpentFollower = target.GetComponent<SerpentPhysicsFollower>();
            }
        }
    }
}
