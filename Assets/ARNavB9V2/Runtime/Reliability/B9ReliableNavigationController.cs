using System;
using System.Collections.Generic;
using ARNavB9V2.Handover;
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
        [SerializeField] private B9TransitionPdrTracker transitionPdr;
        [SerializeField, Min(0.05f)] private float guidanceRefreshSeconds = 0.2f;
        [SerializeField, Min(0.2f)] private float innerNavMeshSampleRadiusMeters = 3f;
        [SerializeField, Min(0.2f)] private float destinationSampleRadiusMeters = 6f;
        [SerializeField, Min(0f)] private float minimumPdrHoldSeconds = 0.35f;

        private readonly B9NavigationStateMachine stateMachine = new B9NavigationStateMachine();
        private readonly List<Vector3> transitionGuidance = new List<Vector3>(32);
        private NavMeshPath indoorPreviewPath;
        private float enteredPdrAt;
        private float nextGuidanceAt;

        public B9NavigationState State => stateMachine.Current;
        public B9PoseSource ActiveSource { get; private set; } = B9PoseSource.Gps;
        public Vector3 CurrentCampusPosition => transitionPdr != null && transitionPdr.IsTracking
            ? transitionPdr.CampusPosition
            : outdoor != null && outdoor.LocationProvider != null
                ? outdoor.LocationProvider.CampusPosition
                : Vector3.zero;
        public Vector3 CurrentMapWorldPosition
        {
            get
            {
                B9PortalAnchor portal = geometry != null ? geometry.PrimaryPortal : null;
                return portal != null
                    ? portal.CampusToMapWorldPoint(CurrentCampusPosition)
                    : CurrentCampusPosition;
            }
        }
        public float TransitionRemainingDistanceMeters { get; private set; }
        public event Action<B9ReliabilityTransition> StateChanged;

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

        private void Awake()
        {
            indoorPreviewPath = new NavMeshPath();
            vpsTransition?.SetExternalHandoverControl(true);
        }

        private void OnEnable()
        {
            if (vpsTransition != null)
                vpsTransition.StateChanged += HandleVpsStateChanged;
        }

        private void OnDisable()
        {
            if (vpsTransition != null)
                vpsTransition.StateChanged -= HandleVpsStateChanged;
        }

        private void Update()
        {
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
                    UpdatePdrPresentation();
                    break;
            }
        }

        private void EvaluateOutdoorEntry()
        {
            if (geometry == null || outdoor == null || outdoor.LocationProvider == null
                || !outdoor.LocationProvider.HasReliableFix
                || outdoor.RouteController == null
                || string.IsNullOrWhiteSpace(outdoor.RouteController.SelectedRoomId))
                return;

            Vector3 campusPoint = outdoor.LocationProvider.CampusPosition;
            if (!geometry.ContainsOuterCampusPoint(campusPoint))
                return;

            transitionPdr.BeginTracking(campusPoint);
            enteredPdrAt = Time.unscaledTime;
            outdoor.PoseController.enabled = false;
            outdoor.RouteController.enabled = false;
            ActiveSource = B9PoseSource.Pdr;
            ChangeState(
                B9NavigationState.EnteringWithPdr,
                "entered outer B9 volume; GPS correction frozen");
            UpdatePdrPresentation(force: true);
        }

        private void EvaluateInnerEntryOrRetreat()
        {
            Vector3 campusPoint = transitionPdr.CampusPosition;
            if (geometry.ContainsInnerCampusPoint(campusPoint))
            {
                ChangeState(
                    B9NavigationState.VpsScanning,
                    "entered true B9 volume; start MultiSet localization");
                vpsTransition.BeginAutomaticLocalization();
                return;
            }

            if (!geometry.ContainsOuterCampusPoint(campusPoint))
                RestoreOutdoorGps("left B9 handover volume before scanning");
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

        private void BuildContinuousTransitionGuidance()
        {
            if (geometry == null || outdoor.RibbonRenderer == null || foundation == null)
                return;

            B9PortalAnchor portal = geometry.PrimaryPortal;
            if (portal == null)
                return;

            transitionGuidance.Clear();
            AddDistinct(transitionGuidance, transitionPdr.CampusPosition);
            AddDistinct(transitionGuidance, portal.OutdoorCampusAnchor.position);

            B9RoomAnchor destination = FindSelectedRoomAnchor();
            bool hasIndoorPath = destination != null
                                 && NavMesh.SamplePosition(
                                     portal.IndoorMapAnchor.position,
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
                        portal.MapWorldToCampusPoint(indoorPreviewPath.corners[i]));
                }
            }
            else if (destination != null)
            {
                AddDistinct(
                    transitionGuidance,
                    portal.MapWorldToCampusPoint(destination.transform.position));
            }

            TransitionRemainingDistanceMeters = CalculateDistance(transitionGuidance);
            if (transitionGuidance.Count >= 2)
                outdoor.RibbonRenderer.SetPath(transitionGuidance);
        }

        private B9RoomAnchor FindSelectedRoomAnchor()
        {
            string roomId = outdoor?.RouteController?.SelectedRoomId;
            if (string.IsNullOrWhiteSpace(roomId) || foundation?.RoomAnchors == null)
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
                        ChangeState(B9NavigationState.VpsFailed, vpsTransition.FailureReason);
                    break;
            }
        }

        private void CompleteIndoorHandover()
        {
            transitionPdr?.StopTracking();
            outdoor?.MinimapController?.ClearPoseOverride();
            if (geometry?.CampusModelProxy != null)
                geometry.CampusModelProxy.gameObject.SetActive(false);
            ActiveSource = B9PoseSource.Vps;
            ChangeState(B9NavigationState.IndoorVps, "MultiSet localization accepted");
        }

        private void RestoreOutdoorGps(string reason)
        {
            transitionPdr?.StopTracking();
            outdoor?.MinimapController?.ClearPoseOverride();
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
            StateChanged?.Invoke(new B9ReliabilityTransition(
                previous,
                next,
                ActiveSource,
                reason,
                Time.unscaledTime));
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
