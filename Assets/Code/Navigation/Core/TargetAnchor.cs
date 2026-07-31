using UnityEngine;

/// <summary>
/// Neo một GameObject vào tọa độ GPS cố định bằng cách chuyển đổi lat/lon sang
/// vị trí Unity world space. Y-axis được giữ nguyên (không dùng altitude GPS).
///
/// Luồng hiển thị:
///   Awake()       → ẩn toàn bộ renderer (tránh hiện nhầm ở vị trí sai trước khi GPS sẵn sàng)
///   Recalculate() → tính vị trí từ GPS, đặt transform, rồi mới bật renderer lại
///
/// Khóa world (lock): Sau khi Recalculate đặt được vị trí, mỗi khung cố định lại
/// transform trong world để không script/physics làm neo trôi trong Unity (Neo vẫn có thể
/// không khớp thực tế nếu GPS người dùng sai — khóa không sửa vấn đề đó).
/// </summary>
public class TargetAnchor : MonoBehaviour
{
    /// <summary>
    /// BẬT: chỉ POI là <see cref="CurrentSelectedDestination"/> mới hiện, các POI khác ẩn.
    /// TẮT: tất cả POI hiện theo logic distance/occlusion thường.
    /// MobileNavigationHUD.SelectTarget() bật flag này khi user chọn 1 điểm đích.
    /// </summary>
    public static bool OnlyShowSelectedDestination = true;

    /// <summary>POI hiện đang được user chọn làm đích. Null = chưa chọn (mọi POI hiện).</summary>
    public static TargetAnchor CurrentSelectedDestination = null;

    [Header("Tọa độ GPS của điểm đến")]
    public string displayName;

    [Tooltip("Vĩ độ (latitude) theo độ thập phân, hệ WGS84.")]
    public double targetLat;

    [Tooltip("Kinh độ (longitude) theo độ thập phân, hệ WGS84.")]
    public double targetLon;

    [Header("Hiển thị theo khoảng cách")]
    [Tooltip("Ẩn vật thể khi người dùng cách xa hơn mức này (mét). 0 = luôn hiện.")]
    [SerializeField] private float maxVisibilityMeters = 80f;

    [Header("Occlusion — ẩn khi bị tòa nhà che")]
    [Tooltip("Bật: raycast từ camera tới POI; nếu trúng occluder (cube tòa nhà) TRƯỚC khi tới POI → ẩn. " +
             "Cube tòa nhà phải có Collider và nằm trong Occluder Mask. Trên device cube vô hình nhưng vẫn che.")]
    [SerializeField] private bool hideWhenOccluded = false;
    [Tooltip("Layer của các cube tòa nhà (occluder). Set đúng layer chứa Buildings/Walls.")]
    [SerializeField] private LayerMask occluderMask = ~0;
    [Tooltip("Chiều cao điểm ngắm trên POI khi raycast (m) — ngắm giữa capsule thay vì gốc, tránh bị tường thấp che oan.")]
    [SerializeField] private float occlusionProbeHeight = 1f;
    [Tooltip("Đệm (m) trừ vào khoảng cách raycast — tránh raycast tự trúng collider của chính POI.")]
    [SerializeField] private float occlusionPadding = 0.5f;

    [Header("Ổn định vị trí trong Unity")]
    [Tooltip("Sau Recalculate(), giữ transform.position world cố định mỗi frame (tránh bị kéo trôi).")]
    [SerializeField] private bool lockWorldPositionAfterSolve = true;

    // Tên hiển thị: dùng displayName nếu có, ngược lại dùng tên GameObject
    public string TargetName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;

    private bool _positionReady;   // true sau khi Recalculate() đã đặt đúng vị trí GPS
    private Camera _mainCamera;
    private Vector3 _lockedWorldPosition;
    private bool _hasWorldLock;

    void Awake()
    {
        // Ẩn object ngay từ đầu để tránh người dùng thấy nó ở vị trí sai
        // (trước khi GPS cấp fix và Recalculate() tính được tọa độ đúng).
        // GPSStartupOverlay sẽ gọi Recalculate() → vật thể sẽ tự hiện lại đúng chỗ.
        SetVisible(false);
    }

    void Update()
    {
        if (!_positionReady) return;

        // Filter theo selection: nếu user đã chọn 1 đích, các POI khác ẩn hoàn toàn.
        if (OnlyShowSelectedDestination && CurrentSelectedDestination != null && CurrentSelectedDestination != this)
        {
            SetVisible(false);
            return;
        }

        // Không feature nào bật → giữ behavior cũ (luôn hiện sau Recalculate)
        if (maxVisibilityMeters <= 0f && !hideWhenOccluded) return;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        // 1. Visibility theo khoảng cách (XZ, bỏ chiều cao để tránh lỗi mặt đất gồ ghề)
        bool withinDistance = true;
        if (maxVisibilityMeters > 0f)
        {
            Vector2 playerXZ = new Vector2(_mainCamera.transform.position.x, _mainCamera.transform.position.z);
            Vector2 anchorXZ = new Vector2(transform.position.x, transform.position.z);
            withinDistance = Vector2.Distance(playerXZ, anchorXZ) <= maxVisibilityMeters;
        }

        // 2. Occlusion: bị tòa nhà che → ẩn
        bool occluded = hideWhenOccluded && IsOccludedByBuilding(_mainCamera);

        SetVisible(withinDistance && !occluded);
    }

    /// <summary>
    /// Raycast từ camera tới POI (ngắm giữa capsule). Nếu trúng occluder (cube tòa nhà) trước khi
    /// tới POI → POI đang bị che → trả về true. Cube vô hình trên device vẫn chặn nhờ Collider.
    /// </summary>
    private bool IsOccludedByBuilding(Camera cam)
    {
        Vector3 camPos = cam.transform.position;
        Vector3 probe = transform.position + Vector3.up * occlusionProbeHeight;
        Vector3 toPoi = probe - camPos;
        float dist = toPoi.magnitude;
        if (dist <= occlusionPadding) return false;

        return Physics.Raycast(
            camPos,
            toPoi.normalized,
            dist - occlusionPadding,
            occluderMask,
            QueryTriggerInteraction.Ignore);
    }

    void LateUpdate()
    {
        if (!lockWorldPositionAfterSolve || !_hasWorldLock)
            return;
        transform.position = _lockedWorldPosition;
    }

    void Start()
    {
        // Kiểm tra xem GPSStartupOverlay có đang chạy không.
        // Nếu CÓ: chỉ tính vị trí, KHÔNG hiện — để Overlay kiểm soát thời điểm hiện.
        // Nếu KHÔNG (Editor, scene không có overlay): tính vị trí VÀ hiện luôn.
        bool overlayPresent = Object.FindFirstObjectByType<GPSStartupOverlay>() != null;

        if (overlayPresent)
        {
            // Tính vị trí nhưng giữ ẩn — Overlay sẽ gọi Recalculate() để reveal
            MapOrigin mapOrigin = MapOrigin.FindPrimary();
            if (mapOrigin != null)
            {
                Vector3 gpsPos = mapOrigin.GetUnityPositionFromGPS(targetLat, targetLon);
                transform.position = new Vector3(gpsPos.x, transform.position.y, gpsPos.z);
            }
        }
        else
        {
            // Không có overlay (Editor / scene khác): hiện luôn
            Recalculate();
        }
    }

    /// <summary>
    /// Tính lại vị trí world space từ tọa độ GPS đã lưu và đặt transform.
    /// Gọi lại hàm này khi MapOrigin thay đổi hoặc sau khi calibrate GPS.
    /// Sau khi tính xong, renderer sẽ được bật lại để vật thể hiển thị đúng chỗ.
    /// </summary>
    [ContextMenu("Recalculate Position from GPS")]
    public void Recalculate()
    {
        MapOrigin mapOrigin = MapOrigin.FindPrimary();
        if (mapOrigin == null)
        {
            Debug.LogError($"[TargetAnchor] '{gameObject.name}': Không tìm thấy MapOrigin trong scene.");
            return;
        }

        // Chuyển tọa độ GPS → vị trí Unity world (East, 0, North)
        Vector3 gpsPos = mapOrigin.GetUnityPositionFromGPS(targetLat, targetLon);

        // Chỉ cập nhật XZ, giữ nguyên Y (chiều cao đã thiết kế trong Editor)
        transform.position = new Vector3(gpsPos.x, transform.position.y, gpsPos.z);

        // Đánh dấu vị trí đã sẵn sàng — Update() sẽ bắt đầu kiểm tra khoảng cách
        _positionReady = true;

        // Bật renderer sau khi đã đặt đúng vị trí → tránh flicker ở vị trí sai
        // (Update() sẽ tự ẩn lại nếu người dùng đang ở quá xa)
        SetVisible(true);

        if (lockWorldPositionAfterSolve)
        {
            _lockedWorldPosition = transform.position;
            _hasWorldLock = true;
        }

        Debug.Log($"[TargetAnchor] '{TargetName}' đặt tại world ({gpsPos.x:F2}, {transform.position.y:F2}, {gpsPos.z:F2}) " +
                  $"từ GPS ({targetLat:F7}, {targetLon:F7})");

        // #region agent log — DEBUG 40cacb — Log B: Des world position + GPS input (tests H.B H.E)
        try {
            long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Tính lại ENU thô để kiểm tra double→float precision loss
            double enuE = gpsPos.x; // đây là float đã cast từ double ENU.e
            string line = "{\"sessionId\":\"40cacb\",\"timestamp\":" + ts +
                ",\"location\":\"TargetAnchor.cs:Recalculate\",\"hypothesisId\":\"B_E\"" +
                ",\"message\":\"DES_POSITION_CALCULATED\"" +
                ",\"data\":{" +
                "\"name\":\"" + TargetName + "\"" +
                ",\"inputLat\":" + targetLat.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"inputLon\":" + targetLon.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"worldX\":" + gpsPos.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"worldZ\":" + gpsPos.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"originLat\":" + mapOrigin.originLat.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"originLon\":" + mapOrigin.originLon.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                "}}\n";
            string path = System.IO.Path.Combine(Application.persistentDataPath, "debug-40cacb.log");
            System.IO.File.AppendAllText(path, line);
            Debug.Log("[DBG40cacb-B] DES=" + TargetName +
                " lat=" + targetLat.ToString("F8") + " lon=" + targetLon.ToString("F8") +
                " worldX=" + gpsPos.x.ToString("F3") + " worldZ=" + gpsPos.z.ToString("F3"));
        } catch (System.Exception ex) { Debug.LogWarning("[DBG40cacb] LogB failed: " + ex.Message); }
        // #endregion
    }

    /// <summary>
    /// Bật hoặc tắt tất cả Renderer trong object này và các con của nó.
    /// Dùng để ẩn trước khi GPS sẵn sàng, rồi hiện lại sau khi vị trí đã được tính đúng.
    /// </summary>
    public void SetVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Vẽ đường thẳng đứng và vòng tròn để xem vị trí anchor trong Scene View
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Vector3 pos = transform.position;
        Gizmos.DrawLine(pos + Vector3.up * 0.1f, pos + Vector3.up * 3f);
        Gizmos.DrawWireSphere(pos, 0.4f);

        // Hiện tọa độ GPS dưới dạng label trong Scene View
        UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
        string label = $"{TargetName}\nLat: {targetLat:F6}\nLon: {targetLon:F6}";
        UnityEditor.Handles.Label(pos + Vector3.up * 3.2f, label);
    }
#endif
}
