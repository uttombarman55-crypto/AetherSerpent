using UnityEngine;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// Represents an individual bone/segment within the serpent's physical spine.
    /// Stores physical dynamics (Verlet position, previous position, velocity, mass)
    /// and visual properties (scale, material property block, metabolic glow).
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class SerpentSegment : MonoBehaviour
    {
        [Header("Spine Hierarchy")]
        public int Index { get; private set; }
        public float NormalizedIndex { get; private set; } // 0 at head, 1 at tail tip

        [Header("Physical Dynamics")]
        public Vector3 PreviousPosition;
        public Vector3 CurrentPhysicsPosition;
        public Quaternion PreviousRotation;
        public Quaternion CurrentPhysicsRotation;
        public Vector3 Velocity;
        public float Mass = 1.0f;
        public float Radius = 0.5f;

        [Header("Growth Animation State")]
        public float CurrentGrowthScale { get; private set; } = 1.0f;
        private float targetGrowthScale = 1.0f;
        private float growthLerpSpeed = 4.0f;

        [Header("Visual Root Reference")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer[] segmentRenderers;

        // Material Property Block for zero-allocation shader updates (Evolution, Wetness, Metabolic Pulse)
        private MaterialPropertyBlock propBlock;
        private static readonly int MetabolicGlowProp = Shader.PropertyToID("_MetabolicGlow");
        private static readonly int EvolutionStageProp = Shader.PropertyToID("_EvolutionStage");
        private static readonly int ScaleTintProp = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private float currentMetabolicGlow = 0f;

        public void Initialize(int index, int totalExpectedSegments, Vector3 startPos, Quaternion startRot, bool startCollapsed = false)
        {
            Index = index;
            NormalizedIndex = totalExpectedSegments > 1 ? (float)index / (totalExpectedSegments - 1) : 0f;

            transform.position = startPos;
            transform.rotation = startRot;

            PreviousPosition = startPos;
            CurrentPhysicsPosition = startPos;
            PreviousRotation = startRot;
            CurrentPhysicsRotation = startRot;
            Velocity = Vector3.zero;

            // Biological mass distribution: Neck is nimble, body is heavy, tail is light & whippy
            float bodyMassCurve = Mathf.Sin(Mathf.Clamp01(NormalizedIndex) * Mathf.PI);
            Mass = Mathf.Lerp(1.2f, 3.5f, bodyMassCurve); // 3.5kg mid-body, 1.2kg at head/tail
            Radius = Mathf.Lerp(0.55f, 0.25f, NormalizedIndex);

            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }

            if (segmentRenderers == null || segmentRenderers.Length == 0)
            {
                segmentRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (startCollapsed)
            {
                CurrentGrowthScale = 0.001f;
                targetGrowthScale = 1.0f;
            }
            else
            {
                CurrentGrowthScale = 1.0f;
                targetGrowthScale = 1.0f;
            }

            ApplyVisualScale(CurrentGrowthScale);
        }

        public void UpdatePhysicsTransform(Vector3 pos, Quaternion rot, float deltaTime)
        {
            PreviousPosition = CurrentPhysicsPosition;
            PreviousRotation = CurrentPhysicsRotation;

            if (deltaTime > 0.0001f)
            {
                Velocity = (pos - PreviousPosition) / deltaTime;
            }

            CurrentPhysicsPosition = pos;
            CurrentPhysicsRotation = rot;
        }

        public void InterpolateVisual(float alpha)
        {
            transform.position = Vector3.Lerp(PreviousPosition, CurrentPhysicsPosition, alpha);
            transform.rotation = Quaternion.Slerp(PreviousRotation, CurrentPhysicsRotation, alpha);

            // Handle smooth growth scale animation
            if (Mathf.Abs(CurrentGrowthScale - targetGrowthScale) > 0.001f)
            {
                CurrentGrowthScale = Mathf.MoveTowards(CurrentGrowthScale, targetGrowthScale, Time.deltaTime * growthLerpSpeed);
                ApplyVisualScale(CurrentGrowthScale);
            }

            // Decay metabolic glow pulse
            if (currentMetabolicGlow > 0.01f)
            {
                currentMetabolicGlow = Mathf.MoveTowards(currentMetabolicGlow, 0f, Time.deltaTime * 2.5f);
                UpdateMaterialProperties();
            }
        }

        public void TriggerMetabolicPulse(float intensity)
        {
            currentMetabolicGlow = Mathf.Max(currentMetabolicGlow, intensity);
            UpdateMaterialProperties();
        }

        public void SetEvolutionStage(float stage)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            propBlock.SetFloat(EvolutionStageProp, stage);
            ApplyPropertyBlock();
        }

        private void UpdateMaterialProperties()
        {
            if (segmentRenderers == null || segmentRenderers.Length == 0) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            propBlock.SetFloat(MetabolicGlowProp, currentMetabolicGlow);
            ApplyPropertyBlock();
        }

        private void ApplyPropertyBlock()
        {
            if (segmentRenderers == null) return;
            for (int i = 0; i < segmentRenderers.Length; i++)
            {
                if (segmentRenderers[i] != null)
                {
                    segmentRenderers[i].SetPropertyBlock(propBlock);
                }
            }
        }

        private void ApplyVisualScale(float scale)
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * scale;
            }
        }

        public void SetTargetGrowthScale(float targetScale, float speed = 3.5f)
        {
            targetGrowthScale = targetScale;
            growthLerpSpeed = speed;
        }
    }
}
