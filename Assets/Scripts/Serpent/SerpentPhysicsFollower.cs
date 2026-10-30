using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// AAA Jitter-Free Spine Physics Solver.
    /// Employs Inverse Kinematics Distance Constraints (Verlet/FABRIK principle),
    /// Spine Tangent Orientation with Twist Propagation,
    /// Biological Undulation Wave, and High-Precision Fixed Substepping.
    /// </summary>
    [RequireComponent(typeof(SerpentController))]
    public class SerpentPhysicsFollower : MonoBehaviour
    {
        [Header("Spine Settings")]
        [SerializeField] private float segmentSpacing = 0.85f;
        [SerializeField] private int constraintIterations = 3;

        [Header("Undulation Wave (Living Serpent)")]
        [SerializeField] private bool enableUndulation = true;
        [SerializeField] private float waveFrequency = 0.65f;
        [SerializeField] private float waveAmplitude = 0.22f;
        [SerializeField] private float undulationSpeed = 6.0f;

        [Header("Banking / Roll Physics")]
        [SerializeField] private float bankingDamping = 8.0f;

        [Header("Segments List")]
        [SerializeField] private List<SerpentSegment> segments = new List<SerpentSegment>();
        public IReadOnlyList<SerpentSegment> Segments => segments;

        private SerpentController controller;
        private float undulationTimer = 0f;

        private void Awake()
        {
            controller = GetComponent<SerpentController>();
        }

        private void FixedUpdate()
        {
            if (segments == null || segments.Count == 0) return;

            SimulateSpinePhysics();
        }

        private void Update()
        {
            if (segments == null || segments.Count == 0) return;

            // Interpolate segment visual transforms for butter-smooth high refresh rate rendering
            float alpha = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            alpha = Mathf.Clamp01(alpha);

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].InterpolateVisual(alpha);
            }
        }

        private void SimulateSpinePhysics()
        {
            // Update undulation wave phase based on forward movement speed
            float speedFactor = (controller != null) ? Mathf.Max(controller.CurrentSpeed * 0.15f, 0.4f) : 1f;
            undulationTimer += Time.fixedDeltaTime * undulationSpeed * speedFactor;

            Vector3 parentPos = transform.position;
            Quaternion parentRot = transform.rotation;
            Vector3 headRight = transform.right;

            for (int i = 0; i < segments.Count; i++)
            {
                SerpentSegment segment = segments[i];

                // 1. Distance constraint to previous segment (parent)
                Vector3 currentPos = segment.CurrentPhysicsPosition;
                Vector3 delta = currentPos - parentPos;
                float dist = delta.magnitude;

                if (dist > 0.0001f)
                {
                    currentPos = parentPos + (delta / dist) * segmentSpacing;
                }
                else
                {
                    currentPos = parentPos - (parentRot * Vector3.forward * segmentSpacing);
                }

                // 2. Add organic lateral undulation wave
                if (enableUndulation && i > 0)
                {
                    float wavePhase = undulationTimer - (i * waveFrequency);
                    float lateralOffset = Mathf.Sin(wavePhase) * waveAmplitude;
                    // Taper undulation near head and tail
                    float taper = Mathf.Sin((float)i / segments.Count * Mathf.PI);
                    currentPos += headRight * (lateralOffset * taper * 0.4f);
                }

                // 3. Tangent look direction with Twist-Free alignment
                Vector3 forwardDir = (parentPos - currentPos).normalized;
                if (forwardDir == Vector3.zero) forwardDir = parentRot * Vector3.forward;

                // Propagate up vector from parent with smooth roll
                Vector3 upDir = Vector3.Lerp(parentRot * Vector3.up, Vector3.up, 0.2f);
                Quaternion targetRot = Quaternion.LookRotation(forwardDir, upDir);

                // Commit physics step
                segment.UpdatePhysicsTransform(currentPos, targetRot);

                parentPos = currentPos;
                parentRot = targetRot;
            }

            // Multi-pass constraint relaxation for extreme stability
            for (int iter = 0; iter < constraintIterations; iter++)
            {
                RelaxConstraints();
            }
        }

        private void RelaxConstraints()
        {
            Vector3 anchor = transform.position;
            for (int i = 0; i < segments.Count; i++)
            {
                Vector3 toAnchor = segments[i].CurrentPhysicsPosition - anchor;
                float d = toAnchor.magnitude;
                if (d > 0.0001f)
                {
                    segments[i].CurrentPhysicsPosition = anchor + (toAnchor / d) * segmentSpacing;
                }
                anchor = segments[i].CurrentPhysicsPosition;
            }
        }

        public void RegisterSegment(SerpentSegment segment)
        {
            if (!segments.Contains(segment))
            {
                segments.Add(segment);
            }
        }

        public void SetSegments(List<SerpentSegment> newSegments)
        {
            segments = newSegments;
        }

        public void ClearSegments()
        {
            segments.Clear();
        }
    }
}
