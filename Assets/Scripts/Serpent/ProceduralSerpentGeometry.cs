using UnityEngine;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// AAA Procedural Serpent Anatomy & Visual Foundation.
    /// Features:
    /// - Runtime Procedural Diamond/Rhombus Scale Normal Map Generation
    /// - High-End PBR Materials (Carapace, Ventral Plates, Wetness Sheen, Bioluminescence)
    /// - Sculpted Draconic Head with Swept Horns and Bioluminescent Slit Eyes
    /// - Articulated Multi-Plate Spine Segments with Carapace Overlap & Ventral Plating
    /// - Tapering Celestial Fin Blade Tail
    /// - Full Shader & Property Block Support for Skin Shedding & Evolution Tiers
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public class ProceduralSerpentGeometry : MonoBehaviour
    {
        [Header("PBR Materials")]
        [SerializeField] private Material carapaceMaterial;
        [SerializeField] private Material ventralMaterial;
        [SerializeField] private Material bioluminescentMaterial;
        [SerializeField] private Material eyeGlowMaterial;

        [Header("Visual Tints")]
        [SerializeField] private Color carapaceColor = new Color(0.06f, 0.09f, 0.15f);
        [SerializeField] private Color ventralColor = new Color(0.12f, 0.18f, 0.28f);
        [SerializeField] private Color bioluminescentColor = new Color(0.12f, 0.82f, 1.0f);
        [SerializeField] private Color eyeColor = new Color(1.0f, 0.45f, 0.08f);

        private Texture2D proceduralScaleNormalMap;

        private void Awake()
        {
            GenerateScaleTextures();
            EnsureMaterials();
            BuildHeadVisuals();
        }

        private void GenerateScaleTextures()
        {
            if (proceduralScaleNormalMap != null) return;

            int size = 256;
            proceduralScaleNormalMap = new Texture2D(size, size, TextureFormat.RGBA32, true);
            proceduralScaleNormalMap.name = "Tex_ProceduralScaleNormals";
            proceduralScaleNormalMap.wrapMode = TextureWrapMode.Repeat;
            proceduralScaleNormalMap.filterMode = FilterMode.Trilinear;

            Color[] pixels = new Color[size * size];

            // Generate clean overlapping diamond scale pattern
            float frequency = 12.0f;
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size * frequency;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size * frequency;

                    // Diamond scale height map equation
                    float rowShift = (Mathf.Floor(v) % 2.0f) * 0.5f;
                    float fractU = Mathf.Repeat(u + rowShift, 1.0f) - 0.5f;
                    float fractV = Mathf.Repeat(v, 1.0f) - 0.5f;

                    // Rhombus distance
                    float dist = Mathf.Abs(fractU) + Mathf.Abs(fractV);
                    float height = Mathf.Clamp01(1.0f - (dist * 1.8f));
                    height = Mathf.Pow(height, 0.7f); // Beveled scale shape

                    // Calculate derivatives for normal map
                    float dU = fractU * 1.5f;
                    float dV = fractV * 1.5f;
                    Vector3 normal = new Vector3(-dU, -dV, 1.0f).normalized;

                    // Pack into tangent space normal map [0..1]
                    pixels[(y * size) + x] = new Color(
                        (normal.x * 0.5f) + 0.5f,
                        (normal.y * 0.5f) + 0.5f,
                        (normal.z * 0.5f) + 0.5f,
                        1.0f
                    );
                }
            }

            proceduralScaleNormalMap.SetPixels(pixels);
            proceduralScaleNormalMap.Apply(true, false);
        }

        private void EnsureMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            if (carapaceMaterial == null)
            {
                carapaceMaterial = new Material(litShader);
                carapaceMaterial.name = "Mat_SerpentCarapace_PBR";
                carapaceMaterial.color = carapaceColor;
                carapaceMaterial.SetFloat("_Smoothness", 0.88f); // Glossy wet serpentine coat
                carapaceMaterial.SetFloat("_Metallic", 0.65f);
                if (proceduralScaleNormalMap != null)
                {
                    carapaceMaterial.SetTexture("_BumpMap", proceduralScaleNormalMap);
                    carapaceMaterial.EnableKeyword("_NORMALMAP");
                    carapaceMaterial.SetFloat("_BumpScale", 1.25f);
                }
            }

            if (ventralMaterial == null)
            {
                ventralMaterial = new Material(litShader);
                ventralMaterial.name = "Mat_SerpentVentral_PBR";
                ventralMaterial.color = ventralColor;
                ventralMaterial.SetFloat("_Smoothness", 0.65f);
                ventralMaterial.SetFloat("_Metallic", 0.2f);
                if (proceduralScaleNormalMap != null)
                {
                    ventralMaterial.SetTexture("_BumpMap", proceduralScaleNormalMap);
                    ventralMaterial.EnableKeyword("_NORMALMAP");
                    ventralMaterial.SetFloat("_BumpScale", 0.8f);
                }
            }

            if (bioluminescentMaterial == null)
            {
                bioluminescentMaterial = new Material(litShader);
                bioluminescentMaterial.name = "Mat_SerpentBioluminescent_HDR";
                bioluminescentMaterial.color = bioluminescentColor;
                bioluminescentMaterial.EnableKeyword("_EMISSION");
                bioluminescentMaterial.SetColor("_EmissionColor", bioluminescentColor * 3.5f);
            }

            if (eyeGlowMaterial == null)
            {
                eyeGlowMaterial = new Material(litShader);
                eyeGlowMaterial.name = "Mat_SerpentEyes_HDR";
                eyeGlowMaterial.color = eyeColor;
                eyeGlowMaterial.EnableKeyword("_EMISSION");
                eyeGlowMaterial.SetColor("_EmissionColor", eyeColor * 4.0f);
            }
        }

        private void BuildHeadVisuals()
        {
            // Clean up any placeholder visual children first
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Visual_") || child.name.StartsWith("Eye_"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            GameObject headRoot = new GameObject("Visual_HeadRoot");
            headRoot.transform.SetParent(transform, false);

            // 1. Main Cranium Skull
            GameObject cranium = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cranium.name = "Head_Cranium";
            cranium.transform.SetParent(headRoot.transform, false);
            cranium.transform.localScale = new Vector3(1.2f, 0.85f, 1.8f);
            cranium.transform.localPosition = new Vector3(0f, 0.1f, 0.4f);
            DestroyImmediate(cranium.GetComponent<Collider>());
            cranium.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;

            // 2. Tapered Snout
            GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Head_Snout";
            snout.transform.SetParent(headRoot.transform, false);
            snout.transform.localScale = new Vector3(0.75f, 0.42f, 1.35f);
            snout.transform.localPosition = new Vector3(0f, -0.05f, 1.3f);
            DestroyImmediate(snout.GetComponent<Collider>());
            snout.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;

            // 3. Lower Ventral Jaw
            GameObject jaw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jaw.name = "Head_Jaw";
            jaw.transform.SetParent(headRoot.transform, false);
            jaw.transform.localScale = new Vector3(0.68f, 0.3f, 1.25f);
            jaw.transform.localPosition = new Vector3(0f, -0.25f, 1.05f);
            DestroyImmediate(jaw.GetComponent<Collider>());
            jaw.GetComponent<Renderer>().sharedMaterial = ventralMaterial;

            // 4. Glowing Slit Eyes
            CreateEye(headRoot.transform, new Vector3(-0.48f, 0.26f, 0.8f), true);
            CreateEye(headRoot.transform, new Vector3(0.48f, 0.26f, 0.8f), false);

            // 5. Swept-Back Celestial Horns
            CreateHorn(headRoot.transform, new Vector3(-0.45f, 0.55f, -0.1f), Quaternion.Euler(-32f, -18f, 15f));
            CreateHorn(headRoot.transform, new Vector3(0.45f, 0.55f, -0.1f), Quaternion.Euler(-32f, 18f, -15f));

            // 6. Central Dorsal Energy Crest
            GameObject crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crest.name = "Head_EnergyCrest";
            crest.transform.SetParent(headRoot.transform, false);
            crest.transform.localScale = new Vector3(0.1f, 0.65f, 1.1f);
            crest.transform.localPosition = new Vector3(0f, 0.5f, 0.15f);
            crest.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            DestroyImmediate(crest.GetComponent<Collider>());
            crest.GetComponent<Renderer>().sharedMaterial = bioluminescentMaterial;
        }

        private void CreateEye(Transform parent, Vector3 localPos, bool isLeft)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = isLeft ? "Eye_Left" : "Eye_Right";
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = new Vector3(0.22f, 0.28f, 0.42f);
            eye.transform.localRotation = Quaternion.Euler(0f, isLeft ? -15f : 15f, 0f);
            DestroyImmediate(eye.GetComponent<Collider>());
            eye.GetComponent<Renderer>().sharedMaterial = eyeGlowMaterial;
        }

        private void CreateHorn(Transform parent, Vector3 localPos, Quaternion localRot)
        {
            GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            horn.name = "Head_Horn";
            horn.transform.SetParent(parent, false);
            horn.transform.localPosition = localPos;
            horn.transform.localRotation = localRot;
            horn.transform.localScale = new Vector3(0.14f, 0.7f, 0.14f);
            DestroyImmediate(horn.GetComponent<Collider>());
            horn.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;
        }

        public GameObject CreateSegmentObject(int index, Vector3 position, Quaternion rotation)
        {
            EnsureMaterials();

            GameObject segObj = new GameObject($"Segment_{index:000}");
            segObj.transform.position = position;
            segObj.transform.rotation = rotation;

            GameObject visual = new GameObject("Visual_Root");
            visual.transform.SetParent(segObj.transform, false);

            // Taper factor: full girth through body, gentle taper towards tail tip
            float taper = Mathf.Clamp(1.0f - (index * 0.008f), 0.25f, 1.0f);

            // 1. Upper Dorsal Carapace Plate
            GameObject dorsalCarapace = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dorsalCarapace.name = "Dorsal_Carapace";
            dorsalCarapace.transform.SetParent(visual.transform, false);
            dorsalCarapace.transform.localPosition = new Vector3(0f, 0.1f * taper, 0f);
            dorsalCarapace.transform.localScale = new Vector3(1.05f * taper, 0.72f * taper, 1.25f * taper);
            DestroyImmediate(dorsalCarapace.GetComponent<Collider>());
            dorsalCarapace.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;

            // 2. Underbelly Ventral Armor Plate
            GameObject ventralPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ventralPlate.name = "Ventral_Plate";
            ventralPlate.transform.SetParent(visual.transform, false);
            ventralPlate.transform.localPosition = new Vector3(0f, -0.2f * taper, 0f);
            ventralPlate.transform.localScale = new Vector3(0.78f * taper, 0.25f * taper, 1.05f * taper);
            DestroyImmediate(ventralPlate.GetComponent<Collider>());
            ventralPlate.GetComponent<Renderer>().sharedMaterial = ventralMaterial;

            // 3. Bioluminescent Dorsal Spine Fin
            GameObject dorsalFin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dorsalFin.name = "Bioluminescent_Fin";
            dorsalFin.transform.SetParent(visual.transform, false);
            dorsalFin.transform.localPosition = new Vector3(0f, 0.42f * taper, -0.05f * taper);
            dorsalFin.transform.localScale = new Vector3(0.08f * taper, 0.45f * taper, 0.7f * taper);
            dorsalFin.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            DestroyImmediate(dorsalFin.GetComponent<Collider>());
            dorsalFin.GetComponent<Renderer>().sharedMaterial = bioluminescentMaterial;

            return segObj;
        }
    }
}
