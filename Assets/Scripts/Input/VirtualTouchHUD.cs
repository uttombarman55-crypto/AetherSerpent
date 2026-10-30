using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AetherSerpent.Input
{
    /// <summary>
    /// Provides on-screen touch virtual joystick and buttons for mobile gameplay.
    /// Automatically enables on mobile/handheld platforms or when touch input is detected.
    /// </summary>
    public class VirtualTouchHUD : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Joystick Graphics")]
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;
        [SerializeField] private float handleRange = 80f;

        private Vector2 touchStartPosition;
        private bool isDragging = false;

        private void Start()
        {
            // Auto hide if not on mobile/touch, but allow editor testing
            bool isMobilePlatform = Application.isMobilePlatform;
#if UNITY_EDITOR
            isMobilePlatform = true; // Enabled in editor for quick testing
#endif
            gameObject.SetActive(isMobilePlatform);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isDragging = true;
            touchStartPosition = eventData.position;
            if (joystickBackground != null)
            {
                joystickBackground.position = touchStartPosition;
            }
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 direction = eventData.position - touchStartPosition;
            float distance = Mathf.Clamp(direction.magnitude, 0f, handleRange);
            Vector2 clampedOffset = direction.normalized * distance;

            if (joystickHandle != null && joystickBackground != null)
            {
                joystickHandle.position = (Vector2)joystickBackground.position + clampedOffset;
            }

            Vector2 normalizedInput = clampedOffset / handleRange;
            if (InputReader.Instance != null)
            {
                InputReader.Instance.SetVirtualMove(normalizedInput);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isDragging = false;
            if (joystickHandle != null && joystickBackground != null)
            {
                joystickHandle.position = joystickBackground.position;
            }

            if (InputReader.Instance != null)
            {
                InputReader.Instance.SetVirtualMove(Vector2.zero);
            }
        }

        // Public buttons for mobile
        public void OnSprintButtonDown() => InputReader.Instance?.SetVirtualSprint(true);
        public void OnSprintButtonUp() => InputReader.Instance?.SetVirtualSprint(false);

        public void OnAscendButtonDown() => InputReader.Instance?.SetVirtualAscend(1f);
        public void OnAscendButtonUp() => InputReader.Instance?.SetVirtualAscend(0f);

        public void OnDescendButtonDown() => InputReader.Instance?.SetVirtualAscend(-1f);
        public void OnDescendButtonUp() => InputReader.Instance?.SetVirtualAscend(0f);
    }
}

