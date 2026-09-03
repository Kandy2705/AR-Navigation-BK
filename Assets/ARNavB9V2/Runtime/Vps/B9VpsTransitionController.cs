using System;
using System.Collections;
using System.Reflection;
using ARNavB9V2.Data;
using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Events;

namespace ARNavB9V2.Vps
{
    /// <summary>
    /// Owns the automatic handover from outdoor GPS navigation to B9 VPS localization.
    /// MultiSet is accessed through its public runtime component and UnityEvents, while
    /// the V2 flow remains independent from the legacy hybrid scripts.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class B9VpsTransitionController : MonoBehaviour
    {
        public enum TransitionState
        {
            WaitingForEntrance,
            StartingVps,
            Scanning,
            IndoorLocalized,
            Failed,
        }

        private const BindingFlags ReflectionFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Header("B9 + MultiSet")]
        [SerializeField] private B9BuildingDefinition building;
        [SerializeField] private GameObject sdkManagerRoot;
        [SerializeField] private MonoBehaviour mapLocalizationManager;

        [Header("Outdoor handover")]
        [SerializeField] private B9OutdoorSceneContext outdoorContext;

        [Header("Indoor handover")]
        [SerializeField] private B9SceneContext foundation;
        [SerializeField] private B9MapVisibility mapVisibility;
        [SerializeField] private B9IndoorSceneContext indoorContext;
        [SerializeField, Min(0f)] private float localizationPoseSettleSeconds = 0.35f;
        [SerializeField] private bool allowSdkInvocationInEditor;
        [SerializeField] private bool externalHandoverControl;
        [Header("HARMONY experiment gates")]
        [SerializeField] private bool useQualityThreshold = true;
        [SerializeField] private bool useTemporalDwell = true;
        [SerializeField] private bool useMapIdCheck = true;
        [SerializeField] private bool useRecoveryFsm = true;
        [SerializeField, Range(0f, 1f)] private float minimumLocalizationQuality = 0.45f;
        [SerializeField, Min(0f)] private float localizationDwellSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float stablePositionDeltaMeters = 2.5f;
        [SerializeField, Range(1f, 180f)] private float stableHeadingDeltaDegrees = 60f;

        private UnityEvent localizationInitEvent;
        private UnityEvent localizationRequestedEvent;
        private UnityEvent localizationSuccessEvent;
        private UnityEvent localizationFailureEvent;
        private Coroutine sdkStartCoroutine;
        private bool localizationInitObserved;
        private float scanStartedAt;
        private bool eventsSubscribed;
        private bool indoorHandoverPending;
        private int localizationAttemptCount;
        private B9HarmonyExperimentProfile experimentProfile =
            B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony);
        private float localizationSucceededAt = -1f;
        private float vpsStableSince = -1f;
        private bool previousVpsThresholdPassed;
        private bool previousVpsDwellPassed;

        public TransitionState State { get; private set; } = TransitionState.WaitingForEntrance;
        public string FailureReason { get; private set; } = string.Empty;
        public string ActiveMapCode => building != null ? building.PrimaryMapCode : string.Empty;
        public bool IsApproximatePdrLocalization { get; private set; }
        public bool RetryAvailable => useRecoveryFsm && State == TransitionState.Failed;
        public int LocalizationAttemptCount => localizationAttemptCount;
        public float LastLocalizationQuality { get; private set; } = 0.5f;
        public bool VpsValid { get; private set; }
        public float VpsAgeSeconds => localizationSucceededAt < 0f
            ? float.PositiveInfinity
            : Mathf.Max(0f, Time.unscaledTime - localizationSucceededAt);
        public float LastVpsConfidence { get; private set; }
        public bool LastVpsConfidenceAvailable { get; private set; }
        public string LastLocalizedMapId { get; private set; } = string.Empty;
        public bool LastMapIdAvailable { get; private set; }
        public bool LastMapMatchesBuilding { get; private set; }
        public float VpsStableSeconds => vpsStableSince < 0f
            ? 0f
            : Mathf.Max(0f, Time.unscaledTime - vpsStableSince);
        public bool VpsThresholdPassed { get; private set; }
        public bool VpsDwellGatePassed { get; private set; }
        public float LastPositionDeltaMeters { get; private set; }
        public float LastHeadingDeltaDegrees { get; private set; }
        public string CandidateSource => VpsThresholdPassed && VpsDwellGatePassed
            ? "VPS"
            : "LastTrusted";
        public string LastGateReason { get; private set; } = string.Empty;
        public float CurrentScanElapsedSeconds => scanStartedAt > 0f
            && (State == TransitionState.StartingVps
                || State == TransitionState.Scanning
                || State == TransitionState.Failed)
                ? Mathf.Max(0f, Time.unscaledTime - scanStartedAt)
                : 0f;
        public event Action<TransitionState> StateChanged;
        public event Action<string, string> ExperimentDecision;

        public void ApplyExperimentProfile(B9HarmonyExperimentProfile profile)
        {
            useQualityThreshold = profile.QualityThreshold;
            useTemporalDwell = profile.TemporalDwell;
            useMapIdCheck = profile.MapIdCheck;
            useRecoveryFsm = profile.RecoveryFsm;
            experimentProfile = profile;
            minimumLocalizationQuality = profile.VpsEnterReliability;
            localizationDwellSeconds = profile.VpsDwellSeconds;
            ResetResearchGateState();
        }

        public void SetExternalHandoverControl(bool enabled)
        {
            externalHandoverControl = enabled;
        }

        public void Configure(
            B9BuildingDefinition definition,
            GameObject sdkRoot,
            MonoBehaviour localizer,
            B9OutdoorSceneContext outdoor,
            B9SceneContext sceneFoundation,
            B9MapVisibility visibility,
            bool invokeSdkInEditor = false)
        {
            building = definition;
            sdkManagerRoot = sdkRoot;
            mapLocalizationManager = localizer;
            outdoorContext = outdoor;
            foundation = sceneFoundation;
            mapVisibility = visibility;
            allowSdkInvocationInEditor = invokeSdkInEditor;
        }

        public void AttachIndoorNavigation(B9IndoorSceneContext context)
        {
            indoorContext = context;
        }

        public void ConfigureLocalizationTiming(float poseSettleSeconds = 0.35f)
        {
            localizationPoseSettleSeconds = Mathf.Max(0f, poseSettleSeconds);
        }

        private void Awake()
        {
            if (sdkManagerRoot != null)
                sdkManagerRoot.SetActive(true);
            if (foundation != null && foundation.ModelRoot != null)
                foundation.ModelRoot.gameObject.SetActive(false);
            indoorContext?.PrepareForLocalization();
            TrySubscribeSdkEvents();
            // Keep the official MultiSet component dormant outdoors. Its own
            // auto/background/relocalization flow starts inside the VPS region.
            SetMultiSetLocalizerEnabled(false);
        }

        private void OnEnable()
        {
            TrySubscribeSdkEvents();
        }

        private void OnDisable()
        {
            StopSdkStartCoroutine();
            UnsubscribeSdkEvents();
            SetMultiSetLocalizerEnabled(false);
        }

        private void Update()
        {
            if (!externalHandoverControl
                && State == TransitionState.WaitingForEntrance
                && outdoorContext != null
                && outdoorContext.RouteController != null
                && outdoorContext.RouteController.HasArrivedAtEntrance)
            {
                BeginAutomaticLocalization();
            }

            // MultiSet owns first-localization retry, background localization and
            // relocalization. HARMONY does not run a competing scan timeout.
        }

        public void BeginAutomaticLocalization()
        {
            if (State != TransitionState.WaitingForEntrance)
                return;
            StartLocalizationAttempt();
        }

        public void RetryLocalization()
        {
            if (!RetryAvailable)
                return;
            StartLocalizationAttempt();
        }

        public void CancelLocalization()
        {
            StopSdkStartCoroutine();
            SetMultiSetLocalizerEnabled(false);
            IsApproximatePdrLocalization = false;
            indoorHandoverPending = false;
            scanStartedAt = 0f;
            FailureReason = string.Empty;
            SetState(TransitionState.WaitingForEntrance);
        }

        public void ReturnToOutdoor()
        {
            CancelLocalization();
            indoorContext?.PrepareForLocalization();
            if (foundation != null && foundation.ModelRoot != null)
                foundation.ModelRoot.gameObject.SetActive(false);
        }

        private void StartLocalizationAttempt()
        {
            StopSdkStartCoroutine();
            SetMultiSetLocalizerEnabled(false);
            IsApproximatePdrLocalization = false;
            FailureReason = string.Empty;
            LastGateReason = string.Empty;
            ResetResearchGateState();
            indoorHandoverPending = false;
            localizationInitObserved = false;
            localizationAttemptCount++;
            scanStartedAt = 0f;

            if (!ValidateRuntimeReferences(out string reason))
            {
                Fail(reason);
                return;
            }

            if (!ConfigureMultiSetForB9(out reason))
            {
                Fail(reason);
                return;
            }

            TrySubscribeSdkEvents();
            if (!eventsSubscribed)
            {
                Fail("Không nối được callback LocalizationSuccess/Failure của MultiSet.");
                return;
            }

            scanStartedAt = Time.unscaledTime;
            SetState(TransitionState.StartingVps);
            SetMultiSetLocalizerEnabled(true);
            sdkStartCoroutine = StartCoroutine(StartOfficialMultiSetLocalization());
        }

        private IEnumerator StartOfficialMultiSetLocalization()
        {
            // Enabling the component subscribes it to MultiSet authentication again.
            // If authentication completed while it was disabled outdoors, that event
            // is not replayed, so trigger the SDK's public entry point once. MultiSet
            // remains responsible for frame count/interval, first-success retries,
            // background localization and relocalization.
            yield return new WaitForSecondsRealtime(2.25f);

            if (localizationInitObserved
                || mapLocalizationManager == null
                || !mapLocalizationManager.isActiveAndEnabled
                || State != TransitionState.StartingVps)
            {
                sdkStartCoroutine = null;
                yield break;
            }

#if UNITY_EDITOR
            if (!allowSdkInvocationInEditor)
            {
                sdkStartCoroutine = null;
                Fail("VPS thật chỉ quét trên thiết bị. Build iOS/Android để thử camera hoặc dùng Debug/Simulate VPS Success.");
                yield break;
            }
#endif

            MethodInfo method = mapLocalizationManager.GetType().GetMethod(
                "LocalizeFrame",
                ReflectionFlags,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
            {
                sdkStartCoroutine = null;
                Fail("MultiSet MapLocalizationManager không có LocalizeFrame().");
                yield break;
            }

            try
            {
                method.Invoke(mapLocalizationManager, null);
            }
            catch (TargetInvocationException exception)
            {
                sdkStartCoroutine = null;
                string detail = exception.InnerException != null
                    ? exception.InnerException.Message
                    : exception.Message;
                Fail("Không thể bắt đầu MultiSet VPS: " + detail);
                yield break;
            }
            catch (Exception exception)
            {
                sdkStartCoroutine = null;
                Fail("Không thể bắt đầu MultiSet VPS: " + exception.Message);
                yield break;
            }

            sdkStartCoroutine = null;
        }

        private bool ConfigureMultiSetForB9(out string reason)
        {
            Type type = mapLocalizationManager.GetType();
            if (!TrySetFieldOrProperty(type, "mapOrMapsetCode", building.PrimaryMapCode))
            {
                reason = "Không gán được mã map B9 cho MultiSet.";
                return false;
            }

            FieldInfo localizationType = type.GetField("localizationType", ReflectionFlags);
            if (localizationType == null || !localizationType.FieldType.IsEnum)
            {
                reason = "MultiSet thiếu cấu hình localizationType.";
                return false;
            }

            try
            {
                object mapValue = Enum.Parse(localizationType.FieldType, "Map", true);
                localizationType.SetValue(mapLocalizationManager, mapValue);
            }
            catch (Exception exception)
            {
                reason = "Không chọn được chế độ Single Map B9: " + exception.Message;
                return false;
            }

            // The package prefab remains authoritative for autoLocalize,
            // backgroundLocalization, relocalization, frame capture, blur checking
            // and first-localization retry.
            reason = string.Empty;
            return true;
        }

        private bool ValidateRuntimeReferences(out string reason)
        {
            if (building == null)
            {
                reason = "Thiếu cấu hình tòa B9.";
                return false;
            }
            if (building.PrimaryMapCode != "MAP_9LME2PB7Y3EN")
            {
                reason = "Mã VPS B9 không đúng MAP_9LME2PB7Y3EN.";
                return false;
            }
            if (sdkManagerRoot == null || mapLocalizationManager == null)
            {
                reason = "Thiếu runtime MultiSet SDK.";
                return false;
            }
            if (foundation == null || foundation.MapSpace == null || foundation.ModelRoot == null)
            {
                reason = "Thiếu Map Space/model indoor B9.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void TrySubscribeSdkEvents()
        {
            if (eventsSubscribed || mapLocalizationManager == null)
                return;

            localizationInitEvent = GetSdkEvent("LocalizationInit");
            localizationRequestedEvent = GetSdkEvent("LocalizationRequested");
            localizationSuccessEvent = GetSdkEvent("LocalizationSuccess");
            localizationFailureEvent = GetSdkEvent("LocalizationFailure");
            if (localizationSuccessEvent == null || localizationFailureEvent == null)
                return;

            localizationInitEvent?.AddListener(HandleLocalizationInit);
            localizationRequestedEvent?.AddListener(HandleLocalizationRequested);
            localizationSuccessEvent.AddListener(HandleLocalizationSuccess);
            localizationFailureEvent.AddListener(HandleLocalizationFailure);
            eventsSubscribed = true;
        }

        private void UnsubscribeSdkEvents()
        {
            if (!eventsSubscribed)
                return;
            localizationInitEvent?.RemoveListener(HandleLocalizationInit);
            localizationRequestedEvent?.RemoveListener(HandleLocalizationRequested);
            localizationSuccessEvent?.RemoveListener(HandleLocalizationSuccess);
            localizationFailureEvent?.RemoveListener(HandleLocalizationFailure);
            eventsSubscribed = false;
        }

        private UnityEvent GetSdkEvent(string fieldName)
        {
            FieldInfo field = mapLocalizationManager.GetType().GetField(fieldName, ReflectionFlags);
            return field?.GetValue(mapLocalizationManager) as UnityEvent;
        }

        private void HandleLocalizationInit()
        {
            localizationInitObserved = true;
            if (State == TransitionState.StartingVps)
                SetState(TransitionState.Scanning);
        }

        private void HandleLocalizationRequested()
        {
            // MultiSet can issue multiple requests through first-success retry,
            // background localization and relocalization.
            if (scanStartedAt <= 0f)
                scanStartedAt = Time.unscaledTime;
            SetState(TransitionState.Scanning);
        }

        private void HandleLocalizationSuccess()
        {
            if (State != TransitionState.StartingVps && State != TransitionState.Scanning)
                return;
            if (indoorHandoverPending)
                return;
            StopSdkStartCoroutine();
            indoorHandoverPending = true;
            VpsValid = true;
            localizationSucceededAt = Time.unscaledTime;
            StartCoroutine(CompleteIndoorHandoverAfterSdkPose());
        }

        private IEnumerator CompleteIndoorHandoverAfterSdkPose()
        {
            yield return null;
            Transform mapSpace = foundation != null ? foundation.MapSpace : null;
            Camera camera = foundation != null ? foundation.ArCamera : null;
            Vector3 previousPosition = mapSpace != null && camera != null
                ? mapSpace.InverseTransformPoint(camera.transform.position)
                : Vector3.zero;
            Quaternion previousRotation = mapSpace != null && camera != null
                ? Quaternion.Inverse(mapSpace.rotation) * camera.transform.rotation
                : Quaternion.identity;
            if (localizationPoseSettleSeconds > 0f)
                yield return new WaitForSecondsRealtime(localizationPoseSettleSeconds);

            while (State == TransitionState.StartingVps || State == TransitionState.Scanning)
            {
                Vector3 currentPosition = mapSpace != null && camera != null
                    ? mapSpace.InverseTransformPoint(camera.transform.position)
                    : previousPosition;
                Quaternion currentRotation = mapSpace != null && camera != null
                    ? Quaternion.Inverse(mapSpace.rotation) * camera.transform.rotation
                    : previousRotation;
                bool gatesPassed = EvaluateExperimentGates(
                    previousPosition,
                    previousRotation,
                    currentPosition,
                    currentRotation,
                    out string gateFailure);
                previousPosition = currentPosition;
                previousRotation = currentRotation;

                if (!gatesPassed)
                {
                    bool waitingForDwellQuality = useTemporalDwell
                                                  && useQualityThreshold
                                                  && LastMapGatePassed();
                    if (waitingForDwellQuality)
                    {
                        yield return new WaitForSecondsRealtime(0.1f);
                        continue;
                    }
                    RejectLocalization(gateFailure);
                    yield break;
                }

                if (!useTemporalDwell || VpsDwellGatePassed)
                    break;
                yield return new WaitForSecondsRealtime(0.1f);
            }

            if (State != TransitionState.StartingVps && State != TransitionState.Scanning)
            {
                indoorHandoverPending = false;
                yield break;
            }

            ActivateIndoorNavigation();
        }

        public bool CompleteApproximatePdrLocalization(
            Vector3 estimatedMapWorldPosition,
            Quaternion estimatedMapWorldRotation,
            string detail = "MultiSet did not localize within the time limit")
        {
            bool scanSessionActive = State == TransitionState.StartingVps
                                      || State == TransitionState.Scanning
                                      || State == TransitionState.Failed
                                      && scanStartedAt > 0f;
            if (!scanSessionActive)
                return false;
            if (!ValidateRuntimeReferences(out _))
                return false;

            StopSdkStartCoroutine();
            SetMultiSetLocalizerEnabled(false);
            indoorHandoverPending = false;
            VpsValid = false;
            IsApproximatePdrLocalization = true;
            LastGateReason = detail ?? "PDR approximate pose";
            ExperimentDecision?.Invoke("pdr_approximate_localization", LastGateReason);

            if (!ReanchorMapSpaceToApproximatePose(
                    estimatedMapWorldPosition,
                    estimatedMapWorldRotation))
            {
                IsApproximatePdrLocalization = false;
                return false;
            }

            return ActivateIndoorNavigation();
        }

        private bool ActivateIndoorNavigation()
        {
            if (foundation == null || foundation.ModelRoot == null)
                return false;

            foundation.ModelRoot.gameObject.SetActive(true);
            mapVisibility?.ApplyVisibilityPolicy();
            ReanchorIndoorNavMesh();

            if (outdoorContext != null)
            {
                outdoorContext.RibbonRenderer?.ClearPath();
                if (outdoorContext.RouteController != null)
                    outdoorContext.RouteController.enabled = false;
                if (outdoorContext.PoseController != null)
                    outdoorContext.PoseController.enabled = false;
                if (outdoorContext.LocationProvider != null)
                    outdoorContext.LocationProvider.enabled = false;
                if (outdoorContext.MinimapController != null)
                    outdoorContext.MinimapController.enabled = false;
                if (outdoorContext.SchoolGround != null)
                    outdoorContext.SchoolGround.gameObject.SetActive(false);
                if (outdoorContext.UserMarker != null)
                    outdoorContext.UserMarker.gameObject.SetActive(false);
                if (outdoorContext.EntranceMarker != null)
                    outdoorContext.EntranceMarker.gameObject.SetActive(false);
            }

            ConfigureIndoorMinimap();
            string selectedRoom = outdoorContext != null
                                  && outdoorContext.RouteController != null
                ? outdoorContext.RouteController.SelectedRoomId
                : "B9-104";
            indoorContext?.BeginNavigation(selectedRoom);
            SetState(TransitionState.IndoorLocalized);
            return true;
        }

        private bool ReanchorMapSpaceToApproximatePose(
            Vector3 estimatedMapWorldPosition,
            Quaternion estimatedMapWorldRotation)
        {
            Transform mapSpace = foundation != null ? foundation.MapSpace : null;
            Camera camera = foundation != null ? foundation.ArCamera : null;
            if (mapSpace == null || camera == null)
                return false;

            Vector3 mapLocalPosition = mapSpace.InverseTransformPoint(
                estimatedMapWorldPosition);
            Quaternion mapLocalRotation = Quaternion.Inverse(mapSpace.rotation)
                                          * estimatedMapWorldRotation;
            mapSpace.rotation = camera.transform.rotation
                                * Quaternion.Inverse(mapLocalRotation);
            Vector3 mappedPosition = mapSpace.TransformPoint(mapLocalPosition);
            mapSpace.position += camera.transform.position - mappedPosition;
            return true;
        }

        private void HandleLocalizationFailure()
        {
            if (State != TransitionState.StartingVps && State != TransitionState.Scanning)
                return;

            VpsValid = false;
            ExperimentDecision?.Invoke(
                "quality_threshold_failed",
                "VPS provider invalid: MultiSet LocalizationFailure");
            // The official firstLocalizationUntilSuccess flow owns retry. A failed
            // frame batch is not treated as a failed navigation session here.
            if (scanStartedAt <= 0f)
                scanStartedAt = Time.unscaledTime;
            indoorHandoverPending = false;
            SetState(TransitionState.Scanning);
        }

        private bool EvaluateExperimentGates(
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 endPosition,
            Quaternion endRotation,
            out string failure)
        {
            LastPositionDeltaMeters = Vector3.Distance(startPosition, endPosition);
            LastHeadingDeltaDegrees = Quaternion.Angle(startRotation, endRotation);
            float positionScore = DescendingScore(LastPositionDeltaMeters, 0.35f, 3f);
            float headingScore = DescendingScore(LastHeadingDeltaDegrees, 5f, 45f);
            float motionScore = Mathf.Min(positionScore, headingScore);
            bool confidenceAvailable = TryReadLocalizationFloat(
                out float sdkConfidence,
                "confidence",
                "Confidence",
                "localizationConfidence",
                "lastConfidence");
            if (confidenceAvailable && sdkConfidence > 1f)
                sdkConfidence *= 0.01f;
            sdkConfidence = Mathf.Clamp01(sdkConfidence);
            LastVpsConfidenceAvailable = confidenceAvailable;
            // MapLocalizationManager 1.9.2 exposes a no-argument success event. When
            // confidence is unavailable, successful provider validation is the explicit
            // fallback signal; availability remains false in CSV for auditability.
            LastVpsConfidence = confidenceAvailable ? sdkConfidence : 1f;
            float confidenceScore = confidenceAvailable
                ? Mathf.InverseLerp(
                    experimentProfile.MinimumVpsConfidence,
                    1f,
                    sdkConfidence)
                : 1f;

            LastMapIdAvailable = TryReadLocalizationString(
                out string mapId,
                "localizedMapId",
                "mapId",
                "MapId",
                "mapID",
                "currentMapId");
            LastLocalizedMapId = LastMapIdAvailable ? mapId : string.Empty;
            LastMapMatchesBuilding = LastMapIdAvailable
                                     && building != null
                                     && building.IsAcceptedMapId(mapId);
            if (useMapIdCheck)
            {
                if (!LastMapIdAvailable)
                {
                    failure = "Không đọc được Map-ID từ kết quả MultiSet.";
                    LastGateReason = failure;
                    return false;
                }
                if (!LastMapMatchesBuilding)
                {
                    failure = "Map-ID không khớp B9: " + mapId;
                    LastGateReason = failure;
                    return false;
                }
            }

            float dwellScore = experimentProfile.VpsDwellSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(VpsStableSeconds / experimentProfile.VpsDwellSeconds);
            LastLocalizationQuality = Mathf.Clamp01((
                confidenceScore * experimentProfile.VpsWeightConfidence
                + 1f * experimentProfile.VpsWeightFreshness
                + motionScore * experimentProfile.VpsWeightMotion
                + (LastMapMatchesBuilding ? 1f : 0f) * experimentProfile.VpsWeightMapMatch
                + dwellScore * experimentProfile.VpsWeightDwell)
                / experimentProfile.VpsWeightSum);
            VpsThresholdPassed = VpsValid
                                 && LastLocalizationQuality
                                 >= experimentProfile.VpsEnterReliability;
            if (VpsThresholdPassed)
            {
                if (vpsStableSince < 0f)
                {
                    vpsStableSince = Time.unscaledTime;
                    if (useTemporalDwell)
                        ExperimentDecision?.Invoke("dwell_started", "VPS dwell started");
                }
            }
            else if (vpsStableSince >= 0f)
            {
                if (useTemporalDwell)
                {
                    ExperimentDecision?.Invoke(
                        "dwell_reset",
                        $"VPS dwell reset because qVPS={LastLocalizationQuality:0.00} "
                        + $"dropped below tauVPS={experimentProfile.VpsEnterReliability:0.00}");
                }
                vpsStableSince = -1f;
            }
            VpsDwellGatePassed = !useTemporalDwell
                                 || VpsStableSeconds >= experimentProfile.VpsDwellSeconds;
            PublishVpsGateChanges();

            if (useQualityThreshold && !VpsThresholdPassed)
            {
                failure = $"Chất lượng VPS {LastLocalizationQuality:0.00} dưới ngưỡng "
                          + experimentProfile.VpsEnterReliability.ToString("0.00");
                LastGateReason = failure;
                return false;
            }

            if (useTemporalDwell && !VpsDwellGatePassed)
            {
                failure = $"VPS dwell {VpsStableSeconds:0.00}s/"
                          + $"{experimentProfile.VpsDwellSeconds:0.00}s";
                LastGateReason = failure;
                return false;
            }

            failure = string.Empty;
            LastGateReason = "accepted";
            return true;
        }

        private bool LastMapGatePassed()
        {
            return !useMapIdCheck || LastMapIdAvailable && LastMapMatchesBuilding;
        }

        private void PublishVpsGateChanges()
        {
            if (VpsThresholdPassed != previousVpsThresholdPassed)
            {
                ExperimentDecision?.Invoke(
                    VpsThresholdPassed ? "quality_threshold_passed" : "quality_threshold_failed",
                    $"qVPS={LastLocalizationQuality:0.00} "
                    + (VpsThresholdPassed ? ">=" : "<")
                    + $" tauVPS={experimentProfile.VpsEnterReliability:0.00}");
                previousVpsThresholdPassed = VpsThresholdPassed;
            }
            if (VpsDwellGatePassed && !previousVpsDwellPassed && useTemporalDwell)
            {
                ExperimentDecision?.Invoke(
                    "dwell_passed",
                    $"VPS dwell passed: {VpsStableSeconds:0.00}s >= "
                    + $"{experimentProfile.VpsDwellSeconds:0.00}s");
            }
            previousVpsDwellPassed = VpsDwellGatePassed;
        }

        private void ResetResearchGateState()
        {
            VpsValid = false;
            localizationSucceededAt = -1f;
            vpsStableSince = -1f;
            VpsThresholdPassed = false;
            VpsDwellGatePassed = !useTemporalDwell;
            previousVpsThresholdPassed = false;
            previousVpsDwellPassed = VpsDwellGatePassed;
            LastPositionDeltaMeters = 0f;
            LastHeadingDeltaDegrees = 0f;
            LastVpsConfidence = 0f;
            LastVpsConfidenceAvailable = false;
            LastLocalizedMapId = string.Empty;
            LastMapIdAvailable = false;
            LastMapMatchesBuilding = false;
        }

        private static float DescendingScore(float value, float good, float bad)
        {
            if (!float.IsFinite(value))
                return 0f;
            return 1f - Mathf.InverseLerp(good, bad, value);
        }

        private void RejectLocalization(string reason)
        {
            indoorHandoverPending = false;
            FailureReason = reason;
            // Keep listening to MultiSet's background/relocalization callbacks. Do
            // not launch a second custom LocalizeFrame loop beside the SDK.
            SetState(TransitionState.Scanning);
        }

        private bool TryReadLocalizationString(out string value, params string[] memberNames)
        {
            value = ReadMemberValue(mapLocalizationManager, memberNames)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return true;
            object response = ReadMemberValue(
                mapLocalizationManager,
                "lastLocalizationResult",
                "localizationResult",
                "lastResponse",
                "response");
            value = ReadMemberValue(response, memberNames)?.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private bool TryReadLocalizationFloat(out float value, params string[] memberNames)
        {
            object raw = ReadMemberValue(mapLocalizationManager, memberNames);
            if (raw == null)
            {
                object response = ReadMemberValue(
                    mapLocalizationManager,
                    "lastLocalizationResult",
                    "localizationResult",
                    "lastResponse",
                    "response");
                raw = ReadMemberValue(response, memberNames);
            }

            try
            {
                if (raw != null)
                {
                    value = Convert.ToSingle(raw);
                    return float.IsFinite(value);
                }
            }
            catch
            {
                // Optional SDK metadata can have a vendor-specific representation.
            }
            value = 0f;
            return false;
        }

        private static object ReadMemberValue(object source, params string[] memberNames)
        {
            if (source == null)
                return null;
            Type type = source.GetType();
            for (int i = 0; i < memberNames.Length; i++)
            {
                try
                {
                    FieldInfo field = type.GetField(memberNames[i], ReflectionFlags);
                    if (field != null)
                        return field.GetValue(source);
                    PropertyInfo property = type.GetProperty(memberNames[i], ReflectionFlags);
                    if (property != null && property.CanRead)
                        return property.GetValue(source);
                }
                catch
                {
                    // Continue through aliases when an SDK property getter is unavailable.
                }
            }
            return null;
        }

        private void ReanchorIndoorNavMesh()
        {
            NavMeshSurface surface = foundation != null ? foundation.NavMeshSurface : null;
            if (surface == null || surface.navMeshData == null || !surface.isActiveAndEnabled)
                return;

            surface.RemoveData();
            surface.AddData();
        }

        private void ConfigureIndoorMinimap()
        {
            if (foundation == null || foundation.MinimapCamera == null || foundation.ModelRoot == null)
                return;

            Renderer[] renderers = foundation.ModelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float height = Mathf.Max(15f, extent * 2f);
            Camera camera = foundation.MinimapCamera;
            camera.transform.position = bounds.center + Vector3.up * height;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(8f, extent * 1.15f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = height + Mathf.Max(30f, extent * 3f);

            int mask = 1 << 0;
            int mapLayer = LayerMask.NameToLayer("MapPlane");
            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            if (mapLayer >= 0) mask |= 1 << mapLayer;
            if (minimapLayer >= 0) mask |= 1 << minimapLayer;
            camera.cullingMask = mask;
            camera.enabled = true;
        }

        private void Fail(string reason)
        {
            StopSdkStartCoroutine();
            SetMultiSetLocalizerEnabled(false);
            indoorHandoverPending = false;
            FailureReason = reason;
            SetState(TransitionState.Failed);
        }

        private void SetState(TransitionState value)
        {
            if (State == value)
                return;
            State = value;
            StateChanged?.Invoke(value);
        }

        private void SetMultiSetLocalizerEnabled(bool value)
        {
            if (mapLocalizationManager == null)
                return;

            GameObject localizerObject = mapLocalizationManager.gameObject;
            if (value)
            {
                if (!localizerObject.activeSelf)
                    localizerObject.SetActive(true);
                mapLocalizationManager.enabled = true;
                return;
            }

            mapLocalizationManager.StopAllCoroutines();
            mapLocalizationManager.enabled = false;
            if (localizerObject.activeSelf)
                localizerObject.SetActive(false);
        }

        private void StopSdkStartCoroutine()
        {
            if (sdkStartCoroutine == null)
                return;
            StopCoroutine(sdkStartCoroutine);
            sdkStartCoroutine = null;
        }

        private bool TrySetFieldOrProperty(Type type, string memberName, object value)
        {
            FieldInfo field = type.GetField(memberName, ReflectionFlags);
            if (field != null && value != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(mapLocalizationManager, value);
                return true;
            }
            if (field != null && value == null)
            {
                field.SetValue(mapLocalizationManager, null);
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, ReflectionFlags);
            if (property != null && property.CanWrite
                && value != null && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(mapLocalizationManager, value);
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Simulate Arrival And Start VPS")]
        private void DebugStartVps()
        {
            if (State == TransitionState.WaitingForEntrance)
                BeginAutomaticLocalization();
        }

        [ContextMenu("Debug/Simulate VPS Success")]
        private void DebugLocalizationSuccess()
        {
            if (State == TransitionState.WaitingForEntrance)
                SetState(TransitionState.Scanning);
            HandleLocalizationSuccess();
        }

        [ContextMenu("Debug/Simulate VPS Failure")]
        private void DebugLocalizationFailure()
        {
            if (State == TransitionState.WaitingForEntrance)
                SetState(TransitionState.Scanning);
            HandleLocalizationFailure();
        }
#endif
    }
}
