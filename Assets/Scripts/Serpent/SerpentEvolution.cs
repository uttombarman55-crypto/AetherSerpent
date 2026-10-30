using System;
using System.Collections;
using UnityEngine;
using AetherSerpent.Core;
using AetherSerpent.CameraSystem;
using AetherSerpent.Environment;
using AetherSerpent.Input;

namespace AetherSerpent.Serpent
{
    public enum EvolutionPath
    {
        Primal,     // Base starting form
        Titan,      // Massive, Armored, Earthquake slams
        Celestial,  // Winged, Luminous, Solar bursts & Flight
        Abyssal     // Multi-crested, Toxic, Shadow warp & Miasma
    }

    /// <summary>
    /// Master Evolution System for the Serpent.
    /// Manages:
    /// - Evolution essence accumulation (Titan, Celestial, Abyssal)
    /// - Cinematic Transformation Sequence (Time Dilation, VFX Flares, Camera Orbit)
    /// - Anatomical Metamorphosis (Materials, Geometry, Scale, Wings, Horns)
    /// - Active Class Abilities (Seismic Slam, Solar Burst, Void Warp)
    /// </summary>
    [RequireComponent(typeof(SerpentController))]
    public class SerpentEvolution : MonoBehaviour
    {
        public static SerpentEvolution Instance { get; private set; }

        [Header("Current Form")]
        [SerializeField] private EvolutionPath currentPath = EvolutionPath.Primal;
        public EvolutionPath CurrentPath => currentPath;

        [Header("Evolution Essence")]
        [SerializeField] private float titanEssence = 0f;
        [SerializeField] private float celestialEssence = 0f;
        [SerializeField] private float abyssalEssence = 0f;
        [SerializeField] private float essenceThresholdForEvolution = 100f;

        public float TitanEssence => titanEssence;
        public float CelestialEssence => celestialEssence;
        public float AbyssalEssence => abyssalEssence;
        public float EssenceThreshold => essenceThresholdForEvolution;

        [Header("Ability Cooldowns")]
        [SerializeField] private float abilityCooldown = 6.0f;
        private float lastAbilityTime = -10f;
        public float AbilityCooldownNormalized => Mathf.Clamp01((Time.time - lastAbilityTime) / abilityCooldown);
        public bool IsAbilityReady => Time.time >= lastAbilityTime + abilityCooldown;

        public event Action<EvolutionPath> OnEvolutionCompleted;
        public event Action<float, float, float> OnEssenceChanged;
        public event Action OnAbilityTriggered;

        private SerpentController controller;
        private SerpentGrowth growth;
        private ProceduralSerpentGeometry geometryBuilder;

        // Visual Transformation Attachment References
        private GameObject wingsRoot;
        private GameObject titanPlatingRoot;
        private GameObject abyssalCrestRoot;

        private void Awake()
        {
            Instance = this;
            controller = GetComponent<SerpentController>();
            growth = GetComponent<SerpentGrowth>();
            geometryBuilder = GetComponent<ProceduralSerpentGeometry>();
        }

        private void Update()
        {
            CheckAbilityInput();
        }

        private void CheckAbilityInput()
        {
            // Activate special ability on E or Gamepad West
            bool triggerAbility = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (kb != null && (kb.qKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)) triggerAbility = true;
            if (pad != null && pad.buttonWest.wasPressedThisFrame) triggerAbility = true;
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.R)) triggerAbility = true;
#endif

            if (triggerAbility && IsAbilityReady && currentPath != EvolutionPath.Primal)
            {
                ExecutePathAbility();
            }
        }

        public void AwardEssence(PreyType preyType, float amount)
        {
            switch (preyType)
            {
                case PreyType.ObsidianGolem:
                    titanEssence += amount;
                    break;
                case PreyType.LuminescentSprite:
                case PreyType.CelestialMote:
                    celestialEssence += amount;
                    break;
                case PreyType.ToxicVoidLeech:
                    abyssalEssence += amount;
                    break;
                case PreyType.AncientMemoryCore:
                    titanEssence += amount * 0.5f;
                    celestialEssence += amount * 0.5f;
                    abyssalEssence += amount * 0.5f;
                    break;
            }

            OnEssenceChanged?.Invoke(titanEssence, celestialEssence, abyssalEssence);

            // Check if ready for evolution prompt
            if (currentPath == EvolutionPath.Primal)
            {
                float maxEssence = Mathf.Max(titanEssence, celestialEssence, abyssalEssence);
                if (maxEssence >= essenceThresholdForEvolution)
                {
                    var ui = FindFirstObjectByType<UI.EvolutionSelectionUI>();
                    if (ui != null && !ui.IsOpen)
                    {
                        ui.OpenEvolutionPrompt();
                    }
                }
            }
        }

        public void Evolve(EvolutionPath newPath)
        {
            if (currentPath == newPath) return;
            StartCoroutine(ExecuteCinematicEvolution(newPath));
        }

        private IEnumerator ExecuteCinematicEvolution(EvolutionPath newPath)
        {
            Debug.Log($"[SerpentEvolution] Initiating cinematic transformation into: {newPath}");

            // 1. Slow-motion time dilation
            Time.timeScale = 0.18f;

            // 2. Camera trauma shake & flash
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.AddTrauma(0.85f);
            }

            // 3. Transformation VFX flash
            GameObject flashObj = new GameObject("Evolution_Flash");
            flashObj.transform.position = transform.position;
            Light flashLight = flashObj.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 35f;
            flashLight.intensity = 15f;
            Color flashCol = (newPath == EvolutionPath.Titan) ? new Color(1.0f, 0.45f, 0.1f) :
                             (newPath == EvolutionPath.Celestial) ? new Color(0.3f, 0.9f, 1.0f) :
                             new Color(0.85f, 0.15f, 0.95f);
            flashLight.color = flashCol;

            yield return new WaitForSecondsRealtime(1.2f);

            // 4. Morph anatomy and visuals
            currentPath = newPath;
            ApplyMorphology(newPath);

            // 5. Restore time scale with smooth easing
            float timer = 0f;
            while (timer < 0.6f)
            {
                timer += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(0.18f, 1.0f, timer / 0.6f);
                yield return null;
            }
            Time.timeScale = 1.0f;
            Destroy(flashObj);

            OnEvolutionCompleted?.Invoke(newPath);

            // Story milestone
            if (StoryManager.Instance != null)
            {
                StoryManager.Instance.UnlockMilestone($"EVOLVED_{newPath.ToString().ToUpper()}");
                StoryManager.Instance.TriggerMemory(
                    $"ASCENDANCE: THE {newPath.ToString().ToUpper()}",
                    $"The metamorphosis is complete. You bear the mark of the {newPath}.\nUnleash your primordial power with [Q / R / Gamepad X].",
                    6.0f
                );
            }
        }

        private void ApplyMorphology(EvolutionPath path)
        {
            // Remove previous morph roots
            if (wingsRoot != null) Destroy(wingsRoot);
            if (titanPlatingRoot != null) Destroy(titanPlatingRoot);
            if (abyssalCrestRoot != null) Destroy(abyssalCrestRoot);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            switch (path)
            {
                case EvolutionPath.Titan:
                    // Massive armored basalt plates and heavy scales
                    titanPlatingRoot = new GameObject("Morph_TitanPlates");
                    titanPlatingRoot.transform.SetParent(transform, false);

                    GameObject armor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    armor.transform.SetParent(titanPlatingRoot.transform, false);
                    armor.transform.localPosition = new Vector3(0f, 0.5f, 0.2f);
                    armor.transform.localScale = new Vector3(1.8f, 0.6f, 2.2f);
                    DestroyImmediate(armor.GetComponent<Collider>());

                    Material titanMat = new Material(litShader);
                    titanMat.color = new Color(0.12f, 0.1f, 0.08f);
                    titanMat.SetFloat("_Metallic", 0.85f);
                    titanMat.SetFloat("_Smoothness", 0.45f);
                    armor.GetComponent<Renderer>().sharedMaterial = titanMat;

                    transform.localScale = Vector3.one * 1.35f; // Titan is massive
                    break;

                case EvolutionPath.Celestial:
                    // Luminous starlight wings
                    wingsRoot = new GameObject("Morph_CelestialWings");
                    wingsRoot.transform.SetParent(transform, false);

                    CreateCelestialWing(wingsRoot.transform, new Vector3(-1.6f, 0.4f, 0.3f), Quaternion.Euler(0f, 25f, -15f), litShader);
                    CreateCelestialWing(wingsRoot.transform, new Vector3(1.6f, 0.4f, 0.3f), Quaternion.Euler(0f, -25f, 15f), litShader);

                    transform.localScale = Vector3.one * 1.1f;
                    break;

                case EvolutionPath.Abyssal:
                    // Multi-horned deep violet shadow crest
                    abyssalCrestRoot = new GameObject("Morph_AbyssalCrest");
                    abyssalCrestRoot.transform.SetParent(transform, false);

                    CreateAbyssalSpike(abyssalCrestRoot.transform, new Vector3(-0.6f, 0.7f, 0.2f), Quaternion.Euler(-40f, -25f, 20f), litShader);
                    CreateAbyssalSpike(abyssalCrestRoot.transform, new Vector3(0.6f, 0.7f, 0.2f), Quaternion.Euler(-40f, 25f, -20f), litShader);
                    CreateAbyssalSpike(abyssalCrestRoot.transform, new Vector3(0f, 0.85f, -0.4f), Quaternion.Euler(-50f, 0f, 0f), litShader);

                    transform.localScale = Vector3.one * 1.05f;
                    break;
            }
        }

        private void CreateCelestialWing(Transform parent, Vector3 localPos, Quaternion localRot, Shader shader)
        {
            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = "Celestial_Wing";
            wing.transform.SetParent(parent, false);
            wing.transform.localPosition = localPos;
            wing.transform.localRotation = localRot;
            wing.transform.localScale = new Vector3(2.4f, 0.08f, 1.2f);
            DestroyImmediate(wing.GetComponent<Collider>());

            Material wingMat = new Material(shader);
            Color glowGold = new Color(1.0f, 0.85f, 0.35f);
            wingMat.color = glowGold;
            wingMat.EnableKeyword("_EMISSION");
            wingMat.SetColor("_EmissionColor", glowGold * 4.0f);
            wing.GetComponent<Renderer>().sharedMaterial = wingMat;
        }

        private void CreateAbyssalSpike(Transform parent, Vector3 localPos, Quaternion localRot, Shader shader)
        {
            GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spike.name = "Abyssal_Spike";
            spike.transform.SetParent(parent, false);
            spike.transform.localPosition = localPos;
            spike.transform.localRotation = localRot;
            spike.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
            DestroyImmediate(spike.GetComponent<Collider>());

            Material spikeMat = new Material(shader);
            Color voidPurple = new Color(0.85f, 0.12f, 1.0f);
            spikeMat.color = voidPurple;
            spikeMat.EnableKeyword("_EMISSION");
            spikeMat.SetColor("_EmissionColor", voidPurple * 4.5f);
            spike.GetComponent<Renderer>().sharedMaterial = spikeMat;
        }

        public void ExecutePathAbility()
        {
            lastAbilityTime = Time.time;
            OnAbilityTriggered?.Invoke();

            switch (currentPath)
            {
                case EvolutionPath.Titan:
                    // Seismic Slam: Massive ground shockwave, shatters nearby reactive environment
                    Debug.Log("[SerpentEvolution] TITAN SEISMIC SLAM ACTIVATED!");
                    if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.AddTrauma(0.8f);

                    // Blast surrounding reactive rocks and prey
                    Collider[] colliders = Physics.OverlapSphere(transform.position, 18.0f);
                    foreach (var col in colliders)
                    {
                        var reactive = col.GetComponentInParent<ReactiveEnvironment>();
                        if (reactive != null) reactive.TriggerFracture(transform.position);
                    }
                    break;

                case EvolutionPath.Celestial:
                    // Solar Burst: Blinding light pulse and instantaneous vertical glide
                    Debug.Log("[SerpentEvolution] CELESTIAL SOLAR BURST ACTIVATED!");
                    if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.AddTrauma(0.35f);

                    // Surge speed & vertical altitude
                    if (controller != null)
                    {
                        transform.position += Vector3.up * 6.5f;
                    }
                    if (SerpentVitals.Instance != null)
                    {
                        SerpentVitals.Instance.Heal(30f); // Restores health
                    }
                    break;

                case EvolutionPath.Abyssal:
                    // Void Warp: Teleports forward instantaneously leaving shadow miasma
                    Debug.Log("[SerpentEvolution] ABYSSAL VOID WARP ACTIVATED!");
                    if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.AddTrauma(0.4f);

                    transform.position += transform.forward * 16.0f;
                    break;
            }
        }
    }
}
