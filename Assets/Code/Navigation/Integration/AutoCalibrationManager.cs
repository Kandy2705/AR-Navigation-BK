using UnityEngine;

/// <summary>
/// Phase A2 — Auto-detect snap: tự hiệu chỉnh khi user đi NGANG 1 điểm đã khảo sát (TargetAnchor),
/// KHÔNG cần bấm gì. Mỗi lần qua 1 anchor = reset độ chính xác về ~1-2m.
///
/// Cơ chế:
///   - Theo dõi vị trí AR camera (user) so với các TargetAnchor trong scene
///   - Khi vào trong proximityRadius + GPS đủ tốt + qua cooldown + chưa snap anchor này gần đây
///     → gọi gpsTracker.CalibrateAtSurveyedPoint(anchor.lat, anchor.lon, snap:true)
///   - Thông báo qua HUD toast
///
/// Lưu ý độ tin cậy:
///   - Trước lần snap ĐẦU: proximity dựa GPS (±) nên có thể snap khi thực tế còn cách vài mét.
///     Để chính xác nhất, nên snap thủ công lần đầu tại điểm đã đánh dấu (long-press), rồi để
///     auto lo các anchor sau.
///   - Sau lần snap đầu: VIO chính xác → detect các anchor tiếp theo tin cậy hơn nhiều.
/// </summary>
public class AutoCalibrationManager : MonoBehaviour
{
    [Header("References (auto-resolve nếu để trống)")]
    [SerializeField] private SimpleGPSTracker gpsTracker;
    [SerializeField] private Camera userCamera;

    [Header("Bật/tắt")]
    [Tooltip("BẬT mặc định với safety gates strict (proximity 3m, accuracy ≤5m). " +
             "Khi user đi gần POI biết trước → tự hiệu chỉnh bias. Tắt nếu khu GPS xấu.")]
    [SerializeField] private bool autoSnapEnabled = true;

    [Header("Trigger gates — strict để tránh snap nhầm")]
    [Tooltip("Bán kính (m) quanh anchor để kích hoạt auto-snap. Strict = ít false-snap.")]
    [SerializeField] private float proximityRadiusMeters = 3f;
    [Tooltip("Chỉ auto-snap khi GPS accuracy <= mức này (m). Strict = chỉ snap khi GPS tốt.")]
    [SerializeField] private float maxGpsAccuracyForSnap = 5f;
    [Tooltip("Chỉ auto-snap khi user gần như đứng yên (≤ m/s) — tránh snap khi đi qua nhanh.")]
    [SerializeField] private float maxUserSpeedForSnap = 0.5f;
    [Tooltip("Phải rời anchor xa hơn mức này (m) mới cho phép snap LẠI chính anchor đó.")]
    [SerializeField] private float rearmDistanceMeters = 30f;
    [Tooltip("Tối thiểu giây giữa 2 lần snap (chống spam).")]
    [SerializeField] private float minSecondsBetweenSnaps = 15f;
    [Tooltip("Giây giữa mỗi lần kiểm tra proximity (tiết kiệm CPU).")]
    [SerializeField] private float checkInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private float _nextCheckTime;
    private float _lastSnapTime = -999f;
    private TargetAnchor _lastSnapped;
    private bool _rearmed = true;
    private Vector3 _lastUserPosition;
    private float _lastUserPositionTime = -1f;

    private void Awake()
    {
        if (gpsTracker == null)
            gpsTracker = FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (!autoSnapEnabled) return;   // mặc định tắt — GPS auto-snap không tin được gần nhà
        if (gpsTracker == null) return;
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + checkInterval;

        if (!gpsTracker.HasLocationFix) return;

        if (userCamera == null)
            userCamera = gpsTracker.ArCamera != null ? gpsTracker.ArCamera : Camera.main;
        if (userCamera == null) return;

        // Tìm anchor gần nhất (re-find mỗi lần để bắt POI spawn động từ PoiSpawner)
        TargetAnchor[] anchors = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None);
        if (anchors == null || anchors.Length == 0) return;

        Vector3 user = userCamera.transform.position;
        Vector2 userXZ = new Vector2(user.x, user.z);

        TargetAnchor nearest = null;
        float nearestDist = float.MaxValue;
        foreach (TargetAnchor a in anchors)
        {
            if (a == null) continue;
            Vector2 aXZ = new Vector2(a.transform.position.x, a.transform.position.z);
            float d = Vector2.Distance(userXZ, aXZ);
            if (d < nearestDist) { nearestDist = d; nearest = a; }
        }
        if (nearest == null) return;

        // Re-arm: rời anchor vừa snap đủ xa → cho phép snap lại nó lần sau
        if (_lastSnapped != null && nearest == _lastSnapped && nearestDist > rearmDistanceMeters)
            _rearmed = true;

        // Velocity check: tính tốc độ user dựa trên 2 frame cách nhau
        float userSpeed = 0f;
        if (_lastUserPositionTime > 0f)
        {
            float dt = Time.time - _lastUserPositionTime;
            if (dt > 0f) userSpeed = Vector3.Distance(user, _lastUserPosition) / dt;
        }
        _lastUserPosition = user;
        _lastUserPositionTime = Time.time;

        bool gpsOk      = gpsTracker.CurrentHorizontalAccuracy <= maxGpsAccuracyForSnap
                          && gpsTracker.CurrentHorizontalAccuracy > 0f;
        bool cooldownOk = Time.time - _lastSnapTime >= minSecondsBetweenSnaps;
        bool anchorOk   = nearest != _lastSnapped || _rearmed;
        bool speedOk    = userSpeed <= maxUserSpeedForSnap;

        if (nearestDist <= proximityRadiusMeters && gpsOk && cooldownOk && anchorOk && speedOk)
            DoSnap(nearest, anchors);
        else if (verboseLog && nearestDist <= proximityRadiusMeters)
        {
            if (!gpsOk)
                Debug.Log($"[AutoCalibration] Gần {nearest.TargetName} ({nearestDist:F1}m) nhưng GPS yếu " +
                          $"(±{gpsTracker.CurrentHorizontalAccuracy:F0}m > {maxGpsAccuracyForSnap:F0}m).");
            else if (!speedOk)
                Debug.Log($"[AutoCalibration] Gần {nearest.TargetName} nhưng user đang đi nhanh " +
                          $"({userSpeed:F1} m/s > {maxUserSpeedForSnap:F1}) — đứng yên 1s để snap.");
        }
    }

    private void DoSnap(TargetAnchor anchor, TargetAnchor[] allAnchors)
    {
        if (!gpsTracker.CalibrateAtSurveyedPoint(anchor.targetLat, anchor.targetLon, snapToSurveyedPoint: true))
            return;

        _lastSnapTime = Time.time;
        _lastSnapped = anchor;
        _rearmed = false;

        // Recalculate tất cả anchor về đúng vị trí sau khi gốc đổi
        foreach (TargetAnchor a in allAnchors)
            if (a != null) a.Recalculate();

        var hud = FindFirstObjectByType<MobileNavigationHUD>();
        if (hud != null) hud.ShowToast($"✓ Đã hiệu chỉnh tại {anchor.TargetName}");

        if (verboseLog)
            Debug.Log($"[AutoCalibration] Auto-snap tại {anchor.TargetName} " +
                      $"(GPS ±{gpsTracker.CurrentHorizontalAccuracy:F0}m).");
    }
}
