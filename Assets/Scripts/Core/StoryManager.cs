using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherSerpent.Core
{
    /// <summary>
    /// Governs atmospheric narrative progression and memory flashback triggers.
    /// Manages story milestones and invokes poetic memory vignettes in the HUD.
    /// </summary>
    public class StoryManager : MonoBehaviour
    {
        public static StoryManager Instance { get; private set; }

        public struct MemoryRecord
        {
            public string Title;
            public string PoeticText;
            public float DisplayDuration;
        }

        private readonly HashSet<string> unlockedMilestones = new HashSet<string>();

        public event Action<MemoryRecord> OnMemoryTriggered;
        public event Action<string> OnMilestoneReached;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Initial Awakening Memory after a brief entrance
            Invoke(nameof(TriggerAwakeningMemory), 2.5f);
        }

        private void TriggerAwakeningMemory()
        {
            TriggerMemory(
                "THE AWAKENING",
                "Before the stars were named, the Great Coil slumbered in the celestial deep.\nNow the pulse stirs once more. Slither forth and reclaim the primordial aether.",
                5.5f
            );
            UnlockMilestone("AWAKENING");
        }

        public void TriggerMemory(string title, string text, float duration = 5.0f)
        {
            MemoryRecord record = new MemoryRecord
            {
                Title = title,
                PoeticText = text,
                DisplayDuration = duration
            };

            Debug.Log($"[StoryManager] Memory Unlocked: {title}");
            OnMemoryTriggered?.Invoke(record);
        }

        public void UnlockMilestone(string milestoneId)
        {
            if (!unlockedMilestones.Contains(milestoneId))
            {
                unlockedMilestones.Add(milestoneId);
                Debug.Log($"[StoryManager] Milestone Reached: {milestoneId}");
                OnMilestoneReached?.Invoke(milestoneId);
            }
        }

        public bool HasMilestone(string milestoneId) => unlockedMilestones.Contains(milestoneId);
    }
}
