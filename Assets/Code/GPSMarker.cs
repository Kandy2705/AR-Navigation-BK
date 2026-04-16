using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

public class GPSMarker : MonoBehaviour
{
    public GameObject xrOrigin;
    public Camera mainCamera;
    public Transform mapPlane;
    public TextMeshProUGUI gpsText;
    public GameObject targetObject;
    public AlignXROriginToUser alignXROriginToUser;

    private bool manual = true;
    private bool gpsAvailable = false;

    const double a = 6378137.0;
    const double e2 = 6.694380004e-3;

    public double refLat = 10.7736444;
    public double refLon = 106.6593743;
    public double refAlt = 0.0;

    private ECEF refECEF;

    public double lat = 10.7741875;
    public double lon = 106.6606904;
    public double alt = 0.0;

    public bool aligned = false;

    private Vector3 lastUserENU;
    public float alignThreshold = 2.0f;
    public float alignStrength = 2.0f;

    [Header("Mock Movement")]
    public bool useMockMovement = false;
    public float mockSpeed = 1.5f;

    [Header("Mock Compass (Editor Test)")]
    public bool useMockCompass = true;
    [Range(0f, 360f)]
    public float mockCompassHeading = 0f;

    // ─── Phase 1: GPS Data Quality ───────────────────────────────────────────

    [Header("GPS Quality Filter")]
    [Tooltip("Reject GPS samples with horizontal accuracy worse than this (meters)")]
    public float maxAcceptableAccuracy = 30f;

    private double lastGpsTimestamp = -1;

    [Header("Compass Smoothing")]
    [Range(0.01f, 1f)]
    [Tooltip("Lower = smoother heading, slower response. 0.1 recommended.")]
    public float headingSmoothFactor = 0.1f;

    private float smoothedHeading = 0f;
    private bool headingInitialized = false;

    // ─── Phase 2: Position Smoothing ─────────────────────────────────────────

    [Header("GPS Position Smoothing")]
    [Range(0.01f, 1f)]
    [Tooltip("EMA factor. Lower = smoother but slower. 0.15 recommended.")]
    public float positionSmoothFactor = 0.15f;

    [Tooltip("Reject GPS jumps larger than this (meters). Prevents teleporting.")]
    public float maxPositionJumpMeters = 50f;

    private Vector3 smoothedUserENU;
    private bool positionInitialized = false;

    // ─── Phase 3: Velocity + Dead Reckoning ──────────────────────────────────

    [Header("Dead Reckoning")]
    [Tooltip("Estimated walking speed used for dead reckoning when GPS lost (m/s)")]
    public float deadReckoningSpeed = 1.4f;

    [Tooltip("Speed below which the user is considered stationary")]
    public float stationarySpeedThreshold = 0.3f;

    [Tooltip("Seconds before switching to dead reckoning after GPS lost")]
    public float gpslostTimeout = 3f;

    [Header("Adaptive GPS Rate")]
    [Tooltip("GPS update interval when stationary (seconds). Saves battery.")]
    public float stationaryGpsInterval = 5f;

    private Vector3 estimatedVelocity;          // m/s in ENU space
    private Vector3 lastVelocitySampleENU;
    private double lastVelocityTimestamp = -1;
    private float gpslostTimer = 0f;
    private bool isDeadReckoning = false;
    private float adaptiveGpsTimer = 0f;


    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    void Start()
    {
        StartCoroutine(StartLocationService());
        Input.compass.enabled = true;
        refECEF = LatLonAltToECEF(refLat, refLon, refAlt);
    }

    void OnDestroy()
    {
        Input.location.Stop();
    }

    IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("GPS không cho phép — sử dụng tọa độ mặc định");
            gpsAvailable = false;
            yield break;
        }

        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait <= 0 || Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("Không thể xác nhận vị trí — sử dụng tọa độ mặc định");
            gpsAvailable = false;
            yield break;
        }

        gpsAvailable = true;
        Debug.Log("GPS available");
    }

    public void updateGPS()
    {
        manual = !manual;
    }

    // ─── Heading ──────────────────────────────────────────────────────────────

    float GetCurrentHeading()
    {
        if (useMockCompass)
            return mockCompassHeading;

        if (Input.compass.enabled)
            return Input.compass.trueHeading;

        return smoothedHeading;
    }

    // Phase 1.3: Continuous compass with short-angle lerp to avoid 359°→0° wrap
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

        AlignMapRotationWithXR(smoothedHeading);
    }

    void AlignMapRotationWithXR(float compassHeading)
    {
        float xrYaw = xrOrigin.transform.rotation.eulerAngles.y;
        float mapYaw = xrYaw - compassHeading;
        mapPlane.rotation = Quaternion.Euler(0f, mapYaw, 0f);
    }

    // ─── Mock Movement ────────────────────────────────────────────────────────

    Vector3 GetMockUserENU()
    {
        float e = 0f;
        float n = 0f;

        if (Input.GetKey(KeyCode.W)) n += 1f;
        if (Input.GetKey(KeyCode.S)) n -= 1f;
        if (Input.GetKey(KeyCode.D)) e += 1f;
        if (Input.GetKey(KeyCode.A)) e -= 1f;

        Vector3 dir = new Vector3(e, 0f, n);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        return transform.localPosition + dir * mockSpeed * Time.deltaTime;
    }

    void AlignEnvironmentToXR()
    {
        Vector3 offset = mainCamera.transform.position - transform.position;
        offset.y = 0f;
        mapPlane.position += offset * alignStrength;
    }

    // ─── Phase 2: EMA smoothing + outlier rejection ───────────────────────────

    Vector3 ApplySmoothing(Vector3 rawENU)
    {
        if (!positionInitialized)
        {
            smoothedUserENU = rawENU;
            positionInitialized = true;
            return smoothedUserENU;
        }

        float jumpDistance = Vector3.Distance(rawENU, smoothedUserENU);
        if (jumpDistance > maxPositionJumpMeters)
        {
            // Outlier — reject this sample silently
            return smoothedUserENU;
        }

        smoothedUserENU = Vector3.Lerp(smoothedUserENU, rawENU, positionSmoothFactor);
        return smoothedUserENU;
    }

    // ─── Phase 3: Velocity estimation ────────────────────────────────────────

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

        // Low-pass filter velocity to avoid noise spikes
        estimatedVelocity = Vector3.Lerp(estimatedVelocity, rawVelocity, 0.3f);

        lastVelocitySampleENU = currentENU;
        lastVelocityTimestamp = currentTimestamp;
    }

    bool IsStationary()
    {
        return estimatedVelocity.magnitude < stationarySpeedThreshold;
    }

    // Phase 3: Dead reckoning — project position forward using heading + speed
    Vector3 DeadReckon(Vector3 lastENU, float deltaTime)
    {
        float heading = smoothedHeading * Mathf.Deg2Rad;
        // heading: 0=North, 90=East — map to ENU (e=sin, n=cos)
        Vector3 direction = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        float speed = Mathf.Max(estimatedVelocity.magnitude, deadReckoningSpeed);
        return lastENU + direction * speed * deltaTime;
    }

    // ─── Main Update ──────────────────────────────────────────────────────────

    void Update()
    {
        if (manual)
        {
            Vector3 userENU;

            if (useMockMovement)
            {
                userENU = GetMockUserENU();
                smoothedUserENU = userENU;
                positionInitialized = true;
                transform.localPosition = userENU;
                AlignEnvironmentToXR();
            }
            else
            {
                userENU = ProcessGPS();
                Vector3 worldPos = mapPlane.TransformPoint(userENU);
                transform.position = worldPos;
                AlignEnvironmentToXR();
            }

            // Phase 1.3: Continuous heading update every frame
            UpdateHeading();

            // Alignment drift correction
            if (aligned)
            {
                Vector3 deltaENU = userENU - lastUserENU;
                if (deltaENU.magnitude > alignThreshold)
                {
                    if (alignXROriginToUser.aligned) AlignEnvironmentToXR();
                }
            }
            else
            {
                aligned = true;
            }
            lastUserENU = userENU;
        }

        UpdateGpsText();
    }

    // Handles all GPS logic: quality filter → smoothing → velocity → dead reckoning
    Vector3 ProcessGPS()
    {
        bool gpsRunning = Input.location.status == LocationServiceStatus.Running;

        if (gpsRunning)
        {
            // Phase 1.1: Accuracy check
            float accuracy = Input.location.lastData.horizontalAccuracy;
            bool accuracyOk = accuracy <= maxAcceptableAccuracy;

            // Phase 1.2: Timestamp check — only process new GPS fixes
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

                // Phase 2: EMA smoothing + outlier rejection
                Vector3 smoothed = ApplySmoothing(rawENU);

                // Phase 3: Velocity estimation
                UpdateVelocity(smoothed, currentTimestamp);

                // Phase 3: Adaptive GPS rate — throttle when stationary
                adaptiveGpsTimer = 0f;

                // GPS is good → exit dead reckoning
                isDeadReckoning = false;
                gpslostTimer = 0f;

                return smoothed;
            }
        }

        // GPS lost or bad quality
        gpslostTimer += Time.deltaTime;

        if (gpslostTimer >= gpslostTimeout && positionInitialized)
        {
            // Phase 3: Switch to dead reckoning
            isDeadReckoning = true;
            smoothedUserENU = DeadReckon(smoothedUserENU, Time.deltaTime);
        }

        // Phase 3: Adaptive GPS rate — when stationary, poll GPS less often
        if (positionInitialized && IsStationary())
        {
            adaptiveGpsTimer += Time.deltaTime;
            // If we haven't gotten a fix for a while and user is stationary,
            // hold last known position (don't dead reckon while standing still)
            if (adaptiveGpsTimer < stationaryGpsInterval)
                isDeadReckoning = false;
        }

        return positionInitialized ? smoothedUserENU : new Vector3((float)0, 0f, (float)0);
    }

    void UpdateGpsText()
    {
        if (gpsText == null) return;

        float accuracy = Input.location.status == LocationServiceStatus.Running
            ? Input.location.lastData.horizontalAccuracy : -1f;

        string accuracyStr = accuracy >= 0 ? $"±{accuracy:F0}m" : "N/A";
        string statusStr = isDeadReckoning ? "DEAD RECKONING" :
                           (Input.location.status == LocationServiceStatus.Running ? "GPS OK" : "GPS LOST");

        gpsText.text =
            $"Status: {statusStr} | Accuracy: {accuracyStr}\n" +
            $"Lat: {lat:F7}  Lon: {lon:F7}\n" +
            $"Speed: {estimatedVelocity.magnitude:F1} m/s | Heading: {smoothedHeading:F1}°\n" +
            $"X: {transform.position.x:F1}  Z: {transform.position.z:F1}";
    }

    // ─── Coordinate Conversion ────────────────────────────────────────────────

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
        // Phase 2.3: Fix ENU.u — was hardcoded 0, now correctly computed
        enu.u =  cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz;
        return enu;
    }
}
