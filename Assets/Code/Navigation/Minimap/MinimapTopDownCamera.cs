using UnityEngine;

/// <summary>
/// Moves the minimap camera so it always looks straight down at the player.
/// Uses <see cref="SimpleGPSTracker.ArCamera"/> when set so the map tracks ARCore / first-person
/// movement; otherwise falls back to <see cref="Camera.main"/>.
/// </summary>
public class MinimapTopDownCamera : MonoBehaviour
{
    [Header("Follow target")]
    [Tooltip("Gán Main Camera nếu muốn ép theo một camera cố định.\nIf set, follows this camera every frame (overrides tracker / main).")]
    [SerializeField] private Camera followCameraOverride;

    [Header("Minimap framing — chỉnh trong Play hoặc Edit")]
    [Tooltip(
        "Độ cao camera minimap so với người chơi (mét). Gõ số tùy ý — không giới hạn trên.\n" +
        "Cao hơn = xa mặt đất hơn, thường ít bị vật che; không đổi mức thu phóng bản đồ (xem View Radius bên dưới).")]
    [Min(1f)]
    [SerializeField]
    private float heightAbovePlayer = 45f;

    [Tooltip(
        "Mức zoom (orthographic half-height, mét). Nhỏ hơn = gần hơn, to rõ hơn trên vòng minimap.\n" +
        "Smaller = zoomed in; larger = see more around you.")]
    [SerializeField]
    [Range(2f, 50f)]
    private float viewRadiusMeters = 5f;

    private Camera _cam;
    private SimpleGPSTracker _gps;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _gps = FindFirstObjectByType<SimpleGPSTracker>();
        ApplyOrthographicSize();
    }

    void OnValidate()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        ApplyOrthographicSize();
    }

    void LateUpdate()
    {
        Transform follow = ResolveFollowTransform();
        if (follow == null) return;

        Vector3 p = follow.position;
        transform.position = new Vector3(p.x, p.y + heightAbovePlayer, p.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void ApplyOrthographicSize()
    {
        if (_cam != null) _cam.orthographicSize = viewRadiusMeters;
    }

    /// <summary>Runtime ortho override (e.g. minimap expand zoom). Does not change serialized <see cref="viewRadiusMeters"/>.</summary>
    public void SetRuntimeOrthographicSize(float meters)
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        if (_cam != null) _cam.orthographicSize = Mathf.Max(0.5f, meters);
    }

    /// <summary>Re-apply inspector <see cref="viewRadiusMeters"/> to the camera after a temporary runtime size.</summary>
    public void RestoreInspectorViewRadius()
    {
        ApplyOrthographicSize();
    }

    public float InspectorViewRadiusMeters => viewRadiusMeters;

    private Transform ResolveFollowTransform()
    {
        if (followCameraOverride != null) return followCameraOverride.transform;

        if (_gps == null) _gps = FindFirstObjectByType<SimpleGPSTracker>();
        if (_gps != null && _gps.ArCamera != null) return _gps.ArCamera.transform;

        Camera main = Camera.main;
        return main != null ? main.transform : null;
    }
}
