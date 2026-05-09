using System.Collections;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Runtime GPS State (read-only)")]
    [SerializeField] private bool hasRecentGoodFix;
    [SerializeField] private float lastHorizontalAccuracyMeters = -1f;
    [SerializeField] private float lastEnuDistanceFromRefMeters = -1f;
    [SerializeField] private bool lastFixRejectedAsJump;
    [SerializeField] private float lastRejectedJumpMeters = -1f;

    public bool HasRecentGoodFix => hasRecentGoodFix;
    public float LastHorizontalAccuracyMeters => lastHorizontalAccuracyMeters;
    public float LastEnuDistanceFromRefMeters => lastEnuDistanceFromRefMeters;
    public bool LastFixRejectedAsJump => lastFixRejectedAsJump;
    public float LastRejectedJumpMeters => lastRejectedJumpMeters;

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

    [Header("Debug UI")]
    [Tooltip("Show local/world UserIcon coordinates and target distance on the GPS debug text.")]
    public bool showPositionDebugDetails = true;

    [Tooltip("Snap XROrigin XZ to this marker each frame (no Lerp). Runs in LateUpdate so the camera rig matches the icon even if something moves the icon after Update. On real GPS outdoors you may prefer off + Align Strength.")]
    public bool instantXROriginAlign = false;

    [Tooltip("When an Editor mock driver is attached to UserIcon, disable GPSMarker's XR alignment to avoid fighting over XR Origin.")]
    public bool disableXrAlignmentWhenEditorMockDriverPresent = true;

    [Header("XR Origin follow tuning")]
    [Tooltip("If XR Origin is farther than this (meters) from the UserIcon in XZ, snap immediately instead of lerping. Prevents large lag when user moves quickly or GPS updates jump.")]
    public float xrOriginSnapIfFartherThanMeters = 2.0f;

    [Header("User icon under XR Origin")]
    [Tooltip("When UserIcon is parented under xrOrigin, GPS drives xrOrigin world XZ from map ENU; UserIcon keeps this local offset (e.g. lift marker slightly above floor).")]
    public Vector3 userIconLocalOffset = Vector3.zero;

    [Tooltip("When UserIcon is under xrOrigin, scale lossyScale is multiplied by parent — set desired approximate uniform world scale here (0 uses current lossy magnitude once at runtime).")]
    public float userIconUniformWorldScale = 0f;

    Vector3 lastGpsMapWorldPosition;
    bool lastGpsMapWorldPositionValid;

    [Header("Transform diagnostic logs")]
    [Tooltip("Throttle Debug.Log output for poses (world/local position + euler). Use to trace map vs XR mismatch.")]
    public bool logTransformDiagnosticsToConsole = false;

    [Tooltip("Seconds between transform diagnostic logs when enabled.")]
    public float transformDiagnosticInterval = 1f;

    [Tooltip("Outdoor map root (e.g. MapBK). If null and logging is on, tries GameObject.Find(\"MapBK\") once at runtime.")]
    public Transform mapBkRoot;

    [Tooltip("Also append compact mapBK + XR yaw lines to the GPS HUD text.")]
    public bool appendTransformDiagToGpsHud = false;

    [Tooltip("Adds a Screen Space Overlay panel with world/local position + euler for mapPlane, MapBK, XR Origin, camera, UserIcon.")]
    public bool showEnvironmentTransformOverlay = true;

    [Tooltip("How often to refresh the environment overlay text (seconds).")]
    public float environmentOverlayRefreshInterval = 0.25f;

    [Tooltip("If true, the environment overlay starts hidden (use the on-screen toggle button to show it).")]
    public bool environmentOverlayStartHidden = true;

    private float lastTransformDiagnosticLogTime = -999f;
    private bool mapBkLookupAttempted;
    private GameObject envOverlayRoot;
    private TextMeshProUGUI envOverlayLabel;
    private float lastEnvironmentOverlayRefreshTime = -999f;
    private GameObject envOverlayPanel;
    private Button envOverlayToggleButton;
    private TextMeshProUGUI envOverlayToggleLabel;
    private bool envOverlayPanelVisible = true;

#if UNITY_EDITOR
    [Header("Editor Mock GPS (no WASD)")]
    [Tooltip("When enabled (Editor only), ignore Input.location and WASD. Use the mock lat/lon below to compute user ENU and move UserIcon accordingly.")]
    public bool useEditorMockGps = false;

    [Tooltip("Mock latitude (degrees). Used only when useEditorMockGps is enabled.")]
    public double editorMockLat = 10.7736502;
    [Tooltip("Mock longitude (degrees). Used only when useEditorMockGps is enabled.")]
    public double editorMockLon = 106.6607895;
    [Tooltip("Mock altitude (meters). Used only when useEditorMockGps is enabled.")]
    public double editorMockAlt = 0.0;
#endif

    [Header("Mock Compass (Editor Test)")]
    public bool useMockCompass = true;
    [Range(0f, 360f)]
    public float mockCompassHeading = 0f;

#if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Editor-only: when GPS has no valid fix, keep the user icon on the AR camera so XR Origin + icon look consistent while testing.")]
    public bool editorFollowCameraWhenNoGps = true;
#endif

    [Header("No GPS fallback (manual start position)")]
    [Tooltip("When true and GPS has no valid fix yet, place the UserIcon at a fixed ENU offset (meters) from refLat/refLon instead of following the camera.")]
    public bool useNoGpsFallbackPosition = false;

    [Tooltip("ENU offset (meters) used when useNoGpsFallbackPosition is enabled. X=East, Z=North. Y is applied as a world-space lift.")]
    public Vector3 noGpsFallbackEnuMeters = Vector3.zero;

    // ─── Phase 1: GPS Data Quality ───────────────────────────────────────────

    [Header("GPS Quality Filter")]
    [Tooltip("Reject GPS samples with horizontal accuracy worse than this (meters)")]
    public float maxAcceptableAccuracy = 30f;
    [Tooltip("Requested Android GPS accuracy in meters")]
    public float desiredAccuracyMeters = 5f;
    [Tooltip("Minimum distance in meters before Android reports a new GPS position")]
    public float updateDistanceMeters = 1f;

    private double lastGpsTimestamp = -1;

    [Header("Compass Smoothing")]
    [Range(0.01f, 1f)]
    [Tooltip("Lower = smoother heading, slower response. 0.1 recommended.")]
    public float headingSmoothFactor = 0.1f;

    private float smoothedHeading = 0f;
    private bool headingInitialized = false;

    [Header("Outdoor Stabilization")]
    [Tooltip("If enabled, the outdoor map is re-centered to the AR camera after meaningful GPS movement. Keep disabled to avoid jitter.")]
    public bool continuouslyAlignEnvironment = false;
    [Tooltip("Minimum GPS movement before re-centering the outdoor map.")]
    public float environmentRealignDistanceMeters = 2f;
    [Tooltip("Minimum seconds between outdoor map re-centers.")]
    public float environmentRealignInterval = 1f;
    [Tooltip("Minimum seconds between compass-driven map rotation updates.")]
    public float compassUpdateInterval = 0.25f;
    [Tooltip("Ignore tiny compass changes to keep outdoor content stable.")]
    public float minHeadingChangeDegrees = 3f;
    [Tooltip("Do not enable this when mapPlane contains NavMesh/targets. Rotating navigation world breaks pathfinding.")]
    public bool rotateMapPlaneWithCompass = false;
    [Tooltip("Optional visual-only root for compass rotation (minimap graphics only, not NavMesh/targets).")]
    public Transform compassVisualRoot;
    [Tooltip("Minimum seconds between GPS status text refreshes.")]
    public float gpsTextUpdateInterval = 0.5f;

    private Vector3 lastAlignedUserENU;
    private float lastEnvironmentAlignTime = -999f;
    private float lastHeadingUpdateTime = -999f;
    private float lastAppliedHeading = -999f;
    private bool headingApplied = false;
    private float lastGpsTextUpdateTime = -999f;

    // ─── Runtime update indicator (stuck detector) ────────────────────────────
    [Header("Runtime update indicator")]
    [Tooltip("Show a small on-screen heartbeat so users can confirm data is updating (not frozen).")]
    public bool showRuntimeUpdateIndicator = true;

    [Tooltip("If no new GPS fix arrives for longer than this, indicator turns red (seconds).")]
    public float gpsStaleAfterSeconds = 3.0f;

    [Tooltip("If no heading update occurs for longer than this, indicator turns amber (seconds).")]
    public float headingStaleAfterSeconds = 2.0f;

    private float lastGpsFixUnscaledTime = -999f;
    private float lastHeadingUpdateUnscaledTime = -999f;
    private int gpsFixCounter;
    private int headingCounter;
    private bool indicatorPulse;
    private Image updateIndicatorDot;
    private TextMeshProUGUI updateIndicatorText;

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
    [Tooltip("Disable dead reckoning entirely (recommended for unstable GPS/compass devices).")]
    public bool enableDeadReckoning = true;
    [Tooltip("Estimated walking speed used for dead reckoning when GPS lost (m/s)")]
    public float deadReckoningSpeed = 1.4f;

    [Tooltip("Maximum speed allowed for dead reckoning projection (m/s). Prevents runaway drift.")]
    public float maxDeadReckoningSpeed = 2.2f;

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
        ResolveMapBkRootIfNeeded();
        EnsureEnvironmentTransformOverlay();
        ApplyEnvironmentOverlayVisibility(environmentOverlayStartHidden ? false : true);

        // Important: this field is serialized on existing scenes. When the field was added later,
        // existing prefab/scene instances may have it defaulted to 0, which disables snap and
        // can cause large XR/UserIcon separation on device. Ensure a safe runtime default.
        if (xrOriginSnapIfFartherThanMeters <= 0f)
            xrOriginSnapIfFartherThanMeters = 2.0f;

        if (userIconUniformWorldScale <= 0f)
            userIconUniformWorldScale = Mathf.Max(transform.lossyScale.x, 0.001f);
    }

    void OnEnable()
    {
        aligned = false;
        headingApplied = false;
        headingInitialized = false;
        positionInitialized = false;
        lastGpsTimestamp = -1;
        lastVelocityTimestamp = -1;
        estimatedVelocity = Vector3.zero;
        gpslostTimer = 0f;
        isDeadReckoning = false;
        adaptiveGpsTimer = 0f;
        lastEnvironmentAlignTime = -999f;
        lastHeadingUpdateTime = -999f;
        lastGpsTextUpdateTime = -999f;
        lastTransformDiagnosticLogTime = -999f;
        lastEnvironmentOverlayRefreshTime = -999f;
        mapBkLookupAttempted = false;
        envOverlayPanelVisible = true;
        lastGpsFixUnscaledTime = -999f;
        lastHeadingUpdateUnscaledTime = -999f;
        gpsFixCounter = 0;
        headingCounter = 0;
        indicatorPulse = false;
        hasRecentGoodFix = false;
        lastHorizontalAccuracyMeters = -1f;
        lastEnuDistanceFromRefMeters = -1f;
        lastFixRejectedAsJump = false;
        lastRejectedJumpMeters = -1f;
    }

    void OnDisable()
    {
        if (envOverlayRoot != null)
            envOverlayRoot.SetActive(false);
    }

    void OnDestroy()
    {
        Input.location.Stop();
        if (envOverlayRoot != null)
        {
            Destroy(envOverlayRoot);
            envOverlayRoot = null;
            envOverlayLabel = null;
        }
    }

    IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("GPS không cho phép — sử dụng tọa độ mặc định");
            gpsAvailable = false;
            yield break;
        }

        // Prefer explicit settings to reduce noisy updates on some devices.
        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

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
        if (Time.time - lastHeadingUpdateTime < compassUpdateInterval) return; // throttle: max 4x/giây
        lastHeadingUpdateTime = Time.time;
        lastHeadingUpdateUnscaledTime = Time.unscaledTime;
        headingCounter++;
        indicatorPulse = true;

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

        if (!headingApplied || Mathf.Abs(Mathf.DeltaAngle(lastAppliedHeading, smoothedHeading)) >= minHeadingChangeDegrees)
        {
            AlignMapRotationWithXR(smoothedHeading);
            lastAppliedHeading = smoothedHeading;
            headingApplied = true;
        }
    }

    void AlignMapRotationWithXR(float compassHeading)
    {
        if (xrOrigin == null)
        {
            return;
        }

        float xrYaw = xrOrigin.transform.rotation.eulerAngles.y;
        float mapYaw = xrYaw - compassHeading;

        if (compassVisualRoot != null)
        {
            compassVisualRoot.rotation = Quaternion.Euler(0f, mapYaw, 0f);
            return;
        }

        if (rotateMapPlaneWithCompass && mapPlane != null)
        {
            mapPlane.rotation = Quaternion.Euler(0f, mapYaw, 0f);
        }
    }

#if UNITY_EDITOR
    Vector3 GetEditorMockGpsENU()
    {
        // Treat editor mock GPS as a "good fix" in local ENU space.
        ECEF pointECEF = LatLonAltToECEF(editorMockLat, editorMockLon, editorMockAlt);
        ENU enu = ECEFToENU(pointECEF, refECEF, refLat, refLon);
        return new Vector3((float)enu.e, 0f, (float)enu.n);
    }
#endif

    void AlignEnvironmentToXR()
    {
        if (mainCamera == null || mapPlane == null)
        {
            return;
        }

        Vector3 offset = mainCamera.transform.position - transform.position;
        offset.y = 0f;
        mapPlane.position += offset * alignStrength;
    }

    /// <summary>Moves XR Origin XZ to the GPS-derived world map position (single authority). UserIcon follows via hierarchy when parented under xrOrigin.</summary>
    void ApplyGpsWorldXZToXROrigin(Vector3 worldMapPosition)
    {
        if (xrOrigin == null) return;
        if (disableXrAlignmentWhenEditorMockDriverPresent && GetComponent<EditorUserIconMockRigDriver>() != null) return;

        Vector3 targetPosition = new Vector3(worldMapPosition.x, xrOrigin.transform.position.y, worldMapPosition.z);

        float snapDist = Mathf.Max(0f, xrOriginSnapIfFartherThanMeters);
        if (snapDist > 0f)
        {
            Vector3 a = new Vector3(xrOrigin.transform.position.x, 0f, xrOrigin.transform.position.z);
            Vector3 b = new Vector3(targetPosition.x, 0f, targetPosition.z);
            if (Vector3.Distance(a, b) >= snapDist)
            {
                xrOrigin.transform.position = targetPosition;
                return;
            }
        }

        if (instantXROriginAlign)
        {
            xrOrigin.transform.position = targetPosition;
            return;
        }

        xrOrigin.transform.position = Vector3.Lerp(xrOrigin.transform.position, targetPosition, alignStrength * Time.deltaTime);
    }

    bool UserIconIsUnderXROrigin()
    {
        return xrOrigin != null && transform.IsChildOf(xrOrigin.transform);
    }

    void SyncUserIconUnderXROrigin()
    {
        if (!UserIconIsUnderXROrigin()) return;
        transform.localPosition = userIconLocalOffset;
        float tw = userIconUniformWorldScale > 0f ? userIconUniformWorldScale : 0.001f;
        float ps = xrOrigin.transform.lossyScale.x;
        if (ps > 1e-5f)
            transform.localScale = Vector3.one * (tw / ps);
    }

    /// <summary>Lerp-based alignment only (instant snap runs in LateUpdate).</summary>
    void ApplyGpsWorldXZToXROriginSmoothOnly(Vector3 worldMapPosition)
    {
        if (instantXROriginAlign) return;
        ApplyGpsWorldXZToXROrigin(worldMapPosition);
    }

    void LateUpdate()
    {
        if (!manual || xrOrigin == null || !instantXROriginAlign) return;
#if UNITY_EDITOR
        if (disableXrAlignmentWhenEditorMockDriverPresent && GetComponent<EditorUserIconMockRigDriver>() != null) return;
#endif
#if UNITY_EDITOR
        if (useEditorMockGps)
        {
            if (lastGpsMapWorldPositionValid)
                ApplyGpsWorldXZToXROrigin(lastGpsMapWorldPosition);
            return;
        }
#endif

        if (!lastGpsMapWorldPositionValid || !hasRecentGoodFix) return;
        ApplyGpsWorldXZToXROrigin(lastGpsMapWorldPosition);
    }

    // ─── Phase 2: EMA smoothing + outlier rejection ───────────────────────────

    Vector3 ApplySmoothing(Vector3 rawENU)
    {
        if (!positionInitialized)
        {
            smoothedUserENU = rawENU;
            positionInitialized = true;
            lastFixRejectedAsJump = false;
            lastRejectedJumpMeters = -1f;
            return smoothedUserENU;
        }

        float jumpDistance = Vector3.Distance(rawENU, smoothedUserENU);
        if (jumpDistance > maxPositionJumpMeters)
        {
            // Outlier — reject this sample (but expose state so navigation can be hidden)
            lastFixRejectedAsJump = true;
            lastRejectedJumpMeters = jumpDistance;
            return smoothedUserENU;
        }

        lastFixRejectedAsJump = false;
        lastRejectedJumpMeters = -1f;
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

        float measuredSpeed = estimatedVelocity.magnitude;
        if (measuredSpeed < stationarySpeedThreshold)
        {
            return lastENU;
        }

        float speed = measuredSpeed > 0.001f ? measuredSpeed : deadReckoningSpeed;
        speed = Mathf.Clamp(speed, 0.1f, Mathf.Max(0.1f, maxDeadReckoningSpeed));
        return lastENU + direction * speed * deltaTime;
    }

    // ─── Main Update ──────────────────────────────────────────────────────────

    void Update()
    {
        // Keep overlay alive even when GPS isn't ready yet (so we can debug hierarchy/poses).
        RefreshEnvironmentTransformOverlay();

        if (manual)
        {
            Vector3 userENU;

#if UNITY_EDITOR
            // If an editor mock driver is controlling UserIcon movement, do not overwrite its position here.
            // (Rotation can still be updated elsewhere; UI text can still update.)
            bool editorMockDriverPresent = GetComponent<EditorUserIconMockRigDriver>() != null;
#endif

#if UNITY_EDITOR
            if (!editorMockDriverPresent && useEditorMockGps)
            {
                userENU = GetEditorMockGpsENU();
                // Mark as valid so navigation + alignment logic can run in Editor without Input.location.
                hasRecentGoodFix = true;
                positionInitialized = true;
                lastFixRejectedAsJump = false;
                lastRejectedJumpMeters = -1f;
                lastEnuDistanceFromRefMeters = userENU.magnitude;

                Vector3 mapBasePos = mapPlane != null ? mapPlane.position : Vector3.zero;
                Vector3 editorMockMapWorldPos = mapBasePos + new Vector3(userENU.x, 0f, userENU.z);
                lastGpsMapWorldPosition = editorMockMapWorldPos;
                lastGpsMapWorldPositionValid = true;

                if (!editorMockDriverPresent)
                {
                    if (UserIconIsUnderXROrigin())
                        SyncUserIconUnderXROrigin();
                    else
                        transform.position = editorMockMapWorldPos;
                }

                ApplyGpsWorldXZToXROriginSmoothOnly(editorMockMapWorldPos);
                UpdateHeading();
                UpdateGpsText();
                MaybeLogTransformDiagnostics();
                RefreshEnvironmentTransformOverlay();
                lastUserENU = userENU;
                return;
            }
#endif

            userENU = ProcessGPS();
            // No fake data: if we haven't ever had a good fix yet, do not move the icon/origin.
            if (!positionInitialized || !hasRecentGoodFix)
            {
                if (useNoGpsFallbackPosition)
                {
                    Vector3 fallbackBasePos = mapPlane != null ? mapPlane.position : Vector3.zero;
                    Vector3 fallbackWorldPos = fallbackBasePos + new Vector3(noGpsFallbackEnuMeters.x, 0f, noGpsFallbackEnuMeters.z);
                    fallbackWorldPos.y += noGpsFallbackEnuMeters.y;

                    if (UserIconIsUnderXROrigin())
                    {
                        // Keep XR height stable; only move in XZ toward the fallback.
                        xrOrigin.transform.position = new Vector3(fallbackWorldPos.x, xrOrigin.transform.position.y, fallbackWorldPos.z);
                        SyncUserIconUnderXROrigin();
                    }
                    else
                    {
                        transform.position = fallbackWorldPos;
                    }
                }
#if UNITY_EDITOR
                else if (!editorMockDriverPresent && editorFollowCameraWhenNoGps && mainCamera != null)
                {
                    Vector3 cam = mainCamera.transform.position;
                    if (UserIconIsUnderXROrigin())
                    {
                        xrOrigin.transform.position = new Vector3(cam.x, xrOrigin.transform.position.y, cam.z);
                        SyncUserIconUnderXROrigin();
                    }
                    else
                    {
                        transform.position = new Vector3(cam.x, transform.position.y, cam.z);
                    }
                }
#endif
                lastGpsMapWorldPositionValid = false;
                UpdateHeading();
                UpdateGpsText();
                MaybeLogTransformDiagnostics();
                RefreshEnvironmentTransformOverlay();
                return;
            }

            // IMPORTANT: Do not use mapPlane.TransformPoint here because mapPlane is rotated by compass.
            // TransformPoint would rotate the world position whenever heading changes, causing XR origin jitter.
            Vector3 basePos = mapPlane != null ? mapPlane.position : Vector3.zero;
            Vector3 gpsMapWorldPos = basePos + new Vector3(userENU.x, 0f, userENU.z);
            lastGpsMapWorldPosition = gpsMapWorldPos;
            lastGpsMapWorldPositionValid = true;

            if (
#if UNITY_EDITOR
                !editorMockDriverPresent &&
#endif
                true)
            {
                if (UserIconIsUnderXROrigin())
                    SyncUserIconUnderXROrigin();
                else
                    transform.position = gpsMapWorldPos;
            }

            ApplyGpsWorldXZToXROriginSmoothOnly(gpsMapWorldPos);

            UpdateHeading();

            if (!aligned)
                aligned = hasRecentGoodFix;

            lastUserENU = userENU;
        }

        UpdateGpsText();
        MaybeLogTransformDiagnostics();
        RefreshEnvironmentTransformOverlay();
    }

    void EnsureEnvironmentTransformOverlay()
    {
        if (!showEnvironmentTransformOverlay || envOverlayLabel != null) return;

        envOverlayRoot = new GameObject("EnvironmentTransformDebugOverlay");

        Canvas canvas = envOverlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Ensure we stay on top of other runtime overlays (Hybrid mode switcher is ~5400).
        canvas.sortingOrder = 5600;

        CanvasScaler scaler = envOverlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390f, 844f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = envOverlayRoot.AddComponent<GraphicRaycaster>();
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        // Runtime update indicator (always visible when enabled).
        if (showRuntimeUpdateIndicator)
        {
            GameObject indicatorGo = new GameObject("UpdateIndicator");
            indicatorGo.transform.SetParent(envOverlayRoot.transform, false);
            RectTransform indicatorRect = indicatorGo.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0f, 1f);
            indicatorRect.anchorMax = new Vector2(0f, 1f);
            indicatorRect.pivot = new Vector2(0f, 1f);
            indicatorRect.anchoredPosition = new Vector2(10f, -86f);
            indicatorRect.sizeDelta = new Vector2(360f, 28f);

            GameObject dotGo = new GameObject("Dot");
            dotGo.transform.SetParent(indicatorGo.transform, false);
            RectTransform dotRect = dotGo.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0f, 0.5f);
            dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            dotRect.anchoredPosition = new Vector2(0f, 0f);
            dotRect.sizeDelta = new Vector2(14f, 14f);

            updateIndicatorDot = dotGo.AddComponent<Image>();
            updateIndicatorDot.color = new Color(0.22f, 0.92f, 0.46f, 1f);
            updateIndicatorDot.raycastTarget = false;

            GameObject textGo2 = new GameObject("Text");
            textGo2.transform.SetParent(indicatorGo.transform, false);
            RectTransform txtRect = textGo2.AddComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0f, 0f);
            txtRect.anchorMax = new Vector2(1f, 1f);
            txtRect.offsetMin = new Vector2(20f, 0f);
            txtRect.offsetMax = new Vector2(0f, 0f);

            updateIndicatorText = textGo2.AddComponent<TextMeshProUGUI>();
            updateIndicatorText.alignment = TextAlignmentOptions.Left;
            updateIndicatorText.enableWordWrapping = false;
            updateIndicatorText.fontSize = 11f;
            updateIndicatorText.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            updateIndicatorText.raycastTarget = false;
            if (gpsText != null && gpsText.font != null)
                updateIndicatorText.font = gpsText.font;
        }

        // Toggle button (always visible when overlay system is enabled).
        GameObject toggleGo = new GameObject("Toggle");
        toggleGo.transform.SetParent(envOverlayRoot.transform, false);
        RectTransform toggleRect = toggleGo.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(0f, 1f);
        toggleRect.pivot = new Vector2(0f, 1f);
        toggleRect.anchoredPosition = new Vector2(10f, -120f);
        toggleRect.sizeDelta = new Vector2(170f, 34f);

        Image toggleBg = toggleGo.AddComponent<Image>();
        toggleBg.color = new Color(0.06f, 0.07f, 0.09f, 0.85f);

        envOverlayToggleButton = toggleGo.AddComponent<Button>();
        envOverlayToggleButton.targetGraphic = toggleBg;
        envOverlayToggleButton.onClick.AddListener(() =>
        {
            ApplyEnvironmentOverlayVisibility(!envOverlayPanelVisible);
        });

        GameObject toggleTextGo = new GameObject("Label");
        toggleTextGo.transform.SetParent(toggleGo.transform, false);
        RectTransform toggleTextRect = toggleTextGo.AddComponent<RectTransform>();
        toggleTextRect.anchorMin = Vector2.zero;
        toggleTextRect.anchorMax = Vector2.one;
        toggleTextRect.offsetMin = new Vector2(10f, 6f);
        toggleTextRect.offsetMax = new Vector2(-10f, -6f);

        envOverlayToggleLabel = toggleTextGo.AddComponent<TextMeshProUGUI>();
        envOverlayToggleLabel.alignment = TextAlignmentOptions.Left;
        envOverlayToggleLabel.fontSize = 12f;
        envOverlayToggleLabel.color = Color.white;
        envOverlayToggleLabel.raycastTarget = false;
        if (gpsText != null && gpsText.font != null)
            envOverlayToggleLabel.font = gpsText.font;

        // Panel background (improves readability over camera feed).
        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(envOverlayRoot.transform, false);
        envOverlayPanel = panelGo;
        RectTransform panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(10f, -160f);
        panelRect.sizeDelta = new Vector2(380f, 520f);

        Image panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.03f, 0.04f, 0.62f);

        GameObject textGo = new GameObject("PoseBody");
        textGo.transform.SetParent(panelGo.transform, false);

        RectTransform rect = textGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);

        envOverlayLabel = textGo.AddComponent<TextMeshProUGUI>();
        envOverlayLabel.alignment = TextAlignmentOptions.TopLeft;
        envOverlayLabel.enableWordWrapping = true;
        envOverlayLabel.fontSize = 12f;
        envOverlayLabel.color = new Color(0.75f, 0.98f, 1f, 1f);
        envOverlayLabel.raycastTarget = false;
        if (gpsText != null && gpsText.font != null)
            envOverlayLabel.font = gpsText.font;

        // Ensure correct initial state
        ApplyEnvironmentOverlayVisibility(environmentOverlayStartHidden ? false : true);
    }

    void ApplyEnvironmentOverlayVisibility(bool visible)
    {
        envOverlayPanelVisible = visible;
        if (envOverlayPanel != null)
            envOverlayPanel.SetActive(visible);
        if (envOverlayToggleLabel != null)
            envOverlayToggleLabel.text = visible ? "Env debug: ON (tap to hide)" : "Env debug: OFF (tap to show)";
    }

    void RefreshEnvironmentTransformOverlay()
    {
        if (!Application.isPlaying) return;

        if (!showEnvironmentTransformOverlay)
        {
            if (envOverlayRoot != null)
                envOverlayRoot.SetActive(false);
            return;
        }

        EnsureEnvironmentTransformOverlay();
        if (envOverlayRoot != null && !envOverlayRoot.activeSelf)
            envOverlayRoot.SetActive(true);

        RefreshRuntimeUpdateIndicator();
        if (!envOverlayPanelVisible) return;
        if (envOverlayLabel == null) return;

        float iv = Mathf.Max(0.05f, environmentOverlayRefreshInterval);
        if (Time.unscaledTime - lastEnvironmentOverlayRefreshTime < iv) return;
        lastEnvironmentOverlayRefreshTime = Time.unscaledTime;

        ResolveMapBkRootIfNeeded();
        envOverlayLabel.text = BuildEnvironmentOverlayText();
    }

    void RefreshRuntimeUpdateIndicator()
    {
        if (!showRuntimeUpdateIndicator) return;
        if (updateIndicatorDot == null || updateIndicatorText == null) return;

        float now = Time.unscaledTime;
        float gpsAge = now - lastGpsFixUnscaledTime;
        float headingAge = now - lastHeadingUpdateUnscaledTime;

        Color baseColor;
        if (gpsAge <= Mathf.Max(0.1f, gpsStaleAfterSeconds))
            baseColor = new Color(0.22f, 0.92f, 0.46f, 1f);
        else if (headingAge <= Mathf.Max(0.1f, headingStaleAfterSeconds))
            baseColor = new Color(1.0f, 0.74f, 0.2f, 1f);
        else
            baseColor = new Color(1.0f, 0.28f, 0.28f, 1f);

        float pulseAlpha = indicatorPulse ? 0.45f + 0.55f * Mathf.PingPong(now * 6f, 1f) : 1f;
        updateIndicatorDot.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(pulseAlpha));

        string gpsAgeText = lastGpsFixUnscaledTime < -100f ? "N/A" : $"{gpsAge:0.0}s";
        string headingAgeText = lastHeadingUpdateUnscaledTime < -100f ? "N/A" : $"{headingAge:0.0}s";
        updateIndicatorText.text =
            $"Updates: GPS#{gpsFixCounter} ({gpsAgeText} ago) | Heading#{headingCounter} ({headingAgeText} ago) | {Input.location.status}";
    }

    string BuildEnvironmentOverlayText()
    {
        var sb = new StringBuilder(640);
        sb.AppendLine("<b>Environment / map poses</b>");
        AppendPoseLines(sb, "UserIcon", transform);
        AppendPoseLines(sb, "mapPlane", mapPlane);
        AppendPoseLines(sb, "MapBK", mapBkRoot);
        AppendPoseLines(sb, "compassVisualRoot", compassVisualRoot);
        Transform xrT = xrOrigin != null ? xrOrigin.transform : null;
        AppendPoseLines(sb, "xrOrigin", xrT);
        Transform camT = mainCamera != null ? mainCamera.transform : null;
        AppendPoseLines(sb, "mainCamera", camT);

        sb.Append($"rotateMapPlane={rotateMapPlaneWithCompass} | ");
        sb.AppendLine($"instantXrAlign={instantXROriginAlign}");

        float raw = useMockCompass
            ? mockCompassHeading
            : Input.compass.enabled ? Input.compass.trueHeading : float.NaN;
        if (!float.IsNaN(raw))
            sb.AppendLine($"compass rawHeading={raw:F1}° | smoothed={smoothedHeading:F1}°");
        else
            sb.AppendLine($"compass (off/mock) smoothed={smoothedHeading:F1}°");

        return sb.ToString();
    }

    static void AppendPoseLines(StringBuilder sb, string label, Transform t)
    {
        if (t == null)
        {
            sb.AppendLine($"{label}: <null>");
            return;
        }

        Vector3 w = t.position;
        Vector3 we = t.eulerAngles;
        Vector3 lp = t.localPosition;
        Vector3 le = t.localEulerAngles;
        sb.AppendLine($"{label}  (parent: {(t.parent != null ? t.parent.name : "—")})");
        sb.AppendLine($"  w pos {w.x:F2}, {w.y:F2}, {w.z:F2}   w euler {we.x:F0}, {we.y:F0}, {we.z:F0}");
        sb.AppendLine($"  l pos {lp.x:F2}, {lp.y:F2}, {lp.z:F2}   l euler {le.x:F0}, {le.y:F0}, {le.z:F0}");
    }

    void ResolveMapBkRootIfNeeded()
    {
        if (mapBkRoot != null || mapBkLookupAttempted) return;
        if (!logTransformDiagnosticsToConsole && !appendTransformDiagToGpsHud && !showEnvironmentTransformOverlay) return;
        mapBkLookupAttempted = true;
        var go = GameObject.Find("MapBK");
        if (go != null)
            mapBkRoot = go.transform;
    }

    void MaybeLogTransformDiagnostics()
    {
        if (!logTransformDiagnosticsToConsole) return;
        if (transformDiagnosticInterval <= 0f) transformDiagnosticInterval = 0.25f;
        if (Time.time - lastTransformDiagnosticLogTime < transformDiagnosticInterval)
            return;
        lastTransformDiagnosticLogTime = Time.time;
        ResolveMapBkRootIfNeeded();

        Transform xrT = xrOrigin != null ? xrOrigin.transform : null;
        Transform camT = mainCamera != null ? mainCamera.transform : null;

        float rawHeading = useMockCompass
            ? mockCompassHeading
            : Input.compass.enabled ? Input.compass.trueHeading : smoothedHeading;

        Debug.Log(
            "[GPSMarker][Transforms]\n" +
            $"{FmtTransformDiag("UserIcon", transform)}\n" +
            $"{FmtTransformDiag("mapPlane", mapPlane)}\n" +
            $"{FmtTransformDiag("mapBk", mapBkRoot)}\n" +
            $"{FmtTransformDiag("xrOrigin", xrT)}\n" +
            $"{FmtTransformDiag("mainCamera", camT)}\n" +
            $"{FmtTransformDiag("compassVisualRoot", compassVisualRoot)}\n" +
            $"rawHeading≈{rawHeading:F1}° | smoothedHeading={smoothedHeading:F1}° | headingApplied={headingApplied}\n" +
            $"parentChain UserIcon=\"{DescribeParentChain(transform)}\"");
    }

    static string DescribeParentChain(Transform t, int maxDepth = 5)
    {
        if (t == null) return "(null)";
        var names = "";
        Transform p = t;
        for (int i = 0; i < maxDepth && p != null; i++)
        {
            names += (names.Length > 0 ? " <- " : "") + p.name;
            p = p.parent;
        }
        return names;
    }

    static string FmtTransformDiag(string label, Transform t)
    {
        if (t == null) return $"{label}: (missing)";
        Vector3 wp = t.position;
        Vector3 we = t.eulerAngles;
        Vector3 lp = t.localPosition;
        Vector3 le = t.localEulerAngles;
        string lossy = $"{t.lossyScale.x:F3},{t.lossyScale.y:F3},{t.lossyScale.z:F3}";
        return $"{label}: world pos=({wp.x:F2},{wp.y:F2},{wp.z:F2}) euler=({we.x:F1},{we.y:F1},{we.z:F1}) | " +
               $"local pos=({lp.x:F2},{lp.y:F2},{lp.z:F2}) local euler=({le.x:F1},{le.y:F1},{le.z:F1}) lossyScale=({lossy})";
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
            lastHorizontalAccuracyMeters = accuracy;

            // Phase 1.2: Timestamp check — only process new GPS fixes
            double currentTimestamp = Input.location.lastData.timestamp;
            bool isNewFix = currentTimestamp > lastGpsTimestamp;

            // If we keep receiving new fixes but accuracy is poor, treat it as "degraded GPS",
            // not "GPS lost" (prevents constant dead-reckoning on noisy devices).
            if (isNewFix)
            {
                lastGpsTimestamp = currentTimestamp;
                lastGpsFixUnscaledTime = Time.unscaledTime;
                gpsFixCounter++;
                indicatorPulse = true;
                if (accuracyOk)
                {
                    lat = Input.location.lastData.latitude;
                    lon = Input.location.lastData.longitude;
                    alt = Input.location.lastData.altitude;

                    ECEF pointECEF = LatLonAltToECEF(lat, lon, alt);
                    ENU enu = ECEFToENU(pointECEF, refECEF, refLat, refLon);
                    Vector3 rawENU = new Vector3((float)enu.e, 0f, (float)enu.n);
                    lastEnuDistanceFromRefMeters = rawENU.magnitude;

                    // Phase 2: EMA smoothing + outlier rejection
                    Vector3 smoothed = ApplySmoothing(rawENU);

                    // Phase 3: Velocity estimation
                    UpdateVelocity(smoothed, currentTimestamp);

                    // Phase 3: Adaptive GPS rate — throttle when stationary
                    adaptiveGpsTimer = 0f;

                    // GPS is good → exit dead reckoning
                    isDeadReckoning = false;
                    gpslostTimer = 0f;
                    hasRecentGoodFix = true;

                    return smoothed;
                }

                // Degraded fix: reset "lost timer" and keep last good position.
                gpslostTimer = 0f;
                isDeadReckoning = false;
                hasRecentGoodFix = false;
            }
        }

        // GPS lost or bad quality
        gpslostTimer += Time.deltaTime;
        hasRecentGoodFix = false;

        if (enableDeadReckoning && gpslostTimer >= gpslostTimeout && positionInitialized)
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

        // No fake ENU origin: until we have a valid position, return current smoothed (if any) and keep state invalid.
        return positionInitialized ? smoothedUserENU : smoothedUserENU;
    }

    void UpdateGpsText()
    {
        if (gpsText == null) return;
        if (Time.time - lastGpsTextUpdateTime < gpsTextUpdateInterval) return;
        lastGpsTextUpdateTime = Time.time;

        float accuracy = Input.location.status == LocationServiceStatus.Running
            ? Input.location.lastData.horizontalAccuracy : -1f;

        string accuracyStr = accuracy >= 0 ? $"±{accuracy:F0}m" : "N/A";
        string statusStr = isDeadReckoning ? "DEAD RECKONING" :
                           (Input.location.status == LocationServiceStatus.Running ? "GPS OK" : "GPS LOST");
        string jumpStr = lastFixRejectedAsJump ? $" | JUMP REJECT {lastRejectedJumpMeters:0}m" : "";

        Vector3 worldPos = transform.position;
        Vector3 localPos = transform.localPosition;
        string positionDetails = showPositionDebugDetails
            ? $"\nWorld X/Z: {worldPos.x:F1}, {worldPos.z:F1} | Local X/Z: {localPos.x:F1}, {localPos.z:F1}"
            : $"\nX: {worldPos.x:F1}  Z: {worldPos.z:F1}";

        if (showPositionDebugDetails && targetObject != null)
        {
            Vector3 targetWorld = targetObject.transform.position;
            Vector3 a = new Vector3(worldPos.x, 0f, worldPos.z);
            Vector3 b = new Vector3(targetWorld.x, 0f, targetWorld.z);
            float flatDistance = Vector3.Distance(a, b);
            positionDetails += $"\nTarget X/Z: {targetWorld.x:F1}, {targetWorld.z:F1} | Flat: {flatDistance:F1}m";
        }

        if (appendTransformDiagToGpsHud)
        {
            ResolveMapBkRootIfNeeded();
            float mapBkYaw = mapBkRoot != null ? mapBkRoot.eulerAngles.y : float.NaN;
            float xrYaw = xrOrigin != null ? xrOrigin.transform.eulerAngles.y : float.NaN;
            string mapBkLine = mapBkRoot != null
                ? $"MapBK worldY={mapBkYaw:F1}° localY={mapBkRoot.localEulerAngles.y:F1}°"
                : "MapBK (not found — assign mapBkRoot or name object MapBK)";
            string xrLine = xrOrigin != null ? $" | XR root worldY={xrYaw:F1}°" : " | XR (null)";
            positionDetails += $"\n{mapBkLine}{xrLine}";
        }

        gpsText.text =
            $"Status: {statusStr}{jumpStr} | Accuracy: {accuracyStr}\n" +
            $"Lat: {lat:F7}  Lon: {lon:F7}\n" +
            $"Speed: {estimatedVelocity.magnitude:F1} m/s | Heading: {smoothedHeading:F1}°" +
            positionDetails;
    }

    void MaybeAlignEnvironmentToXR(Vector3 userENU)
    {
        if (mapPlane == null || mainCamera == null)
        {
            aligned = true;
            return;
        }

        if (!aligned)
        {
            Vector3 basePos = mapPlane != null ? mapPlane.position : Vector3.zero;
            Vector3 worldPos = basePos + new Vector3(userENU.x, 0f, userENU.z);
            ApplyGpsWorldXZToXROriginSmoothOnly(worldPos);
            aligned = true;
            lastAlignedUserENU = userENU;
            lastEnvironmentAlignTime = Time.time;
            return;
        }

        if (!continuouslyAlignEnvironment)
        {
            return;
        }

        if (Time.time - lastEnvironmentAlignTime < environmentRealignInterval)
        {
            return;
        }

        if (Vector3.Distance(userENU, lastAlignedUserENU) < environmentRealignDistanceMeters)
        {
            return;
        }

        AlignEnvironmentToXR();
        lastAlignedUserENU = userENU;
        lastEnvironmentAlignTime = Time.time;
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
