using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Coordinator route theo phase, dựa trên <see cref="HybridNavigationState"/> +
    /// <see cref="HybridDestination"/>:
    ///
    ///   - dest outdoor:
    ///         user → POI (single phase, source = Outdoor NavMesh).
    ///   - dest indoor, state ∈ {Outdoor, ApproachingEntrance}:
    ///         Phase 1 = user → entrance.linkedIndoorStartTransform (hoặc anchor pos).
    ///   - dest indoor, state == TransitionScanning:
    ///         pause path (no waypoints).
    ///   - dest indoor, state ∈ {Indoor, Relocalization}:
    ///         Phase 2 = user (campus pose từ VPS) → POI.
    ///   - dest outdoor, user đang indoor (Indoor/Relocalization):
    ///         Phase 1 = user → nearest exit anchor (source = Indoor).
    ///         Sau khi outdoor: Phase 2 = user → POI.
    ///
    /// Path lấy bằng <see cref="NavMesh.CalculatePath"/> trên NavMesh chung
    /// (NavMesh chung serve cả indoor + outdoor surfaces).
    /// Fallback nếu sample fail: 2-point straight line.
    ///
    /// Coordinator KHÔNG render. Render là việc của <see cref="HybridPathRenderer"/> /
    /// arrow / hoặc adapter cho ARPathFinder/SDK NavigationController.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public class HybridRouteCoordinator : MonoBehaviour
    {
        public enum RouteSource
        {
            None,
            Outdoor,
            Indoor,
            HandoverPause, // TransitionScanning — không vẽ path.
        }

        public enum RoutePhase
        {
            None,
            DirectOutdoor,         // user → POI outdoor
            OutdoorToEntrance,     // user → entrance (Phase 1 chuẩn bị vào nhà)
            IndoorToPoi,           // user → POI indoor
            IndoorToExit,          // user → exit anchor (đang muốn ra ngoài)
            Pause,                 // chờ localization
        }

        [Header("References (auto-resolve nếu null)")]
        [SerializeField] private HybridLocalizationManager manager;

        [Header("Destination")]
        [SerializeField] private HybridDestination destination = new HybridDestination();

        [Header("Path computation")]
        [Tooltip("Bán kính NavMesh.SamplePosition (m).")]
        [SerializeField] private float navMeshSampleRadius = 3f;

        [Tooltip("Mỗi N giây mới recompute path.")]
        [SerializeField] private float recomputeIntervalSeconds = 0.4f;

        [Tooltip("Recompute ngay khi user di chuyển quá Y mét so với origin lần tính trước.")]
        [SerializeField] private float recomputeMinUserDeltaMeters = 0.3f;

        [Tooltip("Cho phép fallback straight-line khi không sample được NavMesh.")]
        [SerializeField] private bool allowStraightLineFallback = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = false;

        // ---------------------------------------------------------------------
        // Public read-only
        // ---------------------------------------------------------------------

        public RouteSource CurrentSource { get; private set; } = RouteSource.None;
        public RoutePhase CurrentPhase { get; private set; } = RoutePhase.None;
        public Vector3 CurrentOrigin { get; private set; }
        public Vector3 CurrentTarget { get; private set; }
        public IReadOnlyList<Vector3> Corners => _corners;
        public bool IsStraightLineFallback { get; private set; }
        public string LastNote { get; private set; } = "";

        public HybridDestination Destination => destination;

        /// <summary>Vị trí waypoint kế tiếp (sau corner 0 hoặc target nếu không có).</summary>
        public Vector3 NextWaypoint
        {
            get
            {
                if (_corners.Count >= 2) return _corners[1];
                if (_corners.Count == 1) return _corners[0];
                return CurrentTarget;
            }
        }

        public event Action<RoutePhase, RoutePhase> OnPhaseChanged;
        public event Action<RouteSource, RouteSource> OnSourceChanged;

        // ---------------------------------------------------------------------
        // State
        // ---------------------------------------------------------------------

        private readonly List<Vector3> _corners = new List<Vector3>();
        private NavMeshPath _path;
        private float _nextRecomputeTime;
        private Vector3 _lastComputeOrigin;

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public void SetDestination(HybridDestination dest)
        {
            destination = dest;
            _nextRecomputeTime = 0f;
        }

        public void ClearDestination()
        {
            destination = new HybridDestination();
            ChangePhase(RoutePhase.None, RouteSource.None, "destination cleared");
            _corners.Clear();
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void Awake()
        {
            _path = new NavMeshPath();
        }

        private void OnEnable()
        {
            if (manager == null) manager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (manager == null || destination == null || !destination.IsValid)
            {
                if (CurrentSource != RouteSource.None) ChangePhase(RoutePhase.None, RouteSource.None, "no destination/manager");
                return;
            }

            var (nextPhase, nextSource, target, originOverride) = ResolvePhase();
            if (nextPhase != CurrentPhase || nextSource != CurrentSource)
            {
                ChangePhase(nextPhase, nextSource, $"state={manager.CurrentState}, dest indoor={destination.isIndoor}");
            }

            if (nextPhase == RoutePhase.Pause || nextPhase == RoutePhase.None)
            {
                return;
            }

            Vector3 origin = originOverride ?? manager.CurrentUserCampusPosition;
            CurrentOrigin = origin;
            CurrentTarget = target;

            bool dueRecompute = Time.time >= _nextRecomputeTime
                                || (origin - _lastComputeOrigin).sqrMagnitude
                                    > recomputeMinUserDeltaMeters * recomputeMinUserDeltaMeters;
            if (dueRecompute)
            {
                _nextRecomputeTime = Time.time + recomputeIntervalSeconds;
                _lastComputeOrigin = origin;
                ComputePath(origin, target);
            }
        }

        // ---------------------------------------------------------------------
        // Phase resolution
        // ---------------------------------------------------------------------

        private (RoutePhase phase, RouteSource source, Vector3 target, Vector3? originOverride) ResolvePhase()
        {
            var state = manager.CurrentState;

            // Dest outdoor.
            if (!destination.isIndoor)
            {
                // Nếu user đang ở indoor states → Phase 1 = exit.
                if (state == HybridNavigationState.Indoor || state == HybridNavigationState.Relocalization)
                {
                    var exit = EntranceAnchor.FindForBuilding(manager.ActiveBuilding, requireEntrance: false);
                    if (exit != null && exit.CanExit)
                    {
                        return (RoutePhase.IndoorToExit, RouteSource.Indoor, exit.CampusWorldPosition, null);
                    }
                }
                return (RoutePhase.DirectOutdoor, RouteSource.Outdoor, destination.CampusPosition, null);
            }

            // Dest indoor.
            switch (state)
            {
                case HybridNavigationState.Outdoor:
                case HybridNavigationState.ApproachingEntrance:
                {
                    var entry = EntranceAnchor.FindForBuilding(destination.building, requireEntrance: true);
                    if (entry == null)
                    {
                        return (RoutePhase.Pause, RouteSource.None, destination.CampusPosition, null);
                    }
                    Vector3 target = entry.LinkedIndoorStartTransform != null
                        ? entry.LinkedIndoorStartTransform.position
                        : entry.CampusWorldPosition;
                    return (RoutePhase.OutdoorToEntrance, RouteSource.Outdoor, target, null);
                }
                case HybridNavigationState.TransitionScanning:
                    return (RoutePhase.Pause, RouteSource.HandoverPause, destination.CampusPosition, null);
                case HybridNavigationState.Indoor:
                case HybridNavigationState.Relocalization:
                    return (RoutePhase.IndoorToPoi, RouteSource.Indoor, destination.CampusPosition, null);
                case HybridNavigationState.ExitingIndoor:
                {
                    var exit = EntranceAnchor.FindForBuilding(manager.ActiveBuilding, requireEntrance: false);
                    Vector3 t = exit != null ? exit.CampusWorldPosition : destination.CampusPosition;
                    return (RoutePhase.IndoorToExit, RouteSource.Indoor, t, null);
                }
                default:
                    return (RoutePhase.Pause, RouteSource.None, destination.CampusPosition, null);
            }
        }

        private void ChangePhase(RoutePhase phase, RouteSource source, string note)
        {
            var prevPhase = CurrentPhase;
            var prevSource = CurrentSource;
            CurrentPhase = phase;
            CurrentSource = source;
            LastNote = note;
            if (verboseLog) Debug.Log($"[HybridRouteCoordinator] phase {prevPhase}→{phase}  source {prevSource}→{source}  ({note})");
            if (phase != prevPhase) OnPhaseChanged?.Invoke(prevPhase, phase);
            if (source != prevSource) OnSourceChanged?.Invoke(prevSource, source);
        }

        // ---------------------------------------------------------------------
        // Path computation
        // ---------------------------------------------------------------------

        private void ComputePath(Vector3 origin, Vector3 target)
        {
            _corners.Clear();
            IsStraightLineFallback = false;

            bool originOk = NavMesh.SamplePosition(origin, out var oHit, navMeshSampleRadius, NavMesh.AllAreas);
            bool targetOk = NavMesh.SamplePosition(target, out var tHit, navMeshSampleRadius, NavMesh.AllAreas);

            if (originOk && targetOk
                && NavMesh.CalculatePath(oHit.position, tHit.position, NavMesh.AllAreas, _path)
                && _path.status == NavMeshPathStatus.PathComplete)
            {
                _corners.AddRange(_path.corners);
                return;
            }

            if (allowStraightLineFallback)
            {
                IsStraightLineFallback = true;
                _corners.Add(origin);
                _corners.Add(target);
            }
        }
    }
}
