using System;
using System.Collections.Generic;
using UnityEngine;
using AetherSerpent.CameraSystem;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// Manages growth dynamics, segment pooling/instantiation, and notifies
    /// the dynamic camera system when the serpent's physical stature changes.
    /// </summary>
    [RequireComponent(typeof(SerpentPhysicsFollower))]
    public class SerpentGrowth : MonoBehaviour
    {
        [Header("Starting Configuration")]
        [SerializeField] private int initialSegments = 16;
        [SerializeField] private float segmentScaleFactor = 0.985f;

        [Header("Prefabs & Templates")]
        [SerializeField] private GameObject segmentPrefab;

        public int CurrentSegmentCount => follower != null ? follower.Segments.Count : 0;
        public event Action<int> OnSegmentCountChanged;

        private SerpentPhysicsFollower follower;
        private ProceduralSerpentGeometry geometryBuilder;

        private void Awake()
        {
            follower = GetComponent<SerpentPhysicsFollower>();
            geometryBuilder = GetComponent<ProceduralSerpentGeometry>();
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
                Vector3 segPos = startPos + (backDir * (0.85f * (i + 1)));
                SpawnSegment(i, segPos, startRot);
            }

            OnSegmentCountChanged?.Invoke(follower.Segments.Count);
        }

        public void AddSegments(int count)
        {
            if (follower == null) return;

            int currentCount = follower.Segments.Count;
            Vector3 spawnPos = transform.position;
            Quaternion spawnRot = transform.rotation;

            if (currentCount > 0)
            {
                var lastSeg = follower.Segments[currentCount - 1];
                spawnPos = lastSeg.CurrentPhysicsPosition;
                spawnRot = lastSeg.CurrentPhysicsRotation;
            }

            for (int i = 0; i < count; i++)
            {
                int index = currentCount + i;
                Vector3 pos = spawnPos - (spawnRot * Vector3.forward * 0.85f);
                SpawnSegment(index, pos, spawnRot);
            }

            OnSegmentCountChanged?.Invoke(follower.Segments.Count);

            // Trigger slight camera trauma for gratifying growth feedback
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.AddTrauma(0.18f);
            }
        }

        private void SpawnSegment(int index, Vector3 position, Quaternion rotation)
        {
            GameObject segObj;

            if (segmentPrefab != null)
            {
                segObj = Instantiate(segmentPrefab, position, rotation);
            }
            else
            {
                // Fallback to procedural visual builder
                if (geometryBuilder != null)
                {
                    segObj = geometryBuilder.CreateSegmentObject(index, position, rotation);
                }
                else
                {
                    segObj = new GameObject($"Segment_{index:00}");
                    segObj.transform.position = position;
                    segObj.transform.rotation = rotation;
                }
            }

            segObj.transform.SetParent(null); // Keep in world space for independent physics

            SerpentSegment segment = segObj.GetComponent<SerpentSegment>();
            if (segment == null) segment = segObj.AddComponent<SerpentSegment>();
            segment.Initialize(index, position, rotation);

            follower.RegisterSegment(segment);
        }
    }
}
