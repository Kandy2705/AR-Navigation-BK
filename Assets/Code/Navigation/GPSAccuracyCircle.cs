using UnityEngine;

/// <summary>
/// Draws a world-space ring around the user showing the GPS horizontal accuracy radius.
/// Ring center follows the AR camera in real-time (not GPS-lag).
/// Color codes: green (≤10m) → yellow (≤25m) → red (>25m).
/// Attach to any GameObject that has a LineRenderer, or let MobileNavigationHUD create it.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class GPSAccuracyCircle : MonoBehaviour
{
    [Header("References")]
    public SimpleGPSTracker gpsTracker;
    [Tooltip("AR Camera. If not set, Camera.main is used automatically.")]
    public Camera arCamera;

    [Header("Appearance")]
    [SerializeField] private int   segments  = 64;
    [SerializeField] private float lineWidth = 0.06f;
    [Tooltip("Maximum ring radius in meters regardless of GPS accuracy. Keeps ring small and unobtrusive.")]
    [SerializeField] private float maxRingRadius = 0.5f;
    [SerializeField] private float groundY   = 0.05f;

    private LineRenderer _line;
    private float _lastDrawnAccuracy = -1f;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace  = true;
        _line.loop           = true;
        _line.startWidth     = lineWidth;
        _line.endWidth       = lineWidth;
        _line.positionCount  = segments;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows    = false;

        // Use the built-in Sprites/Default material so it renders without lighting
        _line.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Start()
    {
        if (gpsTracker == null)
            gpsTracker = Object.FindFirstObjectByType<SimpleGPSTracker>();

        if (arCamera == null)
            arCamera = Camera.main;
    }

    void Update()
    {
        if (gpsTracker == null || !gpsTracker.HasLocationFix)
        {
            _line.enabled = false;
            return;
        }

        _line.enabled = true;

        float acc = gpsTracker.CurrentHorizontalAccuracy;

        // Redraw ring color only when accuracy changes noticeably
        if (Mathf.Abs(acc - _lastDrawnAccuracy) > 0.5f)
        {
            _lastDrawnAccuracy = acc;
            ApplyColor(acc);
        }

        // Center the ring on the camera every frame — camera is real-time IMU tracked,
        // so the ring follows the user instantly instead of lagging behind GPS updates.
        // Cap radius so ring stays small and unobtrusive regardless of GPS accuracy.
        PlaceRing(Mathf.Min(acc, maxRingRadius));
    }

    private void ApplyColor(float acc)
    {
        Color c;
        if      (acc <= 5f)  c = new Color(0.20f, 1.00f, 0.35f, 0.45f);   // green
        else if (acc <= 12f) c = new Color(1.00f, 0.85f, 0.10f, 0.45f);   // yellow
        else                 c = new Color(1.00f, 0.28f, 0.18f, 0.45f);   // red

        _line.startColor = c;
        _line.endColor   = c;
    }

    private void PlaceRing(float radiusMeters)
    {
        // Use camera XZ position so ring follows the user in real-time.
        // Fall back to GPS position if camera reference is missing.
        Vector3 center = arCamera != null
            ? new Vector3(arCamera.transform.position.x, groundY, arCamera.transform.position.z)
            : new Vector3(gpsTracker.SmoothedWorldPosition.x, groundY, gpsTracker.SmoothedWorldPosition.z);

        float step = 2f * Mathf.PI / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            _line.SetPosition(i, new Vector3(
                center.x + radiusMeters * Mathf.Cos(angle),
                center.y,
                center.z + radiusMeters * Mathf.Sin(angle)));
        }
    }
}
