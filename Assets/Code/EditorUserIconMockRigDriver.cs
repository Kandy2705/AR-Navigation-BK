using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// EditorUserIconMockRigDriver
/// - Drives UserIcon movement + rotation in Editor (WASD + yaw + pitch).
/// - Makes XR Origin + Camera follow the UserIcon pose:
///   - XR Origin: position XZ + yaw (Y rotation)
///   - Camera: pitch (X rotation) via local rotation
///
/// Intended usage:
/// - Attach to UserIcon (the map marker).
/// - Assign xrOrigin + xrCamera in Inspector.
/// - Disable other scripts that also move XR Origin from UserIcon (e.g., GPSMarker instant align) to avoid fights.
/// </summary>
public class EditorUserIconMockRigDriver : MonoBehaviour
{
    [Header("Enable")]
    [SerializeField] private bool enableInEditor = true;

    [Header("References")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Camera xrCamera;

    [Header("Movement (UserIcon)")]
    [SerializeField] private float moveSpeedMetersPerSecond = 1.6f;
    [Tooltip("If true: W moves along UserIcon forward. If false: W moves along world +Z.")]
    [SerializeField] private bool moveRelativeToUserYaw = true;

    [Header("Rotation (UserIcon yaw)")]
    [SerializeField] private float yawDegreesPerSecond = 120f;
    [SerializeField] private bool enableRightMouseYaw = true;
    [SerializeField] private float mouseYawSensitivity = 3.5f;

    [Header("Camera Pitch (look down/up)")]
    [SerializeField] private bool enablePitch = true;
    [SerializeField] private float pitchDegreesPerSecond = 90f;
    [SerializeField] private bool enableRightMousePitch = true;
    [SerializeField] private float mousePitchSensitivity = 3.0f;
    [SerializeField] private float pitchMinDegrees = -80f;
    [SerializeField] private float pitchMaxDegrees = 25f;
    [SerializeField] private float pitchDegrees = -15f;

    [Header("Follow Options")]
    [Tooltip("Keep XR Origin Y unchanged; only follow XZ. Recommended for AR rigs.")]
    [SerializeField] private bool followXZOnly = true;
    [Tooltip("Apply yaw to XR Origin so camera heading matches UserIcon.")]
    [SerializeField] private bool followYaw = true;

    private void Reset()
    {
        xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (xrOrigin != null && xrOrigin.Camera != null)
            xrCamera = xrOrigin.Camera;
    }

    private void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (xrCamera == null && xrOrigin != null && xrOrigin.Camera != null)
            xrCamera = xrOrigin.Camera;
    }

    private void Update()
    {
#if !UNITY_EDITOR
        return;
#else
        if (!enableInEditor) return;
        if (xrOrigin == null || xrCamera == null) return;

        float dt = Time.deltaTime;

        // --- UserIcon yaw ---
        float yawInput = 0f;
        if (Input.GetKey(KeyCode.Q)) yawInput -= 1f;
        if (Input.GetKey(KeyCode.E)) yawInput += 1f;

        float yawDelta = yawInput * yawDegreesPerSecond * dt;
        if (enableRightMouseYaw && Input.GetMouseButton(1))
        {
            yawDelta += Input.GetAxis("Mouse X") * mouseYawSensitivity;
        }

        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            transform.rotation = Quaternion.Euler(0f, yawDelta, 0f) * transform.rotation;
        }

        // --- Move UserIcon (WASD) ---
        float x = 0f;
        float z = 0f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;

        Vector3 input = new Vector3(x, 0f, z);
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (input.sqrMagnitude > 0.0001f)
        {
            Vector3 moveDir = input;
            if (moveRelativeToUserYaw)
            {
                Vector3 fwd = transform.forward;
                Vector3 right = transform.right;
                fwd.y = 0f;
                right.y = 0f;
                fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
                right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
                moveDir = right * input.x + fwd * input.z;
                if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
            }

            transform.position += moveDir * moveSpeedMetersPerSecond * dt;
        }

        // --- Camera pitch ---
        if (enablePitch)
        {
            float pitchInput = 0f;
            if (Input.GetKey(KeyCode.R)) pitchInput += 1f;
            if (Input.GetKey(KeyCode.F)) pitchInput -= 1f;

            pitchDegrees += pitchInput * pitchDegreesPerSecond * dt;

            if (enableRightMousePitch && Input.GetMouseButton(1))
            {
                pitchDegrees -= Input.GetAxis("Mouse Y") * mousePitchSensitivity;
            }

            pitchDegrees = Mathf.Clamp(pitchDegrees, pitchMinDegrees, pitchMaxDegrees);
        }
#endif
    }

    private void LateUpdate()
    {
#if !UNITY_EDITOR
        return;
#else
        if (!enableInEditor) return;
        if (xrOrigin == null || xrCamera == null) return;

        // --- Follow position (make camera land on UserIcon XZ) ---
        Vector3 desiredCameraPos = transform.position;
        if (followXZOnly) desiredCameraPos.y = xrCamera.transform.position.y;

        Vector3 offset = desiredCameraPos - xrCamera.transform.position;
        if (followXZOnly) offset.y = 0f;

        xrOrigin.transform.position += offset;

        // --- Follow yaw ---
        if (followYaw)
        {
            float yaw = transform.rotation.eulerAngles.y;
            xrOrigin.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // --- Apply pitch to camera local rotation ---
        if (enablePitch)
        {
            Vector3 e = xrCamera.transform.localEulerAngles;
            xrCamera.transform.localRotation = Quaternion.Euler(pitchDegrees, e.y, e.z);
        }
#endif
    }
}

