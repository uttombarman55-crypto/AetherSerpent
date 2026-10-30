using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Serpent
{
    /// <summary>
    /// High-performance Object Pool for serpent segments.
    /// Eliminates garbage collection spikes during rapid continuous growth.
    /// </summary>
    public class SerpentSegmentPool : MonoBehaviour
    {
        public static SerpentSegmentPool Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int prewarmCount = 80;
        [SerializeField] private int maxCapacity = 500;

        private readonly Queue<SerpentSegment> pool = new Queue<SerpentSegment>();
        private ProceduralSerpentGeometry geometryBuilder;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            geometryBuilder = GetComponent<ProceduralSerpentGeometry>();
            if (geometryBuilder == null)
            {
                geometryBuilder = FindFirstObjectByType<ProceduralSerpentGeometry>();
            }
        }

        private void Start()
        {
            Prewarm();
        }

        private void Prewarm()
        {
            if (geometryBuilder == null) return;

            for (int i = 0; i < prewarmCount; i++)
            {
                SerpentSegment segment = CreateNewSegment(i);
                segment.gameObject.SetActive(false);
                pool.Enqueue(segment);
            }
        }

        private SerpentSegment CreateNewSegment(int index)
        {
            GameObject segObj = geometryBuilder != null 
                ? geometryBuilder.CreateSegmentObject(index, Vector3.zero, Quaternion.identity)
                : new GameObject($"Pooled_Segment_{index:000}");

            segObj.transform.SetParent(transform);

            SerpentSegment segment = segObj.GetComponent<SerpentSegment>();
            if (segment == null) segment = segObj.AddComponent<SerpentSegment>();

            return segment;
        }

        public SerpentSegment GetSegment(int index, Vector3 position, Quaternion rotation)
        {
            SerpentSegment segment;
            if (pool.Count > 0)
            {
                segment = pool.Dequeue();
                segment.transform.position = position;
                segment.transform.rotation = rotation;
                segment.gameObject.SetActive(true);
            }
            else
            {
                segment = CreateNewSegment(index);
                segment.transform.position = position;
                segment.transform.rotation = rotation;
            }

            return segment;
        }

        public void ReturnSegment(SerpentSegment segment)
        {
            if (segment == null) return;
            segment.gameObject.SetActive(false);
            segment.transform.SetParent(transform);
            if (pool.Count < maxCapacity)
            {
                pool.Enqueue(segment);
            }
            else
            {
                Destroy(segment.gameObject);
            }
        }
    }
}

