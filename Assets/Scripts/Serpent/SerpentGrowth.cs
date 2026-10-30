using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.CameraSystem;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// AAA Dynamic Serpent Growth System.
    /// Features:
    /// - Starts with 40-60 bones/segments for ultra-fluid anatomical realism
    /// - Smooth Metabolic Digestion Queue (zero instant pops)
    /// - Traveling Metabolic Energy Wave down the spine
    /// - Animated Tail Unfurling with Zero-Allocation Object Pooling
    /// - Support for Massive Colossal Sizes (up to 300+ segments)
    /// - Evolution Stage Progression (Juvenile -> Elder -> Celestial Leviathan)
    /// </summary>
    [RequireComponent(typeof(SerpentPhysicsFollower))]
    public class SerpentGrowth : MonoBehaviour
    {
        [Header("Starting Anatomy")]
        [Range(40, 80)]
        [SerializeField] private int initialSegments = 50;
        [SerializeField] private float segmentSpacing = 0.85f;

        [Header("Smooth Digestion Growth Settings")]
        [SerializeField] private float digestionPulseSpeed = 25.0f; // Speed of glowing wave down spine
        [SerializeField] private float segmentUnfurlDuration = 0.65f;
        [SerializeField] private float minGrowthInterval = 0.25f;

        [Header("Evolution Tiers")]
        [SerializeField] private int elderTierSegments = 90;
        [SerializeField] private int celestialTierSegments = 160;

        public int CurrentSegmentCount => follower != null ? follower.Segments.Count : 0;
        public float EvolutionProgress { get; private set; } // 0.0 to 1.0

        public event Action<int> OnSegmentCountChanged;
        public event Action<float> OnEvolutionProgressChanged;

        private SerpentPhysicsFollower follower;
        private ProceduralSerpentGeometry geometryBuilder;
        private SerpentSegmentPool segmentPool;

        private readonly Queue<int> growthQueue = new Queue<int>();
        private bool isProcessingGrowth = false;
        private float lastGrowthTime;

        private void Awake()
        {
            follower = GetComponent<SerpentPhysicsFollower>();
            geometryBuilder = GetComponent<ProceduralSerpentGeometry>();
            segmentPool = GetComponent<SerpentSegmentPool>();
            if (segmentPool == null) segmentPool = FindFirstObjectByType<SerpentSegmentPool>();
        }

        private void Start()
        {
            BuildInitialBody();
        }

        public void BuildInitialBody()
        {
            if (follower == null) follower = GetComponent<SerpentPhysicsFollower>();

            follower.ClearSegments();
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 backDir = -transform.forward;

            for (int i = 0; i < initialSegments; i++)
            {
                Vector3 segPos = startPos + (backDir * (segmentSpacing * (i + 1)));
                SpawnSegmentImmediate(i, initialSegments, segPos, startRot);
            }

            UpdateEvolutionMetrics();
            OnSegmentCountChanged?.Invoke(follower.Segments.Count);
        }

        /// <summary>
        /// Called when the serpent eats an Aether Orb.
        /// Adds to smooth digestion queue rather than instantly popping in segments.
        /// </summary>
        public void AddSegments(int count)
        {
            for (int i = 0; i < count; i++)
            {
                growthQueue.Enqueue(1);
            }

            if (!isProcessingGrowth)
            {
                StartCoroutine(ProcessGrowthQueue());
            }

            // Start traveling metabolic digestion wave from head
            StartCoroutine(SendMetabolicPulse());

            // Satisfying camera trauma kick
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.AddTrauma(0.2f);
            }
        }

        private IEnumerator SendMetabolicPulse()
        {
            if (follower == null) yield break;

            int count = follower.Segments.Count;
            float delayPerSegment = 1.0f / digestionPulseSpeed;

            for (int i = 0; i < count; i++)
            {
                if (i < follower.Segments.Count && follower.Segments[i] != null)
                {
                    follower.Segments[i].TriggerMetabolicPulse(2.8f);
                }
                yield return new WaitForSeconds(delayPerSegment);
            }
        }

        private IEnumerator ProcessGrowthQueue()
        {
            isProcessingGrowth = true;

            while (growthQueue.Count > 0)
            {
                growthQueue.Dequeue();

                // Wait for the pulse or minimum interval
                float timeSinceLast = Time.time - lastGrowthTime;
                if (timeSinceLast < minGrowthInterval)
                {
                    yield return new WaitForSeconds(minGrowthInterval - timeSinceLast);
                }

                SpawnNewAnimatedSegment();
                lastGrowthTime = Time.time;
                yield return null;
            }

            isProcessingGrowth = false;
        }

        private void SpawnNewAnimatedSegment()
        {
            if (follower == null) return;

            int currentCount = follower.Segments.Count;
            Vector3 spawnPos = transform.position;
            Quaternion spawnRot = transform.rotation;

            if (currentCount > 0)
            {
                var lastSeg = follower.Segments[currentCount - 1];
                spawnPos = lastSeg.CurrentPhysicsPosition - (lastSeg.CurrentPhysicsRotation * Vector3.forward * segmentSpacing);
                spawnRot = lastSeg.CurrentPhysicsRotation;
            }

            // Retrieve segment from pool
            SerpentSegment newSegment;
            if (segmentPool != null)
            {
                newSegment = segmentPool.GetSegment(currentCount, spawnPos, spawnRot);
            }
            else
            {
                GameObject segObj = (geometryBuilder != null)
                    ? geometryBuilder.CreateSegmentObject(currentCount, spawnPos, spawnRot)
                    : new GameObject($"Segment_{currentCount:000}");

                newSegment = segObj.GetComponent<SerpentSegment>();
                if (newSegment == null) newSegment = segObj.AddComponent<SerpentSegment>();
            }

            newSegment.transform.SetParent(null);
            newSegment.Initialize(currentCount, currentCount + 1, spawnPos, spawnRot, startCollapsed: true);
            newSegment.SetTargetGrowthScale(1.0f, 1f / segmentUnfurlDuration);
            newSegment.TriggerMetabolicPulse(3.5f);

            follower.RegisterSegment(newSegment);

            UpdateEvolutionMetrics();
            OnSegmentCountChanged?.Invoke(follower.Segments.Count);
        }

        private void SpawnSegmentImmediate(int index, int total, Vector3 position, Quaternion rotation)
        {
            SerpentSegment segment;
            if (segmentPool != null)
            {
                segment = segmentPool.GetSegment(index, position, rotation);
            }
            else
            {
                GameObject segObj = (geometryBuilder != null)
                    ? geometryBuilder.CreateSegmentObject(index, position, rotation)
                    : new GameObject($"Segment_{index:000}");

                segment = segObj.GetComponent<SerpentSegment>();
                if (segment == null) segment = segObj.AddComponent<SerpentSegment>();
            }

            segment.transform.SetParent(null);
            segment.Initialize(index, total, position, rotation, startCollapsed: false);
            follower.RegisterSegment(segment);
        }

        private void UpdateEvolutionMetrics()
        {
            int count = follower != null ? follower.Segments.Count : initialSegments;
            EvolutionProgress = Mathf.Clamp01((float)(count - initialSegments) / (celestialTierSegments - initialSegments));

            // Notify all segments of current evolution stage
            if (follower != null)
            {
                for (int i = 0; i < follower.Segments.Count; i++)
                {
                    if (follower.Segments[i] != null)
                    {
                        follower.Segments[i].SetEvolutionStage(EvolutionProgress);
                    }
                }
            }

            OnEvolutionProgressChanged?.Invoke(EvolutionProgress);
        }
    }
}
