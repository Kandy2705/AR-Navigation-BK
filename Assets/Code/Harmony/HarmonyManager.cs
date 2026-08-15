using System;
using ARNav.Hybrid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNav.Harmony
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class HarmonyManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private HarmonyConfig config;

        [Header("Providers (auto-resolve if null)")]
        [SerializeField] private GpsLocalizationProvider gpsProvider;
        [SerializeField] private VpsLocalizationProvider vpsProvider;
        [SerializeField] private HybridLocalizationManager legacyManager;
        [SerializeField] private IndoorMapSwitcher indoorMapSwitcher;

        [Header("Tick")]
        [Min(0.02f)]
        [SerializeField] private float evaluationIntervalSeconds = 0.1f;
        [SerializeField] private bool verboseLog = true;

        public HarmonyConfig Config => config;
        public HarmonyState State => _stateMachine?.Current ?? HarmonyState.Outdoor;
        public HarmonyLocalizationSource ActiveSource { get; private set; } =
            HarmonyLocalizationSource.GPS;
        public HarmonyReliabilitySnapshot Reliability { get; private set; }
        public HarmonyGpsSample GpsSample { get; private set; }
        public HarmonyVpsSample VpsSample { get; private set; }
        public BuildingId ActiveBuilding { get; private set; } = BuildingId.None;
        public EntranceAnchor ActiveEntrance { get; private set; }
        public float DistanceToEntrance { get; private set; } = float.PositiveInfinity;
        public string StatusReason { get; private set; } = "boot";
        public Vector3 CurrentCampusPosition { get; private set; }
        public Quaternion CurrentCampusRotation { get; private set; } = Quaternion.identity;
        public bool CurrentPoseIsFresh { get; private set; }
        public float LastPositionJumpMeters { get; private set; }
        public float LastHeadingJumpDegrees { get; private set; }
        public HarmonyExperimentVersion ExperimentVersion => config != null
            ? config.ExperimentVersion
            : HarmonyExperimentVersion.Current;
        public float StateAgeSeconds => _stateMachine != null
            ? _stateMachine.StateAge(Time.unscaledTime)
            : 0f;
        public float ModeAgeSeconds => _stateMachine != null
            ? _stateMachine.ModeAge(Time.unscaledTime)
            : 0f;

        public event Action<HarmonyStateTransition> StateChanged;
        public event Action<string, bool, float, float> HandoverCompleted;

        private HarmonyStateMachine _stateMachine;
        private LocalizationReliabilityEvaluator _reliabilityEvaluator;
        private HarmonyHandoverController _handoverController;
        private BuildingId _destinationBuilding = BuildingId.None;
        private BuildingLocalizationProfile _destinationProfile;
        private string _destinationFloorId = string.Empty;
        private float _nextEvaluationTime;
        private float _scanStartedAt;
        private float _nextVpsRetryAt;
        private float _sourceLostAt = -1f;
        private bool _exitRequested;
        private bool _automaticExitArmed;
        private HarmonyLocalizationSource _lastTrustedSource = HarmonyLocalizationSource.GPS;
        private HarmonyLocalizationSource _relocalizationSource = HarmonyLocalizationSource.LastTrusted;
        private Vector3 _lastTrustedPosition;
        private Quaternion _lastTrustedRotation = Quaternion.identity;
        private bool _hasLastTrustedPose;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "HybridGPSMap" && sceneName != "Hybrid Navigation")
                return;
            if (FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject("HARMONY V3");
            go.AddComponent<GpsLocalizationProvider>();
            go.AddComponent<VpsLocalizationProvider>();
            go.AddComponent<HarmonyManager>();
            go.AddComponent<UncertaintyGuidanceRenderer>();
            go.AddComponent<HarmonyExperimentLogger>();
            go.AddComponent<HarmonyDebugOverlay>();
        }

        private void Awake()
        {
            if (config == null)
                config = Resources.Load<HarmonyConfig>("HarmonyConfigV3");
            if (config == null)
                config = HarmonyConfig.CreateRuntimeDefaults();
            ResolveReferences();
            BuildRuntime();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BuildRuntime();
            legacyManager?.EnableHarmonyAuthority(this);
        }

        private void OnDestroy()
        {
            if (config != null && config.name == "HarmonyConfig_RuntimeDefaults")
                Destroy(config);
        }

        public void SetExperimentVersion(HarmonyExperimentVersion version)
        {
            if (config != null) config.ExperimentVersion = version;
            ResetHarmony();
        }

        public void SetDestinationBuilding(
            BuildingId building,
            BuildingLocalizationProfile profile = null,
            string floorId = "")
        {
            _destinationBuilding = building;
            _destinationProfile = profile;
            _destinationFloorId = floorId ?? string.Empty;
            vpsProvider?.Configure(building, profile, config);
        }

        public void RequestExit()
        {
            _exitRequested = true;
        }

        public void RequestImmediateExit(string reason = "manual outdoor override")
        {
            _exitRequested = false;
            _automaticExitArmed = false;
            _handoverController?.ReturnToOutdoor();
            ActiveBuilding = BuildingId.None;
            ActiveEntrance = null;
            ActiveSource = HarmonyLocalizationSource.GPS;
            Transition(HarmonyState.Outdoor, reason, true, true);
        }

        public void ResetHarmony()
        {
            _handoverController?.ReturnToOutdoor();
            _reliabilityEvaluator?.Reset();
            _stateMachine?.Initialize(Time.unscaledTime);
            ActiveBuilding = BuildingId.None;
            ActiveEntrance = null;
            ActiveSource = HarmonyLocalizationSource.GPS;
            StatusReason = "reset";
            _exitRequested = false;
            _automaticExitArmed = false;
            _sourceLostAt = -1f;
            _hasLastTrustedPose = false;
        }

        private void BuildRuntime()
        {
            if (_stateMachine == null)
            {
                _stateMachine = new HarmonyStateMachine();
                _stateMachine.Initialize(Time.unscaledTime);
                _stateMachine.Changed += HandleStateChanged;
            }
            _reliabilityEvaluator ??= new LocalizationReliabilityEvaluator();
            if (_handoverController == null && indoorMapSwitcher != null &&
                vpsProvider != null && vpsProvider.Source != null)
            {
                _handoverController =
                    new HarmonyHandoverController(indoorMapSwitcher, vpsProvider.Source);
            }
        }

        private void ResolveReferences()
        {
            gpsProvider ??= GetComponent<GpsLocalizationProvider>();
            gpsProvider ??= FindFirstObjectByType<GpsLocalizationProvider>(
                FindObjectsInactive.Include);
            vpsProvider ??= GetComponent<VpsLocalizationProvider>();
            vpsProvider ??= FindFirstObjectByType<VpsLocalizationProvider>(
                FindObjectsInactive.Include);
            legacyManager ??= FindFirstObjectByType<HybridLocalizationManager>(
                FindObjectsInactive.Include);
            indoorMapSwitcher ??= FindFirstObjectByType<IndoorMapSwitcher>(
                FindObjectsInactive.Include);
        }

        private void Update()
        {
            SampleAndPublish();
            if (Time.unscaledTime < _nextEvaluationTime) return;
            _nextEvaluationTime = Time.unscaledTime + evaluationIntervalSeconds;

            ResolveReferences();
            BuildRuntime();
            UpdateEntrance();
            EvaluateReliability();
            EvaluateState();
            PublishLegacySnapshot();
        }

        private void SampleAndPublish()
        {
            GpsSample = gpsProvider != null ? gpsProvider.Read() : default;
            VpsSample = vpsProvider != null ? vpsProvider.Read() : default;
            SelectCurrentPose();
            PublishLegacySnapshot();
        }

        private void UpdateEntrance()
        {
            if (State == HarmonyState.Outdoor || State == HarmonyState.EnteringTransition)
            {
                ActiveEntrance = EntranceAnchor.FindNearest(
                    GpsSample.CampusPosition,
                    _destinationBuilding,
                    requireEntrance: true);
            }
            else if (ActiveEntrance == null && ActiveBuilding != BuildingId.None)
            {
                ActiveEntrance = EntranceAnchor.FindForBuilding(
                    ActiveBuilding,
                    requireEntrance: false);
            }

            Vector3 reference = GpsSample.IsValid
                ? GpsSample.CampusPosition
                : CurrentCampusPosition;
            DistanceToEntrance = ActiveEntrance != null
                ? HorizontalDistance(reference, ActiveEntrance.CampusWorldPosition)
                : float.PositiveInfinity;
        }

        private void EvaluateReliability()
        {
            float radius = ActiveEntrance != null
                ? ActiveEntrance.TriggerRadiusMeters
                : 0f;
            Reliability = _reliabilityEvaluator.Evaluate(
                GpsSample,
                VpsSample,
                DistanceToEntrance,
                radius,
                ActiveSource,
                config,
                Time.unscaledTime);
        }

        private void EvaluateState()
        {
            switch (State)
            {
                case HarmonyState.Outdoor:
                    EvaluateOutdoor();
                    break;
                case HarmonyState.EnteringTransition:
                    EvaluateEntering();
                    break;
                case HarmonyState.VpsScanning:
                    EvaluateVpsScanning();
                    break;
                case HarmonyState.Indoor:
                    EvaluateIndoor();
                    break;
                case HarmonyState.Relocalization:
                    EvaluateRelocalization();
                    break;
                case HarmonyState.ExitingTransition:
                    EvaluateExiting();
                    break;
                case HarmonyState.Uncertain:
                    EvaluateUncertain();
                    break;
            }
        }

        private void EvaluateOutdoor()
        {
            ActiveSource = HarmonyLocalizationSource.GPS;
            if (ActiveEntrance == null || !GpsSample.IsValid)
            {
                StatusReason = ActiveEntrance == null
                    ? "No entrance configured for destination"
                    : Reliability.GpsReason;
                return;
            }

            float approachRadius = ActiveEntrance.TriggerRadiusMeters *
                                   Mathf.Max(1f, config.approachRadiusMultiplier);
            if (DistanceToEntrance <= approachRadius)
                Transition(HarmonyState.EnteringTransition, "approaching entrance");
        }

        private void EvaluateEntering()
        {
            ActiveSource = HarmonyLocalizationSource.GPS;
            if (ActiveEntrance == null)
            {
                Transition(HarmonyState.Outdoor, "entrance unavailable");
                return;
            }

            float approachRadius = ActiveEntrance.TriggerRadiusMeters *
                                   Mathf.Max(1f, config.approachRadiusMultiplier);
            if (DistanceToEntrance > approachRadius * 1.15f)
            {
                Transition(HarmonyState.Outdoor, "left entrance approach");
                return;
            }

            float enterRadius = ActiveEntrance.TriggerRadiusMeters *
                                Mathf.Max(0.5f, config.enterRadiusMultiplier);
            if (DistanceToEntrance > enterRadius) return;

            ActiveBuilding = ActiveEntrance.BuildingId;
            vpsProvider?.Configure(ActiveBuilding, _destinationProfile, config);
            string failure = "HARMONY handover controller unavailable";
            if (_handoverController == null ||
                !_handoverController.BeginVpsScan(
                    ActiveBuilding,
                    string.IsNullOrEmpty(_destinationFloorId)
                        ? ActiveEntrance.FloorId
                        : _destinationFloorId,
                    out failure))
            {
                ActiveBuilding = BuildingId.None;
                StatusReason = failure;
                Transition(HarmonyState.Outdoor, failure, false, true);
                return;
            }

            _scanStartedAt = Time.unscaledTime;
            _nextVpsRetryAt = Time.unscaledTime + config.vpsRetrySeconds;
            Transition(HarmonyState.VpsScanning, "inside transition zone");
        }

        private void EvaluateVpsScanning()
        {
            ActiveSource = HarmonyLocalizationSource.GPS;
            StatusReason = Reliability.VpsReason;

            if (!config.UseReliabilityGate)
            {
                CompleteGpsToVps("V1 fixed switching");
                return;
            }

            if (CanEnterVps(out string reason))
            {
                CompleteGpsToVps(reason);
                return;
            }
            StatusReason = reason;

            if (Time.unscaledTime >= _nextVpsRetryAt)
            {
                _nextVpsRetryAt = Time.unscaledTime + config.vpsRetrySeconds;
                _handoverController?.RetryVps();
            }

            if (Time.unscaledTime - _scanStartedAt >= config.vpsScanTimeoutSeconds)
            {
                _relocalizationSource = HarmonyLocalizationSource.GPS;
                Transition(HarmonyState.Relocalization, $"VPS scan timeout: {reason}");
            }
        }

        private bool CanEnterVps(out string reason)
        {
            if (!VpsSample.IsValid)
            {
                reason = "VPS pose unavailable/stale";
                return false;
            }
            if (VpsSample.ConfidenceAvailable && VpsSample.Confidence < config.minimumVpsConfidence)
            {
                reason = $"VPS confidence {VpsSample.Confidence:0.00} too low";
                return false;
            }
            if (config.RequireMapIdMatch)
            {
                if (!VpsSample.MapIdAvailable)
                {
                    reason = "VPS map ID unavailable";
                    return false;
                }
                if (!VpsSample.MapMatchesBuilding)
                {
                    reason = $"VPS map ID mismatch ({VpsSample.MapId})";
                    return false;
                }
            }
            if (config.UseReliabilityGate && Reliability.Vps < config.vpsEnterReliability)
            {
                reason = $"VPS reliability too low ({Reliability.Vps:0.00})";
                return false;
            }
            if (config.RequireVpsDwell && Reliability.VpsStableSeconds < config.vpsDwellSeconds)
            {
                reason = $"VPS not stable enough ({Reliability.VpsStableSeconds:0.0}s)";
                return false;
            }
            if (config.UseContinuityGate && !EnsureVpsContinuity(out reason))
            {
                return false;
            }
            reason = "VPS valid + stable + continuous";
            return true;
        }

        private bool EnsureVpsContinuity(out string reason)
        {
            if (ActiveEntrance == null || vpsProvider == null ||
                vpsProvider.Source == null)
            {
                reason = "handover calibration unavailable";
                return false;
            }

            Vector3 gpsReference = GpsSample.IsValid
                ? GpsSample.CampusPosition
                : ActiveEntrance.CampusWorldPosition;
            if (!vpsProvider.Source.EnsureHandoverCalibration(
                    ActiveEntrance,
                    gpsReference))
            {
                reason = "handover calibration unavailable";
                return false;
            }

            VpsSample = vpsProvider.Read();
            CalculateContinuity(GpsSample, VpsSample);
            if (LastPositionJumpMeters > config.maxHandoverPositionJumpMeters)
            {
                reason = $"position jump {LastPositionJumpMeters:0.0}m";
                return false;
            }
            if (GpsSample.HasHeading &&
                LastHeadingJumpDegrees > config.maxHandoverHeadingJumpDegrees)
            {
                reason = $"heading jump {LastHeadingJumpDegrees:0}°";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void CompleteGpsToVps(string reason)
        {
            CalculateContinuity(GpsSample, VpsSample);
            ActiveSource = HarmonyLocalizationSource.VPS;
            // The entrance and exit commonly share the same anchor/trigger. Do not
            // interpret the handover position as an immediate request to leave.
            // Automatic exit becomes armed only after the user has first moved
            // outside the exit zone, then later comes back to it.
            _automaticExitArmed = false;
            RememberTrusted(VpsSample.CampusPosition, VpsSample.CampusRotation);
            bool changed = Transition(
                HarmonyState.Indoor,
                reason,
                changesSource: true,
                force: !config.EnforceMinimumModeDuration);
            if (changed)
                HandoverCompleted?.Invoke("GPS_TO_VPS", true,
                    LastPositionJumpMeters, LastHeadingJumpDegrees);
        }

        private void EvaluateIndoor()
        {
            ActiveSource = HarmonyLocalizationSource.VPS;
            bool isNearExit = IsNearExit();
            if (!isNearExit)
                _automaticExitArmed = true;

            bool automaticExit = _automaticExitArmed && isNearExit;
            if (_exitRequested || automaticExit)
            {
                string reason = _exitRequested
                    ? "exit requested"
                    : "returned to exit zone";
                if (Transition(HarmonyState.ExitingTransition, reason))
                {
                    _exitRequested = false;
                    _automaticExitArmed = false;
                }
                return;
            }

            if (VpsSample.IsValid)
            {
                _sourceLostAt = -1f;
                RememberTrusted(VpsSample.CampusPosition, VpsSample.CampusRotation);
                StatusReason = Reliability.VpsReason;
                return;
            }

            if (_sourceLostAt < 0f) _sourceLostAt = Time.unscaledTime;
            if (Time.unscaledTime - _sourceLostAt >= config.sourceLossGraceSeconds)
            {
                _relocalizationSource = HarmonyLocalizationSource.LastTrusted;
                Transition(HarmonyState.Relocalization, "VPS lost");
            }
        }

        private void EvaluateExiting()
        {
            ActiveSource = HarmonyLocalizationSource.VPS;
            StatusReason = Reliability.GpsReason;
            if (!config.UseReliabilityGate)
            {
                CompleteVpsToGps("V1 fixed switching");
                return;
            }

            if (!GpsSample.IsValid ||
                Reliability.Gps < config.gpsExitReliability ||
                Reliability.GpsStableSeconds < config.gpsDwellSeconds)
            {
                return;
            }

            CalculateContinuity(GpsSample, VpsSample);
            if (LastPositionJumpMeters > config.maxHandoverPositionJumpMeters ||
                (GpsSample.HasHeading &&
                 LastHeadingJumpDegrees > config.maxHandoverHeadingJumpDegrees))
            {
                Transition(HarmonyState.Uncertain, "GPS/VPS exit continuity failed");
                return;
            }
            CompleteVpsToGps("GPS valid + stable + continuous");
        }

        private void CompleteVpsToGps(string reason)
        {
            CalculateContinuity(GpsSample, VpsSample);
            _handoverController?.ReturnToOutdoor();
            ActiveSource = HarmonyLocalizationSource.GPS;
            ActiveBuilding = BuildingId.None;
            _automaticExitArmed = false;
            RememberTrusted(GpsSample.CampusPosition, GpsSample.CampusRotation);
            bool changed = Transition(
                HarmonyState.Outdoor,
                reason,
                changesSource: true,
                force: !config.EnforceMinimumModeDuration);
            if (changed)
                HandoverCompleted?.Invoke("VPS_TO_GPS", true,
                    LastPositionJumpMeters, LastHeadingJumpDegrees);
        }

        private void EvaluateRelocalization()
        {
            ActiveSource = _relocalizationSource;
            
            if (_relocalizationSource == HarmonyLocalizationSource.GPS)
            {
                if (ActiveEntrance == null || 
                    DistanceToEntrance > ActiveEntrance.TriggerRadiusMeters * Mathf.Max(1f, config.approachRadiusMultiplier) * 1.15f)
                {
                    Transition(HarmonyState.Outdoor, "left entrance approach during relocalization");
                    return;
                }
            }
            
            if (CanEnterVps(out string reason))
            {
                ActiveSource = HarmonyLocalizationSource.VPS;
                Transition(HarmonyState.Indoor, "VPS recovered: " + reason);
                return;
            }

            if (Time.unscaledTime >= _nextVpsRetryAt)
            {
                _nextVpsRetryAt = Time.unscaledTime + config.vpsRetrySeconds;
                _handoverController?.RetryVps();
            }

            if (StateAgeSeconds >= config.relocalizationTimeoutSeconds)
            {
                Transition(HarmonyState.Uncertain, "Relocalization timeout");
            }
        }

        private void EvaluateUncertain()
        {
            ActiveSource = HarmonyLocalizationSource.LastTrusted;
            if (CanEnterVps(out string reason))
            {
                ActiveSource = HarmonyLocalizationSource.VPS;
                Transition(HarmonyState.Indoor, "VPS recovered: " + reason);
                return;
            }
            if (GpsSample.IsValid &&
                Reliability.Gps >= config.gpsExitReliability &&
                Reliability.GpsStableSeconds >= config.gpsDwellSeconds)
            {
                _handoverController?.ReturnToOutdoor();
                ActiveSource = HarmonyLocalizationSource.GPS;
                ActiveBuilding = BuildingId.None;
                Transition(HarmonyState.Outdoor, "GPS recovered");
            }
        }

        private bool IsNearExit()
        {
            EntranceAnchor exit = EntranceAnchor.FindForBuilding(
                ActiveBuilding,
                requireEntrance: false);
            if (exit == null || !exit.CanExit) return false;
            ActiveEntrance = exit;
            Vector3 position = VpsSample.IsValid
                ? VpsSample.CampusPosition
                : CurrentCampusPosition;
            float radius = exit.TriggerRadiusMeters *
                           Mathf.Max(1f, config.exitRadiusMultiplier);
            return HorizontalDistance(position, exit.CampusWorldPosition) <= radius;
        }

        private void CalculateContinuity(
            HarmonyGpsSample gps,
            HarmonyVpsSample vps)
        {
            LastPositionJumpMeters = gps.IsValid && vps.IsValid
                ? HorizontalDistance(gps.CampusPosition, vps.CampusPosition)
                : float.PositiveInfinity;
            LastHeadingJumpDegrees = gps.IsValid && vps.IsValid && gps.HasHeading
                ? Mathf.Abs(Mathf.DeltaAngle(
                    gps.HeadingDegrees,
                    vps.CampusRotation.eulerAngles.y))
                : 0f;
        }

        private void SelectCurrentPose()
        {
            switch (ActiveSource)
            {
                case HarmonyLocalizationSource.VPS when VpsSample.IsValid:
                    CurrentCampusPosition = VpsSample.CampusPosition;
                    CurrentCampusRotation = VpsSample.CampusRotation;
                    CurrentPoseIsFresh = true;
                    RememberTrusted(CurrentCampusPosition, CurrentCampusRotation);
                    break;
                case HarmonyLocalizationSource.GPS when GpsSample.IsValid:
                    CurrentCampusPosition = GpsSample.CampusPosition;
                    CurrentCampusRotation = GpsSample.CampusRotation;
                    CurrentPoseIsFresh = true;
                    RememberTrusted(CurrentCampusPosition, CurrentCampusRotation);
                    break;
                case HarmonyLocalizationSource.LastTrusted when _hasLastTrustedPose:
                    CurrentCampusPosition = _lastTrustedPosition;
                    CurrentCampusRotation = _lastTrustedRotation;
                    CurrentPoseIsFresh = false;
                    break;
                default:
                    CurrentPoseIsFresh = false;
                    break;
            }
        }

        private void RememberTrusted(Vector3 position, Quaternion rotation)
        {
            _hasLastTrustedPose = true;
            _lastTrustedPosition = position;
            _lastTrustedRotation = rotation;
            _lastTrustedSource = ActiveSource;
        }

        private bool Transition(
            HarmonyState next,
            string reason,
            bool changesSource = false,
            bool force = false)
        {
            bool changed = _stateMachine.TryTransition(
                next,
                reason,
                Time.unscaledTime,
                config.minimumStateDurationSeconds,
                config.minimumModeDurationSeconds,
                changesSource,
                force);
            if (changed) StatusReason = reason;
            return changed;
        }

        private void HandleStateChanged(HarmonyStateTransition transition)
        {
            if (verboseLog)
                Debug.Log($"[HARMONY] {transition.Previous} → {transition.Next}: {transition.Reason}");
            StateChanged?.Invoke(transition);
            PublishLegacySnapshot();
        }

        private void PublishLegacySnapshot()
        {
            if (legacyManager == null) return;
            legacyManager.EnableHarmonyAuthority(this);
            legacyManager.ApplyHarmonySnapshot(
                ToLegacyState(State),
                StatusReason,
                ActiveBuilding,
                ActiveEntrance,
                DistanceToEntrance,
                CurrentCampusPosition,
                CurrentCampusRotation,
                CurrentPoseIsFresh);
        }

        private static HybridNavigationState ToLegacyState(HarmonyState state)
        {
            return state switch
            {
                HarmonyState.Outdoor => HybridNavigationState.Outdoor,
                HarmonyState.EnteringTransition => HybridNavigationState.ApproachingEntrance,
                HarmonyState.VpsScanning => HybridNavigationState.TransitionScanning,
                HarmonyState.Indoor => HybridNavigationState.Indoor,
                HarmonyState.Relocalization => HybridNavigationState.Relocalization,
                HarmonyState.ExitingTransition => HybridNavigationState.ExitingIndoor,
                _ => HybridNavigationState.Uncertain,
            };
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
