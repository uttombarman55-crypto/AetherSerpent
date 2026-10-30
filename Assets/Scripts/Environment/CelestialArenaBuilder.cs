using UnityEngine;

namespace AetherSerpent.Environment
{
    /// <summary>
    /// Procedurally constructs a celestial floating temple arena:
    /// - High-metallic reflective sanctuary dais
    /// - Monolithic perimeter spires demonstrating contact shadows & SSAO
    /// - Atmospheric volumetric light shafts
    /// - Initial scatter of Aether Orbs
    /// </summary>
    public class CelestialArenaBuilder : MonoBehaviour
    {
        [Header("Arena Dimensions")]
        [SerializeField] private float arenaRadius = 55.0f;
        [SerializeField] private int pillarCount = 14;
        [SerializeField] private int initialOrbCount = 20;

        [Header("Colors & Materials")]
        [SerializeField] private Color daisColor = new Color(0.04f, 0.06f, 0.09f);
        [SerializeField] private Color runeColor = new Color(0.1f, 0.7f, 1.0f);

        private void Start()
        {
            BuildArena();
        }

        private void BuildArena()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 1. Reflective Floor Dais
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

            // 2. Monolithic Spires around perimeter
            Material runeMat = new Material(litShader);
            runeMat.name = "Mat_SpireRune";
            runeMat.color = runeColor;
            runeMat.EnableKeyword("_EMISSION");
            runeMat.SetColor("_EmissionColor", runeColor * 3.0f);

            for (int i = 0; i < pillarCount; i++)
            {
                float angle = (float)i / pillarCount * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * arenaRadius;
                float z = Mathf.Sin(angle) * arenaRadius;

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Sanctuary_Pillar_{i:00}";
                pillar.transform.SetParent(transform, false);
                pillar.transform.position = new Vector3(x, 12f, z);
                pillar.transform.localScale = new Vector3(3.5f, 24f, 3.5f);
                pillar.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                pillar.GetComponent<Renderer>().sharedMaterial = floorMat;

                // Rune light crystal
                GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rune.name = "Rune_Crystal";
                rune.transform.SetParent(pillar.transform, false);
                rune.transform.localPosition = new Vector3(0f, 0.45f, 0.52f);
                rune.transform.localScale = new Vector3(0.3f, 0.7f, 0.1f);
                DestroyImmediate(rune.GetComponent<Collider>());
                rune.GetComponent<Renderer>().sharedMaterial = runeMat;
            }

            // 3. Scatter initial Aether Orbs
            for (int i = 0; i < initialOrbCount; i++)
            {
                Vector3 orbPos = new Vector3(
                    Random.Range(-arenaRadius * 0.75f, arenaRadius * 0.75f),
                    Random.Range(2.5f, 16.0f),
                    Random.Range(-arenaRadius * 0.75f, arenaRadius * 0.75f)
                );

                GameObject orbObj = new GameObject($"AetherOrb_{i:00}");
                orbObj.transform.SetParent(transform, false);
                orbObj.transform.position = orbPos;
                orbObj.AddComponent<AetherOrb>();
            }
        }
    }
}
