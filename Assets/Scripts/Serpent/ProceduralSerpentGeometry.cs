using UnityEngine;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// Generates high-fidelity visual geometry for the placeholder serpent:
    /// - Aerodynamic draconic head with glowing bioluminescent eyes and crest
    /// - Articulated spine segments with dorsal carapace and energy rings
    /// - Tapering radiant tail
    /// Ensures stunning immediate visual presentation in URP Deferred & HDR.
    /// </summary>
    public class ProceduralSerpentGeometry : MonoBehaviour
    {
        [Header("Materials (Auto-Generated if unassigned)")]
        [SerializeField] private Material carapaceMaterial;
        [SerializeField] private Material bioluminescentMaterial;
        [SerializeField] private Material eyeGlowMaterial;

        [Header("Colors")]
        [SerializeField] private Color carapaceColor = new Color(0.08f, 0.12f, 0.18f);
        [SerializeField] private Color bioluminescentColor = new Color(0.1f, 0.85f, 1.0f);
        [SerializeField] private Color eyeColor = new Color(1.0f, 0.35f, 0.1f);

        private void Awake()
        {
            EnsureMaterials();
            BuildHeadVisuals();
        }

        private void EnsureMaterials()
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit");
            if (standardShader == null) standardShader = Shader.Find("Standard");

            if (carapaceMaterial == null)
            {
                carapaceMaterial = new Material(standardShader);
                carapaceMaterial.name = "Mat_SerpentCarapace";
                carapaceMaterial.color = carapaceColor;
                carapaceMaterial.SetFloat("_Smoothness", 0.85f);
                carapaceMaterial.SetFloat("_Metallic", 0.65f);
            }

            if (bioluminescentMaterial == null)
            {
                bioluminescentMaterial = new Material(standardShader);
                bioluminescentMaterial.name = "Mat_SerpentBioluminescent";
                bioluminescentMaterial.color = bioluminescentColor;
                bioluminescentMaterial.EnableKeyword("_EMISSION");
                bioluminescentMaterial.SetColor("_EmissionColor", bioluminescentColor * 2.8f);
            }

            if (eyeGlowMaterial == null)
            {
                eyeGlowMaterial = new Material(standardShader);
                eyeGlowMaterial.name = "Mat_SerpentEyes";
                eyeGlowMaterial.color = eyeColor;
                eyeGlowMaterial.EnableKeyword("_EMISSION");
                eyeGlowMaterial.SetColor("_EmissionColor", eyeColor * 3.5f);
            }
        }

        private void BuildHeadVisuals()
        {
            // Main Head Cranium
            GameObject cranium = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cranium.name = "Visual_HeadCranium";
            cranium.transform.SetParent(transform, false);
            cranium.transform.localScale = new Vector3(1.1f, 0.8f, 1.6f);
            cranium.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            DestroyImmediate(cranium.GetComponent<Collider>());
            cranium.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;

            // Snout
            GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Visual_HeadSnout";
            snout.transform.SetParent(transform, false);
            snout.transform.localScale = new Vector3(0.7f, 0.45f, 1.2f);
            snout.transform.localPosition = new Vector3(0f, -0.1f, 1.2f);
            DestroyImmediate(snout.GetComponent<Collider>());
            snout.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;

            // Left Eye
            CreateEye(new Vector3(-0.45f, 0.22f, 0.65f), true);
            // Right Eye
            CreateEye(new Vector3(0.45f, 0.22f, 0.65f), false);

            // Dorsal Crest Horn
            GameObject crest = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crest.name = "Visual_DorsalCrest";
            crest.transform.SetParent(transform, false);
            crest.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            crest.transform.localPosition = new Vector3(0f, 0.5f, -0.2f);
            crest.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            DestroyImmediate(crest.GetComponent<Collider>());
            crest.GetComponent<Renderer>().sharedMaterial = bioluminescentMaterial;
        }

        private void CreateEye(Vector3 localPos, bool isLeft)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = isLeft ? "Eye_Left" : "Eye_Right";
            eye.transform.SetParent(transform, false);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = new Vector3(0.25f, 0.22f, 0.35f);
            eye.transform.localRotation = Quaternion.Euler(0f, isLeft ? -20f : 20f, 0f);
            DestroyImmediate(eye.GetComponent<Collider>());
            eye.GetComponent<Renderer>().sharedMaterial = eyeGlowMaterial;
        }

        public GameObject CreateSegmentObject(int index, Vector3 position, Quaternion rotation)
        {
            EnsureMaterials();

            GameObject segObj = new GameObject($"Segment_{index:00}");
            segObj.transform.position = position;
            segObj.transform.rotation = rotation;

            // Visual Root
            GameObject visual = new GameObject("Visual_Root");
            visual.transform.SetParent(segObj.transform, false);

            // Taper factor down the body length
            float taper = Mathf.Clamp(1.0f - (index * 0.025f), 0.35f, 1.0f);

            // Body Sphere/Capsule
            GameObject bodyMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bodyMesh.name = "Body_Mesh";
            bodyMesh.transform.SetParent(visual.transform, false);
            bodyMesh.transform.localScale = new Vector3(0.95f * taper, 0.75f * taper, 1.15f * taper);
            DestroyImmediate(bodyMesh.GetComponent<Collider>());
            bodyMesh.GetComponent<Renderer>().sharedMaterial = carapaceMaterial;

            // Bioluminescent Dorsal Spine / Ring
            GameObject energySpine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            energySpine.name = "Energy_Spine";
            energySpine.transform.SetParent(visual.transform, false);
            energySpine.transform.localPosition = new Vector3(0f, 0.35f * taper, 0f);
            energySpine.transform.localScale = new Vector3(0.12f * taper, 0.38f * taper, 0.6f * taper);
            energySpine.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            DestroyImmediate(energySpine.GetComponent<Collider>());
            energySpine.GetComponent<Renderer>().sharedMaterial = bioluminescentMaterial;

            return segObj;
        }
    }
}
