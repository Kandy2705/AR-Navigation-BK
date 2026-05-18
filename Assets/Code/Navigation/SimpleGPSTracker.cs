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

    [Header("NavMesh map matching")]
    [Tooltip("Snap each GPS map XZ onto the nearest walkable NavMesh point so the user avatar stays on bakeable terrain (fewer dips into obstacle volumes). Uses raw GPS if no hit.")]
    [SerializeField] private bool snapGpsPositionsToNavMesh = true;
    [Tooltip("NavMesh.SamplePosition search radius (m) around the GPS map point.")]
    [SerializeField] private float navMeshSnapSampleRadiusMeters = 8f;
    [SerializeField] private bool logNavMeshSnapFallback;

    // PlayerPrefs keys for persisted calibration offset
    private const string PrefOffsetLat = "gps_offset_lat";
    private const string PrefOffsetLon = "gps_offset_lon";
    private const string PrefExtraNorthYaw = "gps_extra_north_yaw";

    // --- GPS state ---
    private MapOrigin mapOrigin;
    private bool isGpsReady;
    private double currentLatitude;
    private double currentLongitude;
    private float currentHorizontalAccuracy = -1f;
    private double lastTimestamp = -1;

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
    public LocationServiceStatus CurrentStatus => Input.location.status;
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
        mapOrigin = Object.FindFirstObjectByType<MapOrigin>();
        if (mapOrigin == null)
        {
            Debug.LogError("[SimpleGPSTracker] MapOrigin not found in scene.");
            yield break;
        }

        // Restore persisted calibration offset
        _offsetLat = PlayerPrefs.GetFloat(PrefOffsetLat, 0f);
        _offsetLon = PlayerPrefs.GetFloat(PrefOffsetLon, 0f);
        if (HasCalibration)
            Debug.Log($"[SimpleGPSTracker] Loaded calibration offset: dLat={_offsetLat:F8} dLon={_offsetLon:F8}");

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

        // desiredAccuracy=5 m, updateDistance=1 m — ask OS for high-quality fixes
        Input.location.Start(5f, 1f);

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
        if (!_hasGpsTarget) return;

        float t = smoothSpeed * Time.deltaTime;
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
        if (_pendingInitialCameraGpsAlign && _hasFirstValidFix && _isNorthAligned && xrOrigin != null && arCamera != null)
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
        // 1. Accuracy filter
        if (data.horizontalAccuracy > accuracyThresholdMeters)
        {
            Debug.LogWarning($"[SimpleGPSTracker] Fix rejected — accuracy {data.horizontalAccuracy:F1} m > {accuracyThresholdMeters:F1} m threshold.");
            return;
        }

        currentLatitude            = data.latitude;
        currentLongitude           = data.longitude;
        currentHorizontalAccuracy  = data.horizontalAccuracy;

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
        _gpsTargetPosition = mapPos;
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

        float headingSum = 0f;
        int validSamples = 0;

        for (int i = 0; i < compassSampleCount; i++)
        {
            yield return new WaitForSeconds(compassSampleInterval);
            float h = Input.compass.trueHeading;
            // headingAccuracy == 0 on some devices even when valid; accept any non-negative heading.
            if (h >= 0f)
            {
                headingSum += h;
                validSamples++;
            }
            Debug.Log($"[SimpleGPSTracker] Compass sample {i + 1}/{compassSampleCount}: " +
                      $"heading={h:F1}° accuracy={Input.compass.headingAccuracy:F1}°");
        }

        if (validSamples == 0)
        {
            Debug.LogWarning("[SimpleGPSTracker] No valid compass readings — North alignment skipped.");
            _isNorthAligned = true;
            yield break;
        }

        float avgHeading  = headingSum / validSamples;
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
    public bool CalibrateAtOrigin()
    {
        if (mapOrigin == null) return false;
        return CalibrateAtSurveyedPoint(mapOrigin.originLat, mapOrigin.originLon);
    }

    /// <summary>
    /// Bias correction while standing at a surveyed point (MapOrigin or Des lat/lon).
    /// </summary>
    public bool CalibrateAtSurveyedPoint(double surveyedLat, double surveyedLon)
    {
        if (!HasLocationFix || mapOrigin == null) return false;

        if (currentHorizontalAccuracy > calibrateMaxAccuracyMeters)
        {
            Debug.LogWarning($"[SimpleGPSTracker] Calibrate rejected — accuracy {currentHorizontalAccuracy:F1} m > {calibrateMaxAccuracyMeters:F1} m.");
            return false;
        }

        _offsetLat = currentLatitude - surveyedLat;
        _offsetLon = currentLongitude - surveyedLon;

        PlayerPrefs.SetFloat(PrefOffsetLat, (float)_offsetLat);
        PlayerPrefs.SetFloat(PrefOffsetLon, (float)_offsetLon);
        PlayerPrefs.Save();

        ResetGpsTrackingStateAfterCalibration();

        Debug.Log($"[SimpleGPSTracker] Calibrated — dLat={_offsetLat:F8}  dLon={_offsetLon:F8}  " +
                  $"(≈{_offsetLat * 111320f:F1} m N,  {_offsetLon * 111320f * Mathf.Cos((float)(mapOrigin.originLat * Mathf.Deg2Rad)):F1} m E)");
        return true;
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
    }
}
