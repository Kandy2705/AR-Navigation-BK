using System;
using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// State machine trung tâm cho hybrid indoor/outdoor handover (Vòng 2).
    ///
    /// Trách nhiệm:
    ///   - Driver duy nhất chuyển <see cref="HybridNavigationState"/>.
    ///   - Đọc gate (<see cref="LocalizationQualityGate"/>) để ra quyết định transition.
    ///   - Khi cần đổi mode (Outdoor↔Indoor) thì gọi <see cref="IndoorMapSwitcher.EnterIndoor"/> /
    ///     <see cref="IndoorMapSwitcher.ExitToOutdoor"/> — vốn đã ép HybridModeController.
    ///   - Phơi ra "current user campus pose" (Round 3 sẽ dùng cho route).
    ///   - KHÔNG mutate XR Origin. KHÔNG mở UI login/popup.
    ///
    /// Trigger logic Round 2:
    ///   - Outdoor: tìm <see cref="EntranceAnchor"/> gần nhất (lọc theo
    ///     <see cref="destinationBuilding"/>). Trong vùng approach → ApproachingEntrance.
    ///   - ApproachingEntrance: trong triggerRadius của anchor → TransitionScanning + EnterIndoor.
    ///   - TransitionScanning: gate.IndoorReady → Indoor. Timeout → Relocalization.
    ///   - Indoor: gate.IndoorLost → Relocalization.
    ///   - Relocalization: gate.IndoorReady lại → Indoor. Timeout → Uncertain.
    ///   - ExitingIndoor: gate.OutdoorReady → Outdoor + ExitToOutdoor.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30)]
    public class HybridLocalizationManager : MonoBehaviour
    {
        [Header("Providers / gate (auto-resolve nếu null)")]
        [SerializeField] private OutdoorPoseProvider outdoorProvider;
        [SerializeField] private MultisetPoseProvider indoorProvider;
        [SerializeField] private LocalizationQualityGate qualityGate;

        [Header("Indoor switcher / mode controller (auto-resolve nếu null)")]
        [SerializeField] private IndoorMapSwitcher indoorMapSwitcher;
        [SerializeField] private HybridModeController hybridModeController;

        [Header("Destination (optional)")]
        [Tooltip("Building đích cuối — dùng để lọc EntranceAnchor. BuildingId.None = approach bất kỳ entrance gần nhất.")]
        [SerializeField] private BuildingId destinationBuilding = BuildingId.None;

        [Tooltip("Profile của building. Nếu null sẽ tự dùng default thresholds trên gate.")]
        [SerializeField] private BuildingLocalizationProfile destinationProfile;

        [Header("Trigger geometry")]
        [Tooltip("Nhân với EntranceAnchor.triggerRadiusMeters để ra vùng ApproachingEntrance.")]
        [SerializeField] private float approachingRadiusMultiplier = 2.5f;

        [Tooltip("Khoảng giây min ở mỗi state mới (chống dao động). Thấp hơn = vào Indoor sớm hơn sau VPS.")]
        [SerializeField] private float minStateDwellSeconds = 0.25f;

        [Header("Timeouts")]
        [Tooltip("TransitionScanning → Relocalization nếu không localize sau X giây.")]
        [SerializeField] private float scanTimeoutSeconds = 25f;

        [Tooltip("Relocalization → Uncertain nếu không recover sau X giây.")]
        [SerializeField] private float relocalizationGiveUpSeconds = 60f;
        [SerializeField] private float relocalizationRetrySeconds = 5f;

        [Header("Automatic exit handover")]
        [SerializeField] private bool automaticExitWhenVpsLostNearDoor = true;
        [SerializeField] private float automaticExitOutdoorStableSeconds = 4f;
        [SerializeField] private float automaticExitRadiusMultiplier = 2f;

        [Header("Tick")]
        [SerializeField] private float evaluationIntervalSeconds = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        // ---------------------------------------------------------------------
        // Public read-only
        // ---------------------------------------------------------------------

        public HybridNavigationState CurrentState { get; private set; } = HybridNavigationState.Outdoor;
        public string LastTransitionReason { get; private set; } = "boot";
        public double LastTransitionTime { get; private set; }
        public BuildingId ActiveBuilding { get; private set; } = BuildingId.None;
        public EntranceAnchor ActiveEntrance { get; private set; }
        public float DistanceToActiveEntrance { get; private set; } = float.PositiveInfinity;

        /// <summary>Pose nguồn duy nhất cho navigation: outdoor → từ OutdoorPoseProvider; indoor states → từ MultisetPoseProvider (campus world).</summary>
        public Vector3 CurrentUserCampusPosition { get; private set; }
        public Quaternion CurrentUserCampusRotation { get; private set; } = Quaternion.identity;
        public bool CurrentPoseIsFresh { get; private set; }

        public OutdoorPoseProvider OutdoorProvider => outdoorProvider;
        public MultisetPoseProvider IndoorProvider => indoorProvider;
        public LocalizationQualityGate QualityGate => qualityGate;

        public event Action<HybridNavigationState, HybridNavigationState, string> OnStateChanged;

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public void SetDestinationBuilding(BuildingId buildingId, BuildingLocalizationProfile profile = null)
        {
            destinationBuilding = buildingId;
            destinationProfile = profile;
            if (qualityGate != null) qualityGate.SetActiveProfile(profile);
            if (indoorProvider != null) indoorProvider.SetCurrentBuilding(buildingId, "");
        }

        public void RequestExit()
        {
            if (CurrentState == HybridNavigationState.Indoor || CurrentState == HybridNavigationState.Relocalization)
            {
                ChangeState(HybridNavigationState.ExitingIndoor, "user requested exit");
            }
        }

        /// <summary>Manual override từ UI: đưa cả navigation state và presentation về Outdoor.</summary>
        public void RequestImmediateExit(string reason = "manual outdoor override")
        {
            SafeExitToOutdoor();
            indoorProvider?.SetCurrentBuilding(BuildingId.None, "");
            qualityGate?.ResetCounters();
            ActiveBuilding = BuildingId.None;
            ActiveEntrance = null;
            ChangeState(HybridNavigationState.Outdoor, reason);
        }

        public void ForceState(HybridNavigationState state, string reason = "forced")
        {
            ChangeState(state, reason);
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private float _nextEvalTime;
        private float _stateEnterTime;
        private float _scanStartTime;
        private float _relocStartTime;
        private float _outdoorReadyNearExitSince = -1f;
        private float _lastCalibrationWarningTime = -999f;
        private float _nextRelocalizationAttemptTime;
        private float _nextScanningAttemptTime;

        private void OnEnable()
        {
            ResolveReferences(force: true);
            _stateEnterTime = Time.time;
            if (qualityGate != null && destinationProfile != null) qualityGate.SetActiveProfile(destinationProfile);
        }

        private void Update()
        {
            UpdateCurrentPose();

            if (Time.time < _nextEvalTime) return;
            _nextEvalTime = Time.time + evaluationIntervalSeconds;

            ResolveReferences(force: false);
            UpdateActiveEntrance();
            EvaluateState();
        }

        private void ResolveReferences(bool force)
        {
            if (force || outdoorProvider == null) outdoorProvider ??= FindFirstObjectByType<OutdoorPoseProvider>(FindObjectsInactive.Include);
            if (force || indoorProvider == null || !indoorProvider.isActiveAndEnabled)
                indoorProvider = MultisetPoseProvider.FindActiveProvider();
            if (force || qualityGate == null) qualityGate ??= FindFirstObjectByType<LocalizationQualityGate>(FindObjectsInactive.Include);
            if (force || indoorMapSwitcher == null) indoorMapSwitcher ??= FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
            if (force || hybridModeController == null) hybridModeController ??= FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }

        // ---------------------------------------------------------------------
        // Pose source selection
        // ---------------------------------------------------------------------

        private void UpdateCurrentPose()
        {
            bool indoorState = CurrentState == HybridNavigationState.Indoor
                               || CurrentState == HybridNavigationState.Relocalization
                               || CurrentState == HybridNavigationState.TransitionScanning;

            if (indoorState && indoorProvider != null && indoorProvider.HasFreshPose)
            {
                var p = indoorProvider.Last;
                CurrentUserCampusPosition = p.CampusPosition;
                CurrentUserCampusRotation = p.CampusRotation;
                CurrentPoseIsFresh = true;
                return;
            }

            if (outdoorProvider != null && outdoorProvider.HasFreshFix)
            {
                CurrentUserCampusPosition = outdoorProvider.UserCampusPosition;
                CurrentPoseIsFresh = true;
                return;
            }

            CurrentPoseIsFresh = false;
        }

        // ---------------------------------------------------------------------
        // Entrance proximity
        // ---------------------------------------------------------------------

        private void UpdateActiveEntrance()
        {
            if (outdoorProvider == null)
            {
                ActiveEntrance = null;
                DistanceToActiveEntrance = float.PositiveInfinity;
                return;
            }

            // Trong indoor states, dùng entrance đã chốt (không re-pick).
            if (CurrentState == HybridNavigationState.TransitionScanning
                || CurrentState == HybridNavigationState.Indoor
                || CurrentState == HybridNavigationState.Relocalization)
            {
                return;
            }

            // Trong ExitingIndoor, ưu tiên anchor exit của ActiveBuilding.
            if (CurrentState == HybridNavigationState.ExitingIndoor)
            {
                if (ActiveEntrance != null && ActiveEntrance.CanExit) return;
                ActiveEntrance = EntranceAnchor.FindForBuilding(ActiveBuilding, requireEntrance: false);
                if (ActiveEntrance != null)
                {
                    DistanceToActiveEntrance = HorizontalDistance(CurrentUserCampusPosition, ActiveEntrance.CampusWorldPosition);
                }
                return;
            }

            // Outdoor / ApproachingEntrance: tìm gần nhất theo destination filter.
            var nearest = EntranceAnchor.FindNearest(
                CurrentUserCampusPosition,
                destinationBuilding,
                requireEntrance: true);
            ActiveEntrance = nearest;
            DistanceToActiveEntrance = nearest != null
                ? HorizontalDistance(CurrentUserCampusPosition, nearest.CampusWorldPosition)
                : float.PositiveInfinity;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------------------------------------------------------------------
        // State evaluation
        // ---------------------------------------------------------------------

        private void EvaluateState()
        {
            if (Time.time - _stateEnterTime < minStateDwellSeconds) return;

            switch (CurrentState)
            {
                case HybridNavigationState.Outdoor:
                    EvalOutdoor();
                    break;
                case HybridNavigationState.ApproachingEntrance:
                    EvalApproaching();
                    break;
                case HybridNavigationState.TransitionScanning:
                    EvalScanning();
                    break;
                case HybridNavigationState.Indoor:
                    EvalIndoor();
                    break;
                case HybridNavigationState.Relocalization:
                    EvalRelocalization();
                    break;
                case HybridNavigationState.ExitingIndoor:
                    EvalExiting();
                    break;
                case HybridNavigationState.Uncertain:
                    EvalUncertain();
                    break;
            }
        }

        private void EvalOutdoor()
        {
            if (ActiveEntrance == null) return;
            float approachRadius = ActiveEntrance.TriggerRadiusMeters * Mathf.Max(1f, approachingRadiusMultiplier);
            if (DistanceToActiveEntrance <= approachRadius)
            {
                ChangeState(HybridNavigationState.ApproachingEntrance,
                    $"in approach radius of {ActiveEntrance.BuildingId} ({DistanceToActiveEntrance:0.0}m ≤ {approachRadius:0.0}m)");
            }
        }

        private void EvalApproaching()
        {
            if (ActiveEntrance == null)
            {
                ChangeState(HybridNavigationState.Outdoor, "lost entrance reference");
                return;
            }
            float approachRadius = ActiveEntrance.TriggerRadiusMeters * Mathf.Max(1f, approachingRadiusMultiplier);
            if (DistanceToActiveEntrance > approachRadius * 1.15f)
            {
                ChangeState(HybridNavigationState.Outdoor, "left approach radius");
                return;
            }
            if (DistanceToActiveEntrance <= ActiveEntrance.TriggerRadiusMeters)
            {
                EnterTransitionScanning();
            }
        }

        private void EnterTransitionScanning()
        {
            ActiveBuilding = ActiveEntrance.BuildingId;
            if (indoorProvider != null) indoorProvider.SetCurrentBuilding(ActiveBuilding, ActiveEntrance.FloorId);
            if (qualityGate != null) qualityGate.ResetCounters();

            // Override mã VPS nếu anchor có ép — set TRƯỚC khi gọi EnterIndoor để registry có code đúng.
            // (Quan trọng cho B10 multi-floor mỗi tầng 1 MAP_xxx.)
            // Ở Vòng 2, registry quản mã chính qua BuildingRegistry; entrance-level override sẽ dùng ở Vòng 3.

            if (indoorMapSwitcher != null)
            {
                try
                {
                    bool ok = indoorMapSwitcher.EnterIndoor(ActiveBuilding);
                    if (!ok)
                    {
                        Debug.LogError(
                            $"[HybridLocalizationManager] Không thể vào {ActiveBuilding}: thiếu registry/scene binding/map code. " +
                            "Giữ Outdoor, không chạy VPS gate bằng dữ liệu sai.");
                        indoorProvider?.SetCurrentBuilding(BuildingId.None, "");
                        ActiveBuilding = BuildingId.None;
                        ChangeState(HybridNavigationState.Outdoor, "indoor map unavailable");
                        return;
                    }

                    indoorMapSwitcher.RequestLocalization();
                    _nextScanningAttemptTime =
                        Time.time + Mathf.Max(1f, relocalizationRetrySeconds);
                }
                catch (System.Exception ex)
                {
                    // SDK MultiSet LocalizeFrame có thể NRE trong Editor khi không có simulation data
                    // hoặc khi mapMeshHandler/cameraCollider chưa sẵn sàng. Fail closed để
                    // state machine không công bố indoor với map/presentation chưa đồng bộ.
                    Debug.LogWarning($"[HybridLocalizationManager] IndoorMapSwitcher.EnterIndoor({ActiveBuilding}) ném exception: {ex.GetType().Name}: {ex.Message}");
                    indoorProvider?.ResetLocalizationSession();
                    indoorProvider?.SetCurrentBuilding(BuildingId.None, "");
                    ActiveBuilding = BuildingId.None;
                    ChangeState(HybridNavigationState.Outdoor, "indoor switch exception");
                    return;
                }
            }
            else
            {
                Debug.LogError(
                    "[HybridLocalizationManager] Thiếu IndoorMapSwitcher; không thể chọn map VPS an toàn.");
                indoorProvider?.SetCurrentBuilding(BuildingId.None, "");
                ActiveBuilding = BuildingId.None;
                ChangeState(HybridNavigationState.Outdoor, "missing indoor map switcher");
                return;
            }

            _scanStartTime = Time.time;
            ChangeState(HybridNavigationState.TransitionScanning, $"at door of {ActiveBuilding}");
        }

        private void EvalScanning()
        {
            if (qualityGate == null) return;
            if (qualityGate.IndoorReady)
            {
                EntranceAnchor activeEntrance = ActiveEntrance;
                if (activeEntrance == null)
                {
                    BeginRelocalization("localized without active entrance");
                    return;
                }

                Vector3 handoverPosition =
                    outdoorProvider != null && outdoorProvider.HasFreshFix
                        ? outdoorProvider.UserCampusPosition
                        : activeEntrance.CampusWorldPosition;

                if (indoorProvider == null ||
                    !indoorProvider.EnsureHandoverCalibration(activeEntrance, handoverPosition))
                {
                    if (Time.time - _lastCalibrationWarningTime >= 3f)
                    {
                        _lastCalibrationWarningTime = Time.time;
                        Debug.LogWarning(
                            $"[HybridLocalizationManager] VPS đã localize nhưng chưa có calibration cho " +
                            $"{ActiveBuilding}/{activeEntrance.FloorId}. Chưa công bố Indoor.");
                    }
                    return;
                }

                ChangeState(HybridNavigationState.Indoor, "VPS localized + stable");
                return;
            }

            if (Time.time >= _nextScanningAttemptTime)
            {
                _nextScanningAttemptTime =
                    Time.time + Mathf.Max(1f, relocalizationRetrySeconds);
                indoorMapSwitcher?.RequestLocalization();
            }

            if (Time.time - _scanStartTime > scanTimeoutSeconds)
            {
                BeginRelocalization($"scan timeout {scanTimeoutSeconds:0}s");
            }
        }

        private void EvalIndoor()
        {
            if (qualityGate != null && qualityGate.IndoorLost)
            {
                BeginRelocalization(qualityGate.LastIndoorRejectReason);
            }
        }

        private void EvalRelocalization()
        {
            if (qualityGate != null && qualityGate.IndoorReady)
            {
                _outdoorReadyNearExitSince = -1f;
                ChangeState(HybridNavigationState.Indoor, "recovered VPS");
                return;
            }

            if (automaticExitWhenVpsLostNearDoor && IsOutdoorReadyNearActiveExit())
            {
                if (_outdoorReadyNearExitSince < 0f) _outdoorReadyNearExitSince = Time.time;
                if (Time.time - _outdoorReadyNearExitSince >= automaticExitOutdoorStableSeconds)
                {
                    RequestImmediateExit("VPS lost + GPS stable near exit");
                    return;
                }
            }
            else
            {
                _outdoorReadyNearExitSince = -1f;
            }

            if (Time.time >= _nextRelocalizationAttemptTime)
            {
                _nextRelocalizationAttemptTime =
                    Time.time + Mathf.Max(1f, relocalizationRetrySeconds);
                indoorMapSwitcher?.RequestLocalization();
            }

            if (Time.time - _relocStartTime > relocalizationGiveUpSeconds)
            {
                ChangeState(HybridNavigationState.Uncertain, "relocalization gave up");
            }
        }

        private void EvalExiting()
        {
            if (qualityGate != null && qualityGate.OutdoorReady)
            {
                SafeExitToOutdoor();
                if (qualityGate != null) qualityGate.ResetCounters();
                indoorProvider?.SetCurrentBuilding(BuildingId.None, "");
                ActiveBuilding = BuildingId.None;
                ChangeState(HybridNavigationState.Outdoor, "GPS stable at exit");
            }
        }

        private bool IsOutdoorReadyNearActiveExit()
        {
            if (qualityGate == null || !qualityGate.OutdoorReady ||
                outdoorProvider == null || !outdoorProvider.HasFreshFix ||
                ActiveEntrance == null || !ActiveEntrance.CanExit)
            {
                return false;
            }

            float radius = ActiveEntrance.TriggerRadiusMeters *
                           Mathf.Max(1f, automaticExitRadiusMultiplier);
            float distance = HorizontalDistance(
                outdoorProvider.UserCampusPosition,
                ActiveEntrance.CampusWorldPosition);
            return distance <= radius;
        }

        private void BeginRelocalization(string reason)
        {
            _relocStartTime = Time.time;
            _nextRelocalizationAttemptTime = Time.time;
            ChangeState(HybridNavigationState.Relocalization, reason);
            indoorMapSwitcher?.RequestLocalization();
            _nextRelocalizationAttemptTime =
                Time.time + Mathf.Max(1f, relocalizationRetrySeconds);
        }

        private void EvalUncertain()
        {
            // Nếu một trong hai pose source quay lại, đẩy về state phù hợp.
            if (qualityGate == null) return;
            if (qualityGate.IndoorReady)
            {
                ChangeState(HybridNavigationState.Indoor, "indoor returned from Uncertain");
            }
            else if (qualityGate.OutdoorReady)
            {
                SafeExitToOutdoor();
                indoorProvider?.SetCurrentBuilding(BuildingId.None, "");
                qualityGate.ResetCounters();
                ActiveBuilding = BuildingId.None;
                ChangeState(HybridNavigationState.Outdoor, "outdoor returned from Uncertain");
            }
        }

        private void SafeExitToOutdoor()
        {
            try
            {
                if (indoorMapSwitcher != null) indoorMapSwitcher.ExitToOutdoor();
                else if (hybridModeController != null)
                    hybridModeController.ApplyOutdoorFromCoordinator("HybridLocalizationManager.SafeExitToOutdoor");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HybridLocalizationManager] ExitToOutdoor ném exception (bỏ qua): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ChangeState(HybridNavigationState next, string reason)
        {
            if (next == CurrentState) return;
            var prev = CurrentState;
            CurrentState = next;
            LastTransitionReason = reason;
            LastTransitionTime = Time.timeAsDouble;
            _stateEnterTime = Time.time;

            if (verboseLog)
            {
                Debug.Log($"[HybridLocalizationManager] {prev} → {next} ({reason})");
            }
            OnStateChanged?.Invoke(prev, next, reason);
        }
    }
}
