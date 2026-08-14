using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tracks device GPS and moves the XR Origin to match the user's real-world position.
///
/// Architecture:
///   GPS layer  — runs only when hardware provides a new fix (timestamp guard).
///                Filters bad accuracy, rejects implausible jumps, applies calibration offset,
///                optionally snaps XZ onto NavMesh (map matching), then updates _gpsTargetPosition.
///   Render layer — runs every frame. Smoothly lerps XR Origin toward _gpsTargetPosition so
///                  the capsule moves fluidly between GPS fixes (~1–4 s apart on Android).
///
/// Calibration:
///   Call CalibrateAtOrigin() while standing at the physical MapOrigin point to measure and
///   store the GPS systematic bias. The offset is persisted in PlayerPrefs so it survives
///   app restarts.
/// </summary>
public class SimpleGPSTracker : MonoBehaviour
{
    [Header("Tracked Object")]
    public Transform xrOrigin;

    [Header("North Alignment (Compass)")]
    [Tooltip("AR Camera (has TrackedPoseDriver). Drag 'Main Camera' here.\n" +
             "Used once at startup to rotate XR Origin so Unity +Z = geographic North,\n" +
             "ensuring GPS-placed objects appear in the correct real-world direction.")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Number of compass samples to average before applying North correction. More = more stable but slower startup.")]
    [SerializeField] private int compassSampleCount = 6;
    [Tooltip("Seconds between each compass sample.")]
    [SerializeField] private float compassSampleInterval = 0.3f;
    [Tooltip("Reject compass samples with headingAccuracy worse than this (degrees). " +
             "Samples reporting acc <= 0 are accepted (device không hỗ trợ field này). " +
             "Lower = stricter. 15° cân bằng giữa loại nhiễu và đủ samples.")]
    [SerializeField] private float maxAcceptableHeadingAccuracy = 15f;

    [Header("North fine-tune / lock")]
    [Tooltip("Degrees added to compass-derived yaw (after avgHeading - cameraYaw). Use ~180 if the map is flipped vs real north. Loaded/saved via PlayerPrefs when options below are on.")]
    [SerializeField] private float extraNorthYawOffsetDegrees = 0f;
    [Tooltip("On startup, load extraNorthYawOffsetDegrees from PlayerPrefs (key gps_extra_north_yaw).")]
    [SerializeField] private bool loadPersistedExtraNorthYaw = true;
    [Tooltip("After alignment, write extraNorthYawOffsetDegrees to PlayerPrefs (call PersistExtraNorthYawOffsetFromInspector context menu, or use API from UI).")]
    [SerializeField] private bool autoSaveExtraNorthYawAfterAlign = false;
    [Tooltip("Each frame, force XR Origin world Y to the post-alignment value so drift scripts cannot rotate the rig. Recommended for north-up GPSMapPlane; disable if another system must drive origin yaw. Set script execution order AFTER AR if heading still slips.")]
    [SerializeField] private bool lockXrOriginYawAfterNorthAlign = true;

    [Header("GPS Filtering")]
    [Tooltip("Reject GPS fixes with accuracy worse than this (meters).")]
    [SerializeField] private float accuracyThresholdMeters = 5f;
    [Tooltip("Reject position jumps larger than this distance (meters) in a single GPS fix. Prevents teleporting on glitches.")]
    [SerializeField] private float jumpRejectThresholdMeters = 50f;

    [Header("Precision (no VPS)")]
    [Tooltip("Max GPS accuracy allowed when calling CalibrateAtOrigin / CalibrateAtSurveyedPoint.")]
    [SerializeField] private float calibrateMaxAccuracyMeters = 5f;
    [Tooltip("Lưu calibration offset qua các lần mở app (PlayerPrefs). " +
             "TẮT (khuyến nghị): mỗi lần mở app bắt đầu sạch, AutoCalibration tự snap khi đi gần POI " +
             "→ không bị ám bởi offset snap sai từ session trước (Pokemon-GO style, zero thao tác tay). " +
             "BẬT: giữ offset — chỉ dùng nếu bạn calibrate 1 điểm chuẩn và muốn nhớ bias thiết bị.")]
    [SerializeField] private bool persistCalibrationOffset = false;
    [Tooltip("Average several fixes before first lock while the device is nearly stationary.")]
    [SerializeField] private bool averageFirstFixWhileStationary = true;
    [SerializeField] private int firstFixAverageMinSamples = 4;
    [SerializeField] private float firstFixAverageMaxWaitSeconds = 15f;
    [Tooltip("Max spread (m) between averaged samples; wider spread waits for more samples.")]
    [SerializeField] private float firstFixMaxSampleSpreadMeters = 2.5f;

    [Header("Navigation path gate")]
    [Tooltip("Like SetNavigation: hide navigation when smoothed map XZ is farther than this from world origin (0,0). 0 = disabled.")]
    [SerializeField] private float maxNavigationDistanceFromMapOriginMeters = 250f;

    [Header("Render Smoothing")]
    [Tooltip("How fast XR Origin lerps toward the GPS target every frame. Higher = more responsive, lower = smoother. Recommended: 4–8.")]
    [SerializeField] private float smoothSpeed = 5f;
    [Tooltip("Sau khi calibrate tại anchor (CalibrateAtSurveyedPoint, snap=true): giảm smoothSpeed " +
             "xuống mức này để VIO (AR tracking) dẫn dắt chuyển động thay vì GPS noisy. " +
             "0.3 = GPS chỉ correct drift rất chậm; 0 = đóng băng hoàn toàn (chỉ VIO). Recommended: 0.3.")]
    [SerializeField] private float postCalibrationSmoothSpeed = 0f;
    [Tooltip("MA GIÁO: vào VIO mode NGAY sau fix GPS đầu (không cần snap POI). VIO dẫn chuyển động → " +
             "MƯỢT + chính xác relative như Editor, hết giật theo GPS. Trade-off: vị trí TUYỆT ĐỐI vẫn " +
             "lệch theo GPS first-fix (~±10m) cho tới khi đi ngang POI auto-snap sửa. Nhưng chuyển động " +
             "mượt ngay từ đầu. Khuyến nghị BẬT.")]
    [SerializeField] private bool useVioModeFromStart = true;

    [Header("NavMesh map matching")]
    [Tooltip("Snap each GPS map XZ onto the nearest walkable NavMesh point so the user avatar stays on bakeable terrain (fewer dips into obstacle volumes). Uses raw GPS if no hit.")]
    [SerializeField] private bool snapGpsPositionsToNavMesh = true;
    [Tooltip("NavMesh.SamplePosition search radius (m) around the GPS map point.")]
    [SerializeField] private float navMeshSnapSampleRadiusMeters = 15f;
    [SerializeField] private bool logNavMeshSnapFallback;

    [Header("Rolling average filter (optional — chỉ dùng khi VIO OFF)")]
    [Tooltip("Average N readings gần nhất với accuracy weighting. " +
             "TẮT mặc định vì VIO smoothing đã đủ. Bật chỉ khi disable VIO + user thường đứng yên.")]
    [SerializeField] private bool useRollingAverageFilter = false;
    [Tooltip("Số reading giữ trong buffer rolling. Lớn = mượt + lag khi đi. Khuyến nghị 5-10 cho mobile.")]
    [SerializeField] [Range(3, 50)] private int rollingAverageSize = 8;

    // Rolling buffer cho continuous optimization
    private readonly System.Collections.Generic.Queue<(Vector3 pos, float accuracy)> _rollingBuffer
        = new System.Collections.Generic.Queue<(Vector3, float)>();

    // PlayerPrefs keys for persisted calibration offset
    private const string PrefOffsetLat = "gps_offset_lat";
    private const string PrefOffsetLon = "gps_offset_lon";
    private const string PrefExtraNorthYaw = "gps_extra_north_yaw";

    /// <summary>
    /// When true, all XR Origin position/rotation updates are suppressed.
    /// Set by HybridModeController when entering Indoor mode so Multiset VPS
    /// can drive the XR Origin without GPS interference.
    /// </summary>
    [HideInInspector] public bool freezeXROriginUpdate = false;

    [Tooltip("Nếu GPSMarker legacy đang điều khiển cùng XR Origin, SimpleGPSTracker chỉ đọc GPS và không ghi transform.")]
    [SerializeField] private bool yieldRigControlToLegacyGpsMarker = true;
    private bool _legacyGpsMarkerOwnsRig;
    private bool RigWritesSuppressed => freezeXROriginUpdate || _legacyGpsMarkerOwnsRig;

    // --- GPS state ---
    [SerializeField] private MapOrigin mapOrigin;
    private bool isGpsReady;
    private double currentLatitude;
    private double currentLongitude;
    private float currentHorizontalAccuracy = -1f;
    private double lastTimestamp = -1;
    private float _lastAcceptedFixUnscaledTime = -999f;
    private double _lastAcceptedFixTimestamp = -1d;

    // --- Calibration offset (systematic GPS bias correction) ---
    private double _offsetLat;
    private double _offsetLon;

    // --- North alignment (compass-based) ---
    private bool _isNorthAligned;

    // One-shot: sau fix đầu + căn Bắc, dịch XR Origin theo phương ngang để camera trùng XZ với GPS.
    // Tránh lỗi “Des ở chỗ khác mỗi lần mở app” do offset local của camera so với pivot XR Origin.
    private bool _pendingInitialCameraGpsAlign;

    // --- Render-layer state ---
    private bool _hasGpsTarget;
    private bool _hasFirstValidFix;
    private Vector3 _gpsTargetPosition;
    private Vector3 _smoothedPosition;

    // --- Anchor calibration (Option A) ---
    // _activeSmoothSpeed = smoothSpeed bình thường; tụt xuống postCalibrationSmoothSpeed sau khi snap tại anchor.
    private float _activeSmoothSpeed = -1f;
    private bool _hasCalibratedAtAnchor;

    /// <summary>True sau khi user đã calibrate tại 1 anchor (snap XR Origin về điểm khảo sát).</summary>
    public bool HasCalibratedAtAnchor => _hasCalibratedAtAnchor;

    private bool _lastFixRejectedAsJump;
    private float _lastRejectedJumpMeters = -1f;

    /// <summary>World-space yaw enforced on <see cref="xrOrigin"/> when <see cref="lockXrOriginYawAfterNorthAlign"/> is on.</summary>
    private float _lockedNorthYaw;

    private bool _hasLockedNorthYaw;

    private bool _collectingFirstFixAverage;
    private int _firstFixSampleCount;
    private double _avgLatSum;
    private double _avgLonSum;
    private double _avgWeightSum;
    private float _firstFixCollectStartTime;
    private readonly List<Vector3> _firstFixSamplePositions = new List<Vector3>(8);

    private Vector3 _sessionRefinementOffset;

    // --- Public properties ---
    public bool HasLocationFix => isGpsReady && Input.location.status == LocationServiceStatus.Running;
    public double CurrentLatitude  => currentLatitude;
    public double CurrentLongitude => currentLongitude;
    public float  CurrentHorizontalAccuracy => currentHorizontalAccuracy;
    public double LastFixTimestamp => _lastAcceptedFixTimestamp;
    public float FixAgeSeconds => _lastAcceptedFixUnscaledTime < -100f
        ? float.PositiveInfinity
        : Mathf.Max(0f, Time.unscaledTime - _lastAcceptedFixUnscaledTime);
    public bool HasHeading => _isNorthAligned && arCamera != null;
    public float HeadingDegrees => arCamera != null
        ? arCamera.transform.eulerAngles.y
        : NorthCorrectionDeg;
    public LocationServiceStatus CurrentStatus => Input.location.status;
    /// <summary>True nếu app đang chạy ở chế độ VIO (sau snap với useVioModeFromStart = true).</summary>
    public bool IsVioModeActive => _hasCalibratedAtAnchor && useVioModeFromStart;
    /// <summary>GPS-smoothed XZ plus optional proximity refinement toward Des.</summary>
    public Vector3 SmoothedWorldPosition => _smoothedPosition + _sessionRefinementOffset;

    /// <summary>Smoothed position from GPS only (no Des proximity refinement).</summary>
    public Vector3 GpsOnlySmoothedPosition => _smoothedPosition;

    public bool HasCalibration => _offsetLat != 0.0 || _offsetLon != 0.0;

    public float CalibrateMaxAccuracyMeters => calibrateMaxAccuracyMeters;

    public bool IsCollectingFirstFixAverage => _collectingFirstFixAverage;

    /// True once compass correction has been applied (or skipped in Editor / on error).
    /// GPSStartupOverlay waits for this before revealing TargetAnchors.
    public bool IsNorthAligned => _isNorthAligned;

    /// Degrees applied to XR Origin.y to align Unity +Z with geographic North.
    public float NorthCorrectionDeg { get; private set; }

    /// <summary>AR / first-person camera (same as North alignment). Other systems can follow this instead of Camera.main.</summary>
    public Camera ArCamera => arCamera;

    /// <summary>Call after <see cref="HybridModeController"/> retags MainCamera on the outdoor rig (same behaviour as wiring in GPSMapPlane).</summary>
    public void RebindArCamera(Camera cam)
    {
        if (cam != null)
            arCamera = cam;
    }

    /// <summary>
    /// After tuning <see cref="extraNorthYawOffsetDegrees"/> at runtime (e.g. debug slider), persist for next app launch.
    /// </summary>
    public void PersistExtraNorthYawOffset()
    {
        PlayerPrefs.SetFloat(PrefExtraNorthYaw, extraNorthYawOffsetDegrees);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [ContextMenu("Save extra north yaw to PlayerPrefs")]
    private void PersistExtraNorthYawOffsetContextMenu()
    {
        PersistExtraNorthYawOffset();
        Debug.Log($"[SimpleGPSTracker] Saved {PrefExtraNorthYaw} = {extraNorthYawOffsetDegrees:F2}°");
    }
#endif

    // Trả về true khi đã nhận được fix GPS đầu tiên hợp lệ (đã qua bộ lọc accuracy).
    // GPSStartupOverlay dùng property này để biết khi nào GPS đã ổn định,
    // từ đó ẩn màn hình loading và cho phép Des1/Des2 hiện ra.
    public bool HasFirstFix => _hasFirstValidFix;

    /// <summary>The last attempted fix was discarded as an implausible jump (> jumpRejectThresholdMeters).</summary>
    public bool LastFixRejectedAsJump => _lastFixRejectedAsJump;

    /// <summary>Magnitude of rejected jump when <see cref="LastFixRejectedAsJump"/> is true; otherwise -1.</summary>
    public float LastRejectedJumpMeters => _lastRejectedJumpMeters;

    /// <summary>Horizontal distance from map origin plane (XZ at Unity origin).</summary>
    public float DistanceFromMapOriginXZ => new Vector2(_smoothedPosition.x, _smoothedPosition.z).magnitude;

    /// <summary>GPS+bearing sane enough for path rendering (Hybrid SetNavigation gate idea).</summary>
    public bool IsNavigationGpsHealthy
    {
        get
        {
            string ignored;
            return !TryGetPathNavigationBlock(out ignored);
        }
    }

    /// <summary>Returns true if path/navigation overlays should suppress route line; explains why (<paramref name="reason"/>).</summary>
    public bool TryGetPathNavigationBlock(out string reason)
    {
        if (mapOrigin == null)
        {
            reason = "no_origin";
            return true;
        }

        if (!HasLocationFix)
        {
            reason = "no_location_fix";
            return true;
        }

        if (!_hasFirstValidFix)
        {
            reason = "no_first_fix";
            return true;
        }

        if (!_isNorthAligned)
        {
            reason = "north_pending";
            return true;
        }

        if (_lastFixRejectedAsJump)
        {
            reason = "gps_jump";
            return true;
        }

        float maxDist = Mathf.Max(0f, maxNavigationDistanceFromMapOriginMeters);
        if (maxDist > 0f && DistanceFromMapOriginXZ > maxDist)
        {
            reason = "off_map_bounds";
            return true;
        }

        reason = null;
        return false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve: xrOrigin = transform của GO này (SimpleGPSTracker được đặt ON XR Origin)
        if (xrOrigin == null)
            xrOrigin = transform;

        // Auto-resolve: arCamera = Camera.main (HybridModeController sẽ tag đúng camera trước Start)
        if (arCamera == null)
            arCamera = Camera.main;

        if (loadPersistedExtraNorthYaw)
        {
            extraNorthYawOffsetDegrees = PlayerPrefs.GetFloat(PrefExtraNorthYaw, extraNorthYawOffsetDegrees);
        }
    }

    System.Collections.IEnumerator Start()
    {
        mapOrigin ??= MapOrigin.FindPrimary();
        if (mapOrigin == null)
        {
            Debug.LogError("[SimpleGPSTracker] MapOrigin not found in scene.");
            yield break;
        }

        if (yieldRigControlToLegacyGpsMarker)
        {
            foreach (GPSMarker marker in Object.FindObjectsByType<GPSMarker>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (marker == null || marker.xrOrigin == null) continue;
                if (marker.xrOrigin.transform != xrOrigin) continue;
                _legacyGpsMarkerOwnsRig = true;
                Debug.Log(
                    "[SimpleGPSTracker] GPSMarker đang sở hữu XR Origin; tracker chỉ cung cấp dữ liệu, không ghi transform.");
                break;
            }
        }

        if (persistCalibrationOffset)
        {
            // Restore persisted calibration offset
            _offsetLat = PlayerPrefs.GetFloat(PrefOffsetLat, 0f);
            _offsetLon = PlayerPrefs.GetFloat(PrefOffsetLon, 0f);
            if (HasCalibration)
                Debug.Log($"[SimpleGPSTracker] Loaded calibration offset: dLat={_offsetLat:F8} dLon={_offsetLon:F8}");
        }
        else
        {
            // Không persist → mỗi session bắt đầu sạch. Xóa offset cũ trong PlayerPrefs
            // (vd offset snap sai 32m từ lần trước) để không bị ám. AutoCalibration sẽ tự snap lại.
            _offsetLat = 0.0;
            _offsetLon = 0.0;
            PlayerPrefs.DeleteKey(PrefOffsetLat);
            PlayerPrefs.DeleteKey(PrefOffsetLon);
            Debug.Log("[SimpleGPSTracker] persistCalibrationOffset OFF — bắt đầu sạch, offset cũ đã xóa.");
        }

        // Start North alignment in parallel — compass warms up while GPS is initialising
        StartCoroutine(AlignNorthAsync());

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
            float permissionTimeout = 10f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation) &&
                   permissionTimeout > 0f)
            {
                permissionTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[SimpleGPSTracker] Location service is disabled by the user.");
            yield break;
        }

        // Nếu GpsBootstrap đã start service từ lúc app boot → skip để không reset cycle.
        // Chỉ Start() khi đang Stopped (lần đầu / sau permission grant muộn).
        if (Input.location.status == LocationServiceStatus.Stopped)
        {
            // desiredAccuracy=5 m, updateDistance=1 m — ask OS for high-quality fixes
            Input.location.Start(5f, 1f);
            Debug.Log("[SimpleGPSTracker] Started Input.location (no pre-warm).");
        }
        else
        {
            Debug.Log($"[SimpleGPSTracker] Pre-warmed by GpsBootstrap (status={Input.location.status}), skip Start().");
        }

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1f);
            maxWait--;
        }

        if (maxWait <= 0 || Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogWarning($"[SimpleGPSTracker] GPS failed to start. Status: {Input.location.status}");
            yield break;
        }

        isGpsReady = true;
        Debug.Log("[SimpleGPSTracker] GPS ready.");
    }

    void Update()
    {
        if (!HasLocationFix || mapOrigin == null || xrOrigin == null) return;

        // ── GPS LAYER: runs only when hardware provides a new fix ──────────────
        LocationInfo data = Input.location.lastData;
        if (data.timestamp > lastTimestamp)
        {
            lastTimestamp = data.timestamp;
            TryAcceptGpsFix(data);
        }

        // ── RENDER LAYER: runs every frame ─────────────────────────────────────
        if (!_hasGpsTarget || RigWritesSuppressed) return;

        // _activeSmoothSpeed = smoothSpeed bình thường, hoặc postCalibrationSmoothSpeed (chậm)
        // sau khi calibrate tại anchor → VIO dẫn dắt, GPS chỉ correct drift rất chậm.
        if (_activeSmoothSpeed < 0f) _activeSmoothSpeed = smoothSpeed;
        float t = _activeSmoothSpeed * Time.deltaTime;
        _smoothedPosition.x = Mathf.Lerp(_smoothedPosition.x, _gpsTargetPosition.x, t);
        _smoothedPosition.z = Mathf.Lerp(_smoothedPosition.z, _gpsTargetPosition.z, t);

        Vector3 display = SmoothedWorldPosition;
        xrOrigin.position = new Vector3(display.x, xrOrigin.position.y, display.z);
    }

    /// <summary>Applied by <see cref="NavigationProximityRefinement"/> near the active Des.</summary>
    public void SetSessionRefinementOffset(Vector3 worldOffsetXZ)
    {
        _sessionRefinementOffset = new Vector3(worldOffsetXZ.x, 0f, worldOffsetXZ.z);
    }

    public void ClearSessionRefinementOffset() => _sessionRefinementOffset = Vector3.zero;

    void LateUpdate()
    {
        // XR Origin được đặt theo GPS, nhưng camera là con của Origin + ARCore (offset local).
        // Nếu không chỉnh, điểm nhìn trên mặt phẳng ENU lệch mỗi phiên → Des cố định GPS trông “nhảy chỗ”.
        // Chỉ chạy một lần sau khi đã có fix đầu và đã xoay Bắc, để khớp camera XZ với _smoothedPosition.
        if (!RigWritesSuppressed && _pendingInitialCameraGpsAlign && _hasFirstValidFix && _isNorthAligned && xrOrigin != null && arCamera != null)
        {
            Vector3 display = SmoothedWorldPosition;
            Vector3 cam = arCamera.transform.position;
            Vector3 delta = new Vector3(display.x - cam.x, 0f, display.z - cam.z);
            if (delta.sqrMagnitude > 1e-8f)
            {
                xrOrigin.position += delta;
                Debug.Log($"[SimpleGPSTracker] Căn camera–GPS (mặt phẳng XZ) một lần: Δ({delta.x:F2}, {delta.z:F2}) m");
            }

            _pendingInitialCameraGpsAlign = false;
        }

        if (!RigWritesSuppressed)
            EnforceLockedNorthYawIfConfigured();
    }

    private void EnforceLockedNorthYawIfConfigured()
    {
        if (!lockXrOriginYawAfterNorthAlign || !_hasLockedNorthYaw || xrOrigin == null || !_isNorthAligned)
        {
            return;
        }

        Vector3 e = xrOrigin.eulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(e.y, _lockedNorthYaw)) <= 0.01f)
        {
            return;
        }

        xrOrigin.eulerAngles = new Vector3(e.x, _lockedNorthYaw, e.z);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GPS data processing
    // ──────────────────────────────────────────────────────────────────────────

    private void TryAcceptGpsFix(LocationInfo data)
    {
        // LUÔN cập nhật display values (HUD), kể cả khi reading bị reject ở bước sau —
        // user cần thấy GPS có hoạt động + accuracy thực để biết tình trạng.
        currentLatitude            = data.latitude;
        currentLongitude           = data.longitude;
        currentHorizontalAccuracy  = data.horizontalAccuracy;

        // 1. Accuracy filter — chỉ chặn việc xử lý fix cho tracking, không chặn display.
        if (data.horizontalAccuracy > accuracyThresholdMeters)
        {
            Debug.LogWarning($"[SimpleGPSTracker] Fix rejected for tracking — accuracy {data.horizontalAccuracy:F1} m > {accuracyThresholdMeters:F1} m threshold. (Display values updated.)");
            return;
        }

        // 2. Apply calibration offset to correct systematic GPS bias
        double calibratedLat = currentLatitude  - _offsetLat;
        double calibratedLon = currentLongitude - _offsetLon;

        Vector3 rawPos = mapOrigin.GetUnityPositionFromGPS(calibratedLat, calibratedLon);
        Vector3 mapPos = ApplyNavMeshSnapToGpsMapPosition(rawPos);

        // 3. First fix — optional stationary average for a stabler lock
        if (!_hasFirstValidFix)
        {
            if (averageFirstFixWhileStationary && TryAccumulateFirstFixAverage(data, rawPos))
                return;

            CommitFirstFix(mapPos);
            LogFirstFix(mapPos);
            return;
        }

        // 4. Jump rejection — discard implausible single-step teleports
        float jumpDist = new Vector2(mapPos.x - _gpsTargetPosition.x, mapPos.z - _gpsTargetPosition.z).magnitude;
        if (jumpDist > jumpRejectThresholdMeters)
        {
            _lastFixRejectedAsJump = true;
            _lastRejectedJumpMeters = jumpDist;
            Debug.LogWarning($"[SimpleGPSTracker] Jump rejected — {jumpDist:F1} m in one fix.");
            return;
        }

        // 5. Accept fix — update target; render layer will lerp toward it every frame
        _lastFixRejectedAsJump = false;
        _lastRejectedJumpMeters = -1f;

        // Rolling average filter: accuracy-weighted average của N reading gần nhất
        // → Position tự refine theo thời gian, giảm noise √N lần.
        if (useRollingAverageFilter)
        {
            _rollingBuffer.Enqueue((mapPos, data.horizontalAccuracy));
            while (_rollingBuffer.Count > rollingAverageSize)
                _rollingBuffer.Dequeue();

            _gpsTargetPosition = ComputeWeightedAverage(_rollingBuffer);
        }
        else
        {
            _gpsTargetPosition = mapPos;
        }
        MarkFixAccepted(data.timestamp);
    }

    /// <summary>
    /// Accuracy-weighted average: reading có accuracy tốt được weight cao hơn (1/acc²).
    /// Hiệu quả √N giảm noise: 15 readings × accuracy 17m → average ~4m noise.
    /// </summary>
    private static Vector3 ComputeWeightedAverage(System.Collections.Generic.Queue<(Vector3 pos, float accuracy)> buffer)
    {
        Vector3 sum = Vector3.zero;
        float totalWeight = 0f;
        foreach (var (pos, acc) in buffer)
        {
            float weight = acc > 0f ? 1f / (acc * acc) : 1f;
            sum += pos * weight;
            totalWeight += weight;
        }
        return totalWeight > 0f ? sum / totalWeight : Vector3.zero;
    }

    /// <summary>
    /// Projects GPS-derived map XZ onto the closest NavMesh point when enabled. Preserves Y from <paramref name="rawGpsMapPosition"/>.
    /// </summary>
    private Vector3 ApplyNavMeshSnapToGpsMapPosition(Vector3 rawGpsMapPosition)
    {
        // NavMesh.SamplePosition safely returns false when no NavMesh exists (Unity has no NavMesh.isValid on this API).
        if (!snapGpsPositionsToNavMesh)
            return rawGpsMapPosition;

        float preserveY = rawGpsMapPosition.y;
        float probeY = xrOrigin != null ? xrOrigin.position.y : preserveY;
        Vector3 probe = new Vector3(rawGpsMapPosition.x, probeY, rawGpsMapPosition.z);
        const int areaMask = NavMesh.AllAreas;

        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, navMeshSnapSampleRadiusMeters, areaMask))
            return new Vector3(hit.position.x, preserveY, hit.position.z);

        probe.y = 0f;
        if (NavMesh.SamplePosition(probe, out hit, navMeshSnapSampleRadiusMeters, areaMask))
            return new Vector3(hit.position.x, preserveY, hit.position.z);

        if (logNavMeshSnapFallback)
            Debug.LogWarning("[SimpleGPSTracker] NavMesh snap missed — using raw GPS map XZ.");

        return rawGpsMapPosition;
    }

    private bool TryAccumulateFirstFixAverage(LocationInfo data, Vector3 rawPos)
    {
        if (!_collectingFirstFixAverage)
        {
            _collectingFirstFixAverage = true;
            _firstFixCollectStartTime = Time.unscaledTime;
            _firstFixSampleCount = 0;
            _avgLatSum = 0;
            _avgLonSum = 0;
            _avgWeightSum = 0;
            _firstFixSamplePositions.Clear();
        }

        float weight = 1f / Mathf.Max(0.5f, data.horizontalAccuracy);
        _avgLatSum += data.latitude * weight;
        _avgLonSum += data.longitude * weight;
        _avgWeightSum += weight;
        _firstFixSampleCount++;
        _firstFixSamplePositions.Add(rawPos);

        bool timeout = Time.unscaledTime - _firstFixCollectStartTime >= firstFixAverageMaxWaitSeconds;
        bool enoughSamples = _firstFixSampleCount >= firstFixAverageMinSamples;
        bool spreadOk = true;

        if (_firstFixSamplePositions.Count >= 2)
        {
            float maxSpread = 0f;
            for (int i = 0; i < _firstFixSamplePositions.Count; i++)
            {
                for (int j = i + 1; j < _firstFixSamplePositions.Count; j++)
                {
                    float d = Vector3.Distance(_firstFixSamplePositions[i], _firstFixSamplePositions[j]);
                    if (d > maxSpread) maxSpread = d;
                }
            }

            spreadOk = maxSpread <= firstFixMaxSampleSpreadMeters;
        }

        if (!timeout && (!enoughSamples || !spreadOk))
        {
            Debug.Log($"[SimpleGPSTracker] Averaging first fix sample {_firstFixSampleCount}/{firstFixAverageMinSamples}...");
            return true;
        }

        if (_avgWeightSum <= 0f || _firstFixSampleCount == 0)
            return false;

        currentLatitude = _avgLatSum / _avgWeightSum;
        currentLongitude = _avgLonSum / _avgWeightSum;

        double calibratedLat = currentLatitude - _offsetLat;
        double calibratedLon = currentLongitude - _offsetLon;
        Vector3 averagedPos = mapOrigin.GetUnityPositionFromGPS(calibratedLat, calibratedLon);
        Vector3 mapPos = ApplyNavMeshSnapToGpsMapPosition(averagedPos);

        _collectingFirstFixAverage = false;
        _firstFixSamplePositions.Clear();

        CommitFirstFix(mapPos);
        Debug.Log($"[SimpleGPSTracker] First fix (averaged {_firstFixSampleCount} samples) — " +
                  $"Lat:{currentLatitude:F7} Lon:{currentLongitude:F7} Acc:{currentHorizontalAccuracy:F1} m");
        LogFirstFix(mapPos);
        return true;
    }

    private void CommitFirstFix(Vector3 rawPos)
    {
        _lastFixRejectedAsJump = false;
        _lastRejectedJumpMeters = -1f;
        _gpsTargetPosition = rawPos;
        _smoothedPosition = rawPos;
        _hasFirstValidFix = true;
        _hasGpsTarget = true;
        _pendingInitialCameraGpsAlign = true;
        _collectingFirstFixAverage = false;
        _firstFixSamplePositions.Clear();
        ClearSessionRefinementOffset();
        MarkFixAccepted(lastTimestamp);

        // MA GIÁO: vào VIO mode ngay → chuyển động mượt như Editor (VIO dẫn, GPS correct chậm).
        // Không set khi đã snap anchor (giữ smoothSpeed đã có từ snap).
        if (useVioModeFromStart && !_hasCalibratedAtAnchor)
            _activeSmoothSpeed = postCalibrationSmoothSpeed;
    }

    private void MarkFixAccepted(double timestamp)
    {
        _lastAcceptedFixTimestamp = timestamp;
        _lastAcceptedFixUnscaledTime = Time.unscaledTime;
    }

    private void LogFirstFix(Vector3 rawPos)
    {
        Debug.Log($"[SimpleGPSTracker] First fix — Lat:{currentLatitude:F7} Lon:{currentLongitude:F7} Acc:{currentHorizontalAccuracy:F1} m");

        try
        {
            Vector3 xrRot = xrOrigin != null ? xrOrigin.eulerAngles : Vector3.zero;
            long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line = "{\"sessionId\":\"40cacb\",\"timestamp\":" + ts +
                ",\"location\":\"SimpleGPSTracker.cs:CommitFirstFix\",\"hypothesisId\":\"A_C_D\"" +
                ",\"message\":\"FIRST_GPS_FIX\"" +
                ",\"data\":{" +
                "\"lat\":" + currentLatitude.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"lon\":" + currentLongitude.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"accuracy\":" + currentHorizontalAccuracy.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"rawPosX\":" + rawPos.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"rawPosZ\":" + rawPos.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"xrOriginRotY\":" + xrRot.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"isNorthAligned\":" + _isNorthAligned.ToString().ToLower() +
                ",\"northCorrectionDeg\":" + NorthCorrectionDeg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"mapOriginLat\":" + mapOrigin.originLat.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"mapOriginLon\":" + mapOrigin.originLon.ToString("F8", System.Globalization.CultureInfo.InvariantCulture) +
                "}}\n";
            string path = System.IO.Path.Combine(Application.persistentDataPath, "debug-40cacb.log");
            System.IO.File.AppendAllText(path, line);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[DBG40cacb] LogA failed: " + ex.Message);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // North alignment (compass-based, one-time at startup)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rotates XR Origin once so that Unity world +Z aligns with geographic North.
    ///
    /// Why this is necessary:
    ///   AR Foundation builds its world coordinate system from the device orientation at
    ///   session start — Unity +Z does NOT automatically point North. Our ENU GPS
    ///   conversion assumes +Z = North, so without this correction all GPS-placed
    ///   objects appear in the wrong direction.
    ///
    /// Formula:
    ///   correction = compassTrueHeading - arCamera.eulerAngles.y
    ///   xrOrigin.eulerAngles.y = correction
    ///
    ///   Proof: after the rotation, new_camera_worldYaw = correction + cameraLocalYaw
    ///          = (compassHeading - cameraLocalYaw) + cameraLocalYaw = compassHeading
    ///          → camera faces compassHeading° from Unity +Z = compassHeading° from North ✓
    /// </summary>
    private System.Collections.IEnumerator AlignNorthAsync()
    {
#if UNITY_EDITOR
        // Editor has no compass hardware — skip silently so Editor testing is not blocked.
        Debug.Log("[SimpleGPSTracker] Editor: North alignment skipped (no compass).");
        _isNorthAligned = true;
        yield break;
#else
        if (arCamera == null)
        {
            Debug.LogWarning("[SimpleGPSTracker] arCamera not assigned — North alignment skipped! " +
                             "Drag 'Main Camera' into the arCamera field on SimpleGPSTracker.");
            _isNorthAligned = true;
            yield break;
        }

        if (xrOrigin == null)
        {
            Debug.LogError("[SimpleGPSTracker] xrOrigin is null — cannot align North.");
            _isNorthAligned = true;
            yield break;
        }

        Input.compass.enabled = true;

        // Brief warmup: compass hardware needs a moment after being enabled.
        yield return new WaitForSeconds(0.5f);

        // Circular mean: heading is on a circle (0° == 360°), so arithmetic mean
        // breaks near the wrap boundary (e.g. samples [359, 1] should average to 0, not 180).
        float sinSum = 0f;
        float cosSum = 0f;
        int validSamples = 0;

        for (int i = 0; i < compassSampleCount; i++)
        {
            yield return new WaitForSeconds(compassSampleInterval);
            float h   = Input.compass.trueHeading;
            float acc = Input.compass.headingAccuracy;

            // acc <= 0: device không report accuracy (một số Android) — buộc phải chấp nhận
            // acc > maxAcceptable: sample đang bị nhiễu (gần kim loại, magnetometer chưa calibrate)
            bool qualityOk = acc <= 0f || acc <= maxAcceptableHeadingAccuracy;

            if (h >= 0f && qualityOk)
            {
                float hRad = h * Mathf.Deg2Rad;
                sinSum += Mathf.Sin(hRad);
                cosSum += Mathf.Cos(hRad);
                validSamples++;
                Debug.Log($"[SimpleGPSTracker] Compass sample {i + 1}/{compassSampleCount} accepted: " +
                          $"heading={h:F1}° accuracy={acc:F1}°");
            }
            else
            {
                Debug.LogWarning($"[SimpleGPSTracker] Compass sample {i + 1}/{compassSampleCount} rejected: " +
                                 $"heading={h:F1}° accuracy={acc:F1}° (> {maxAcceptableHeadingAccuracy:F0}° threshold)");
            }
        }

        if (validSamples == 0)
        {
            Debug.LogWarning("[SimpleGPSTracker] Tất cả compass samples bị từ chối — môi trường nhiễu từ trường nặng " +
                             "(gần kim loại, trong nhà, hoặc magnetometer chưa calibrate). " +
                             "Hãy ra ngoài trời và xoay điện thoại theo hình số 8 vài giây, rồi khởi động lại app. " +
                             "North alignment skipped — Unity +Z giữ nguyên hướng mặc định.");
            _isNorthAligned = true;
            yield break;
        }

        float avgHeading = Mathf.Atan2(sinSum, cosSum) * Mathf.Rad2Deg;
        if (avgHeading < 0f) avgHeading += 360f;
        float cameraYaw   = arCamera.transform.eulerAngles.y;
        float correction  = avgHeading - cameraYaw + extraNorthYawOffsetDegrees;

        Vector3 eulerBefore = xrOrigin.eulerAngles;
        xrOrigin.eulerAngles  = new Vector3(eulerBefore.x, correction, eulerBefore.z);
        NorthCorrectionDeg    = correction;
        _lockedNorthYaw       = correction;
        _hasLockedNorthYaw    = true;
        _isNorthAligned       = true;

        if (autoSaveExtraNorthYawAfterAlign)
        {
            PlayerPrefs.SetFloat(PrefExtraNorthYaw, extraNorthYawOffsetDegrees);
            PlayerPrefs.Save();
        }

        Debug.Log($"[SimpleGPSTracker] North aligned ✓ — " +
                  $"compass={avgHeading:F1}°  cameraYaw={cameraYaw:F1}°  extraOff={extraNorthYawOffsetDegrees:F1}°  correction={correction:F1}°  " +
                  $"(XR Origin.y = {correction:F1}°)  lockYaw={lockXrOriginYawAfterNorthAlign}");
#endif
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Calibration
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this while standing physically at the MapOrigin point.
    /// Measures the difference between what GPS reports and what MapOrigin expects,
    /// then stores it so all future GPS positions are corrected for systematic bias.
    /// </summary>
    public bool CalibrateAtOrigin(bool snapToSurveyedPoint = false)
    {
        if (mapOrigin == null) return false;
        return CalibrateAtSurveyedPoint(mapOrigin.originLat, mapOrigin.originLon, snapToSurveyedPoint);
    }

    /// <summary>
    /// Bias correction while standing at a surveyed point (MapOrigin or Des lat/lon).
    /// </summary>
    /// <param name="snapToSurveyedPoint">
    /// Option A: nếu true, ngoài việc trừ bias còn SNAP XR Origin để AR camera đứng đúng tại
    /// điểm khảo sát (Unity XZ), rồi chuyển sang "VIO mode" (giảm GPS influence) để AR tracking
    /// dẫn dắt chuyển động. Đây là cơ chế đạt accuracy 1-2m. Khi false: chỉ trừ bias như cũ.
    /// </param>
    public bool CalibrateAtSurveyedPoint(double surveyedLat, double surveyedLon, bool snapToSurveyedPoint = false)
    {
        if (!HasLocationFix || mapOrigin == null) return false;

        // Snap mode KHÔNG phụ thuộc GPS accuracy (snap thẳng về điểm khảo sát). Bias chỉ best-effort.
        // Bias-only mode (cũ) vẫn cần GPS tốt vì nó dựa hoàn toàn vào fix hiện tại.
        if (!snapToSurveyedPoint && currentHorizontalAccuracy > calibrateMaxAccuracyMeters)
        {
            Debug.LogWarning($"[SimpleGPSTracker] Calibrate rejected — accuracy {currentHorizontalAccuracy:F1} m > {calibrateMaxAccuracyMeters:F1} m.");
            return false;
        }

        _offsetLat = currentLatitude - surveyedLat;
        _offsetLon = currentLongitude - surveyedLon;

        PlayerPrefs.SetFloat(PrefOffsetLat, (float)_offsetLat);
        PlayerPrefs.SetFloat(PrefOffsetLon, (float)_offsetLon);
        PlayerPrefs.Save();

        if (snapToSurveyedPoint)
        {
            SnapXrOriginToSurveyedPoint(surveyedLat, surveyedLon);
        }
        else
        {
            ResetGpsTrackingStateAfterCalibration();
        }

        Debug.Log($"[SimpleGPSTracker] Calibrated{(snapToSurveyedPoint ? " (SNAP/VIO)" : "")} — dLat={_offsetLat:F8}  dLon={_offsetLon:F8}  " +
                  $"(≈{_offsetLat * 111320f:F1} m N,  {_offsetLon * 111320f * Mathf.Cos((float)(mapOrigin.originLat * Mathf.Deg2Rad)):F1} m E)");
        return true;
    }

    /// <summary>
    /// Option A core: đặt render-layer state về đúng điểm khảo sát + kích hoạt one-shot camera
    /// align có sẵn (_pendingInitialCameraGpsAlign) để AR camera world XZ trùng điểm khảo sát,
    /// rồi chuyển sang VIO mode (smoothSpeed chậm) để AR tracking dẫn dắt chuyển động.
    /// </summary>
    private void SnapXrOriginToSurveyedPoint(double surveyedLat, double surveyedLon)
    {
        Vector3 surveyedWorld = mapOrigin.GetUnityPositionFromGPS(surveyedLat, surveyedLon);

        // Ép smoothed/target về ĐÚNG điểm khảo sát (không phải GPS noisy).
        // Render layer sẽ đặt xrOrigin = surveyedWorld; LateUpdate (_pendingInitialCameraGpsAlign)
        // shift xrOrigin để camera world XZ = surveyedWorld — đúng path align đã hoạt động cho outdoor.
        _smoothedPosition = surveyedWorld;
        _gpsTargetPosition = surveyedWorld;
        _hasGpsTarget = true;
        _hasFirstValidFix = true;
        _pendingInitialCameraGpsAlign = true;
        _lastFixRejectedAsJump = false;
        _lastRejectedJumpMeters = -1f;
        ClearSessionRefinementOffset();

        // Sau snap: chỉ vào VIO mode nếu useVioModeFromStart = true. Nếu user đã tắt VIO
        // trong Inspector, giữ smoothSpeed bình thường (GPS-driven).
        if (useVioModeFromStart)
        {
            _activeSmoothSpeed = postCalibrationSmoothSpeed;
            Debug.Log($"[SimpleGPSTracker] SNAP → surveyed world=({surveyedWorld.x:F2}, {surveyedWorld.z:F2}). " +
                      $"VIO mode ON (smoothSpeed {smoothSpeed}→{postCalibrationSmoothSpeed}).");
        }
        else
        {
            _activeSmoothSpeed = smoothSpeed;
            Debug.Log($"[SimpleGPSTracker] SNAP → surveyed world=({surveyedWorld.x:F2}, {surveyedWorld.z:F2}). " +
                      $"VIO mode OFF (giữ smoothSpeed={smoothSpeed} — GPS-driven pure).");
        }
        _hasCalibratedAtAnchor = true;
    }

    /// <summary>Clears the stored calibration offset and PlayerPrefs entries.</summary>
    public void ResetCalibration()
    {
        _offsetLat = 0.0;
        _offsetLon = 0.0;
        PlayerPrefs.DeleteKey(PrefOffsetLat);
        PlayerPrefs.DeleteKey(PrefOffsetLon);
        PlayerPrefs.Save();
        ResetGpsTrackingStateAfterCalibration();
        Debug.Log("[SimpleGPSTracker] Calibration reset.");
    }

    private void ResetGpsTrackingStateAfterCalibration()
    {
        _hasFirstValidFix = false;
        _hasGpsTarget = false;
        _lastFixRejectedAsJump = false;
        _lastRejectedJumpMeters = -1f;
        _collectingFirstFixAverage = false;
        _firstFixSamplePositions.Clear();
        ClearSessionRefinementOffset();
        lastTimestamp = -1;
        _lastAcceptedFixTimestamp = -1d;
        _lastAcceptedFixUnscaledTime = -999f;

        // Tắt VIO mode — quay về GPS thuần (reset calibration nghĩa là bỏ anchor).
        _activeSmoothSpeed = smoothSpeed;
        _hasCalibratedAtAnchor = false;
    }
}
