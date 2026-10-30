using UnityEngine;

namespace AetherSerpent.CameraSystem
{
    /// <summary>
    /// AAA 6-DOF Perlin Trauma Camera Shake System.
    /// Uses non-linear trauma decay (Trauma^2 / Trauma^3) for visceral, natural camera impact.
    /// </summary>
    public class CameraShakeManager : MonoBehaviour
    {
        public static CameraShakeManager Instance { get; private set; }

        [Header("Trauma Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float currentTrauma = 0f;
        [SerializeField] private float traumaDecaySpeed = 1.3f;

        [Header("Max Displacements")]
        [SerializeField] private float maxTranslation = 0.45f;
        [SerializeField] private float maxRotation = 3.5f;

        [Header("Frequency")]
        [SerializeField] private float frequency = 22.0f;

        private float timeSeed;

        public Vector3 ShakeOffsetPosition { get; private set; }
        public Vector3 ShakeOffsetRotation { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            timeSeed = Random.value * 100f;
        }

        private void Update()
        {
            if (currentTrauma <= 0.001f)
            {
                currentTrauma = 0f;
                ShakeOffsetPosition = Vector3.zero;
                ShakeOffsetRotation = Vector3.zero;
                return;
            }

            // Trauma decay
            currentTrauma = Mathf.Clamp01(currentTrauma - (traumaDecaySpeed * Time.deltaTime));

            // Non-linear shake intensity curve
            float shakeIntensity = currentTrauma * currentTrauma;
            float t = (Time.time * frequency) + timeSeed;

            // 3-axis positional shake
            float offsetX = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * maxTranslation * shakeIntensity;
            float offsetY = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * maxTranslation * shakeIntensity;
            float offsetZ = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * (maxTranslation * 0.5f) * shakeIntensity;

            // 3-axis rotational shake (pitch, yaw, roll)
            float rotPitch = (Mathf.PerlinNoise(t + 10f, 0f) - 0.5f) * 2f * maxRotation * shakeIntensity;
            float rotYaw   = (Mathf.PerlinNoise(0f, t + 20f) - 0.5f) * 2f * maxRotation * shakeIntensity;
            float rotRoll  = (Mathf.PerlinNoise(t + 30f, t + 40f) - 0.5f) * 2f * maxRotation * shakeIntensity;

            ShakeOffsetPosition = new Vector3(offsetX, offsetY, offsetZ);
            ShakeOffsetRotation = new Vector3(rotPitch, rotYaw, rotRoll);
        }

        /// <summary>
        /// Adds camera trauma (0.0 to 1.0).
        /// </summary>
        public void AddTrauma(float amount)
        {
            currentTrauma = Mathf.Clamp01(currentTrauma + amount);
        }

        public void Shake(float amount, float duration = 0.5f)
        {
            traumaDecaySpeed = 1f / Mathf.Max(duration, 0.05f);
            AddTrauma(amount);
        }
    }
}

