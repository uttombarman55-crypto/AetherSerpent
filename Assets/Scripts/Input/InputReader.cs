using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

namespace AetherSerpent.Input
{
    /// <summary>
    /// Unified Input Reader implementing the Unity New Input System.
    /// Supports:
    /// - Keyboard + Mouse (WASD, Space/Ctrl, Mouse Look, Shift Sprint)
    /// - Gamepad (Left Stick, Triggers, Right Stick, South Button)
    /// - Mobile Touch (Multi-touch drag, virtual joystick, on-screen controls)
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public class InputReader : MonoBehaviour
    {
        public static InputReader Instance { get; private set; }

        [Header("Read Values")]
        public Vector2 MoveInput { get; private set; }
        public float AscendDescendInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public float ZoomInput { get; private set; }
        public bool IsSprinting { get; private set; }

        [Header("Sensitivity Settings")]
        [SerializeField] private float mouseLookSensitivity = 1.0f;
        [SerializeField] private float gamepadLookSensitivity = 2.5f;

        // Events
        public event Action<Vector2> OnMoveEvent;
        public event Action OnSprintStarted;
        public event Action OnSprintEnded;
        public event Action OnInteractEvent;
        public event Action OnPauseEvent;

        // Virtual Touch Override (feeds from VirtualTouchHUD)
        private Vector2 virtualMoveInput;
        private float virtualAscendInput;
        private bool virtualSprint;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
        }

        private void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Disable();
#endif
        }

        private void Update()
        {
            PollInputs();
        }

        private void PollInputs()
        {
            Vector2 move = Vector2.zero;
            float vertical = 0f;
            Vector2 look = Vector2.zero;
            float zoom = 0f;
            bool sprint = false;

#if ENABLE_INPUT_SYSTEM
            // 1. Keyboard
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;

                if (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed) vertical += 1f;
                if (keyboard.leftCtrlKey.isPressed || keyboard.qKey.isPressed) vertical -= 1f;

                if (keyboard.leftShiftKey.isPressed) sprint = true;

                if (keyboard.escapeKey.wasPressedThisFrame) OnPauseEvent?.Invoke();
                if (keyboard.fKey.wasPressedThisFrame) OnInteractEvent?.Invoke();
            }

            // 2. Mouse Look & Zoom
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue() * (mouseLookSensitivity * 0.1f);
                look += mouseDelta;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    zoom += Mathf.Sign(scroll);
                }
            }

            // 3. Gamepad
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stickMove = gamepad.leftStick.ReadValue();
                if (stickMove.sqrMagnitude > 0.04f)
                {
                    move = stickMove;
                }

                float triggerVal = gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
                if (Mathf.Abs(triggerVal) > 0.05f)
                {
                    vertical = triggerVal;
                }

                Vector2 rightStick = gamepad.rightStick.ReadValue() * gamepadLookSensitivity;
                if (rightStick.sqrMagnitude > 0.04f)
                {
                    look += rightStick;
                }

                if (gamepad.buttonSouth.isPressed || gamepad.rightShoulder.isPressed)
                {
                    sprint = true;
                }

                if (gamepad.startButton.wasPressedThisFrame) OnPauseEvent?.Invoke();
                if (gamepad.buttonWest.wasPressedThisFrame) OnInteractEvent?.Invoke();
            }

            // 4. Mobile Touch
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.isInProgress)
                    {
                        // Right side of screen = camera look drag
                        Vector2 touchPos = touch.position.ReadValue();
                        if (touchPos.x > Screen.width * 0.5f)
                        {
                            look += touch.delta.ReadValue() * 0.15f;
                        }
                    }
                }
            }
#else
            // Fallback for legacy input if enabled alongside
            move.x = UnityEngine.Input.GetAxisRaw("Horizontal");
            move.y = UnityEngine.Input.GetAxisRaw("Vertical");
            if (UnityEngine.Input.GetKey(KeyCode.Space)) vertical += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl)) vertical -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) sprint = true;
            look.x = UnityEngine.Input.GetAxis("Mouse X");
            look.y = UnityEngine.Input.GetAxis("Mouse Y");
            zoom = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) OnPauseEvent?.Invoke();
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) OnInteractEvent?.Invoke();
#endif

            // Merge virtual touch UI inputs
            if (virtualMoveInput.sqrMagnitude > 0.001f) move = virtualMoveInput;
            if (Mathf.Abs(virtualAscendInput) > 0.001f) vertical = virtualAscendInput;
            if (virtualSprint) sprint = true;

            // Normalize move vector
            if (move.sqrMagnitude > 1f) move.Normalize();

            // Track state change events
            if (!IsSprinting && sprint) OnSprintStarted?.Invoke();
            if (IsSprinting && !sprint) OnSprintEnded?.Invoke();

            MoveInput = move;
            AscendDescendInput = Mathf.Clamp(vertical, -1f, 1f);
            LookInput = look;
            ZoomInput = zoom;
            IsSprinting = sprint;

            if (move != Vector2.zero)
            {
                OnMoveEvent?.Invoke(move);
            }
        }

        // Methods to receive touch joystick inputs from VirtualTouchHUD
        public void SetVirtualMove(Vector2 move) => virtualMoveInput = move;
        public void SetVirtualAscend(float ascend) => virtualAscendInput = ascend;
        public void SetVirtualSprint(bool sprint) => virtualSprint = sprint;
    }
}
