using UnityEngine;
using AetherSerpent.Core;
using AetherSerpent.Serpent;
using AetherSerpent.CameraSystem;

namespace AetherSerpent.Environment
{
    /// <summary>
    /// Bioluminescent Aether Energy Orb collectible.
    /// Features gentle floating bobbing, pulsating emission,
    /// collection detection, camera impulse feedback, and respawning.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class AetherOrb : MonoBehaviour
    {
        [Header("Oscillation Settings")]
        [SerializeField] private float bobAmplitude = 0.35f;
        [SerializeField] private float bobFrequency = 2.0f;
        [SerializeField] private float rotationSpeed = 45.0f;

        [Header("Value")]
        [SerializeField] private int scoreValue = 100;
        [SerializeField] private int growthSegments = 1;

        private Vector3 initialPosition;
        private float seed;
        private SphereCollider sphereCollider;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 1.2f;

            initialPosition = transform.position;
            seed = Random.value * 50f;

            EnsureVisuals();
        }

        private void EnsureVisuals()
        {
            if (transform.childCount == 0)
            {
                // Create glowing inner sphere
                GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                inner.name = "Orb_Core";
                inner.transform.SetParent(transform, false);
                inner.transform.localScale = Vector3.one * 0.7f;
                DestroyImmediate(inner.GetComponent<Collider>());

                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");

                Material mat = new Material(litShader);
                mat.name = "Mat_AetherOrb";
                Color glowCol = new Color(0.2f, 0.9f, 1.0f);
                mat.color = glowCol;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", glowCol * 4.0f);

                inner.GetComponent<Renderer>().sharedMaterial = mat;

                // Add point light for realistic deferred illumination
                GameObject lightObj = new GameObject("Orb_PointLight");
                lightObj.transform.SetParent(transform, false);
                Light pLight = lightObj.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = glowCol;
                pLight.intensity = 2.5f;
                pLight.range = 8.0f;
            }
        }

        private void Update()
        {
            float newY = initialPosition.y + (Mathf.Sin((Time.time * bobFrequency) + seed) * bobAmplitude);
            transform.position = new Vector3(initialPosition.x, newY, initialPosition.z);
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if head of serpent touched orb
            var growth = other.GetComponentInParent<SerpentGrowth>();
            if (growth != null)
            {
                OnCollected(growth);
            }
        }

        private void OnCollected(SerpentGrowth growth)
        {
            growth.AddSegments(growthSegments);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreValue);
            }

            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.AddTrauma(0.25f);
            }

            // Respawn at a random coordinate in the arena
            Vector3 randomPos = new Vector3(
                Random.Range(-45f, 45f),
                Random.Range(2f, 18f),
                Random.Range(-45f, 45f)
            );

            initialPosition = randomPos;
            transform.position = randomPos;
        }
    }
}
