using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Bridge: mỗi khi <see cref="HybridRouteCoordinator"/> đổi target,
    /// gọi <see cref="ARPathFinder.SetTarget"/> để pipeline ribbon production
    /// vẽ path mesh ngon hơn LineRenderer debug.
    ///
    /// Đặt component này trên cùng GameObject của <see cref="ARPathFinder"/>,
    /// hoặc bất cứ đâu nếu có gán <see cref="arPathFinder"/> tay.
    ///
    /// Hide LineRenderer debug bằng cách disable component <see cref="HybridPathRenderer"/>.
    /// Cả 2 có thể chạy song song để debug — không xung đột.
    ///
    /// Khi phase handover (vào tòa) hoặc target nhảy xa: force <see cref="ARPathFinder.SetTarget"/>
    /// lại để bypass throttle pathUpdateInterval — path ribbon không "bò" chậm.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridArPathFinderBridge : MonoBehaviour
    {
        [SerializeField] private HybridRouteCoordinator coordinator;
        [SerializeField] private ARPathFinder arPathFinder;

        [Tooltip("GameObject runtime giữ position = CurrentTarget — dùng làm targetNode cho ARPathFinder.")]
        [SerializeField] private string targetMarkerName = "HybridRouteTarget";

        [Tooltip("Target nhảy > X mét → force SetTarget lại (bỏ throttle ARPathFinder).")]
        [SerializeField] private float forceRetargetDeltaMeters = 0.75f;

        private Transform _targetMarker;
        private Vector3 _lastPushedTarget = Vector3.positiveInfinity;
        private bool _forceRetarget;

        private void OnEnable()
        {
            if (coordinator == null) coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (arPathFinder == null) arPathFinder = GetComponent<ARPathFinder>();
            if (arPathFinder == null) arPathFinder = FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);

            var go = new GameObject(targetMarkerName);
            _targetMarker = go.transform;
            _lastPushedTarget = Vector3.positiveInfinity;
            _forceRetarget = true;

            if (coordinator != null)
            {
                coordinator.OnPhaseChanged += HandlePhaseChanged;
                coordinator.OnSourceChanged += HandleSourceChanged;
            }
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.OnPhaseChanged -= HandlePhaseChanged;
                coordinator.OnSourceChanged -= HandleSourceChanged;
            }
            if (_targetMarker != null) Destroy(_targetMarker.gameObject);
        }

        private void Update()
        {
            if (coordinator == null || arPathFinder == null || _targetMarker == null) return;
            // Pause phase → hide path bằng cách clear target.
            if (coordinator.CurrentPhase == HybridRouteCoordinator.RoutePhase.None
                || coordinator.CurrentPhase == HybridRouteCoordinator.RoutePhase.Pause)
            {
                if (arPathFinder.TargetNode == _targetMarker) arPathFinder.SetTarget(null);
                _lastPushedTarget = Vector3.positiveInfinity;
                return;
            }

            Vector3 target = coordinator.CurrentTarget;
            _targetMarker.position = target;

            float jump = (_lastPushedTarget - target).magnitude;
            bool needForce = _forceRetarget || jump >= forceRetargetDeltaMeters
                             || arPathFinder.TargetNode != _targetMarker;

            if (needForce)
            {
                // SetTarget luôn force recalc (reset throttle) — kể cả khi cùng Transform.
                arPathFinder.SetTarget(_targetMarker);
                _lastPushedTarget = target;
                _forceRetarget = false;
            }
            else if (arPathFinder.TargetNode != _targetMarker)
            {
                arPathFinder.SetTarget(_targetMarker);
                _lastPushedTarget = target;
            }
        }

        private void HandlePhaseChanged(HybridRouteCoordinator.RoutePhase prev, HybridRouteCoordinator.RoutePhase next)
        {
            _forceRetarget = true;
        }

        private void HandleSourceChanged(HybridRouteCoordinator.RouteSource prev, HybridRouteCoordinator.RouteSource next)
        {
            _forceRetarget = true;
        }
    }
}
