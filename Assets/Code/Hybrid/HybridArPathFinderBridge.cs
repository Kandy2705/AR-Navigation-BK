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
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridArPathFinderBridge : MonoBehaviour
    {
        [SerializeField] private HybridRouteCoordinator coordinator;
        [SerializeField] private ARPathFinder arPathFinder;

        [Tooltip("GameObject runtime giữ position = CurrentTarget — dùng làm targetNode cho ARPathFinder.")]
        [SerializeField] private string targetMarkerName = "HybridRouteTarget";

        private Transform _targetMarker;

        private void OnEnable()
        {
            if (coordinator == null) coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (arPathFinder == null) arPathFinder = GetComponent<ARPathFinder>();
            if (arPathFinder == null) arPathFinder = FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);

            var go = new GameObject(targetMarkerName);
            _targetMarker = go.transform;

            if (coordinator != null)
            {
                coordinator.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (coordinator != null) coordinator.OnPhaseChanged -= HandlePhaseChanged;
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
                return;
            }
            _targetMarker.position = coordinator.CurrentTarget;
            if (arPathFinder.TargetNode != _targetMarker)
            {
                arPathFinder.SetTarget(_targetMarker);
            }
        }

        private void HandlePhaseChanged(HybridRouteCoordinator.RoutePhase prev, HybridRouteCoordinator.RoutePhase next)
        {
            // Hook nếu cần animation lúc đổi phase. Hiện tại Update đã đủ.
        }
    }
}
