using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// MockGPSMarker — Drop-in replacement cho GPSMarker với kiến trúc đúng.
///
/// ĐIỂM KHÁC BIỆT CỐT LÕI:
///   Cũ: GPS update → di chuyển mapPlane (N children) → O(N) cost/frame → LAG
///   Mới: GPS update → di chuyển XROrigin (1 transform)  → O(1) cost/frame → MƯỢT
///
/// Yêu cầu setup:
///   - mapPlane phải ở (0,0,0) trong world space và KHÔNG BAO GIỜ di chuyển
///   - AlignXROriginToUser phải được disable (script này thay thế nó)
///   - xrOrigin, mainCamera phải được gán trong Inspector
/// </summary>
public class MockGPSMarker : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("XROrigin GameObject — ĐÂY SẼ DI CHUYỂN thay vì mapPlane")]
    public XROrigin xrOrigin;

    [Tooltip("AR Camera (child của XROrigin)")]
    public Camera mainCamera;

    [Tooltip("Outdoor map root — ĐỨNG YÊN tại (0,0,0), không bao giờ di chuyển")]
    public Transform mapPlane;

    [Tooltip("TextMeshPro để hiển thị trạng thái GPS")]
    public TextMeshProUGUI gpsText;

    // ─── GPS Reference ────────────────────────────────────────────────────────

    [Header("GPS Reference Point (World Origin)")]
    [Tooltip("Tọa độ GPS tương ứng với vị trí (0,0,0) trong scene")]
    public double refLat = 10.7736444;
    public double refLon = 106.6593743;
    public double refAlt = 0.0;

    private ECEF refECEF;

    [Header("Current GPS (read-only in Inspector)")]
    public double lat = 10.7741875;
    public double lon = 106.6606904;
    public double alt = 0.0;

    // ─── Mock Movement ────────────────────────────────────────────────────────

    [Header("Mock Movement (Editor Test)")]
    [Tooltip("Bật để dùng WASD di chuyển XROrigin trực tiếp trong Editor")]
    public bool useMockMovement = false;
    [Tooltip("Tốc độ di chuyển khi Mock (m/s)")]
    public float mockSpeed = 1.5f;

    // ─── Mock Compass ─────────────────────────────────────────────────────────

    [Header("Mock Compass (Editor Test)")]
    public bool useMockCompass = true;
    [Range(0f, 360f)]
    public float mockCompassHeading = 0f;

    // ─── GPS Quality ──────────────────────────────────────────────────────────

    [Header("GPS Quality Filter")]
    [Tooltip("Bỏ qua GPS sample có accuracy tệ hơn ngưỡng này (meters)")]
    public float maxAcceptableAccuracy = 30f;
    public float desiredAccuracyMeters = 5f;
    public float updateDistanceMeters = 1f;

    private double lastGpsTimestamp = -1;

    // ─── Compass Smoothing ────────────────────────────────────────────────────

    [Header("Compass Smoothing")]
    [Range(0.01f, 1f)]
    public float headingSmoothFactor = 0.1f;

    private float smoothedHeading = 0f;
    private bool headingInitialized = false;

    // ─── Position Smoothing ───────────────────────────────────────────────────

    [Header("GPS Position Smoothing")]
    [Range(0.01f, 1f)]
    [Tooltip("EMA factor. Thấp = mượt hơn nhưng chậm hơn. 0.15 recommended.")]
    public float positionSmoothFactor = 0.15f;

    [Tooltip("Loại bỏ GPS jump lớn hơn ngưỡng này (meters)")]
    public float maxPositionJumpMeters = 50f;

    private Vector3 smoothedUserENU;
    private bool positionInitialized = false;

    // ─── Dead Reckoning ───────────────────────────────────────────────────────

    [Header("Dead Reckoning")]
    public float deadReckoningSpeed = 1.4f;
    public float stationarySpeedThreshold = 0.3f;
    public float gpslostTimeout = 3f;

    [Header("Adaptive GPS Rate")]
    public float stationaryGpsInterval = 5f;

    private Vector3 estimatedVelocity;
    private Vector3 lastVelocitySampleENU;
    private double lastVelocityTimestamp = -1;
    private float gpslostTimer = 0f;
    private bool isDeadReckoning = false;
    private float adaptiveGpsTimer = 0f;

    // ─── GPS Text ─────────────────────────────────────────────────────────────

    [Header("GPS Text")]
    [Tooltip("Tối thiểu bao nhiêu giây giữa 2 lần update GPS text")]
    public float gpsTextUpdateInterval = 0.5f;
    private float lastGpsTextUpdateTime = -999f;

    // ─── Internal ─────────────────────────────────────────────────────────────

    private bool gpsAvailable = false;
    private bool manual = true;

    // ECEF ellipsoid constants (WGS84)
    const double a  = 6378137.0;
    const double e2 = 6.694380004e-3;

    // ═══════════════════════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    void Start()
    {
        refECEF = LatLonAltToECEF(refLat, refLon, refAlt);
        Input.compass.enabled = true;
        StartCoroutine(StartLocationService());

        // Xác nhận mapPlane đứng yên tại origin
        if (mapPlane != null && mapPlane.position != Vector3.zero)
        {
            Debug.LogWarning(
                "[MockGPSMarker] mapPlane không ở (0,0,0)! " +
                "Với kiến trúc mới, mapPlane phải đứng yên tại world origin. " +
                "Đang reset về (0,0,0).");
            mapPlane.position = Vector3.zero;
        }
    }

    void OnEnable()
    {
        // Reset state mỗi khi outdoor environment được bật
        headingInitialized = false;
        positionInitialized = false;
        lastGpsTimestamp = -1;
        lastVelocityTimestamp = -1;
        estimatedVelocity = Vector3.zero;
        gpslostTimer = 0f;
        isDeadReckoning = false;
        adaptiveGpsTimer = 0f;
    }

    void OnDestroy()
    {
        Input.location.Stop();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Main Update
    // ═══════════════════════════════════════════════════════════════════════════

    void Update()
    {
        if (!manual) return;

        if (useMockMovement)
        {
            // Editor: WASD di chuyển XROrigin trực tiếp
            // UserIcon (transform này) theo sau camera
            HandleMockMovement();
        }
        else
        {
            // Thiết bị thật: tính vị trí từ GPS → di chuyển XROrigin
            Vector3 userENU = ProcessGPS();

            // mapPlane tại (0,0,0) nên ENU = world XZ
            transform.position = new Vector3(userENU.x, transform.position.y, userENU.z);

            // *** CORE: Di chuyển XROrigin thay vì mapPlane ***
            AlignXROriginToGPS();
        }

        UpdateHeading();
        UpdateGpsText();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CORE: Di chuyển XROrigin (không phải mapPlane)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Di chuyển XROrigin để AR camera nằm đúng tại vị trí GPS.
    /// mapPlane KHÔNG BAO GIỜ thay đổi → không có rebuild Physics/NavMesh.
    /// Cost: O(1) mỗi frame (chỉ 1 Transform thay đổi).
    /// </summary>
    void AlignXROriginToGPS()
    {
        if (xrOrigin == null || mainCamera == null) return;

        // offset = khoảng cách từ camera đến UserIcon (nơi GPS nói ta đang ở)
        Vector3 offset = transform.position - mainCamera.transform.position;
        offset.y = 0f; // chỉ dịch chuyển theo mặt phẳng ngang

        // Di chuyển XROrigin → camera đi theo → camera khớp với GPS position
        xrOrigin.transform.position += offset;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Mock Movement (Editor)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trong Editor, WASD di chuyển XROrigin trực tiếp (giả lập người đi bộ).
    /// Vì XROrigin di chuyển chứ không phải mapPlane, bạn sẽ THẤY camera
    /// thực sự di chuyển qua Scene view — mượt mà, không lag.
    /// </summary>
    void HandleMockMovement()
    {
        float e = 0f;
        float n = 0f;

        if (Input.GetKey(KeyCode.W)) n += 1f;
        if (Input.GetKey(KeyCode.S)) n -= 1f;
        if (Input.GetKey(KeyCode.D)) e += 1f;
        if (Input.GetKey(KeyCode.A)) e -= 1f;

        Vector3 dir = new Vector3(e, 0f, n);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        if (xrOrigin != null && dir.sqrMagnitude > 0.01f)
        {
            // Di chuyển XROrigin (camera di chuyển, mapPlane đứng yên)
            xrOrigin.transform.position += dir * mockSpeed * Time.deltaTime;
        }

        // UserIcon bám theo camera để biểu diễn vị trí người dùng
        if (mainCamera != null)
        {
            transform.position = new Vector3(
                mainCamera.transform.position.x,
                transform.position.y,
                mainCamera.transform.position.z);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Heading / Compass
    // ═══════════════════════════════════════════════════════════════════════════

    float GetCurrentHeading()
    {
        if (useMockCompass) return mockCompassHeading;
        if (Input.compass.enabled) return Input.compass.trueHeading;
        return smoothedHeading;
    }

    void UpdateHeading()
    {
        if (!useMockCompass && !Input.compass.enabled) return;

        float rawHeading = GetCurrentHeading();

        if (!headingInitialized)
        {
            smoothedHeading = rawHeading;
            headingInitialized = true;
        }
        else
        {
            float delta = Mathf.DeltaAngle(smoothedHeading, rawHeading);
            smoothedHeading += delta * headingSmoothFactor;
            smoothedHeading = (smoothedHeading + 360f) % 360f;
        }

        // Xoay mapPlane để North của bản đồ khớp với compass
        // (chỉ xoay rotation, không dời position → ít tốn hơn dịch chuyển)
        AlignMapRotationWithCompass(smoothedHeading);
    }

    void AlignMapRotationWithCompass(float compassHeading)
    {
        if (xrOrigin == null || mapPlane == null) return;

        float xrYaw = xrOrigin.transform.rotation.eulerAngles.y;
        float mapYaw = xrYaw - compassHeading;
        mapPlane.rotation = Quaternion.Euler(0f, mapYaw, 0f);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GPS Service
    // ═══════════════════════════════════════════════════════════════════════════

    public void ToggleManual() => manual = !manual;

    IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("[MockGPS] GPS không được cấp phép — dùng tọa độ mặc định");
            gpsAvailable = false;
            yield break;
        }

        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait <= 0 || Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("[MockGPS] Không thể xác định vị trí — dùng tọa độ mặc định");
            gpsAvailable = false;
            yield break;
        }

        gpsAvailable = true;
        Debug.Log("[MockGPS] GPS sẵn sàng");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GPS Processing (Phase 1-3)
    // ═══════════════════════════════════════════════════════════════════════════

    Vector3 ProcessGPS()
    {
        bool gpsRunning = Input.location.status == LocationServiceStatus.Running;

        if (gpsRunning)
        {
            float accuracy = Input.location.lastData.horizontalAccuracy;
            bool accuracyOk = accuracy <= maxAcceptableAccuracy;

            double currentTimestamp = Input.location.lastData.timestamp;
            bool isNewFix = currentTimestamp > lastGpsTimestamp;

            if (accuracyOk && isNewFix)
            {
                lastGpsTimestamp = currentTimestamp;
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
                alt = Input.location.lastData.altitude;

                ECEF pointECEF = LatLonAltToECEF(lat, lon, alt);
                ENU enu = ECEFToENU(pointECEF, refECEF, refLat, refLon);
                Vector3 rawENU = new Vector3((float)enu.e, 0f, (float)enu.n);

                Vector3 smoothed = ApplySmoothing(rawENU);
                UpdateVelocity(smoothed, currentTimestamp);

                adaptiveGpsTimer = 0f;
                isDeadReckoning = false;
                gpslostTimer = 0f;

                return smoothed;
            }
        }

        // GPS lost hoặc accuracy tệ
        gpslostTimer += Time.deltaTime;

        if (gpslostTimer >= gpslostTimeout && positionInitialized)
        {
            isDeadReckoning = true;
            smoothedUserENU = DeadReckon(smoothedUserENU, Time.deltaTime);
        }

        if (positionInitialized && estimatedVelocity.magnitude < stationarySpeedThreshold)
        {
            adaptiveGpsTimer += Time.deltaTime;
            if (adaptiveGpsTimer < stationaryGpsInterval)
                isDeadReckoning = false;
        }

        return positionInitialized ? smoothedUserENU : Vector3.zero;
    }

    // ─── Phase 2: EMA Smoothing ───────────────────────────────────────────────

    Vector3 ApplySmoothing(Vector3 rawENU)
    {
        if (!positionInitialized)
        {
            smoothedUserENU = rawENU;
            positionInitialized = true;
            return smoothedUserENU;
        }

        if (Vector3.Distance(rawENU, smoothedUserENU) > maxPositionJumpMeters)
            return smoothedUserENU; // Outlier — bỏ qua

        smoothedUserENU = Vector3.Lerp(smoothedUserENU, rawENU, positionSmoothFactor);
        return smoothedUserENU;
    }

    // ─── Phase 3: Velocity + Dead Reckoning ──────────────────────────────────

    void UpdateVelocity(Vector3 currentENU, double currentTimestamp)
    {
        if (lastVelocityTimestamp < 0)
        {
            lastVelocitySampleENU = currentENU;
            lastVelocityTimestamp = currentTimestamp;
            return;
        }

        double dt = currentTimestamp - lastVelocityTimestamp;
        if (dt <= 0) return;

        Vector3 rawVelocity = (currentENU - lastVelocitySampleENU) / (float)dt;
        estimatedVelocity = Vector3.Lerp(estimatedVelocity, rawVelocity, 0.3f);
        lastVelocitySampleENU = currentENU;
        lastVelocityTimestamp = currentTimestamp;
    }

    Vector3 DeadReckon(Vector3 lastENU, float deltaTime)
    {
        float heading = smoothedHeading * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        float speed = Mathf.Max(estimatedVelocity.magnitude, deadReckoningSpeed);
        return lastENU + direction * speed * deltaTime;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GPS Text Display
    // ═══════════════════════════════════════════════════════════════════════════

    void UpdateGpsText()
    {
        if (gpsText == null) return;
        if (Time.time - lastGpsTextUpdateTime < gpsTextUpdateInterval) return;
        lastGpsTextUpdateTime = Time.time;

        float accuracy = Input.location.status == LocationServiceStatus.Running
            ? Input.location.lastData.horizontalAccuracy : -1f;

        string accuracyStr = accuracy >= 0 ? $"±{accuracy:F0}m" : "N/A";
        string statusStr = isDeadReckoning ? "DEAD RECKONING" :
                          (Input.location.status == LocationServiceStatus.Running
                              ? "GPS OK" : "GPS LOST");

        string xrPosStr = xrOrigin != null
            ? $"XR: ({xrOrigin.transform.position.x:F1}, {xrOrigin.transform.position.z:F1})"
            : "XR: N/A";

        gpsText.text =
            $"Status: {statusStr} | Accuracy: {accuracyStr}\n" +
            $"Lat: {lat:F7}  Lon: {lon:F7}\n" +
            $"Speed: {estimatedVelocity.magnitude:F1} m/s | Heading: {smoothedHeading:F1}°\n" +
            xrPosStr;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Coordinate Conversion (ECEF / ENU — WGS84)
    // ═══════════════════════════════════════════════════════════════════════════

    public struct ECEF { public double x, y, z; }
    public struct ENU  { public double e, n, u; }

    public ECEF GetRefECEF() => refECEF;

    public ECEF LatLonAltToECEF(double latDeg, double lonDeg, double altitude)
    {
        double latR = latDeg * Mathf.Deg2Rad;
        double lonR = lonDeg * Mathf.Deg2Rad;

        double sinLat = System.Math.Sin(latR);
        double cosLat = System.Math.Cos(latR);
        double cosLon = System.Math.Cos(lonR);
        double sinLon = System.Math.Sin(lonR);

        double N = a / System.Math.Sqrt(1.0 - e2 * sinLat * sinLat);

        ECEF result;
        result.x = (N + altitude) * cosLat * cosLon;
        result.y = (N + altitude) * cosLat * sinLon;
        result.z = (N * (1.0 - e2) + altitude) * sinLat;
        return result;
    }

    public ENU ECEFToENU(ECEF point, ECEF refPoint, double refLatDeg, double refLonDeg)
    {
        double refLat = refLatDeg * Mathf.Deg2Rad;
        double refLon = refLonDeg * Mathf.Deg2Rad;

        double dx = point.x - refPoint.x;
        double dy = point.y - refPoint.y;
        double dz = point.z - refPoint.z;

        double sinLat = System.Math.Sin(refLat);
        double cosLat = System.Math.Cos(refLat);
        double sinLon = System.Math.Sin(refLon);
        double cosLon = System.Math.Cos(refLon);

        ENU enu;
        enu.e = -sinLon * dx + cosLon * dy;
        enu.n = -sinLat * cosLon * dx - sinLat * sinLon * dy + cosLat * dz;
        enu.u =  cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz;
        return enu;
    }
}
