using System;
using System.Collections.Generic;
using ARNavB9V2.Data;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Outdoor
{
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class B9OutdoorRouteController : MonoBehaviour
    {
        public enum RouteState
        {
            NoDestination,
            WaitingForGps,
            Calculating,
            NavigatingToB9Entrance,
            ArrivedAtB9Entrance,
            RouteUnavailable,
        }

        [SerializeField] private B9BuildingDefinition building;
        [SerializeField] private B9OutdoorMapDefinition outdoorMap;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private Transform b9EntranceAnchor;
        [SerializeField] private B9CampusDestinationCatalog campusDestinations;
        [SerializeField] private List<B9OutdoorDestinationAnchor> destinationAnchors =
            new List<B9OutdoorDestinationAnchor>();
        [SerializeField] private NavMeshSurface schoolGroundNavMesh;
        [SerializeField] private B9RouteRibbonRenderer ribbonRenderer;
        [SerializeField] private string selectedRoomId = "B9-104";
        [SerializeField] private string selectedDestinationId = "B9";
        [SerializeField] private float routeRefreshSeconds = 0.6f;
        [SerializeField] private float routeRefreshDistanceMeters = 1.25f;
        [SerializeField] private float minimumStartSampleRadiusMeters = 4f;
        [SerializeField] private float maximumStartSampleRadiusMeters = 18f;
        [SerializeField] private float entranceSampleRadiusMeters = 8f;

        private NavMeshPath navMeshPath;
        private Vector3 lastRoutedPosition = Vector3.positiveInfinity;
        private float nextRefreshTime;
        private B9OutdoorDestinationAnchor activeDestinationAnchor;

        public RouteState State { get; private set; } = RouteState.NoDestination;
        public string SelectedRoomId => selectedRoomId;
        public string SelectedDestinationId => selectedDestinationId;
        public string SelectedDestinationName => activeDestinationAnchor != null
            ? activeDestinationAnchor.DisplayName
            : IsIndoorB9Destination ? "Tòa B9" : selectedDestinationId;
        public bool HasDestination => !string.IsNullOrWhiteSpace(selectedDestinationId);
        public bool IsIndoorB9Destination => string.Equals(
                                                 selectedDestinationId,
                                                 "B9",
                                                 StringComparison.OrdinalIgnoreCase)
                                             && !string.IsNullOrWhiteSpace(selectedRoomId);
        public Vector3 ActiveDestinationPosition => DestinationTransform != null
            ? DestinationTransform.position
            : Vector3.zero;
        public float RemainingDistanceMeters { get; private set; }
        public bool HasArrivedAtDestination => State == RouteState.ArrivedAtB9Entrance;
        public bool HasArrivedAtEntrance => IsIndoorB9Destination && HasArrivedAtDestination;
        public event Action<RouteState> StateChanged;

        public void ConfigureRefreshSmoothing(float minimumSeconds, float minimumDistanceMeters)
        {
            routeRefreshSeconds = Mathf.Max(0.05f, minimumSeconds);
            routeRefreshDistanceMeters = Mathf.Max(0.05f, minimumDistanceMeters);
        }

        public void Configure(
            B9BuildingDefinition buildingDefinition,
            B9OutdoorMapDefinition mapDefinition,
            B9OutdoorLocationProvider provider,
            Transform entrance,
            NavMeshSurface navMeshSurface,
            B9RouteRibbonRenderer renderer,
            string defaultRoomId)
        {
            building = buildingDefinition;
            outdoorMap = mapDefinition;
            locationProvider = provider;
            b9EntranceAnchor = entrance;
            schoolGroundNavMesh = navMeshSurface;
            ribbonRenderer = renderer;
            selectedRoomId = defaultRoomId;
            selectedDestinationId = "B9";
        }

        public void ConfigureCampusDestinations(
            B9CampusDestinationCatalog catalog,
            IReadOnlyList<B9OutdoorDestinationAnchor> anchors)
        {
            campusDestinations = catalog;
            destinationAnchors = anchors != null
                ? new List<B9OutdoorDestinationAnchor>(anchors)
                : new List<B9OutdoorDestinationAnchor>();
            ResolveActiveDestination();
        }

        private void Awake()
        {
            navMeshPath = new NavMeshPath();
        }

        private void Start()
        {
            if (!string.IsNullOrWhiteSpace(selectedRoomId))
                SetDestinationRoom(selectedRoomId);
        }

        private void Update()
        {
            RefreshNow();
        }

        /// <summary>Immediately evaluates arrival or recalculates the outdoor route.</summary>
        public void RefreshNow()
        {
            if (!HasDestination || DestinationTransform == null)
                return;

            if (locationProvider == null || !locationProvider.HasReliableFix)
            {
                SetState(RouteState.WaitingForGps);
                return;
            }

            double entranceDistance = B9OutdoorMapDefinition.DistanceMeters(
                locationProvider.Latitude,
                locationProvider.Longitude,
                DestinationLatitude,
                DestinationLongitude);
            if (entranceDistance <= DestinationArrivalRadius)
            {
                RemainingDistanceMeters = (float)entranceDistance;
                bool routeNeedsRefresh = ribbonRenderer != null
                                         && (!ribbonRenderer.HasVisiblePath
                                             || (Time.unscaledTime >= nextRefreshTime
                                                 && Vector3.Distance(
                                                     lastRoutedPosition,
                                                     locationProvider.CampusPosition) >= routeRefreshDistanceMeters));
                if (routeNeedsRefresh)
                {
                    nextRefreshTime = Time.unscaledTime + routeRefreshSeconds;
                    CalculateRoute(
                        RouteState.ArrivedAtB9Entrance,
                        preserveGuidanceOnFailure: true);
                }
                else
                {
                    SetState(RouteState.ArrivedAtB9Entrance);
                }
                return;
            }

            if (ribbonRenderer != null
                && ribbonRenderer.HasVisiblePath
                && (Time.unscaledTime < nextRefreshTime
                    || Vector3.Distance(lastRoutedPosition, locationProvider.CampusPosition)
                    < routeRefreshDistanceMeters))
                return;

            nextRefreshTime = Time.unscaledTime + routeRefreshSeconds;
            CalculateRoute(
                RouteState.NavigatingToB9Entrance,
                preserveGuidanceOnFailure: false);
        }

        public bool SetDestinationRoom(string roomId)
        {
            if (building == null || !building.TryGetRoom(roomId, out _))
                return false;

            selectedRoomId = roomId.Trim().ToUpperInvariant();
            selectedDestinationId = "B9";
            ResolveActiveDestination();
            BeginSelectedDestination();
            return true;
        }

        public bool SetOutdoorDestination(string destinationId)
        {
            if (campusDestinations == null
                || !campusDestinations.TryGet(destinationId, out _))
                return false;

            selectedDestinationId = destinationId.Trim().ToUpperInvariant();
            selectedRoomId = string.Empty;
            if (!ResolveActiveDestination())
                return false;

            BeginSelectedDestination();
            return true;
        }

        private void BeginSelectedDestination()
        {
            lastRoutedPosition = Vector3.positiveInfinity;
            nextRefreshTime = 0f;
            SetState(locationProvider != null && locationProvider.HasReliableFix
                ? RouteState.Calculating
                : RouteState.WaitingForGps);
            if (enabled)
                RefreshNow();
        }

        public void CancelNavigation()
        {
            selectedRoomId = string.Empty;
            selectedDestinationId = string.Empty;
            activeDestinationAnchor = null;
            RemainingDistanceMeters = 0f;
            lastRoutedPosition = Vector3.positiveInfinity;
            nextRefreshTime = 0f;
            ribbonRenderer?.ClearPath();
            SetState(RouteState.NoDestination);
        }

        private bool CalculateRoute(
            RouteState successState,
            bool preserveGuidanceOnFailure)
        {
            navMeshPath ??= new NavMeshPath();
            Transform destination = DestinationTransform;
            if (schoolGroundNavMesh == null || schoolGroundNavMesh.navMeshData == null
                || destination == null || ribbonRenderer == null)
            {
                if (!preserveGuidanceOnFailure)
                {
                    ribbonRenderer?.ClearPath();
                    SetState(RouteState.RouteUnavailable);
                }
                else
                {
                    SetState(successState);
                }
                return false;
            }

            if (successState != RouteState.ArrivedAtB9Entrance)
                SetState(RouteState.Calculating);
            Vector3 userPosition = locationProvider.CampusPosition;
            lastRoutedPosition = userPosition;
            float gpsAccuracy = locationProvider.HorizontalAccuracyMeters;
            float startSampleRadius = float.IsFinite(gpsAccuracy)
                ? Mathf.Clamp(
                    gpsAccuracy + 2f,
                    minimumStartSampleRadiusMeters,
                    maximumStartSampleRadiusMeters)
                : maximumStartSampleRadiusMeters;

            if (!NavMesh.SamplePosition(
                    userPosition,
                    out NavMeshHit startHit,
                    startSampleRadius,
                    NavMesh.AllAreas)
                || !NavMesh.SamplePosition(
                    destination.position,
                    out NavMeshHit endHit,
                    Mathf.Max(entranceSampleRadiusMeters, DestinationArrivalRadius + 3f),
                    NavMesh.AllAreas)
                || !NavMesh.CalculatePath(
                    startHit.position,
                    endHit.position,
                    NavMesh.AllAreas,
                    navMeshPath)
                || navMeshPath.status != NavMeshPathStatus.PathComplete
                || navMeshPath.corners == null
                || navMeshPath.corners.Length < 2)
            {
                if (preserveGuidanceOnFailure
                    && Vector3.Distance(userPosition, destination.position) > 0.25f)
                {
                    // GPS can place the user just outside the baked NavMesh near the
                    // entrance. Keep a short final guide to the VPS scan point instead
                    // of making the route disappear while localization is pending.
                    var finalGuide = new List<Vector3>(2)
                    {
                        userPosition,
                        destination.position,
                    };
                    RemainingDistanceMeters = CalculateDistance(finalGuide);
                    ribbonRenderer.SetPath(finalGuide);
                    SetState(successState);
                    return true;
                }

                if (!preserveGuidanceOnFailure)
                {
                    ribbonRenderer.ClearPath();
                    RemainingDistanceMeters = 0f;
                    SetState(RouteState.RouteUnavailable);
                }
                else
                {
                    SetState(successState);
                }
                return false;
            }

            var points = new List<Vector3>(navMeshPath.corners.Length + 2);
            if (Vector3.Distance(userPosition, startHit.position) > 0.25f)
                points.Add(userPosition);
            points.AddRange(navMeshPath.corners);
            if (Vector3.Distance(points[points.Count - 1], destination.position) > 0.25f)
                points.Add(destination.position);

            RemainingDistanceMeters = CalculateDistance(points);
            ribbonRenderer.SetPath(points);
            SetState(successState);
            return true;
        }

        private bool ResolveActiveDestination()
        {
            activeDestinationAnchor = null;
            if (destinationAnchors != null)
            {
                for (int i = 0; i < destinationAnchors.Count; i++)
                {
                    B9OutdoorDestinationAnchor candidate = destinationAnchors[i];
                    if (candidate != null && string.Equals(
                            candidate.DestinationId,
                            selectedDestinationId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        activeDestinationAnchor = candidate;
                        return true;
                    }
                }
            }

            return IsIndoorB9Destination && b9EntranceAnchor != null;
        }

        private Transform DestinationTransform => activeDestinationAnchor != null
            ? activeDestinationAnchor.transform
            : IsIndoorB9Destination ? b9EntranceAnchor : null;

        private double DestinationLatitude => activeDestinationAnchor != null
            ? activeDestinationAnchor.Latitude
            : outdoorMap.EntranceLatitude;

        private double DestinationLongitude => activeDestinationAnchor != null
            ? activeDestinationAnchor.Longitude
            : outdoorMap.EntranceLongitude;

        private float DestinationArrivalRadius => activeDestinationAnchor != null
            ? activeDestinationAnchor.ArrivalRadiusMeters
            : outdoorMap.ArrivalRadiusMeters;

        private static float CalculateDistance(IReadOnlyList<Vector3> points)
        {
            float result = 0f;
            for (int i = 0; i < points.Count - 1; i++)
                result += Vector3.Distance(points[i], points[i + 1]);
            return result;
        }

        private void SetState(RouteState value)
        {
            if (State == value)
                return;
            State = value;
            StateChanged?.Invoke(value);
        }
    }
}
