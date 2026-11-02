using System;
using System.Collections.Generic;
using ARNavB9V2.Data;
using ARNavB9V2.Scene;
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
        [SerializeField] private B10EntranceGroup b10Entrances;
        [SerializeField] private B10BuildingAuthoring b10Building;
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
        private Transform activeB10Entrance;
        private double activeB10EntranceLatitude;
        private double activeB10EntranceLongitude;

        public RouteState State { get; private set; } = RouteState.NoDestination;
        public string SelectedRoomId => selectedRoomId;
        public string SelectedDestinationId => selectedDestinationId;
        public string SelectedDestinationName => activeDestinationAnchor != null
            ? activeDestinationAnchor.DisplayName
            : activeB10Entrance != null
                ? IsIndoorB10Destination
                    ? $"Tòa B10 · {selectedRoomId}"
                    : activeB10Entrance.name
            : IsIndoorB9Destination ? "Tòa B9" : selectedDestinationId;
        public bool HasDestination => !string.IsNullOrWhiteSpace(selectedDestinationId);
        public bool IsIndoorB9Destination => string.Equals(
                                                 selectedDestinationId,
                                                 "B9",
                                                 StringComparison.OrdinalIgnoreCase)
                                             && !string.IsNullOrWhiteSpace(selectedRoomId);
        public bool IsB10Destination => string.Equals(
            selectedDestinationId,
            "B10",
            StringComparison.OrdinalIgnoreCase);
        public bool IsIndoorB10Destination => IsB10Destination
                                              && !string.IsNullOrWhiteSpace(selectedRoomId);
        public Transform ActiveB10Entrance => activeB10Entrance;
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

        public void ConfigureB10Entrances(B10EntranceGroup entrances)
        {
            b10Entrances = entrances;
            if (IsB10Destination)
                ResolveActiveDestination();
        }

        private void Awake()
        {
            navMeshPath = new NavMeshPath();
            if (b10Entrances == null)
                b10Entrances = FindFirstObjectByType<B10EntranceGroup>();
            if (b10Building == null)
                b10Building = FindFirstObjectByType<B10BuildingAuthoring>(FindObjectsInactive.Include);
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
            if (string.IsNullOrWhiteSpace(roomId))
                return false;

            string normalizedRoomId = roomId.Trim().ToUpperInvariant();
            bool isB10Room = normalizedRoomId.StartsWith("B10-", StringComparison.Ordinal);
            if (isB10Room)
            {
                if (b10Building == null)
                    b10Building = FindFirstObjectByType<B10BuildingAuthoring>(FindObjectsInactive.Include);
                if (b10Building == null || !b10Building.TryGetRoom(normalizedRoomId, out _))
                    return false;
            }
            else if (building == null || !building.TryGetRoom(normalizedRoomId, out _))
            {
                return false;
            }

            selectedRoomId = normalizedRoomId;
            selectedDestinationId = isB10Room ? "B10" : "B9";
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
            activeB10Entrance = null;
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
                || !schoolGroundNavMesh.isActiveAndEnabled
                || destination == null || ribbonRenderer == null)
            {
                if (IsB10Destination && destination != null && ribbonRenderer != null)
                    return SetDirectB10Guide(successState, destination);
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

            NavMeshQueryFilter schoolGroundFilter = GetSchoolGroundFilter();
            if (!NavMesh.SamplePosition(
                    userPosition,
                    out NavMeshHit startHit,
                    startSampleRadius,
                    schoolGroundFilter)
                || !NavMesh.SamplePosition(
                    destination.position,
                    out NavMeshHit endHit,
                    Mathf.Max(entranceSampleRadiusMeters, DestinationArrivalRadius + 3f),
                    schoolGroundFilter)
                || !NavMesh.CalculatePath(
                    startHit.position,
                    endHit.position,
                    schoolGroundFilter,
                    navMeshPath)
                || navMeshPath.status != NavMeshPathStatus.PathComplete
                || navMeshPath.corners == null
                || navMeshPath.corners.Length < 2)
            {
                if (IsB10Destination)
                    return SetDirectB10Guide(successState, destination);

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
            activeB10Entrance = null;

            if (IsB10Destination && TryChooseB10Entrance(out Transform entrance))
            {
                activeB10Entrance = entrance;
                if (outdoorMap != null)
                {
                    outdoorMap.CampusToGps(
                        entrance.position,
                        out activeB10EntranceLatitude,
                        out activeB10EntranceLongitude);
                }
                return true;
            }

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
            : activeB10Entrance != null ? activeB10Entrance
            : IsIndoorB9Destination ? b9EntranceAnchor : null;

        private double DestinationLatitude => activeDestinationAnchor != null
            ? activeDestinationAnchor.Latitude
            : activeB10Entrance != null ? activeB10EntranceLatitude
            : outdoorMap.EntranceLatitude;

        private double DestinationLongitude => activeDestinationAnchor != null
            ? activeDestinationAnchor.Longitude
            : activeB10Entrance != null ? activeB10EntranceLongitude
            : outdoorMap.EntranceLongitude;

        private float DestinationArrivalRadius => activeDestinationAnchor != null
            ? activeDestinationAnchor.ArrivalRadiusMeters
            : activeB10Entrance != null && b10Entrances != null
                ? b10Entrances.ArrivalRadiusMeters
            : outdoorMap.ArrivalRadiusMeters;

        private bool TryChooseB10Entrance(out Transform entrance)
        {
            entrance = null;
            if (b10Entrances == null
                || !b10Entrances.ValidateConfiguration(out _))
                return false;

            Vector3 start = locationProvider != null
                ? locationProvider.CampusPosition
                : b10Entrances.Entrances[0].position;
            float bestDistance = float.PositiveInfinity;
            var candidatePath = new NavMeshPath();
            NavMeshQueryFilter schoolGroundFilter = GetSchoolGroundFilter();
            bool hasSampledStart = NavMesh.SamplePosition(
                start,
                out NavMeshHit startHit,
                maximumStartSampleRadiusMeters,
                schoolGroundFilter);

            for (int i = 0; i < b10Entrances.Entrances.Count; i++)
            {
                Transform candidate = b10Entrances.Entrances[i];
                float distance = Vector3.Distance(start, candidate.position);
                if (hasSampledStart
                    && NavMesh.SamplePosition(
                        candidate.position,
                        out NavMeshHit endHit,
                        entranceSampleRadiusMeters,
                        schoolGroundFilter)
                    && NavMesh.CalculatePath(
                        startHit.position,
                        endHit.position,
                        schoolGroundFilter,
                        candidatePath)
                    && candidatePath.status == NavMeshPathStatus.PathComplete
                    && candidatePath.corners != null
                    && candidatePath.corners.Length >= 2)
                {
                    distance = CalculateDistance(candidatePath.corners);
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    entrance = candidate;
                }
            }

            return entrance != null;
        }

        private bool SetDirectB10Guide(RouteState successState, Transform destination)
        {
            var directGuide = new List<Vector3>(2)
            {
                locationProvider.CampusPosition,
                destination.position,
            };
            RemainingDistanceMeters = CalculateDistance(directGuide);
            ribbonRenderer.SetPath(directGuide);
            SetState(successState);
            return true;
        }

        private NavMeshQueryFilter GetSchoolGroundFilter()
        {
            return new NavMeshQueryFilter
            {
                agentTypeID = schoolGroundNavMesh != null
                    ? schoolGroundNavMesh.agentTypeID
                    : 0,
                areaMask = NavMesh.AllAreas,
            };
        }

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
