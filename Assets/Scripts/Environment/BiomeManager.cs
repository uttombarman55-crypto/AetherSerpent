using System;
using UnityEngine;
using AetherSerpent.Serpent;

namespace AetherSerpent.Environment
{
    public enum BiomeType
    {
        CelestialSanctum,
        ObsidianCrag,
        AbyssalMire,
        VerdantCanopy
    }

    /// <summary>
    /// Manages 4 distinct atmospheric biomes within the celestial sanctuary:
    /// - Celestial Sanctum (Luminous Azure & Gold)
    /// - Obsidian Crag (Volcanic Basalt & Fiery Dust)
    /// - Abyssal Mire (Toxic Shadow Mist & Deep Violet)
    /// - Verdant Canopy (Radiant Emerald & Golden Dew)
    /// Smoothly transitions atmospheric fog, ambient lighting, and mood.
    /// </summary>
    public class BiomeManager : MonoBehaviour
    {
        public static BiomeManager Instance { get; private set; }

        [Header("Active Biome")]
        [SerializeField] private BiomeType currentBiome = BiomeType.CelestialSanctum;
        public BiomeType CurrentBiome => currentBiome;
        public string BiomeDisplayName { get; private set; } = "CELESTIAL SANCTUM";

        public event Action<BiomeType, string> OnBiomeChanged;

        private Transform playerHead;
        private Color targetFogColor;
        private float targetFogDensity = 0.012f;

        private void Awake()
        {
            Instance = this;
            UpdateBiomeAtmosphere(currentBiome, true);
        }

        private void Start()
        {
            var controller = FindFirstObjectByType<SerpentController>();
            if (controller != null) playerHead = controller.transform;
        }

        private void Update()
        {
            if (playerHead == null)
            {
                var controller = FindFirstObjectByType<SerpentController>();
                if (controller != null) playerHead = controller.transform;
                return;
            }

            DetectCurrentBiome(playerHead.position);

            // Smoothly blend atmospheric fog
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 1.5f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 1.5f);
        }

        private void DetectCurrentBiome(Vector3 pos)
        {
            BiomeType detected;

            if (pos.x >= 0f && pos.z >= 0f)
            {
                detected = BiomeType.CelestialSanctum;
            }
            else if (pos.x >= 0f && pos.z < 0f)
            {
                detected = BiomeType.ObsidianCrag;
            }
            else if (pos.x < 0f && pos.z < 0f)
            {
                detected = BiomeType.AbyssalMire;
            }
            else
            {
                detected = BiomeType.VerdantCanopy;
            }

            if (detected != currentBiome)
            {
                currentBiome = detected;
                UpdateBiomeAtmosphere(detected, false);
            }
        }

        private void UpdateBiomeAtmosphere(BiomeType biome, bool instant)
        {
            switch (biome)
            {
                case BiomeType.CelestialSanctum:
                    BiomeDisplayName = "CELESTIAL SANCTUM";
                    targetFogColor = new Color(0.04f, 0.09f, 0.18f);
                    targetFogDensity = 0.012f;
                    break;

                case BiomeType.ObsidianCrag:
                    BiomeDisplayName = "OBSIDIAN CRAG";
                    targetFogColor = new Color(0.12f, 0.05f, 0.02f);
                    targetFogDensity = 0.018f;
                    break;

                case BiomeType.AbyssalMire:
                    BiomeDisplayName = "ABYSSAL MIRE";
                    targetFogColor = new Color(0.08f, 0.02f, 0.14f);
                    targetFogDensity = 0.022f;
                    break;

                case BiomeType.VerdantCanopy:
                    BiomeDisplayName = "VERDANT CANOPY";
                    targetFogColor = new Color(0.03f, 0.10f, 0.07f);
                    targetFogDensity = 0.014f;
                    break;
            }

            if (instant)
            {
                RenderSettings.fogColor = targetFogColor;
                RenderSettings.fogDensity = targetFogDensity;
            }

            OnBiomeChanged?.Invoke(biome, BiomeDisplayName);
            Debug.Log($"[BiomeManager] Entered Biome: {BiomeDisplayName}");
        }
    }
}

