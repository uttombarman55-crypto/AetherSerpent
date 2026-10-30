using UnityEngine;

namespace AetherSerpent.Environment
{
    /// <summary>
    /// Procedurally constructs the multi-biome celestial temple arena:
    /// - High-metallic reflective sanctuary dais
    /// - Reactive Monolithic pillars capable of being shattered by Titan slams & high-speed serpent collisions
    /// - Population of diverse prey ecosystems (Celestial Motes, Sprites, Obsidian Golems, Void Leeches, Ancient Memory Cores)
    /// </summary>
    public class CelestialArenaBuilder : MonoBehaviour
    {
        [Header("Arena Dimensions")]
        [SerializeField] private float arenaRadius = 60.0f;
        [SerializeField] private int pillarCount = 16;
        [SerializeField] private int preyPopulationPerType = 6;

        [Header("Colors & Materials")]
        [SerializeField] private Color daisColor = new Color(0.04f, 0.06f, 0.09f);
        [SerializeField] private Color runeColor = new Color(0.1f, 0.7f, 1.0f);

        private void Start()
        {
            BuildArena();
            SpawnBiomeWildlife();
        }

        private void BuildArena()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 1. Reflective Sanctuary Dais
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Celestial_Dais_Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(arenaRadius * 2f, 1f, arenaRadius * 2f);

            Material floorMat = new Material(litShader);
            floorMat.name = "Mat_SanctuaryFloor";
            floorMat.color = daisColor;
            floorMat.SetFloat("_Metallic", 0.85f);
            floorMat.SetFloat("_Smoothness", 0.88f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // 2. Perimeter Spires (Reactive Environment)
            Material runeMat = new Material(litShader);
            runeMat.name = "Mat_SpireRune";
            runeMat.color = runeColor;
            runeMat.EnableKeyword("_EMISSION");
            runeMat.SetColor("_EmissionColor", runeColor * 3.5f);

            for (int i = 0; i < pillarCount; i++)
            {
                float angle = (float)i / pillarCount * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * arenaRadius;
                float z = Mathf.Sin(angle) * arenaRadius;

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Sanctuary_Pillar_{i:00}";
                pillar.transform.SetParent(transform, false);
                pillar.transform.position = new Vector3(x, 10f, z);
                pillar.transform.localScale = new Vector3(3.2f, 20f, 3.2f);
                pillar.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                pillar.GetComponent<Renderer>().sharedMaterial = floorMat;

                // Add Reactive Environment component
                pillar.AddComponent<ReactiveEnvironment>();

                // Glowing Rune Crystal
                GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rune.name = "Rune_Crystal";
                rune.transform.SetParent(pillar.transform, false);
                rune.transform.localPosition = new Vector3(0f, 0.45f, 0.52f);
                rune.transform.localScale = new Vector3(0.35f, 0.75f, 0.1f);
                DestroyImmediate(rune.GetComponent<Collider>());
                rune.GetComponent<Renderer>().sharedMaterial = runeMat;
            }
        }

        private void SpawnBiomeWildlife()
        {
            // Quadrant 1 (x > 0, z > 0): Celestial Motes
            SpawnPreyCluster(PreyType.CelestialMote, 15f, 45f, 15f, 45f, preyPopulationPerType);

            // Quadrant 2 (x > 0, z <= 0): Obsidian Golems
            SpawnPreyCluster(PreyType.ObsidianGolem, 15f, 45f, -45f, -15f, preyPopulationPerType);

            // Quadrant 3 (x <= 0, z <= 0): Toxic Void Leeches
            SpawnPreyCluster(PreyType.ToxicVoidLeech, -45f, -15f, -45f, -15f, preyPopulationPerType);

            // Quadrant 4 (x <= 0, z > 0): Luminescent Sprites
            SpawnPreyCluster(PreyType.LuminescentSprite, -45f, -15f, 15f, 45f, preyPopulationPerType);

            // Center Ring: Ancient Memory Cores (Rare)
            SpawnPreyCluster(PreyType.AncientMemoryCore, -12f, 12f, -12f, 12f, 3);
        }

        private void SpawnPreyCluster(PreyType type, float minX, float maxX, float minZ, float maxZ, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = new Vector3(
                    Random.Range(minX, maxX),
                    Random.Range(2.5f, 14f),
                    Random.Range(minZ, maxZ)
                );

                GameObject preyObj = new GameObject($"Prey_{type}_{i:00}");
                preyObj.transform.SetParent(transform, false);
                preyObj.transform.position = pos;

                // Configure prey component
                var prey = preyObj.AddComponent<PreyEntity>();
                preyObj.SendMessage("EnsureVisuals", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
