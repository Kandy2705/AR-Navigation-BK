using Unity.XR.CoreUtils;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// XROriginEditorFlyController
/// Bay tự do XR Origin trong Editor bằng bàn phím (chỉ chạy trong Editor và Development Build).
///
/// Mặc định:
///   - W / A / S / D : đi tới / trái / lùi / phải (trên mặt phẳng XZ, theo hướng nhìn camera)
///   - Q / E         : hạ / nâng (Y)
///   - Mũi tên ←→    : xoay yaw (quay trái phải)
///   - Mũi tên ↑↓    : pitch (ngẩng / cúi)
///   - Shift         : tăng tốc x3
///   - Space + WASD  : tốc độ chậm x0.3 (đi chính xác)
///   - R             : reset rotation về (0,yaw,0) — bỏ pitch / roll
///
/// Đặt script ở GameObject persistent, gán <see cref="xrOrigin"/> = SharedARRig &gt; XR Origin,
/// <see cref="xrCamera"/> = ARCamera con của XR Origin.
/// Khi chạy trên device hoặc khi <see cref="enableOnlyInEditor"/> = true, script sẽ tự tắt.
/// </summary>
[DisallowMultipleComponent]
public class XROriginEditorFlyController : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Chỉ kích hoạt trong Editor. Tắt khi build để không can thiệp ARFoundation tracking.")]
    [SerializeField] private bool enableOnlyInEditor = true;
    [Tooltip("Kích hoạt vào Awake. Có thể bật/tắt runtime qua property Enabled.")]
    [SerializeField] private bool enabledOnStart = true;

    [Header("References")]
    [Tooltip("XR Origin sẽ bị driver dịch chuyển + xoay yaw.")]
    [SerializeField] private XROrigin xrOrigin;
    [Tooltip("Camera AR con của XR Origin. Pitch (ngẩng/cúi) áp lên local rotation của camera.")]
    [SerializeField] private Camera xrCamera;

    [Header("Movement")]
    [Tooltip("Tốc độ ngang (m/s) ở chế độ thường.")]
    [SerializeField] private float moveSpeed = 2.0f;
    [Tooltip("Tốc độ dọc (m/s) khi nhấn Q/E.")]
    [SerializeField] private float verticalSpeed = 1.5f;
    [Tooltip("Hệ số gia tốc khi nhấn Shift.")]
    [SerializeField] private float fastMultiplier = 3f;
    [Tooltip("Hệ số giảm tốc khi nhấn Space (precision).")]
    [SerializeField] private float slowMultiplier = 0.3f;
    [Tooltip("Khi true, WASD đi theo hướng XZ của camera (forward chiếu xuống mặt phẳng). " +
             "Khi false, đi theo trục thế giới +Z/+X.")]
    [SerializeField] private bool moveRelativeToCameraYaw = true;

    [Header("Rotation")]
    [Tooltip("Tốc độ xoay yaw bằng phím mũi tên (độ/giây).")]
    [SerializeField] private float yawSpeed = 90f;
    [Tooltip("Tốc độ pitch bằng phím mũi tên (độ/giây).")]
    [SerializeField] private float pitchSpeed = 80f;
    [Tooltip("Giới hạn pitch trên (độ) — ngẩng tối đa.")]
    [SerializeField] private float pitchMaxDegrees = 85f;
    [Tooltip("Giới hạn pitch dưới (độ) — cúi tối đa.")]
    [SerializeField] private float pitchMinDegrees = -85f;

    [Header("Hotkeys")]
    [SerializeField] private bool printHelpOnStart = true;

    public bool Enabled
    {
        get => _runtimeEnabled;
        set => _runtimeEnabled = value;
    }

    private bool _runtimeEnabled;
    private float _currentPitch;

    private void Awake()
    {
        _runtimeEnabled = enabledOnStart;

#if !UNITY_EDITOR
        if (enableOnlyInEditor)
        {
            enabled = false;
            return;
        }
#endif

        if (xrOrigin == null)
        {
            xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        }
        if (xrCamera == null && xrOrigin != null)
        {
            xrCamera = xrOrigin.Camera != null ? xrOrigin.Camera : xrOrigin.GetComponentInChildren<Camera>(true);
        }

        if (xrCamera != null)
        {
            _currentPitch = NormalizeAngle(xrCamera.transform.localEulerAngles.x);
        }

        if (printHelpOnStart)
        {
            Debug.Log("[XROriginEditorFly] WASD: di chuyển | Q/E: lên-xuống | ←→: xoay | ↑↓: pitch | " +
                      "Shift: nhanh | Space: chậm | R: reset pitch");
        }
    }

    private void Update()
    {
        if (!_runtimeEnabled) return;
        if (xrOrigin == null) return;

        float dt = Time.deltaTime;
        float speedMul = 1f;
        if (Held(KeyCode.LeftShift) || Held(KeyCode.RightShift)) speedMul *= fastMultiplier;
        if (Held(KeyCode.Space)) speedMul *= slowMultiplier;

        // ── Translation ──────────────────────────────────────────────────
        Vector3 move = Vector3.zero;
        if (Held(KeyCode.W)) move.z += 1f;
        if (Held(KeyCode.S)) move.z -= 1f;
        if (Held(KeyCode.D)) move.x += 1f;
        if (Held(KeyCode.A)) move.x -= 1f;

        if (move.sqrMagnitude > 0f)
        {
            move.Normalize();
            Vector3 worldDir;
            if (moveRelativeToCameraYaw && xrCamera != null)
            {
                Vector3 fwd = xrCamera.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = xrCamera.transform.up;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                worldDir = fwd * move.z + right * move.x;
            }
            else
            {
                worldDir = new Vector3(move.x, 0f, move.z);
            }
            xrOrigin.transform.position += worldDir * moveSpeed * speedMul * dt;
        }

        float vertical = 0f;
        if (Held(KeyCode.E)) vertical += 1f;
        if (Held(KeyCode.Q)) vertical -= 1f;
        if (vertical != 0f)
        {
            xrOrigin.transform.position += Vector3.up * vertical * verticalSpeed * speedMul * dt;
        }

        // ── Yaw (XR Origin Y rotation) ───────────────────────────────────
        float yawDelta = 0f;
        if (Held(KeyCode.RightArrow)) yawDelta += 1f;
        if (Held(KeyCode.LeftArrow))  yawDelta -= 1f;
        if (yawDelta != 0f)
        {
            xrOrigin.transform.Rotate(Vector3.up, yawDelta * yawSpeed * speedMul * dt, Space.World);
        }

        // ── Pitch (camera local X rotation) ──────────────────────────────
        if (xrCamera != null)
        {
            float pitchDelta = 0f;
            if (Held(KeyCode.UpArrow))   pitchDelta -= 1f;
            if (Held(KeyCode.DownArrow)) pitchDelta += 1f;
            if (pitchDelta != 0f)
            {
                _currentPitch = Mathf.Clamp(_currentPitch + pitchDelta * pitchSpeed * speedMul * dt,
                                            pitchMinDegrees, pitchMaxDegrees);
                ApplyCameraPitch(_currentPitch);
            }

            if (Pressed(KeyCode.R))
            {
                _currentPitch = 0f;
                ApplyCameraPitch(0f);
            }
        }
    }

    private void ApplyCameraPitch(float pitchDeg)
    {
        Vector3 e = xrCamera.transform.localEulerAngles;
        e.x = pitchDeg;
        e.z = 0f;
        xrCamera.transform.localEulerAngles = e;
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }

    // ── Input abstraction (hỗ trợ cả old và new InputSystem) ─────────────

    private static bool Held(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return GetKey(kb, key)?.isPressed ?? false;
#else
        return Input.GetKey(key);
#endif
    }

    private static bool Pressed(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return GetKey(kb, key)?.wasPressedThisFrame ?? false;
#else
        return Input.GetKeyDown(key);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static UnityEngine.InputSystem.Controls.KeyControl GetKey(Keyboard kb, KeyCode key)
    {
        switch (key)
        {
            case KeyCode.W: return kb.wKey;
            case KeyCode.A: return kb.aKey;
            case KeyCode.S: return kb.sKey;
            case KeyCode.D: return kb.dKey;
            case KeyCode.Q: return kb.qKey;
            case KeyCode.E: return kb.eKey;
            case KeyCode.R: return kb.rKey;
            case KeyCode.Space: return kb.spaceKey;
            case KeyCode.LeftShift: return kb.leftShiftKey;
            case KeyCode.RightShift: return kb.rightShiftKey;
            case KeyCode.UpArrow: return kb.upArrowKey;
            case KeyCode.DownArrow: return kb.downArrowKey;
            case KeyCode.LeftArrow: return kb.leftArrowKey;
            case KeyCode.RightArrow: return kb.rightArrowKey;
            default: return null;
        }
    }
#endif

    [ContextMenu("Reset Camera Pitch")]
    private void DebugResetPitch()
    {
        _currentPitch = 0f;
        if (xrCamera != null) ApplyCameraPitch(0f);
    }
}
