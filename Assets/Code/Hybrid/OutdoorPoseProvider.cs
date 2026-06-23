using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Pose source outdoor cho HybridLocalizationManager.
    ///
    /// Ưu tiên đọc theo thứ tự:
    ///   1. <see cref="GPSMarker"/> đã có sẵn pipeline filter/smooth/jump-reject.
    ///   2. <see cref="SimpleGPSTracker"/> (variant mới, dùng XROrigin).
    ///   3. (Editor only) <see cref="useCameraAsUserPositionInEditor"/>: trả vị trí AR camera —
    ///      cho phép fly XR Origin tay để test trigger anchor.
    ///
    /// Component KHÔNG chạm XR Origin. Chỉ đọc.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class OutdoorPoseProvider : MonoBehaviour
    {
        public enum Source
        {
            None,
            GpsMarker,
            SimpleGpsTracker,
            EditorCameraFallback,
        }

        [Header("References (auto-resolve nếu null)")]
        [SerializeField] private GPSMarker gpsMarker;
        [SerializeField] private SimpleGPSTracker simpleGpsTracker;
        [SerializeField] private Camera arCamera;

        [Tooltip("XR Origin transform — ưu tiên đọc position từ đây ở editor fallback (vì AR Foundation simulator có thể override AR camera pose).")]
        [SerializeField] private Transform xrOriginTransform;

        [Header("Quality thresholds")]
        [Tooltip("Accuracy tối đa (m) để coi là fresh fix.")]
        [SerializeField] private float maxAcceptableAccuracyMeters = 30f;

        [Header("Editor")]
        [Tooltip("Trong Editor (không có GPS thật), dùng vị trí AR camera làm user position để test trigger.")]
        [SerializeField] private bool useCameraAsUserPositionInEditor = true;

        [Tooltip("Trong Editor, luôn coi pose là fresh nếu fallback camera đang hoạt động.")]
        [SerializeField] private bool editorAlwaysFresh = true;

        [Header("Discovery")]
        [SerializeField] private float discoveryIntervalSeconds = 1f;

        private float _nextDiscoveryTime;

        // ---------------------------------------------------------------------
        // Public read-only
        // ---------------------------------------------------------------------

        public Vector3 UserCampusPosition { get; private set; }
        public float AccuracyMeters { get; private set; } = float.PositiveInfinity;
        public bool LastFixRejectedAsJump { get; private set; }
        public bool HasFreshFix { get; private set; }
        public Source ActiveSource { get; private set; } = Source.None;

        // Mock override (test/editor). Khi <see cref="_useMock"/> true, các nguồn thật bị bỏ qua.
        private bool _useMock;
        private Vector3 _mockPosition;
        private float _mockAccuracy;

        /// <summary>Force user campus position để test trigger (bypass GPS + camera fallback).</summary>
        public void SetMockUserPosition(Vector3 campusPos, float accuracyMeters = 2f)
        {
            _useMock = true;
            _mockPosition = campusPos;
            _mockAccuracy = accuracyMeters;
        }

        public void ClearMockUserPosition()
        {
            _useMock = false;
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            TryDiscover(force: true);
        }

        private void Update()
        {
            if (Time.time >= _nextDiscoveryTime)
            {
                _nextDiscoveryTime = Time.time + discoveryIntervalSeconds;
                TryDiscover(force: false);
            }

            Sample();
        }

        private void TryDiscover(bool force)
        {
            if (gpsMarker == null || force)
            {
                if (gpsMarker == null)
                {
                    gpsMarker = FindFirstObjectByType<GPSMarker>(FindObjectsInactive.Include);
                }
            }
            if (simpleGpsTracker == null || force)
            {
                if (simpleGpsTracker == null)
                {
                    simpleGpsTracker = FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
                }
            }
            if (arCamera == null || force)
            {
                if (arCamera == null) arCamera = Camera.main;
                if (arCamera == null)
                {
                    var xr = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
                    if (xr != null) arCamera = xr.Camera;
                }
            }
            if (xrOriginTransform == null || force)
            {
                var xr = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
                if (xr != null) xrOriginTransform = xr.transform;
            }
        }

        private void Sample()
        {
            // 0. Mock override (test/scenario runner).
            if (_useMock)
            {
                ActiveSource = Source.EditorCameraFallback;
                UserCampusPosition = _mockPosition;
                AccuracyMeters = _mockAccuracy;
                LastFixRejectedAsJump = false;
                HasFreshFix = true;
                return;
            }

            // 1. GPSMarker pipeline (đầy đủ filter/smooth/jump-reject sẵn).
            if (gpsMarker != null && arCamera != null && gpsMarker.HasRecentGoodFix)
            {
                ActiveSource = Source.GpsMarker;
                UserCampusPosition = arCamera.transform.position;
                AccuracyMeters = gpsMarker.LastHorizontalAccuracyMeters;
                LastFixRejectedAsJump = gpsMarker.LastFixRejectedAsJump;
                HasFreshFix = AccuracyMeters > 0f
                              && AccuracyMeters <= maxAcceptableAccuracyMeters
                              && !LastFixRejectedAsJump;
                return;
            }

            // 2. SimpleGPSTracker (variant mới).
            if (simpleGpsTracker != null && simpleGpsTracker.HasLocationFix)
            {
                ActiveSource = Source.SimpleGpsTracker;
                UserCampusPosition = simpleGpsTracker.SmoothedWorldPosition;
                AccuracyMeters = simpleGpsTracker.CurrentHorizontalAccuracy;
                LastFixRejectedAsJump = simpleGpsTracker.LastFixRejectedAsJump;
                HasFreshFix = AccuracyMeters > 0f
                              && AccuracyMeters <= maxAcceptableAccuracyMeters
                              && !LastFixRejectedAsJump;
                return;
            }

            // 3. Editor fallback: ưu tiên XR Origin position (vì AR Foundation simulator
            //    có thể override AR camera pose về (~0,0,0) bất chấp XR Origin đã được
            //    WASD đi xa). Fallback về camera nếu không có XR Origin.
#if UNITY_EDITOR
            if (useCameraAsUserPositionInEditor)
            {
                Transform src = xrOriginTransform != null ? xrOriginTransform
                                : (arCamera != null ? arCamera.transform : null);
                if (src != null)
                {
                    ActiveSource = Source.EditorCameraFallback;
                    UserCampusPosition = src.position;
                    AccuracyMeters = 0f;
                    LastFixRejectedAsJump = false;
                    HasFreshFix = editorAlwaysFresh;
                    return;
                }
            }
#endif

            ActiveSource = Source.None;
            HasFreshFix = false;
            AccuracyMeters = float.PositiveInfinity;
        }
    }
}
