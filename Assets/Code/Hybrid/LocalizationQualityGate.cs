using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Gate đánh giá chất lượng pose outdoor + indoor để state machine ra quyết định transition.
    ///
    /// Hysteresis: mỗi cờ chỉ flip true sau khi điều kiện thỏa liên tục N giây
    /// (<see cref="outdoorStableSeconds"/>, <see cref="indoorStableSeconds"/>),
    /// và flip false sau khi vi phạm liên tục M giây (<see cref="indoorLostGraceSeconds"/>, …).
    /// Mục đích: tránh nhảy state khi GPS/VPS chớp tắt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public class LocalizationQualityGate : MonoBehaviour
    {
        [Header("References (auto-resolve nếu null)")]
        [SerializeField] private OutdoorPoseProvider outdoorProvider;
        [SerializeField] private MultisetPoseProvider indoorProvider;

        [Header("Outdoor thresholds")]
        [Tooltip("Accuracy tối đa (m).")]
        [SerializeField] private float outdoorMaxAccuracyMeters = 15f;

        [Tooltip("Số giây pose outdoor phải fresh + accurate liên tục trước khi flag Ready true.")]
        [SerializeField] private float outdoorStableSeconds = 2f;

        [Tooltip("Số giây pose outdoor mất / kém trước khi flag Ready về false.")]
        [SerializeField] private float outdoorLossGraceSeconds = 3f;

        [Header("Indoor thresholds")]
        [Tooltip("Confidence VPS tối thiểu (range tuỳ SDK). 0 = không lọc.")]
        [SerializeField] private float indoorMinConfidence = 0.5f;

        [Tooltip("Số lần localize success liên tiếp (dựa trên freshness ticks). " +
                 "1 = accept sớm hơn khi vào tòa (path/phase Indoor hiện nhanh).")]
        [SerializeField] private int indoorRequiredConsecutiveSuccesses = 1;

        [Tooltip("Số giây pose indoor phải fresh liên tục trước khi IndoorReady. " +
                 "Thấp hơn = handover Outdoor→Indoor nhanh hơn; quá thấp dễ flicker.")]
        [SerializeField] private float indoorStableSeconds = 0.6f;

        [Tooltip("Số giây pose indoor mất trước khi coi là lost.")]
        [SerializeField] private float indoorLostGraceSeconds = 2.5f;

        [Header("Building profile (optional)")]
        [Tooltip("Profile của building hiện hành. Nếu set, override confidence threshold + accepted map ids.")]
        [SerializeField] private BuildingLocalizationProfile activeProfile;

        // ---------------------------------------------------------------------
        // Public read-only
        // ---------------------------------------------------------------------

        public bool OutdoorReady { get; private set; }
        public bool IndoorReady { get; private set; }
        public bool IndoorLost { get; private set; }
        public int IndoorConsecutiveSuccesses { get; private set; }
        public string LastIndoorRejectReason { get; private set; } = "";

        // Mock overrides for testing/scenario runner.
        private bool _mockIndoorEnabled;
        private bool _mockIndoorReady;
        private bool _mockOutdoorEnabled;
        private bool _mockOutdoorReady;

        public void SetMockIndoorReady(bool ready)
        {
            _mockIndoorEnabled = true;
            _mockIndoorReady = ready;
        }

        public void ClearMockIndoorReady() => _mockIndoorEnabled = false;

        public void SetMockOutdoorReady(bool ready)
        {
            _mockOutdoorEnabled = true;
            _mockOutdoorReady = ready;
        }

        public void ClearMockOutdoorReady() => _mockOutdoorEnabled = false;

        // ---------------------------------------------------------------------
        // Internal counters
        // ---------------------------------------------------------------------

        private float _outdoorStableSince = -1f;
        private float _outdoorBadSince = -1f;
        private float _indoorStableSince = -1f;
        private float _indoorLostSince = -1f;
        private double _lastIndoorFreshTimestamp;

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public void SetActiveProfile(BuildingLocalizationProfile profile)
        {
            activeProfile = profile;
        }

        public void ResetCounters()
        {
            _outdoorStableSince = -1f;
            _outdoorBadSince = -1f;
            _indoorStableSince = -1f;
            _indoorLostSince = -1f;
            IndoorConsecutiveSuccesses = 0;
            OutdoorReady = false;
            IndoorReady = false;
            IndoorLost = true;
            LastIndoorRejectReason = "";
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            if (outdoorProvider == null) outdoorProvider = FindFirstObjectByType<OutdoorPoseProvider>(FindObjectsInactive.Include);
            if (indoorProvider == null) indoorProvider = FindFirstObjectByType<MultisetPoseProvider>(FindObjectsInactive.Include);
            ResetCounters();
        }

        private void Update()
        {
            if (_mockOutdoorEnabled)
            {
                OutdoorReady = _mockOutdoorReady;
            }
            else
            {
                EvaluateOutdoor();
            }

            if (_mockIndoorEnabled)
            {
                IndoorReady = _mockIndoorReady;
                IndoorLost = !_mockIndoorReady;
                LastIndoorRejectReason = _mockIndoorReady ? "" : "mock=lost";
            }
            else
            {
                EvaluateIndoor();
            }
        }

        // ---------------------------------------------------------------------
        // Evaluation
        // ---------------------------------------------------------------------

        private void EvaluateOutdoor()
        {
            if (outdoorProvider == null)
            {
                OutdoorReady = false;
                return;
            }

            bool ok = outdoorProvider.HasFreshFix
                      && outdoorProvider.AccuracyMeters > 0f
                      && outdoorProvider.AccuracyMeters <= outdoorMaxAccuracyMeters
                      && !outdoorProvider.LastFixRejectedAsJump;

            if (ok)
            {
                _outdoorBadSince = -1f;
                if (_outdoorStableSince < 0f) _outdoorStableSince = Time.time;
                if (Time.time - _outdoorStableSince >= outdoorStableSeconds)
                {
                    OutdoorReady = true;
                }
            }
            else
            {
                _outdoorStableSince = -1f;
                if (_outdoorBadSince < 0f) _outdoorBadSince = Time.time;
                if (Time.time - _outdoorBadSince >= outdoorLossGraceSeconds)
                {
                    OutdoorReady = false;
                }
            }
        }

        private void EvaluateIndoor()
        {
            if (indoorProvider == null)
            {
                IndoorReady = false;
                IndoorLost = true;
                LastIndoorRejectReason = "no provider";
                return;
            }

            var last = indoorProvider.Last;
            float minConf = activeProfile != null ? activeProfile.MinAcceptedConfidence : indoorMinConfidence;
            // Profile có thể siết chặt hơn; gate field là floor tối thiểu. Dùng Min để không
            // vô tình cộng dồn "max(2,1)=2" làm handover vào tòa chậm hơn default mới.
            int requiredHits = activeProfile != null
                ? Mathf.Max(1, Mathf.Min(activeProfile.RequiredConsecutiveSuccesses, indoorRequiredConsecutiveSuccesses))
                : Mathf.Max(1, indoorRequiredConsecutiveSuccesses);

            bool fresh = indoorProvider.HasFreshPose;
            bool confOk = last.Confidence >= minConf;
            bool mapIdOk = activeProfile == null
                           || string.IsNullOrEmpty(last.MapId)
                           || activeProfile.IsAcceptedMapId(last.MapId);

            if (fresh && confOk && mapIdOk)
            {
                // Đếm lên mỗi lần timestamp tăng (event/fallback fire).
                if (last.Timestamp > _lastIndoorFreshTimestamp + 0.01)
                {
                    IndoorConsecutiveSuccesses++;
                    _lastIndoorFreshTimestamp = last.Timestamp;
                }
                _indoorLostSince = -1f;
                if (_indoorStableSince < 0f) _indoorStableSince = Time.time;
                bool stable = (Time.time - _indoorStableSince) >= indoorStableSeconds;

                if (stable && IndoorConsecutiveSuccesses >= requiredHits)
                {
                    IndoorReady = true;
                    IndoorLost = false;
                    LastIndoorRejectReason = "";
                    return;
                }
                LastIndoorRejectReason = !stable
                    ? $"warming ({Time.time - _indoorStableSince:0.0}s/{indoorStableSeconds:0.0}s)"
                    : $"hits {IndoorConsecutiveSuccesses}/{requiredHits}";
                return;
            }

            // Pose không pass → reset đếm.
            _indoorStableSince = -1f;
            IndoorConsecutiveSuccesses = 0;
            LastIndoorRejectReason = !fresh ? "stale" : (!confOk ? $"conf<{minConf:0.00}" : "mapId mismatch");

            if (_indoorLostSince < 0f) _indoorLostSince = Time.time;
            if (Time.time - _indoorLostSince >= indoorLostGraceSeconds)
            {
                IndoorReady = false;
                IndoorLost = true;
            }
        }
    }
}
