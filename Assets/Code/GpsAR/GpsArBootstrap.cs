using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace TestAR.GpsAR
{
    /// <summary>
    /// One-shot bootstrap for GPS-only outdoor AR navigation.
    ///
    /// Three sequential steps, all done ONCE at boot:
    ///   1. Hygiene: reset XR Origin rotation/scale, disable GPSMarker.instantXROriginAlign.
    ///   2. Bearing calibration (THE critical step):
    ///      ARCore picks the AR session frame from camera direction at session start, so by default
    ///      Unity world +Z is NOT real North. GPSMarker treats Unity +X = East, +Z = North (ENU),
    ///      so without calibration every POI/path/target will be rotated by whatever heading the
    ///      user happened to face when opening the app — this is the "world East follows initial
    ///      camera direction" symptom.
    ///      Fix: rotate XR Origin around Y so camera.world.eulerAngles.y == compass.trueHeading,
    ///      i.e. Unity +Z aligns with real North. Done ONCE before any ARAnchor exists, so anchors
    ///      created afterwards by AnchoredPOI live in the correctly-aligned frame and stay neo.
    ///   3. GPS stability gate: wait for N consecutive good fixes, then flip <see cref="Aligned"/>.
    ///      AnchoredPOI components observe this flag and only spawn ARAnchors after this point.
    ///
    /// Once Aligned == true, this script never touches XR Origin again. Heading drift after that is
    /// up to ARCore VIO/IMU, not the compass — which is what we want, because compass is unreliable
    /// in cities. If you need to recover from a bad calibration, call <see cref="RecalibrateBearing"/>
    /// (note: it teleports content; existing anchors should be re-created).
    /// </summary>
    public class GpsArBootstrap : MonoBehaviour
    {
        public static GpsArBootstrap Active { get; private set; }

        [Header("References")]
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private GPSMarker gpsMarker;

        [Header("XR Origin Hygiene")]
        [Tooltip("Reset XR Origin rotation to identity at boot. Strongly recommended for AR Foundation.")]
        [SerializeField] private bool forceIdentityRotation = true;
        [Tooltip("Reset XR Origin scale to (1,1,1) at boot. Strongly recommended for AR Foundation.")]
        [SerializeField] private bool forceUnitScale = true;

        [Header("GPSMarker Hygiene")]
        [Tooltip("Disable GPSMarker.instantXROriginAlign so XR Origin is smoothly lerped (less jitter).")]
        [SerializeField] private bool disableInstantAlign = true;

        [Header("Bearing Calibration (one-shot, before any anchor)")]
        [Tooltip("Rotate XR Origin once so Unity +Z = real North (camera world yaw matches compass).")]
        [SerializeField] private bool calibrateBearing = true;
        [Tooltip("Compass heading must stay within this deviation across the stable window to lock.")]
        [SerializeField] private float compassMaxDeviationDegrees = 8f;
        [Tooltip("Compass must stay stable for this many seconds before we trust it.")]
        [SerializeField] private float compassStableSeconds = 1.5f;
        [Tooltip("Minimum time between compass samples for the stability check.")]
        [SerializeField] private float compassSampleInterval = 0.1f;
        [Tooltip("If compass never stabilizes within this time, fall back to the last reading.")]
        [SerializeField] private float compassFallbackAfterSeconds = 8f;

        [Header("GPS Stability Gate")]
        [SerializeField] private float minTimeSinceStartSeconds = 2f;
        [SerializeField] private float maxAcceptableAccuracyMeters = 15f;
        [SerializeField] private int requiredConsecutiveGoodFixes = 5;
        [Tooltip("If running in Editor without GPS, force-aligned after this delay so AnchoredPOI can demo.")]
        [SerializeField] private bool autoAlignInEditor = true;

        [Header("Status UI (optional)")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Debug Read-Only")]
        [SerializeField] private bool bearingCalibrated;
        [SerializeField] private float bearingOffsetDegrees;
        [SerializeField] private float lastCompassHeading;

        public bool Aligned { get; private set; }
        public bool BearingCalibrated => bearingCalibrated;
        public float BearingOffsetDegrees => bearingOffsetDegrees;

        private int consecutiveGoodFixes;
        private float startTime;

        // Compass calibration state
        private float compassStableSince = -1f;
        private float compassAnchorHeading;
        private float lastCompassSampleTime;
        private float bearingPhaseStartTime;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(this);
                return;
            }
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Start()
        {
            startTime = Time.time;

            if (xrOrigin == null) xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (gpsMarker == null) gpsMarker = FindFirstObjectByType<GPSMarker>(FindObjectsInactive.Include);

            ApplyXrOriginHygiene();
            ApplyGpsMarkerHygiene();

            Input.compass.enabled = true;
        }

        private void Update()
        {
            if (Aligned)
            {
                UpdateStatusText(null);
                return;
            }

            if (Time.time - startTime < minTimeSinceStartSeconds)
            {
                UpdateStatusText("Waiting for AR session…");
                return;
            }

#if UNITY_EDITOR
            if (autoAlignInEditor)
            {
                if (!bearingCalibrated)
                {
                    bearingCalibrated = true;
                    bearingOffsetDegrees = xrOrigin != null ? xrOrigin.transform.eulerAngles.y : 0f;
                }
                MarkAligned("Editor auto-align");
                return;
            }
#endif

            // Phase 1: bearing calibration (compass-driven, one-shot)
            if (calibrateBearing && !bearingCalibrated)
            {
                if (bearingPhaseStartTime <= 0f) bearingPhaseStartTime = Time.time;
                TryCompassCalibrate();
                UpdateStatusText(
                    $"Calibrating bearing… hold steady\n" +
                    $"compass: {lastCompassHeading:0.0}°  (stable: {GetCompassStableProgress()})");
                return;
            }

            // Phase 2: GPS stability
            if (gpsMarker == null)
            {
                UpdateStatusText("GPSMarker not assigned");
                return;
            }

            bool fixOk = gpsMarker.HasRecentGoodFix
                         && gpsMarker.LastHorizontalAccuracyMeters > 0
                         && gpsMarker.LastHorizontalAccuracyMeters <= maxAcceptableAccuracyMeters
                         && !gpsMarker.LastFixRejectedAsJump;

            if (fixOk) consecutiveGoodFixes++;
            else consecutiveGoodFixes = 0;

            UpdateStatusText(
                $"Calibrating GPS… {consecutiveGoodFixes}/{requiredConsecutiveGoodFixes}\n" +
                $"acc: {gpsMarker.LastHorizontalAccuracyMeters:0.0} m  bearing: {bearingOffsetDegrees:0.0}°");

            if (consecutiveGoodFixes >= requiredConsecutiveGoodFixes)
            {
                MarkAligned("Stable GPS fix");
            }
        }

        // ─── Bearing calibration ──────────────────────────────────────────────────

        private void TryCompassCalibrate()
        {
            if (Input.compass.timestamp <= 0)
            {
                if (Time.time - bearingPhaseStartTime > compassFallbackAfterSeconds)
                {
                    Debug.LogWarning("[GpsArBootstrap] Compass never produced a sample; bearing calibration falls back to identity.");
                    bearingCalibrated = true;
                }
                return;
            }

            if (Time.time - lastCompassSampleTime < compassSampleInterval) return;
            lastCompassSampleTime = Time.time;

            float h = Input.compass.trueHeading;
            lastCompassHeading = h;

            if (compassStableSince < 0f)
            {
                compassAnchorHeading = h;
                compassStableSince = Time.time;
                return;
            }

            float dev = Mathf.Abs(Mathf.DeltaAngle(compassAnchorHeading, h));
            if (dev > compassMaxDeviationDegrees)
            {
                compassAnchorHeading = h;
                compassStableSince = Time.time;
            }
            else if (Time.time - compassStableSince >= compassStableSeconds)
            {
                ApplyBearingCalibration(h);
            }
            else if (Time.time - bearingPhaseStartTime > compassFallbackAfterSeconds)
            {
                Debug.LogWarning($"[GpsArBootstrap] Compass not stable after {compassFallbackAfterSeconds:0.0}s (dev {dev:0.0}°); locking with last value {h:0.0}°.");
                ApplyBearingCalibration(h);
            }
        }

        /// <summary>
        /// Rotate XR Origin so camera.world.eulerAngles.y matches the given compass heading.
        /// After this, Unity +Z aligns with real-world North and ENU placement is correct.
        /// </summary>
        public void ApplyBearingCalibration(float compassHeading)
        {
            if (xrOrigin == null) return;
            var arCamera = xrOrigin.Camera != null ? xrOrigin.Camera : Camera.main;
            if (arCamera == null) return;

            float camYaw = arCamera.transform.eulerAngles.y;
            float delta = Mathf.DeltaAngle(camYaw, compassHeading);

            float oldY = xrOrigin.transform.eulerAngles.y;
            float newY = (oldY + delta + 360f) % 360f;
            xrOrigin.transform.rotation = Quaternion.Euler(0f, newY, 0f);

            bearingOffsetDegrees = newY;
            bearingCalibrated = true;

            Debug.Log(
                $"[GpsArBootstrap] Bearing calibrated.\n" +
                $"  cameraYaw(before) = {camYaw:0.0}°\n" +
                $"  compass.trueHeading = {compassHeading:0.0}°\n" +
                $"  delta applied = {delta:0.0}°\n" +
                $"  XR Origin Y: {oldY:0.0}° -> {newY:0.0}°");
        }

        /// <summary>
        /// Reset bearing calibration. Call this when the user requests a manual recalibration.
        /// WARNING: existing ARAnchors will appear to teleport — destroy/recreate them via your
        /// own POI manager, or rely on AnchoredPOI's drift-based re-anchor (it will catch up).
        /// </summary>
        public void RecalibrateBearing()
        {
            bearingCalibrated = false;
            compassStableSince = -1f;
            bearingPhaseStartTime = Time.time;
            Aligned = false;
            consecutiveGoodFixes = 0;
            Debug.Log("[GpsArBootstrap] RecalibrateBearing requested.");
        }

        private string GetCompassStableProgress()
        {
            if (compassStableSince < 0f) return "0%";
            float t = (Time.time - compassStableSince) / Mathf.Max(0.01f, compassStableSeconds);
            return $"{Mathf.Clamp01(t) * 100f:0}%";
        }

        // ─── Hygiene ──────────────────────────────────────────────────────────────

        private void MarkAligned(string reason)
        {
            Aligned = true;
            Debug.Log($"[GpsArBootstrap] Aligned ({reason}). POIs may now anchor. Bearing offset: {bearingOffsetDegrees:0.0}°");
            UpdateStatusText("AR ready");
        }

        private void ApplyXrOriginHygiene()
        {
            if (xrOrigin == null)
            {
                Debug.LogWarning("[GpsArBootstrap] XR Origin not assigned; skipping hygiene.");
                return;
            }

            var t = xrOrigin.transform;

            if (forceIdentityRotation && Quaternion.Angle(t.rotation, Quaternion.identity) > 0.01f)
            {
                Debug.Log($"[GpsArBootstrap] Resetting XR Origin rotation from {t.rotation.eulerAngles} to identity.");
                t.rotation = Quaternion.identity;
            }

            if (forceUnitScale && (t.localScale - Vector3.one).sqrMagnitude > 1e-6f)
            {
                Debug.Log($"[GpsArBootstrap] Resetting XR Origin scale from {t.localScale} to (1,1,1).");
                t.localScale = Vector3.one;
            }
        }

        private void ApplyGpsMarkerHygiene()
        {
            if (gpsMarker == null || !disableInstantAlign) return;

            if (gpsMarker.instantXROriginAlign)
            {
                Debug.Log("[GpsArBootstrap] Disabling GPSMarker.instantXROriginAlign for smooth lerp.");
                gpsMarker.instantXROriginAlign = false;
            }
        }

        private void UpdateStatusText(string transientState)
        {
            if (statusText == null) return;

            int anchorCount = POIAnchorService.Instance != null
                ? POIAnchorService.Instance.ActiveAnchorCount
                : 0;
            bool hasAnchorMgr = POIAnchorService.Instance != null && POIAnchorService.Instance.HasAnchorManager;
            string anchorInfo = hasAnchorMgr
                ? $"Anchors: {anchorCount}"
                : "Anchors: (no AR mgr)";

            if (Aligned)
            {
                float acc = gpsMarker != null ? gpsMarker.LastHorizontalAccuracyMeters : -1f;
                statusText.text =
                    $"AR ready  |  {anchorInfo}\n" +
                    $"GPS acc: {acc:0.0} m  bearing: {bearingOffsetDegrees:0.0}°";
            }
            else if (!string.IsNullOrEmpty(transientState))
            {
                statusText.text = $"{transientState}\n{anchorInfo}";
            }
        }
    }
}
