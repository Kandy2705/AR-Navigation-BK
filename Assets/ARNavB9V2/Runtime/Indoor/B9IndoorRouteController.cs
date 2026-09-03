using System;
using System.Collections.Generic;
using ARNavB9V2.Data;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Indoor
{
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class B9IndoorRouteController : MonoBehaviour
    {
        public enum IndoorRouteState
        {
            WaitingForLocalization,
            Calculating,
            Navigating,
            Arrived,
            RouteUnavailable,
        }

        [SerializeField] private B9BuildingDefinition building;
        [SerializeField] private B9SceneContext foundation;
        [SerializeField] private Camera arCamera;
        [SerializeField] private B9IndoorPoseTracker poseTracker;
        [SerializeField] private NavMeshSurface indoorNavMesh;
        [SerializeField] private B9RouteRibbonRenderer ribbonRenderer;
        [SerializeField] private string destinationRoomId = "B9-104";
        [SerializeField] private float routeRefreshSeconds = 0.25f;
        [SerializeField] private float routeRefreshDistanceMeters = 0.2f;
        [SerializeField] private float userSampleRadiusMeters = 5f;
        [SerializeField] private float destinationSampleRadiusMeters = 8f;
        [SerializeField] private float arrivalDistanceMeters = 1.8f;

        private NavMeshPath navMeshPath;
        private B9RoomAnchor destinationAnchor;
        private Transform exitAnchor;
        private Vector3 lastRoutedPosition = Vector3.positiveInfinity;
        private float nextRefreshTime;
        private bool navigationActive;
        private bool navigatingToExit;

        public IndoorRouteState State { get; private set; } = IndoorRouteState.WaitingForLocalization;
        public string DestinationRoomId => destinationRoomId;
        public string DestinationFloorId => navigatingToExit
            ? "F1"
            : destinationAnchor != null ? destinationAnchor.FloorId : string.Empty;
        public Vector3 DestinationWorldPosition => ActiveDestination != null
            ? ActiveDestination.position
            : Vector3.zero;
        public Vector3 CurrentUserWorldPosition => arCamera != null
            ? poseTracker != null && poseTracker.IsTracking
                ? poseTracker.CurrentPosition
                : arCamera.transform.position
            : Vector3.zero;
        public float CurrentHeadingDegrees => poseTracker != null && poseTracker.IsTracking
            ? poseTracker.HeadingDegrees
            : arCamera != null ? arCamera.transform.eulerAngles.y : 0f;
        public float RemainingDistanceMeters { get; private set; }
        public bool NavigationActive => navigationActive;
        public bool NavigatingToExit => navigatingToExit;
        public bool LastArrivalWasExit { get; private set; }
        public int RouteRevision { get; private set; }
        public event Action<IndoorRouteState> StateChanged;

        public void Configure(
            B9BuildingDefinition definition,
            B9SceneContext sceneFoundation,
            Camera displayCamera,
            NavMeshSurface navMeshSurface,
            B9RouteRibbonRenderer renderer,
            string defaultRoomId)
        {
            building = definition;
            foundation = sceneFoundation;
            arCamera = displayCamera;
            indoorNavMesh = navMeshSurface;
            ribbonRenderer = renderer;
            destinationRoomId = defaultRoomId;
            ResolveDestinationAnchor();
        }

        public void AttachPoseTracker(B9IndoorPoseTracker tracker)
        {
            poseTracker = tracker;
            lastRoutedPosition = Vector3.positiveInfinity;
            nextRefreshTime = 0f;
        }

        private void Awake()
        {
            navMeshPath = new NavMeshPath();
            ResolveDestinationAnchor();
        }

        private void Update()
        {
            if (!navigationActive)
                return;

            if (Time.unscaledTime < nextRefreshTime
                && Vector3.Distance(lastRoutedPosition, CurrentUserWorldPosition)
                < routeRefreshDistanceMeters)
                return;

            RefreshRoute(force: false);
        }

        public void PrepareForLocalization()
        {
            navigationActive = false;
            RemainingDistanceMeters = 0f;
            lastRoutedPosition = Vector3.positiveInfinity;
            ribbonRenderer?.ClearPath();
            SetState(IndoorRouteState.WaitingForLocalization);
            enabled = false;
        }

        public bool BeginNavigation(string roomId)
        {
            enabled = true;
            navigationActive = false;
            LastArrivalWasExit = false;
            if (!SetDestinationRoom(roomId))
            {
                ribbonRenderer?.ClearPath();
                SetState(IndoorRouteState.RouteUnavailable);
                return false;
            }

            navigationActive = true;
            RefreshRoute(force: true);
            return State != IndoorRouteState.RouteUnavailable;
        }

        public bool BeginExitNavigation(Transform nearestExitAnchor)
        {
            if (nearestExitAnchor == null)
                return false;

            enabled = true;
            exitAnchor = nearestExitAnchor;
            navigatingToExit = true;
            LastArrivalWasExit = false;
            navigationActive = true;
            lastRoutedPosition = Vector3.positiveInfinity;
            nextRefreshTime = 0f;
            RefreshRoute(force: true);
            return State != IndoorRouteState.RouteUnavailable;
        }

        public void StopNavigation()
        {
            navigationActive = false;
            navigatingToExit = false;
            exitAnchor = null;
            RemainingDistanceMeters = 0f;
            lastRoutedPosition = Vector3.positiveInfinity;
            ribbonRenderer?.ClearPath();
            SetState(IndoorRouteState.WaitingForLocalization);
        }

        public bool CompleteFromPdrFallback(string roomId)
        {
            if (!SetDestinationRoom(roomId))
                return false;

            navigationActive = false;
            navigatingToExit = false;
            exitAnchor = null;
            RemainingDistanceMeters = 0f;
            LastArrivalWasExit = false;
            lastRoutedPosition = Vector3.positiveInfinity;
            ribbonRenderer?.ClearPath();
            SetState(IndoorRouteState.Arrived);
            enabled = false;
            return true;
        }

        public bool SetDestinationRoom(string roomId)
        {
            if (building == null || !building.TryGetRoom(roomId, out _))
                return false;

            destinationRoomId = roomId.Trim().ToUpperInvariant();
            navigatingToExit = false;
            exitAnchor = null;
            if (!ResolveDestinationAnchor())
            {
                if (navigationActive)
                    SetState(IndoorRouteState.RouteUnavailable);
                return false;
            }

            lastRoutedPosition = Vector3.positiveInfinity;
            nextRefreshTime = 0f;
            if (navigationActive)
                RefreshRoute(force: true);
            return true;
        }

        public void RefreshNow()
        {
            if (navigationActive)
                RefreshRoute(force: true);
        }

        private void RefreshRoute(bool force)
        {
            Transform destination = ActiveDestination;
            if (!navigationActive || arCamera == null || destination == null
                || indoorNavMesh == null || indoorNavMesh.navMeshData == null
                || ribbonRenderer == null)
            {
                ribbonRenderer?.ClearPath();
                SetState(IndoorRouteState.RouteUnavailable);
                return;
            }

            Vector3 userPosition = CurrentUserWorldPosition;
            if (!force
                && Time.unscaledTime < nextRefreshTime
                && Vector3.Distance(lastRoutedPosition, userPosition) < routeRefreshDistanceMeters)
                return;

            nextRefreshTime = Time.unscaledTime + routeRefreshSeconds;
            lastRoutedPosition = userPosition;
            SetState(IndoorRouteState.Calculating);
            navMeshPath ??= new NavMeshPath();

            if (!NavMesh.SamplePosition(
                    userPosition,
                    out NavMeshHit startHit,
                    userSampleRadiusMeters,
                    NavMesh.AllAreas)
                || !NavMesh.SamplePosition(
                    destination.position,
                    out NavMeshHit destinationHit,
                    destinationSampleRadiusMeters,
                    NavMesh.AllAreas)
                || !NavMesh.CalculatePath(
                    startHit.position,
                    destinationHit.position,
                    NavMesh.AllAreas,
                    navMeshPath)
                || navMeshPath.status != NavMeshPathStatus.PathComplete
                || navMeshPath.corners == null
                || navMeshPath.corners.Length < 2)
            {
                RemainingDistanceMeters = 0f;
                ribbonRenderer.ClearPath();
                SetState(IndoorRouteState.RouteUnavailable);
                return;
            }

            var points = new List<Vector3>(navMeshPath.corners.Length);
            points.AddRange(navMeshPath.corners);
            RouteRevision++;
            RemainingDistanceMeters = CalculateDistance(points);
            if (RemainingDistanceMeters <= arrivalDistanceMeters)
            {
                ribbonRenderer.ClearPath();
                LastArrivalWasExit = navigatingToExit;
                SetState(IndoorRouteState.Arrived);
                return;
            }

            ribbonRenderer.SetPath(points);
            SetState(IndoorRouteState.Navigating);
        }

        private bool ResolveDestinationAnchor()
        {
            destinationAnchor = null;
            if (foundation == null || foundation.RoomAnchors == null)
                return false;

            for (int i = 0; i < foundation.RoomAnchors.Count; i++)
            {
                B9RoomAnchor candidate = foundation.RoomAnchors[i];
                if (candidate != null && string.Equals(
                        candidate.RoomId,
                        destinationRoomId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    destinationAnchor = candidate;
                    return true;
                }
            }

            return false;
        }

        private Transform ActiveDestination => navigatingToExit
            ? exitAnchor
            : destinationAnchor != null ? destinationAnchor.transform : null;

        private static float CalculateDistance(IReadOnlyList<Vector3> points)
        {
            float distance = 0f;
            for (int i = 1; i < points.Count; i++)
                distance += Vector3.Distance(points[i - 1], points[i]);
            return distance;
        }

        private void SetState(IndoorRouteState value)
        {
            if (State == value)
                return;
            State = value;
            StateChanged?.Invoke(value);
        }
    }
}
