using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AetherSerpent.Core
{
    /// <summary>
    /// Coordinates smooth cinematic scene transitions with cross-fade overlays.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition Settings")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private float fadeDuration = 0.6f;

        private bool isTransitioning = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureFadeOverlay();
        }

        private void EnsureFadeOverlay()
        {
            if (fadeCanvasGroup == null)
            {
                GameObject canvasObj = new GameObject("TransitionCanvas");
                canvasObj.transform.SetParent(transform);
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;

                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();

                GameObject imageObj = new GameObject("FadeImage");
                imageObj.transform.SetParent(canvasObj.transform, false);
                Image img = imageObj.AddComponent<Image>();
                img.color = Color.black;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
            }
        }

        public void TransitionToScene(string sceneName)
        {
            if (isTransitioning) return;
            StartCoroutine(FadeAndSwitchScene(sceneName));
        }

        private IEnumerator FadeAndSwitchScene(string sceneName)
        {
            isTransitioning = true;
            fadeCanvasGroup.blocksRaycasts = true;

            // Fade to black
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;

            // Load Scene Async
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // Small delay for clean stabilization
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade from black
            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (timer / fadeDuration));
                yield return null;
            }
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            isTransitioning = false;
        }
    }
}
