using System;
using System.Collections.Generic;
using ARNavB9V2.Handover;
using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Reliability
{
    /// <summary>
    /// Reliability state owner for GPS -> transition PDR -> MultiSet VPS.
    /// GPS is frozen at the outer volume and VPS is requested only inside the
    /// true inner B9 volume.
    /// </summary>
    [DefaultExecutionOrder(80)]
    [DisallowMultipleComponent]
    public sealed class B9ReliableNavigationController : MonoBehaviour
    {
        [SerializeField] private B9SceneContext foundation;
        [SerializeField] private B9OutdoorSceneContext outdoor;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9IndoorSceneContext indoor;
        [SerializeField] private B9BuildingTransitionGeometry geometry;
        [SerializeField] private B10BuildingAuthoring b10Building;
        [SerializeField] private B9TransitionPdrTracker transitionPdr;
        [SerializeField, Min(0.05f)] private float guidanceRefreshSeconds = 0.2f;
        [SerializeField, Min(0.2f)] private float innerNavMeshSampleRadiusMeters = 3f;
        [SerializeField, Min(0.2f)] private float destinationSampleRadiusMeters = 6f;
        [SerializeField, Min(0f)] private float minimumPdrHoldSeconds = 0.35f;
        [SerializeField, Min(0.5f)] private float pdrFallbackArrivalDistanceMeters = 1.8f;
        [SerializeField, Min(1f)] private float forcedApproximateLocalizationSeconds = 30f;
        [SerializeField, Min(1f)] private float approximatePoseSampleRadiusMeters = 12f;
        [Header("Indoor -> outdoor reliability")]
        [SerializeField, Min(1f)] private float maximumExitGpsAccuracyMeters = 30f;
        [SerializeField, Min(1f)] private float maximumExitGpsPdrSeparationMeters = 35f;
        [SerializeField, Min(0f)] private float exitGpsStableSeconds = 1.5f;
        [SerializeField, Range(1, 8)] private int minimumStableGpsSamples = 2;
        [SerializeField, Min(0.5f)] private float maximumGpsSampleJumpMeters = 12f;

        private readonly B9NavigationStateMachine stateMachine = new B9NavigationStateMachine();
        private readonly List<Vector3> transitionGuidance = new List<Vector3>(32);
        private NavMeshPath indoorPreviewPath;
        private float enteredPdrAt;
        private float nextGuidanceAt;
        private B9PortalAnchor activeExitPortal;
        private Transform activeExitIndoorAnchor;
        private Transform activeExitCampusAnchor;
        private bool exitRouteRequested;
        private int lastExitGpsSampleVersion = -1;
        private int stableExitGpsSamples;
        private float exitGpsStableSince = -1f;
        private Vector3 previousExitGpsPosition;
        private bool hasPreviousExitGpsPosition;
        private string pendingOutdoorDestinationId = string.Empty;
        private string pendingIndoorRoomId = string.Empty;
        private bool useQualityThreshold = true;
        private bool useTemporalDwell = true;
        private bool useRecoveryFsm = true;
        private bool useContinuityGate = true;
        private B9HarmonyExperimentProfile experimentProfile =
            B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony);
        private int qualityGpsSampleVersion = -1;
        private Vector3 previousQualityGpsPosition;
        private float previousQualityGpsAt;
        private float gpsMotionScore = 1f;
        private float gpsQualityStableSince = -1f;
        private IB9HandoverGateParameterSource gateParameterSource;
        private bool previousGpsThresholdPassed;
        private bool previousGpsDwellPassed;
        private float stateChangedAt;
        private bool pdrFallbackDestinationArrived;
        private Vector3 pdrFallbackArrivalCampusPosition;
        private bool forcedApproximateLocalizationUsed;
        private string activeBuildingId = "B9";

        public B9NavigationState State => stateMachine.Current;
        public B9PoseSource ActiveSource { get; private set; } = B9PoseSource.Gps;
        public Vector3 CurrentCampusPosition
        {
            get
            {
                if (pdrFallbackDestinationArrived)
                    return pdrFallbackArrivalCampusPosition;
                if (transitionPdr != null && transitionPdr.IsTracking)
                    return transitionPdr.CampusPosition;
                if (State == B9NavigationState.IndoorVps
                    && indoor?.PoseTracker != null)
                {
                    return MapWorldToCampusPoint(indoor.PoseTracker.CurrentPosition);
                }
                return outdoor != null && outdoor.LocationProvider != null
                    ? outdoor.LocationProvider.CampusPosition
                    : Vector3.zero;
            }
        }
        public Vector3 CurrentMapWorldPosition
        {
            get
            {
                if (State == B9NavigationState.IndoorVps && indoor?.PoseTracker != null)
                    return indoor.PoseTracker.CurrentPosition;
                return CampusToMapWorldPoint(CurrentCampusPosition);
            }
        }
        public float TransitionRemainingDistanceMeters { get; private set; }
        public bool PdrFallbackDestinationArrived => pdrFallbackDestinationArrived;
        public bool ExitRouteRequested => exitRouteRequested;
        public string ActiveExitName => activeExitPortal != null
            ? activeExitPortal.DisplayName
            : activeExitCampusAnchor != null ? activeExitCampusAnchor.name : string.Empty;
        public string ActiveBuildingId => activeBuildingId;
        public int StableExitGpsSamples => stableExitGpsSamples;
        public int RequiredStableExitGpsSamples => minimumStableGpsSamples;
        public float GpsReliability { get; private set; }
        public float GpsStableSeconds => gpsQualityStableSince < 0f
            ? 0f
            : Mathf.Max(0f, Time.unscaledTime - gpsQualityStableSince);
        public bool GpsThresholdPassed { get; private set; }
        public bool GpsDwellGatePassed { get; private set; }
        public string CandidateSource { get; private set; } = "GPS";
        public string DecisionReason { get; private set; } = "boot";
        public float StateAgeSeconds => Mathf.Max(0f, Time.unscaledTime - stateChangedAt);
        public event Action<B9ReliabilityTransition> StateChanged;
        public event Action<string, string> ExperimentDecision;

        public void ApplyExperimentProfile(B9HarmonyExperimentProfile profile)
        {
            useQualityThreshold = profile.QualityThreshold;
            useTemporalDwell = profile.TemporalDwell;
            useRecoveryFsm = profile.RecoveryFsm;
            useContinuityGate = profile.ContinuityGate;
            experimentProfile = profile;
            ResetExitGpsStability();
        }

        public void SetGateParameterSource(IB9HandoverGateParameterSource source)
        {
            gateParameterSource = source;
        }

        public B9HandoverGateParameters CurrentGateParameters => gateParameterSource != null
            ? gateParameterSource.GetGateParameters(experimentProfile)
            : B9HandoverGateParameters.FromProfile(experimentProfile);

        public void Configure(
            B9SceneContext sceneFoundation,
            B9OutdoorSceneContext outdoorContext,
            B9VpsTransitionController transition,
            B9IndoorSceneContext indoorContext,
            B9BuildingTransitionGeometry transitionGeometry,
            B9TransitionPdrTracker pdrTracker)
        {
            foundation = sceneFoundation;
            outdoor = outdoorContext;
            vpsTransition = transition;
            indoor = indoorContext;
            geometry = transitionGeometry;
            transitionPdr = pdrTracker;
            vpsTransition?.SetExternalHandoverControl(true);
        }

        public void AttachTransitionGeometry(B9BuildingTransitionGeometry transitionGeometry)
        {
            geometry = transitionGeometry;
        }

        private void Awake()
        {
            if (geometry == null && foundation != null)
                geometry = foundation.HandoverGeometry;
            if (b10Building == null)
                b10Building = FindFirstObjectByType<B10BuildingAuthoring>(
                    FindObjectsInactive.Include);
            indoorPreviewPath = new NavMeshPath();
            vpsTransition?.SetExternalHandoverControl(true);
            stateChangedAt = Time.unscaledTime;
        }

        private void OnEnable()
        {
            if (vpsTransition != null)
                vpsTransition.StateChanged += HandleVpsStateChanged;
            if (indoor?.RouteController != null)
                indoor.RouteController.StateChanged += HandleIndoorRouteStateChanged;
        }

        private void OnDisable()
        {
            if (vpsTransition != null)
                vpsTransition.StateChanged -= HandleVpsStateChanged;
            if (indoor?.RouteController != null)
                indoor.RouteController.StateChanged -= HandleIndoorRouteStateChanged;
        }

        private void Update()
        {
            EvaluateGpsResearchState();
            switch (State)
            {
                case B9NavigationState.OutdoorGps:
                    EvaluateOutdoorEntry();
                    break;
                case B9NavigationState.EnteringWithPdr:
                    UpdatePdrPresentation();
                    if (Time.unscaledTime - enteredPdrAt >= minimumPdrHoldSeconds)
                        EvaluateInnerEntryOrRetreat();
                    break;
                case B9NavigationState.VpsScanning:
                case B9NavigationState.VpsFailed:
                    if (!pdrFallbackDestinationArrived)
                    {
                        UpdatePdrPresentation();
                        EvaluatePdrFallbackArrival();
                        if (!pdrFallbackDestinationArrived)
                            TryForceApproximateLocalization();
                    }
                    break;
                case B9NavigationState.IndoorVps:
                    EvaluateObservedExit();
                    break;
                case B9NavigationState.ExitingWithPdr:
                    UpdateExitPdrPresentation();
                    EvaluateStableOutdoorGps();
                    break;
            }
        }

        public bool RequestExitToOutdoor()
        {
            pendingOutdoorDestinationId = string.Empty;
            pendingIndoorRoomId = string.Empty;
            return RequestNearestExit();
        }

        public bool NavigateToIndoorRoom(string roomId)
        {
            if (outdoor?.RouteController == null || string.IsNullOrWhiteSpace(roomId))
                return false;

            string normalizedRoomId = roomId.Trim().ToUpperInvariant();
            if (normalizedRoomId.StartsWith("B10-", StringComparison.Ordinal))
                return NavigateToB10Room(normalizedRoomId);

            if (State == B9NavigationState.IndoorVps
                && string.Equals(activeBuildingId, "B10", StringComparison.OrdinalIgnoreCase))
            {
                if (!outdoor.RouteController.SetDestinationRoom(normalizedRoomId))
                    return false;
                pendingOutdoorDestinationId = string.Empty;
                pendingIndoorRoomId = normalizedRoomId;
                return RequestNearestExit();
            }

            if (State == B9NavigationState.OutdoorGps)
            {
                activeBuildingId = "B9";
                vpsTransition?.SelectBuildingForRoom(normalizedRoomId);
            }

            if (pdrFallbackDestinationArrived)
            {
                if (!outdoor.RouteController.SetDestinationRoom(roomId))
                    return false;

                pdrFallbackDestinationArrived = false;
                forcedApproximateLocalizationUsed = false;
                indoor?.PrepareForLocalization();
                transitionPdr?.BeginTracking(pdrFallbackArrivalCampusPosition);
                vpsTransition?.BeginAutomaticLocalization();
                UpdatePdrPresentation(force: true);
                return true;
            }

            if (State == B9NavigationState.IndoorVps)
            {
                CancelExitRequest();
                if (!outdoor.RouteController.SetDestinationRoom(roomId))
                    return false;
                return indoor != null && indoor.BeginNavigation(roomId);
            }

            if (State == B9NavigationState.ExitingWithPdr)
            {
                if (!outdoor.RouteController.SetDestinationRoom(roomId))
                    return false;
                pendingOutdoorDestinationId = string.Empty;
                pendingIndoorRoomId = roomId.Trim().ToUpperInvariant();
                return true;
            }

            return outdoor.RouteController.SetDestinationRoom(roomId);
        }

        private bool NavigateToB10Room(string roomId)
        {
            if (pdrFallbackDestinationArrived)
            {
                if (!outdoor.RouteController.SetDestinationRoom(roomId))
                    return false;
                pdrFallbackDestinationArrived = false;
                indoor?.StopNavigation();
                RestoreOutdoorGps("destination changed from B9 to B10 room");
                activeBuildingId = "B10";
                vpsTransition?.SelectBuildingForRoom(roomId);
                return true;
            }

            switch (State)
            {
                case B9NavigationState.OutdoorGps:
                    activeBuildingId = "B10";
                    if (vpsTransition != null && !vpsTransition.SelectBuildingForRoom(roomId))
                        return false;
                    return outdoor.RouteController.SetDestinationRoom(roomId);
                case B9NavigationState.EnteringWithPdr:
                case B9NavigationState.VpsScanning:
                case B9NavigationState.VpsFailed:
                    vpsTransition?.CancelLocalization();
                    if (!outdoor.RouteController.SetDestinationRoom(roomId))
                        return false;
                    RestoreOutdoorGps("destination changed from B9 handover to B10 room");
                    return true;
                case B9NavigationState.IndoorVps:
                    if (IsB10Active)
                    {
                        CancelExitRequest();
                        if (!outdoor.RouteController.SetDestinationRoom(roomId))
                            return false;
                        return indoor != null && indoor.BeginNavigation(roomId);
                    }
                    if (!outdoor.RouteController.SetDestinationRoom(roomId))
                        return false;
                    pendingOutdoorDestinationId = string.Empty;
                    pendingIndoorRoomId = roomId;
                    return RequestNearestExit();
                case B9NavigationState.ExitingWithPdr:
                    if (!outdoor.RouteController.SetDestinationRoom(roomId))
                        return false;
                    pendingOutdoorDestinationId = string.Empty;
                    pendingIndoorRoomId = roomId;
                    return true;
                default:
                    return false;
            }
        }

        public bool NavigateToOutdoorDestination(string destinationId)
        {
            if (outdoor?.RouteController == null || string.IsNullOrWhiteSpace(destinationId))
                return false;

            if (pdrFallbackDestinationArrived)
            {
                pdrFallbackDestinationArrived = false;
                indoor?.StopNavigation();
                RestoreOutdoorGps("new outdoor destination after PDR arrival");
                return outdoor.RouteController.SetOutdoorDestination(destinationId);
            }

            switch (State)
            {
                case B9NavigationState.OutdoorGps:
                    return outdoor.RouteController.SetOutdoorDestination(destinationId);
                case B9NavigationState.EnteringWithPdr:
                case B9NavigationState.VpsScanning:
                case B9NavigationState.VpsFailed:
                    vpsTransition?.CancelLocalization();
                    RestoreOutdoorGps("destination changed to outdoor building");
                    return outdoor.RouteController.SetOutdoorDestination(destinationId);
                case B9NavigationState.IndoorVps:
                    if (!outdoor.RouteController.SetOutdoorDestination(destinationId))
                        return false;
                    pendingOutdoorDestinationId = destinationId.Trim().ToUpperInvariant();
                    pendingIndoorRoomId = string.Empty;
                    return RequestNearestExit();
                case B9NavigationState.ExitingWithPdr:
                    if (!outdoor.RouteController.SetOutdoorDestination(destinationId))
                        return false;
                    pendingOutdoorDestinationId = destinationId.Trim().ToUpperInvariant();
                    pendingIndoorRoomId = string.Empty;
                    return true;
                default:
                    return false;
            }
        }

        private bool RequestNearestExit()
        {
            if (State != B9NavigationState.IndoorVps || indoor == null)
                return false;

            Vector3 mapPosition = indoor.PoseTracker != null
                ? indoor.PoseTracker.CurrentPosition
                : foundation != null && foundation.ArCamera != null
                    ? foundation.ArCamera.transform.position
                    : Vector3.zero;
            activeExitPortal = null;
            activeExitIndoorAnchor = null;
            activeExitCampusAnchor = null;
            if (IsB10Active)
            {
                if (b10Building == null
                    || !b10Building.TryGetNearestMapEntrance(
                        mapPosition,
                        out activeExitCampusAnchor,
                        out Vector3 indoorPoint))
                    return false;
                GameObject anchorObject = new GameObject("B10 Runtime Exit Anchor");
                activeExitIndoorAnchor = anchorObject.transform;
                activeExitIndoorAnchor.SetParent(b10Building.IndoorMapSpace, true);
                activeExitIndoorAnchor.position = indoorPoint;
                activeExitIndoorAnchor.rotation = b10Building.CampusToMapWorldRotation(
                    activeExitCampusAnchor.rotation);
            }
            else
            {
                if (geometry == null
                    || !geometry.TryGetNearestMapPortal(mapPosition, out activeExitPortal))
                    return false;
                activeExitCampusAnchor = activeExitPortal.OutdoorCampusAnchor;
                activeExitIndoorAnchor = activeExitPortal.IndoorMapAnchor;
            }

            exitRouteRequested = true;
            if (indoor.BeginExitNavigation(activeExitIndoorAnchor))
                return true;

            exitRouteRequested = false;
            activeExitPortal = null;
            DestroyRuntimeExitAnchor();
            pendingOutdoorDestinationId = string.Empty;
            pendingIndoorRoomId = string.Empty;
            return false;
        }

        private void EvaluateObservedExit()
        {
            if (!exitRouteRequested
                || activeExitIndoorAnchor == null
                || indoor?.PoseTracker == null
                || !indoor.PoseTracker.IsTracking)
                return;

            Vector3 mapPosition = indoor.PoseTracker.UnconstrainedPosition;
            // The route's red marker is guidance only. Once the tracked pose is
            // outside the real indoor B9 volume, begin the outdoor handover from
            // the observed position instead of requiring arrival at that marker.
            // The route pose is snapped to NavMesh and can remain stuck inside.
            if (ContainsInnerMapWorldPoint(mapPosition))
                return;

            BeginExitPdrHandover();
        }

        public void CancelExitRequest()
        {
            if (State != B9NavigationState.IndoorVps || !exitRouteRequested)
                return;

            exitRouteRequested = false;
            activeExitPortal = null;
            DestroyRuntimeExitAnchor();
            pendingOutdoorDestinationId = string.Empty;
            pendingIndoorRoomId = string.Empty;
            string roomId = indoor?.RouteController?.DestinationRoomId;
            if (!string.IsNullOrWhiteSpace(roomId))
                outdoor?.RouteController?.SetDestinationRoom(roomId);
            if (!string.IsNullOrWhiteSpace(roomId))
                indoor?.BeginNavigation(roomId);
            else
                indoor?.StopNavigation();
        }

        public void CancelNavigation()
        {
            if (pdrFallbackDestinationArrived)
            {
                pdrFallbackDestinationArrived = false;
                indoor?.StopNavigation();
                RestoreOutdoorGps("navigation ended after PDR arrival");
                outdoor?.RouteController?.CancelNavigation();
                return;
            }

            switch (State)
            {
                case B9NavigationState.OutdoorGps:
                    outdoor?.RouteController?.CancelNavigation();
                    break;
                case B9NavigationState.EnteringWithPdr:
                case B9NavigationState.VpsScanning:
                case B9NavigationState.VpsFailed:
                    vpsTransition?.CancelLocalization();
                    RestoreOutdoorGps("navigation cancelled during entry");
                    outdoor?.RouteController?.CancelNavigation();
                    break;
                case B9NavigationState.IndoorVps:
                    exitRouteRequested = false;
                    activeExitPortal = null;
                    DestroyRuntimeExitAnchor();
                    pendingOutdoorDestinationId = string.Empty;
                    pendingIndoorRoomId = string.Empty;
                    outdoor?.RouteController?.CancelNavigation();
                    indoor?.StopNavigation();
                    break;
                case B9NavigationState.ExitingWithPdr:
                    // Keep the safety handover alive until GPS is trustworthy.
                    outdoor?.RibbonRenderer?.ClearPath();
                    break;
            }
        }

        private void EvaluateOutdoorEntry()
        {
            if (outdoor == null || outdoor.LocationProvider == null
                || !outdoor.LocationProvider.HasReliableFix
                || outdoor.RouteController == null
                || !outdoor.RouteController.IsIndoorB9Destination
                   && !outdoor.RouteController.IsIndoorB10Destination)
                return;

            bool enteringB10 = outdoor.RouteController.IsIndoorB10Destination;
            activeBuildingId = enteringB10 ? "B10" : "B9";
            if (enteringB10)
            {
                if (b10Building == null
                    || !b10Building.ValidateRuntimeConfiguration(out _))
                    return;
            }
            else if (geometry == null)
            {
                geometry = foundation != null ? foundation.HandoverGeometry : null;
                if (geometry == null)
                    return;
            }

            Vector3 campusPoint = outdoor.LocationProvider.CampusPosition;
            if (!ContainsOuterCampusPoint(campusPoint))
                return;
            if (enteringB10
                && outdoor.RouteController.ActiveB10Entrance != null
                && Vector3.Distance(
                    campusPoint,
                    outdoor.RouteController.ActiveB10Entrance.position) > 10f)
                return;

            if (vpsTransition != null
                && !vpsTransition.SelectBuildingForRoom(
                    outdoor.RouteController.SelectedRoomId))
                return;

            forcedApproximateLocalizationUsed = false;
            transitionPdr.BeginTracking(campusPoint);
            enteredPdrAt = Time.unscaledTime;
            outdoor.PoseController.enabled = false;
            outdoor.RouteController.enabled = false;
            ActiveSource = B9PoseSource.Pdr;
            ChangeState(
                B9NavigationState.EnteringWithPdr,
                $"entered outer {activeBuildingId} volume; GPS correction frozen");
            UpdatePdrPresentation(force: true);
        }

        private void EvaluateInnerEntryOrRetreat()
        {
            Vector3 campusPoint = transitionPdr.CampusPosition;
            if (ContainsInnerCampusPoint(campusPoint))
            {
                ChangeState(
                    B9NavigationState.VpsScanning,
                    $"entered true {activeBuildingId} volume; start MultiSet localization");
                vpsTransition.BeginAutomaticLocalization();
                return;
            }

            if (!ContainsOuterCampusPoint(campusPoint))
                RestoreOutdoorGps($"left {activeBuildingId} handover volume before scanning");
        }

        private void UpdatePdrPresentation(bool force = false)
        {
            if (transitionPdr == null || !transitionPdr.IsTracking || outdoor == null)
                return;

            outdoor.MinimapController?.SetPoseOverride(
                transitionPdr.CampusPosition,
                transitionPdr.HeadingDegrees);
            if (!force && Time.unscaledTime < nextGuidanceAt)
                return;
            nextGuidanceAt = Time.unscaledTime + guidanceRefreshSeconds;
            BuildContinuousTransitionGuidance();
        }

        private void EvaluatePdrFallbackArrival()
        {
            if (transitionPdr == null || !transitionPdr.IsTracking)
                return;

            B9RoomAnchor destination = FindSelectedRoomAnchor();
            if (destination == null)
                return;

            Vector3 destinationCampus = MapWorldToCampusPoint(destination.transform.position);
            Vector3 currentCampus = transitionPdr.CampusPosition;
            destinationCampus.y = currentCampus.y;
            float distance = Vector3.Distance(currentCampus, destinationCampus);
            if (distance > pdrFallbackArrivalDistanceMeters)
                return;

            pdrFallbackDestinationArrived = true;
            forcedApproximateLocalizationUsed = false;
            pdrFallbackArrivalCampusPosition = currentCampus;
            TransitionRemainingDistanceMeters = 0f;
            vpsTransition?.CancelLocalization();
            outdoor?.RibbonRenderer?.ClearPath();
            outdoor?.MinimapController?.ClearPoseOverride();
            outdoor?.RouteController?.CancelNavigation();
            transitionPdr.StopTracking();
            indoor?.RouteController?.CompleteFromPdrFallback(destination.RoomId);
            ExperimentDecision?.Invoke(
                "pdr_destination_arrived",
                $"PDR reached {destination.RoomId} within {distance:0.00}m; VPS scan stopped");
        }

        private void TryForceApproximateLocalization()
        {
            if (forcedApproximateLocalizationUsed
                || vpsTransition == null
                || transitionPdr == null
                || !transitionPdr.IsTracking
                || vpsTransition.CurrentScanElapsedSeconds
                   < forcedApproximateLocalizationSeconds)
                return;

            if (!HasActiveMapBridge)
                return;

            Vector3 campusPosition = transitionPdr.CampusPosition;
            Vector3 estimatedMapPosition = CampusToMapWorldPoint(campusPosition);
            Quaternion estimatedMapRotation = CampusToMapWorldRotation(
                Quaternion.Euler(0f, transitionPdr.HeadingDegrees, 0f));
            bool navMeshSampled = NavMesh.SamplePosition(
                estimatedMapPosition,
                out NavMeshHit nearestHit,
                approximatePoseSampleRadiusMeters,
                NavMesh.AllAreas);
            if (navMeshSampled)
                estimatedMapPosition = nearestHit.position;
            else
                estimatedMapPosition = GetActiveIndoorEntrancePoint();

            string detail = navMeshSampled
                ? $"30s VPS timeout; PDR pose projected to nearest {activeBuildingId} NavMesh point ({nearestHit.distance:0.00}m)"
                : $"30s VPS timeout; PDR pose projected to {activeBuildingId} entrance fallback point";
            if (!vpsTransition.CompleteApproximatePdrLocalization(
                    estimatedMapPosition,
                    estimatedMapRotation,
                    detail))
                return;

            forcedApproximateLocalizationUsed = true;
            ActiveSource = B9PoseSource.Pdr;
            CandidateSource = "PDR_APPROXIMATE";
            DecisionReason = detail;
            ExperimentDecision?.Invoke("pdr_approximate_handover_completed", detail);
        }

        private void UpdateExitPdrPresentation()
        {
            if (transitionPdr == null || !transitionPdr.IsTracking || outdoor == null)
                return;
            outdoor.MinimapController?.SetPoseOverride(
                transitionPdr.CampusPosition,
                transitionPdr.HeadingDegrees);
        }

        private void BuildContinuousTransitionGuidance()
        {
            if (outdoor.RibbonRenderer == null || foundation == null || !HasActiveMapBridge)
                return;

            transitionGuidance.Clear();
            AddDistinct(transitionGuidance, transitionPdr.CampusPosition);
            Vector3 outdoorEntrance = GetActiveOutdoorEntrancePoint();
            Vector3 indoorEntrance = GetActiveIndoorEntrancePoint();
            AddDistinct(transitionGuidance, outdoorEntrance);

            B9RoomAnchor destination = FindSelectedRoomAnchor();
            bool hasIndoorPath = destination != null
                                 && NavMesh.SamplePosition(
                                     indoorEntrance,
                                     out NavMeshHit startHit,
                                     innerNavMeshSampleRadiusMeters,
                                     NavMesh.AllAreas)
                                 && NavMesh.SamplePosition(
                                     destination.transform.position,
                                     out NavMeshHit endHit,
                                     destinationSampleRadiusMeters,
                                     NavMesh.AllAreas)
                                 && NavMesh.CalculatePath(
                                     startHit.position,
                                     endHit.position,
                                     NavMesh.AllAreas,
                                     indoorPreviewPath)
                                 && indoorPreviewPath.status == NavMeshPathStatus.PathComplete
                                 && indoorPreviewPath.corners != null
                                 && indoorPreviewPath.corners.Length >= 2;

            if (hasIndoorPath)
            {
                for (int i = 0; i < indoorPreviewPath.corners.Length; i++)
                {
                    AddDistinct(
                        transitionGuidance,
                        MapWorldToCampusPoint(indoorPreviewPath.corners[i]));
                }
            }
            else if (destination != null)
            {
                AddDistinct(
                    transitionGuidance,
                    MapWorldToCampusPoint(destination.transform.position));
            }

            TransitionRemainingDistanceMeters = CalculateDistance(transitionGuidance);
            if (transitionGuidance.Count >= 2)
                outdoor.RibbonRenderer.SetPath(transitionGuidance);
        }

        private B9RoomAnchor FindSelectedRoomAnchor()
        {
            string roomId = outdoor?.RouteController?.SelectedRoomId;
            if (string.IsNullOrWhiteSpace(roomId))
                return null;

            if (roomId.StartsWith("B10-", StringComparison.OrdinalIgnoreCase))
            {
                return b10Building != null
                       && b10Building.TryGetRoom(roomId, out B9RoomAnchor b10Room)
                    ? b10Room
                    : null;
            }
            if (foundation?.RoomAnchors == null)
                return null;

            for (int i = 0; i < foundation.RoomAnchors.Count; i++)
            {
                B9RoomAnchor anchor = foundation.RoomAnchors[i];
                if (anchor != null && string.Equals(
                        anchor.RoomId,
                        roomId,
                        StringComparison.OrdinalIgnoreCase))
                    return anchor;
            }

            return null;
        }

        private void HandleVpsStateChanged(B9VpsTransitionController.TransitionState state)
        {
            switch (state)
            {
                case B9VpsTransitionController.TransitionState.StartingVps:
                case B9VpsTransitionController.TransitionState.Scanning:
                    if (State == B9NavigationState.VpsFailed)
                        ChangeState(B9NavigationState.VpsScanning, "MultiSet retry started");
                    break;
                case B9VpsTransitionController.TransitionState.IndoorLocalized:
                    CompleteIndoorHandover();
                    break;
                case B9VpsTransitionController.TransitionState.Failed:
                    if (State == B9NavigationState.VpsScanning)
                    {
                        if (useRecoveryFsm)
                        {
                            ChangeState(B9NavigationState.VpsFailed, vpsTransition.FailureReason);
                        }
                        else
                        {
                            vpsTransition.CancelLocalization();
                            RestoreOutdoorGps("VPS failed; recovery FSM disabled");
                        }
                    }
                    break;
            }
        }

        private void HandleIndoorRouteStateChanged(B9IndoorRouteController.IndoorRouteState state)
        {
            if (state == B9IndoorRouteController.IndoorRouteState.Arrived
                && State == B9NavigationState.IndoorVps
                && exitRouteRequested)
            {
                BeginExitPdrHandover();
            }
        }

        private void BeginExitPdrHandover()
        {
            if (activeExitIndoorAnchor == null || transitionPdr == null)
                return;

            Vector3 mapPosition = indoor?.PoseTracker != null
                ? indoor.PoseTracker.UnconstrainedPosition
                : activeExitIndoorAnchor.position;
            Vector3 campusSeed = MapWorldToCampusPoint(mapPosition);

            indoor?.StopNavigation();
            indoor?.PoseTracker?.StopTracking();
            indoor?.MinimapController?.Deactivate();
            if (foundation?.ModelRoot != null)
                foundation.ModelRoot.gameObject.SetActive(false);
            if (b10Building?.IndoorModel != null)
                b10Building.IndoorModel.gameObject.SetActive(false);
            if (!IsB10Active && geometry?.CampusModelProxy != null)
                geometry.CampusModelProxy.gameObject.SetActive(true);
            if (IsB10Active && b10Building?.CampusModel != null)
                b10Building.CampusModel.gameObject.SetActive(true);

            if (outdoor != null)
            {
                if (outdoor.SchoolGround != null)
                    outdoor.SchoolGround.gameObject.SetActive(true);
                if (outdoor.LocationProvider != null)
                    outdoor.LocationProvider.enabled = true;
                if (outdoor.PoseController != null)
                    outdoor.PoseController.enabled = false;
                if (outdoor.RouteController != null)
                    outdoor.RouteController.enabled = false;
                if (outdoor.MinimapController != null)
                    outdoor.MinimapController.enabled = true;
                if (outdoor.UserMarker != null)
                    outdoor.UserMarker.gameObject.SetActive(true);
                if (outdoor.EntranceMarker != null)
                    outdoor.EntranceMarker.gameObject.SetActive(true);
                outdoor.RibbonRenderer?.ClearPath();
            }

            transitionPdr.BeginTracking(campusSeed);
            ResetExitGpsStability();
            ActiveSource = B9PoseSource.Pdr;
            ChangeState(
                B9NavigationState.ExitingWithPdr,
                $"leaving {activeBuildingId} from observed pose; waiting for stable GPS");
            UpdateExitPdrPresentation();
        }

        private void EvaluateStableOutdoorGps()
        {
            B9OutdoorLocationProvider gps = outdoor?.LocationProvider;
            if (gps == null)
                return;

            bool newSample = gps.SampleVersion != lastExitGpsSampleVersion;
            if (newSample)
                lastExitGpsSampleVersion = gps.SampleVersion;

            Vector3 gpsPosition = gps.TargetCampusPosition;
            bool reliable = gps.HasReliableFix;
            if (reliable && useQualityThreshold)
            {
                reliable = GpsThresholdPassed;
                if (reliable && useContinuityGate)
                {
                    reliable = transitionPdr != null
                               && Vector3.Distance(gpsPosition, transitionPdr.CampusPosition)
                               <= maximumExitGpsPdrSeparationMeters;
                    if (reliable && newSample && hasPreviousExitGpsPosition)
                    {
                        reliable = Vector3.Distance(gpsPosition, previousExitGpsPosition)
                                   <= maximumGpsSampleJumpMeters;
                    }
                }
            }

            if (newSample)
            {
                previousExitGpsPosition = gpsPosition;
                hasPreviousExitGpsPosition = true;
            }
            if (!reliable)
            {
                stableExitGpsSamples = 0;
                exitGpsStableSince = -1f;
                return;
            }

            if (newSample)
                stableExitGpsSamples++;
            if (exitGpsStableSince < 0f)
                exitGpsStableSince = Time.unscaledTime;
            bool dwellPassed = !useTemporalDwell || GpsDwellGatePassed;
            if (dwellPassed)
            {
                ExperimentDecision?.Invoke(
                    "dwell_passed",
                    $"GPS dwell passed: {GpsStableSeconds:0.00}s >= "
                    + $"{CurrentGateParameters.GpsDwellSeconds:0.00}s");
                CompleteExitToGps();
            }
        }

        private void CompleteExitToGps()
        {
            transitionPdr?.StopTracking();
            outdoor?.MinimapController?.ClearPoseOverride();
            vpsTransition?.ReturnToOutdoor();
            if (outdoor != null)
            {
                if (outdoor.PoseController != null)
                    outdoor.PoseController.enabled = true;
                if (outdoor.RouteController != null)
                {
                    outdoor.RouteController.enabled = true;
                    if (!string.IsNullOrWhiteSpace(pendingIndoorRoomId))
                    {
                        outdoor.RouteController.SetDestinationRoom(pendingIndoorRoomId);
                        outdoor.RouteController.RefreshNow();
                    }
                    else if (!string.IsNullOrWhiteSpace(pendingOutdoorDestinationId))
                    {
                        outdoor.RouteController.SetOutdoorDestination(
                            pendingOutdoorDestinationId);
                        outdoor.RouteController.RefreshNow();
                    }
                    else
                    {
                        outdoor.RouteController.CancelNavigation();
                    }
                }
            }
            exitRouteRequested = false;
            activeExitPortal = null;
            DestroyRuntimeExitAnchor();
            pendingOutdoorDestinationId = string.Empty;
            pendingIndoorRoomId = string.Empty;
            ActiveSource = B9PoseSource.Gps;
            ChangeState(
                B9NavigationState.OutdoorGps,
                $"stable GPS reacquired after leaving {activeBuildingId}");
        }

        private void ResetExitGpsStability()
        {
            lastExitGpsSampleVersion = outdoor?.LocationProvider != null
                ? outdoor.LocationProvider.SampleVersion
                : -1;
            stableExitGpsSamples = 0;
            exitGpsStableSince = -1f;
            previousExitGpsPosition = Vector3.zero;
            hasPreviousExitGpsPosition = false;
            qualityGpsSampleVersion = -1;
            gpsQualityStableSince = -1f;
            previousGpsThresholdPassed = false;
            previousGpsDwellPassed = false;
        }

        private void CompleteIndoorHandover()
        {
            transitionPdr?.StopTracking();
            outdoor?.MinimapController?.ClearPoseOverride();
            if (geometry?.CampusModelProxy != null)
                geometry.CampusModelProxy.gameObject.SetActive(false);
            if (b10Building?.CampusModel != null)
                b10Building.CampusModel.gameObject.SetActive(false);
            bool approximate = vpsTransition != null
                               && vpsTransition.IsApproximatePdrLocalization;
            ActiveSource = approximate ? B9PoseSource.Pdr : B9PoseSource.Vps;
            ChangeState(
                B9NavigationState.IndoorVps,
                approximate
                    ? "30s VPS timeout; indoor navigation started from approximate PDR pose"
                    : "MultiSet localization accepted");
        }

        private void RestoreOutdoorGps(string reason)
        {
            transitionPdr?.StopTracking();
            outdoor?.MinimapController?.ClearPoseOverride();
            forcedApproximateLocalizationUsed = false;
            if (outdoor != null)
            {
                outdoor.PoseController.enabled = true;
                outdoor.RouteController.enabled = true;
                outdoor.RouteController.RefreshNow();
            }
            ActiveSource = B9PoseSource.Gps;
            ChangeState(B9NavigationState.OutdoorGps, reason);
        }

        private void ChangeState(B9NavigationState next, string reason)
        {
            B9NavigationState previous = stateMachine.Current;
            if (!stateMachine.TryTransition(next))
                return;
            stateChangedAt = Time.unscaledTime;
            StateChanged?.Invoke(new B9ReliabilityTransition(
                previous,
                next,
                ActiveSource,
                reason,
                Time.unscaledTime));
        }

        private void EvaluateGpsResearchState()
        {
            B9OutdoorLocationProvider gps = outdoor?.LocationProvider;
            if (gps == null)
                return;

            if (gps.SampleVersion != qualityGpsSampleVersion)
            {
                float now = Time.unscaledTime;
                if (qualityGpsSampleVersion >= 0)
                {
                    float dt = Mathf.Max(0.01f, now - previousQualityGpsAt);
                    float speed = Vector3.Distance(
                        gps.TargetCampusPosition,
                        previousQualityGpsPosition) / dt;
                    gpsMotionScore = DescendingScore(speed, 2.25f, 4.5f);
                }
                else
                {
                    gpsMotionScore = 1f;
                }
                qualityGpsSampleVersion = gps.SampleVersion;
                previousQualityGpsPosition = gps.TargetCampusPosition;
                previousQualityGpsAt = now;
            }

            bool valid = gps.HasReliableFix && gps.SampleAgeSeconds <= 5f;
            float accuracy = valid
                ? DescendingScore(gps.HorizontalAccuracyMeters, 5f, 30f)
                : 0f;
            float freshness = valid
                ? DescendingScore(gps.SampleAgeSeconds, 0.75f, 5f)
                : 0f;
            float motion = valid ? gpsMotionScore : 0f;
            bool nearTransition = State != B9NavigationState.OutdoorGps
                                  || ContainsOuterCampusPoint(gps.CampusPosition);
            float transition = nearTransition ? 0.75f : 1f;

            B9HandoverGateParameters gate = CurrentGateParameters;
            float withoutDwellWeight = experimentProfile.GpsWeightSum
                                       - experimentProfile.GpsWeightDwell;
            float withoutDwell = (
                accuracy * experimentProfile.GpsWeightAccuracy
                + freshness * experimentProfile.GpsWeightFreshness
                + motion * experimentProfile.GpsWeightMotion
                + transition * experimentProfile.GpsWeightTransition)
                / Mathf.Max(0.0001f, withoutDwellWeight);
            bool stableQuality = valid
                                 && withoutDwell >= gate.GpsThreshold;
            if (stableQuality)
            {
                if (gpsQualityStableSince < 0f)
                {
                    gpsQualityStableSince = Time.unscaledTime;
                    if (useTemporalDwell)
                        ExperimentDecision?.Invoke("dwell_started", "GPS dwell started");
                }
            }
            else if (gpsQualityStableSince >= 0f)
            {
                if (useTemporalDwell)
                {
                    ExperimentDecision?.Invoke(
                        "dwell_reset",
                        $"GPS dwell reset because qGPS={withoutDwell:0.00} dropped below "
                        + $"tauGPS={gate.GpsThreshold:0.00}");
                }
                gpsQualityStableSince = -1f;
            }

            float dwell = experimentProfile.GpsDwellScoreSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(GpsStableSeconds / experimentProfile.GpsDwellScoreSeconds);
            GpsReliability = Mathf.Clamp01((
                accuracy * experimentProfile.GpsWeightAccuracy
                + freshness * experimentProfile.GpsWeightFreshness
                + motion * experimentProfile.GpsWeightMotion
                + transition * experimentProfile.GpsWeightTransition
                + dwell * experimentProfile.GpsWeightDwell)
                / experimentProfile.GpsWeightSum);
            GpsThresholdPassed = valid
                                 && GpsReliability >= gate.GpsThreshold;
            GpsDwellGatePassed = !useTemporalDwell
                                 || GpsStableSeconds >= gate.GpsDwellSeconds;
            CandidateSource = State == B9NavigationState.ExitingWithPdr
                              && GpsThresholdPassed && GpsDwellGatePassed
                ? "GPS"
                : ActiveSource.ToString();
            DecisionReason = !valid
                ? "GPS provider invalid or stale"
                : !GpsThresholdPassed
                    ? $"qGPS={GpsReliability:0.00} < tauGPS={gate.GpsThreshold:0.00}"
                    : !GpsDwellGatePassed
                        ? $"GPS dwell {GpsStableSeconds:0.00}s/{gate.GpsDwellSeconds:0.00}s"
                        : $"qGPS={GpsReliability:0.00} and GPS dwell gate passed";

            if (GpsThresholdPassed != previousGpsThresholdPassed)
            {
                ExperimentDecision?.Invoke(
                    GpsThresholdPassed ? "quality_threshold_passed" : "quality_threshold_failed",
                    $"qGPS={GpsReliability:0.00} "
                    + (GpsThresholdPassed ? ">=" : "<")
                    + $" tauGPS={gate.GpsThreshold:0.00}");
                previousGpsThresholdPassed = GpsThresholdPassed;
            }
            if (GpsDwellGatePassed && !previousGpsDwellPassed && useTemporalDwell)
            {
                ExperimentDecision?.Invoke(
                    "dwell_passed",
                    $"GPS dwell passed: {GpsStableSeconds:0.00}s >= "
                    + $"{gate.GpsDwellSeconds:0.00}s");
            }
            previousGpsDwellPassed = GpsDwellGatePassed;
        }

        private static float DescendingScore(float value, float good, float bad)
        {
            if (!float.IsFinite(value))
                return 0f;
            return 1f - Mathf.InverseLerp(good, bad, value);
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (foundation == null || outdoor == null || vpsTransition == null || indoor == null)
                return Fail("Reliability navigation contexts are incomplete", out reason);
            if (geometry == null)
                return Fail("B9 handover geometry is missing", out reason);
            if (!geometry.ValidateConfiguration(out reason))
                return false;
            if (transitionPdr == null)
                return Fail("Transition PDR tracker is missing", out reason);
            reason = string.Empty;
            return true;
        }

        private bool IsB10Active => string.Equals(
            activeBuildingId,
            "B10",
            StringComparison.OrdinalIgnoreCase);

        private bool HasActiveMapBridge => IsB10Active
            ? b10Building != null
              && b10Building.CampusPlacement != null
              && b10Building.IndoorMapSpace != null
            : geometry != null && geometry.PrimaryPortal != null;

        private bool ContainsOuterCampusPoint(Vector3 campusPoint)
        {
            if (IsB10Active)
                return b10Building != null
                       && b10Building.ContainsOuterCampusPoint(campusPoint);
            return geometry != null && geometry.ContainsOuterCampusPoint(campusPoint);
        }

        private bool ContainsInnerCampusPoint(Vector3 campusPoint)
        {
            if (IsB10Active)
                return b10Building != null
                       && b10Building.ContainsInnerCampusPoint(campusPoint);
            return geometry != null && geometry.ContainsInnerCampusPoint(campusPoint);
        }

        private bool ContainsInnerMapWorldPoint(Vector3 mapPoint)
        {
            if (IsB10Active)
                return b10Building != null
                       && b10Building.ContainsInnerMapWorldPoint(mapPoint);
            return geometry != null && geometry.ContainsInnerMapWorldPoint(mapPoint);
        }

        private Vector3 CampusToMapWorldPoint(Vector3 campusPoint)
        {
            if (IsB10Active && b10Building != null)
                return b10Building.CampusToMapWorldPoint(campusPoint);
            B9PortalAnchor portal = geometry != null ? geometry.PrimaryPortal : null;
            return portal != null ? portal.CampusToMapWorldPoint(campusPoint) : campusPoint;
        }

        private Vector3 MapWorldToCampusPoint(Vector3 mapPoint)
        {
            if (IsB10Active && b10Building != null)
                return b10Building.MapWorldToCampusPoint(mapPoint);
            B9PortalAnchor portal = geometry != null ? geometry.PrimaryPortal : null;
            return portal != null ? portal.MapWorldToCampusPoint(mapPoint) : mapPoint;
        }

        private Quaternion CampusToMapWorldRotation(Quaternion campusRotation)
        {
            if (IsB10Active && b10Building != null)
                return b10Building.CampusToMapWorldRotation(campusRotation);
            B9PortalAnchor portal = geometry != null ? geometry.PrimaryPortal : null;
            return portal != null
                ? portal.CampusToMapWorldRotation(campusRotation)
                : campusRotation;
        }

        private Vector3 GetActiveOutdoorEntrancePoint()
        {
            if (IsB10Active)
            {
                Transform selected = outdoor?.RouteController?.ActiveB10Entrance;
                if (selected != null)
                    return selected.position;
                if (b10Building != null
                    && b10Building.TryGetNearestCampusEntrance(
                        CurrentCampusPosition,
                        out Transform nearest))
                    return nearest.position;
            }

            return geometry?.PrimaryPortal?.OutdoorCampusAnchor != null
                ? geometry.PrimaryPortal.OutdoorCampusAnchor.position
                : CurrentCampusPosition;
        }

        private Vector3 GetActiveIndoorEntrancePoint()
        {
            if (IsB10Active)
                return CampusToMapWorldPoint(GetActiveOutdoorEntrancePoint());
            return geometry?.PrimaryPortal?.IndoorMapAnchor != null
                ? geometry.PrimaryPortal.IndoorMapAnchor.position
                : CurrentMapWorldPosition;
        }

        private void DestroyRuntimeExitAnchor()
        {
            if (activeExitIndoorAnchor != null
                && activeExitPortal == null
                && activeExitIndoorAnchor.name == "B10 Runtime Exit Anchor")
                Destroy(activeExitIndoorAnchor.gameObject);
            activeExitIndoorAnchor = null;
            activeExitCampusAnchor = null;
        }

        private static void AddDistinct(List<Vector3> points, Vector3 point)
        {
            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], point) > 0.15f)
                points.Add(point);
        }

        private static float CalculateDistance(IReadOnlyList<Vector3> points)
        {
            float result = 0f;
            for (int i = 1; i < points.Count; i++)
                result += Vector3.Distance(points[i - 1], points[i]);
            return result;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
