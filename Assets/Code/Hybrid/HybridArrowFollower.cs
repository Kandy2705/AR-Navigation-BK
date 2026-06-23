using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Mũi tên (Transform) follow <see cref="HybridRouteCoordinator.NextWaypoint"/> bằng
    /// SmoothDamp — tránh teleport khi path recompute, đặc biệt khi đổi source
    /// Outdoor↔Indoor (pose có thể nhảy do calibration).
    ///
    /// Đặt component này lên 1 GameObject visual (vd 1 mũi tên 3D);
    /// component sẽ tự move/rotate object đó.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridArrowFollower : MonoBehaviour
    {
        [SerializeField] private HybridRouteCoordinator coordinator;
        [SerializeField] private HybridLocalizationManager manager;

        [Header("Smoothing")]
        [Tooltip("Smooth time vị trí (s). Lớn hơn = chậm và mượt hơn.")]
        [SerializeField] private float positionSmoothTime = 0.4f;

        [Tooltip("Smooth time rotation yaw (s).")]
        [SerializeField] private float rotationSmoothTime = 0.25f;

        [Tooltip("Lift mũi tên lên Y trên mặt path (m).")]
        [SerializeField] private float yLift = 0.4f;

        [Header("Hide rules")]
        [Tooltip("Ẩn khi không có route hợp lệ.")]
        [SerializeField] private bool hideWhenIdle = true;

        private Vector3 _posVel;
        private float _yawVel;
        private float _currentYaw;

        private void OnEnable()
        {
            if (coordinator == null) coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (manager == null) manager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
            _currentYaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            if (coordinator == null || manager == null) return;

            bool active = coordinator.CurrentSource != HybridRouteCoordinator.RouteSource.None
                          && coordinator.CurrentSource != HybridRouteCoordinator.RouteSource.HandoverPause
                          && coordinator.Corners != null
                          && coordinator.Corners.Count >= 2;

            if (hideWhenIdle)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = active;
            }
            if (!active) return;

            Vector3 waypoint = coordinator.NextWaypoint;
            Vector3 target = waypoint + Vector3.up * yLift;
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _posVel, positionSmoothTime);

            Vector3 dir = waypoint - manager.CurrentUserCampusPosition;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                float yawTarget = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
                _currentYaw = Mathf.SmoothDampAngle(_currentYaw, yawTarget, ref _yawVel, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            }
        }
    }
}
