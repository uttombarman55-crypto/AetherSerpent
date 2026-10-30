using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AetherSerpent.Core;

namespace AetherSerpent.UI
{
    /// <summary>
    /// Governs the poetic, letterboxed memory vignette UI.
    /// Fades in atmospheric lore text and cinematic black letterbox bars
    /// when the serpent consumes Ancient Memory Cores or achieves milestones.
    /// </summary>
    public class MemoryFlashbackUI : MonoBehaviour
    {
        [Header("Vignette Container")]
        [SerializeField] private GameObject letterboxContainer;
        [SerializeField] private CanvasGroup vignetteCanvasGroup;

        [Header("Text Displays")]
        [SerializeField] private Text memoryTitleText;
        [SerializeField] private Text memoryPoeticBodyText;

        private Coroutine activeVignetteRoutine;

        private void Start()
        {
            if (letterboxContainer != null) letterboxContainer.SetActive(false);

            if (StoryManager.Instance != null)
            {
                StoryManager.Instance.OnMemoryTriggered += ShowMemoryVignette;
            }
        }

        private void OnDestroy()
        {
            if (StoryManager.Instance != null)
            {
                StoryManager.Instance.OnMemoryTriggered -= ShowMemoryVignette;
            }
        }

        public void ShowMemoryVignette(StoryManager.MemoryRecord record)
        {
            if (activeVignetteRoutine != null) StopCoroutine(activeVignetteRoutine);
            activeVignetteRoutine = StartCoroutine(ExecuteVignette(record));
        }

        private IEnumerator ExecuteVignette(StoryManager.MemoryRecord record)
        {
            if (letterboxContainer == null || vignetteCanvasGroup == null) yield break;

            letterboxContainer.SetActive(true);
            if (memoryTitleText != null) memoryTitleText.text = record.Title;
            if (memoryPoeticBodyText != null) memoryPoeticBodyText.text = record.PoeticText;

            // Fade in letterbox
            float timer = 0f;
            float fadeDuration = 0.5f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                vignetteCanvasGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
                yield return null;
            }
            vignetteCanvasGroup.alpha = 1.0f;

            // Display duration
            yield return new WaitForSecondsRealtime(record.DisplayDuration);

            // Fade out
            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                vignetteCanvasGroup.alpha = Mathf.Clamp01(1.0f - (timer / fadeDuration));
                yield return null;
            }
            vignetteCanvasGroup.alpha = 0f;
            letterboxContainer.SetActive(false);
        }
    }
}
