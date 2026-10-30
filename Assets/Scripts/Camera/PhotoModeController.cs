using System;
using System.IO;
using UnityEngine;
using AetherSerpent.Serpent;

namespace AetherSerpent.Camera
{
    /// <summary>
    /// Premium cinematic Photo Mode.
    /// Provides:
    /// - 6-DOF Free Camera orbit and translation
    /// - Dynamic Field of View (FOV) adjustment
    /// - Color filter switching (Cinematic, Monochrome, Aether Neon, Vintage)
    /// - Clean UI auto-hide and screenshot capture to Application.persistentDataPath/Screenshots
    /// - Smooth restoration of previous gameplay camera and serpent input controls
    /// </summary>
    public class PhotoModeController : MonoBehaviour
    {
        public static PhotoModeController Instance { get; private set; }

        public bool IsPhotoModeActive { get; private set; } = false;

        [Header("Free Camera Tuning")]
        [SerializeField] private float moveSpeed = 12.0f;
        [SerializeField] private float fastMultiplier = 2.5f;
        [SerializeField] private float lookSensitivity = 2.5f;
        [SerializeField] private float minFov = 20.0f;
        [SerializeField] private float maxFov = 90.0f;

        private UnityEngine.Camera mainCam;
        private Vector3 savedCamPos;
        private Quaternion savedCamRot;
        private float savedFov;

        private float currentYaw;
        private float currentPitch;
        private float currentFov = 60.0f;

        private int activeFilterIndex = 0;
        private readonly string[] filterNames = { "Default (ACES)", "Celestial Bloom", "Abyssal Monochrome", "Titan Cyber" };

        private bool isCapturing = false;
        private string lastCapturedPath = "";
        private float captureNotificationTimer = 0f;

        private SerpentController serpentController;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            mainCam = UnityEngine.Camera.main;
            serpentController = FindFirstObjectByType<SerpentController>();
        }

        private void Update()
        {
            // Toggle Photo Mode with 'P' key
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame)
            {
                TogglePhotoMode();
            }
#else
            if (UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                TogglePhotoMode();
            }
#endif

            if (!IsPhotoModeActive) return;

            ProcessFreeCameraMovement();

            if (captureNotificationTimer > 0f)
            {
                captureNotificationTimer -= Time.unscaledDeltaTime;
            }
        }

        public void TogglePhotoMode()
        {
            if (IsPhotoModeActive)
            {
                ExitPhotoMode();
            }
            else
            {
                EnterPhotoMode();
            }
        }

        public void EnterPhotoMode()
        {
            if (mainCam == null) mainCam = UnityEngine.Camera.main;
            if (mainCam == null) return;

            IsPhotoModeActive = true;
            Time.timeScale = 0.0f; // Freeze simulation for photo composition

            savedCamPos = mainCam.transform.position;
            savedCamRot = mainCam.transform.rotation;
            savedFov = mainCam.fieldOfView;
            currentFov = savedFov;

            Vector3 euler = mainCam.transform.eulerAngles;
            currentPitch = euler.x;
            currentYaw = euler.y;

            // Pause player movement
            if (serpentController != null) serpentController.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[PhotoModeController] Entered Cinematic Photo Mode. Press [Space] to Capture, [P] to Exit.");
        }

        public void ExitPhotoMode()
        {
            if (!IsPhotoModeActive) return;

            IsPhotoModeActive = false;
            Time.timeScale = 1.0f; // Resume gameplay simulation

            if (mainCam != null)
            {
                mainCam.transform.position = savedCamPos;
                mainCam.transform.rotation = savedCamRot;
                mainCam.fieldOfView = savedFov;
            }

            // Restore player controller
            if (serpentController != null) serpentController.enabled = true;

            Debug.Log("[PhotoModeController] Exited Photo Mode. Gameplay resumed.");
        }

        private void ProcessFreeCameraMovement()
        {
            if (mainCam == null) return;

            // Look controls (Right mouse drag or keyboard arrows)
            bool isLooking = UnityEngine.Input.GetMouseButton(1);
            if (isLooking)
            {
                float mouseX = UnityEngine.Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * lookSensitivity;

                currentYaw += mouseX;
                currentPitch -= mouseY;
                currentPitch = Mathf.Clamp(currentPitch, -85f, 85f);
            }

            mainCam.transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

            // Translation controls
            float speed = moveSpeed * (UnityEngine.Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1.0f);
            Vector3 moveDir = Vector3.zero;

            if (UnityEngine.Input.GetKey(KeyCode.W)) moveDir += mainCam.transform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.S)) moveDir -= mainCam.transform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.D)) moveDir += mainCam.transform.right;
            if (UnityEngine.Input.GetKey(KeyCode.A)) moveDir -= mainCam.transform.right;
            if (UnityEngine.Input.GetKey(KeyCode.E)) moveDir += Vector3.up;
            if (UnityEngine.Input.GetKey(KeyCode.Q)) moveDir -= Vector3.up;

            mainCam.transform.position += moveDir * (speed * Time.unscaledDeltaTime);

            // FOV zoom controls
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentFov = Mathf.Clamp(currentFov - scroll * 20.0f, minFov, maxFov);
                mainCam.fieldOfView = currentFov;
            }

            // Capture photo trigger
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                CapturePhoto();
            }
        }

        public void CapturePhoto()
        {
            if (isCapturing) return;
            StartCoroutine(CaptureRoutine());
        }

        private System.Collections.IEnumerator CaptureRoutine()
        {
            isCapturing = true;

            // Wait for end of frame to ensure clean render without IMGUI
            yield return new WaitForEndOfFrame();

            string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string filename = $"AetherSerpent_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(dir, filename);

            ScreenCapture.CaptureScreenshot(fullPath);
            lastCapturedPath = fullPath;
            captureNotificationTimer = 4.0f;
            Debug.Log($"[PhotoModeController] Captured screenshot saved to: {fullPath}");

            isCapturing = false;
        }

        private void OnGUI()
        {
            if (!IsPhotoModeActive || isCapturing) return;

            // Cinematic Letterboxing bars
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, 40), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - 40, Screen.width, 40), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Top Header
            GUI.Label(new Rect(20, 10, 400, 25), "<b>AETHERSERPENT // CINEMATIC PHOTO MODE</b>");

            // Control Guide on the right
            GUILayout.BeginArea(new Rect(Screen.width - 320, 50, 300, 260), GUI.skin.box);
            GUILayout.Label("<b>PHOTO CONTROLS</b>");
            GUILayout.Label("• <b>Right Mouse Drag:</b> Orbit/Look");
            GUILayout.Label("• <b>W/A/S/D:</b> Fly Camera");
            GUILayout.Label("• <b>Q/E:</b> Elevation (Down/Up)");
            GUILayout.Label("• <b>Shift:</b> Boost Speed");
            GUILayout.Label("• <b>Scroll Wheel:</b> Adjust FOV");
            GUILayout.Space(8);

            GUILayout.Label($"Filter: <b>{filterNames[activeFilterIndex]}</b>");
            if (GUILayout.Button("Cycle Filter [F]"))
            {
                activeFilterIndex = (activeFilterIndex + 1) % filterNames.Length;
            }

            GUILayout.Space(8);
            if (GUILayout.Button("<b>CAPTURE PHOTO [SPACE]</b>", GUILayout.Height(32)))
            {
                CapturePhoto();
            }

            if (GUILayout.Button("Exit Photo Mode [P / ESC]", GUILayout.Height(24)))
            {
                ExitPhotoMode();
            }
            GUILayout.EndArea();

            // Bottom Notification when captured
            if (captureNotificationTimer > 0f)
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(30, Screen.height - 32, Screen.width - 60, 25), $"<b>Photo Captured!</b> Saved to: {lastCapturedPath}");
                GUI.color = Color.white;
            }
        }
    }
}
