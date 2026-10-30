using UnityEngine;
using AetherSerpent.Core;
using AetherSerpent.Serpent;
using AetherSerpent.CameraSystem;

namespace AetherSerpent.Environment
{
    public enum PreyType
    {
        CelestialMote,      // Energy & Growth
        LuminescentSprite,  // Agile Wisp, Speed Buff + Celestial XP
        ObsidianGolem,      // Heavy Armor, Titan XP + Mass
        ToxicVoidLeech,     // Shadow Parasite, Abyssal XP + Venom
        AncientMemoryCore   // Narrative Flashback & Massive Evolution XP
    }

    /// <summary>
    /// Interactive Prey and Wildlife entity in the sanctuary.
    /// Supports wandering locomotion, evasive reactions, dynamic bioluminescence,
    /// and tailored nutritional / evolution rewards upon consumption.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PreyEntity : MonoBehaviour
    {
        [Header("Prey Configuration")]
        [SerializeField] private PreyType preyType = PreyType.CelestialMote;
        [SerializeField] private float nutrition = 25f;
        [SerializeField] private int growthSegments = 2;
        [SerializeField] private int scoreReward = 200;
        [SerializeField] private float evolutionXPReward = 35f;

        [Header("Locomotion & Evasion")]
        [SerializeField] private bool isMobile = true;
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float evasionRadius = 9.0f;
        [SerializeField] private float evasionSpeed = 8.0f;
        [SerializeField] private float wanderRadius = 30.0f;

        private Vector3 spawnOrigin;
        private Vector3 targetWanderPoint;
        private float wanderTimer = 0f;
        private Transform playerSerpentHead;
        private SphereCollider triggerCollider;

        private void Awake()
        {
            spawnOrigin = transform.position;
            triggerCollider = GetComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = 1.35f;

            PickNewWanderPoint();
            EnsureVisuals();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            var controller = FindFirstObjectByType<SerpentController>();
            if (controller != null) playerSerpentHead = controller.transform;
        }

        private void EnsureVisuals()
        {
            if (transform.childCount == 0)
            {
                GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                core.name = "Prey_VisualCore";
                core.transform.SetParent(transform, false);
                DestroyImmediate(core.GetComponent<Collider>());

                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");

                Material mat = new Material(litShader);
                Color glowCol;

                switch (preyType)
                {
                    case PreyType.LuminescentSprite:
                        glowCol = new Color(0.2f, 0.95f, 0.7f);
                        core.transform.localScale = Vector3.one * 0.85f;
                        break;
                    case PreyType.ObsidianGolem:
                        glowCol = new Color(1.0f, 0.45f, 0.1f);
                        core.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
                        break;
                    case PreyType.ToxicVoidLeech:
                        glowCol = new Color(0.85f, 0.15f, 0.95f);
                        core.transform.localScale = new Vector3(0.7f, 0.7f, 1.2f);
                        break;
                    case PreyType.AncientMemoryCore:
                        glowCol = new Color(1.0f, 0.9f, 0.3f);
                        core.transform.localScale = Vector3.one * 1.5f;
                        break;
                    default:
                        glowCol = new Color(0.15f, 0.85f, 1.0f);
                        core.transform.localScale = Vector3.one * 0.75f;
                        break;
                }

                mat.name = $"Mat_Prey_{preyType}";
                mat.color = glowCol;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", glowCol * 4.0f);
                core.GetComponent<Renderer>().sharedMaterial = mat;

                // Point light for deferred illumination
                GameObject lightObj = new GameObject("Prey_PointLight");
                lightObj.transform.SetParent(transform, false);
                Light pl = lightObj.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = glowCol;
                pl.intensity = 2.8f;
                pl.range = 7.5f;
            }
        }

        private void Update()
        {
            if (playerSerpentHead == null) FindPlayer();

            if (isMobile)
            {
                ProcessMovement();
            }
            else
            {
                // Gentle floating bob
                transform.position = spawnOrigin + (Vector3.up * Mathf.Sin(Time.time * 2.0f) * 0.4f);
            }
        }

        private void ProcessMovement()
        {
            Vector3 currentPos = transform.position;

            // Check if player is close for evasion
            if (playerSerpentHead != null)
            {
                float distToHead = Vector3.Distance(currentPos, playerSerpentHead.position);
                if (distToHead < evasionRadius)
                {
                    Vector3 fleeDir = (currentPos - playerSerpentHead.position).normalized;
                    transform.position += fleeDir * (evasionSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fleeDir), Time.deltaTime * 8f);
                    return;
                }
            }

            // Normal calm wandering
            wanderTimer += Time.deltaTime;
            if (wanderTimer > 4.5f || Vector3.Distance(currentPos, targetWanderPoint) < 1.5f)
            {
                PickNewWanderPoint();
            }

            Vector3 moveDir = (targetWanderPoint - currentPos).normalized;
            transform.position += moveDir * (moveSpeed * Time.deltaTime);
            if (moveDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 4f);
            }
        }

        private void PickNewWanderPoint()
        {
            wanderTimer = 0f;
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            targetWanderPoint = spawnOrigin + new Vector3(circle.x, Random.Range(-2f, 6f), circle.y);
        }

        private void OnTriggerEnter(Collider other)
        {
            var growth = other.GetComponentInParent<SerpentGrowth>();
            if (growth != null)
            {
                Consume(growth);
            }
        }

        private void Consume(SerpentGrowth growth)
        {
            // 1. Replenish hunger & vitals
            if (SerpentVitals.Instance != null)
            {
                SerpentVitals.Instance.Feed(nutrition, 20f);
            }

            // 2. Growth & Digestion queue
            growth.AddSegments(growthSegments);

            // 3. Award Score
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreReward);
            }

            // 4. Award Evolution XP
            var evolution = growth.GetComponent<SerpentEvolution>();
            if (evolution != null)
            {
                evolution.AwardEssence(preyType, evolutionXPReward);
            }

            // 5. Special prey traits & Story triggers
            if (preyType == PreyType.AncientMemoryCore)
            {
                TriggerNarrativeMemory();
            }

            // 6. Camera feedback
            if (CameraShakeManager.Instance != null)
            {
                float trauma = (preyType == PreyType.AncientMemoryCore) ? 0.45f : 0.22f;
                CameraShakeManager.Instance.AddTrauma(trauma);
            }

            // Respawn prey elsewhere in the sanctuary
            Respawn();
        }

        private void TriggerNarrativeMemory()
        {
            if (StoryManager.Instance != null)
            {
                string[] memoryTitles = {
                    "MEMORY: THE ANCIENT TECTONICS",
                    "MEMORY: THE SOLAR ASCENT",
                    "MEMORY: THE SHADOW TRENCH",
                    "MEMORY: THE PRIMEVAL SEED"
                };

                string[] memoryTexts = {
                    "The world shattered under the weight of the First Titan.\nMountains bowed where its armored coils pressed.",
                    "Wings of woven starlight carried the Celestial across the void.\nNo boundary could bind its radiant flight.",
                    "In the crushing darkness below all light, the Abyssal serpent forged its venom.\nTo conquer the abyss, one must become the abyss.",
                    "A fragment of the eternal lattice stirs within your veins.\nThe metamorphosis draws near."
                };

                int index = Random.Range(0, memoryTitles.Length);
                StoryManager.Instance.TriggerMemory(memoryTitles[index], memoryTexts[index], 5.5f);
            }
        }

        private void Respawn()
        {
            Vector3 randomPos = new Vector3(
                Random.Range(-50f, 50f),
                Random.Range(3f, 18f),
                Random.Range(-50f, 50f)
            );

            spawnOrigin = randomPos;
            transform.position = randomPos;
            PickNewWanderPoint();
        }
    }
}
