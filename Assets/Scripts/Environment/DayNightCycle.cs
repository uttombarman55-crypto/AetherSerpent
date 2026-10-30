using UnityEngine;

namespace AetherSerpent.Environment
{
    /// <summary>
    /// Governs celestial day-night orbital progression.
    /// Manages:
    /// - Continuous sun & moon rotation
    /// - PBR light intensity and color temperature transitions
    /// - Time of day readout (Dawn, Midday, Dusk, Midnight)
    /// - Atmosphere & fog color synchronization
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Cycle Configuration")]
        [SerializeField] private float dayDurationInSeconds = 120.0f; // 2 minutes full cycle
        [Range(0f, 1f)]
        [SerializeField] private float timeOfDay = 0.25f; // 0 = Midnight, 0.25 = Dawn, 0.5 = Noon, 0.75 = Dusk

        [Header("Light References")]
        [SerializeField] private Light sunLight;

        [Header("Color Gradients")]
        [SerializeField] private Gradient sunColorGradient;
        [SerializeField] private AnimationCurve sunIntensityCurve;

        public float NormalizedTime => timeOfDay;
        public bool IsDaytime => timeOfDay >= 0.22f && timeOfDay <= 0.78f;

        public string TimeDescription
        {
            get
            {
                if (timeOfDay >= 0.2f && timeOfDay < 0.35f) return "DAWN";
                if (timeOfDay >= 0.35f && timeOfDay < 0.65f) return "MIDDAY";
                if (timeOfDay >= 0.65f && timeOfDay < 0.8f) return "DUSK";
                return "MIDNIGHT";
            }
        }

        private void Awake()
        {
            Instance = this;
            if (sunLight == null)
            {
                sunLight = RenderSettings.sun;
                if (sunLight == null) sunLight = FindFirstObjectByType<Light>();
            }

            SetupDefaultGradients();
        }

        private void SetupDefaultGradients()
        {
            if (sunColorGradient == null || sunColorGradient.colorKeys.Length == 0)
            {
                sunColorGradient = new Gradient();

                GradientColorKey[] colors = new GradientColorKey[4];
                colors[0] = new GradientColorKey(new Color(0.15f, 0.2f, 0.45f), 0.0f); // Night
                colors[1] = new GradientColorKey(new Color(1.0f, 0.6f, 0.35f), 0.25f); // Dawn
                colors[2] = new GradientColorKey(new Color(0.95f, 0.98f, 1.0f), 0.5f);  // Noon
                colors[3] = new GradientColorKey(new Color(1.0f, 0.35f, 0.25f), 0.75f); // Dusk

                GradientAlphaKey[] alphas = new GradientAlphaKey[2];
                alphas[0] = new GradientAlphaKey(1.0f, 0.0f);
                alphas[1] = new GradientAlphaKey(1.0f, 1.0f);

                sunColorGradient.SetKeys(colors, alphas);
            }

            if (sunIntensityCurve == null || sunIntensityCurve.length == 0)
            {
                sunIntensityCurve = new AnimationCurve(
                    new Keyframe(0f, 0.15f),
                    new Keyframe(0.25f, 1.0f),
                    new Keyframe(0.5f, 1.6f),
                    new Keyframe(0.75f, 1.0f),
                    new Keyframe(1.0f, 0.15f)
                );
            }
        }

        private void Update()
        {
            timeOfDay += (Time.deltaTime / dayDurationInSeconds);
            if (timeOfDay >= 1.0f) timeOfDay -= 1.0f;

            UpdateLighting();
        }

        private void UpdateLighting()
        {
            // Rotate sun across sky: 360 degrees
            float sunAngle = (timeOfDay * 360f) - 90f;
            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
                sunLight.color = sunColorGradient.Evaluate(timeOfDay);
                sunLight.intensity = sunIntensityCurve.Evaluate(timeOfDay);
            }

            // Sync atmospheric ambient color
            RenderSettings.ambientSkyColor = Color.Lerp(
                new Color(0.04f, 0.06f, 0.15f),
                new Color(0.25f, 0.35f, 0.55f),
                sunIntensityCurve.Evaluate(timeOfDay) / 1.6f
            );
        }
    }
}

