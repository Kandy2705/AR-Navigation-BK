using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Mũi tên (Transform) follow <see cref="HybridRouteCoordinator.NextWaypoint"/> bằng
    /// SmoothDamp cho chuyển động nhỏ — nhưng SNAP ngay khi phase đổi hoặc target nhảy xa
    /// (handover Outdoor→Indoor). SmoothDamp thuần khiến "điểm chỉ đường" bò chậm vài giây.
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
        [Tooltip("Smooth time vị trí (s) khi target chỉ lệch nhẹ. Lớn hơn = chậm và mượt hơn.")]
        [SerializeField] private float positionSmoothTime = 0.18f;

        [Tooltip("Smooth time rotation yaw (s).")]
        [SerializeField] private float rotationSmoothTime = 0.12f;

        [Tooltip("Nếu waypoint nhảy > X mét (hoặc phase đổi): snap ngay, không SmoothDamp.")]
        [SerializeField] private float snapDistanceMeters = 1.25f;

        [Tooltip("Lift mũi tên lên Y trên mặt path (m).")]
        [SerializeField] private float yLift = 0.4f;

        [Header("Hide rules")]
        [Tooltip("Ẩn khi không có route hợp lệ.")]
        [SerializeField] private bool hideWhenIdle = true;

        private Vector3 _posVel;
        private float _yawVel;
        private float _currentYaw;
        private HybridRouteCoordinator.RoutePhase _lastPhase = HybridRouteCoordinator.RoutePhase.None;
        private bool _hasPose;
        private float _harmonyAlpha = 1f;
        private bool _harmonyVisible = true;
        private Color _harmonyTint = Color.white;
        private MaterialPropertyBlock _propertyBlock;

        public void SetHarmonyGuidance(float alpha, bool visible, Color tint)
        {
            _harmonyAlpha = Mathf.Clamp01(alpha);
            _harmonyVisible = visible;
            _harmonyTint = tint;
        }

        private void OnEnable()
        {
            if (coordinator == null) coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (manager == null) manager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
            _currentYaw = transform.eulerAngles.y;
            _hasPose = false;
            _lastPhase = HybridRouteCoordinator.RoutePhase.None;
            if (coordinator != null) coordinator.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (coordinator != null) coordinator.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(HybridRouteCoordinator.RoutePhase prev, HybridRouteCoordinator.RoutePhase next)
        {
            // Handover: reset velocity + force snap frame kế.
            _posVel = Vector3.zero;
            _yawVel = 0f;
            _hasPose = false;
            _lastPhase = next;
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
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    r.enabled = active && _harmonyVisible && _harmonyAlpha > 0.001f;
            }
            if (!active) return;

            Vector3 waypoint = coordinator.NextWaypoint;
            Vector3 target = waypoint + Vector3.up * yLift;

            bool phaseChanged = coordinator.CurrentPhase != _lastPhase;
            _lastPhase = coordinator.CurrentPhase;

            float jump = _hasPose ? Vector3.Distance(transform.position, target) : float.PositiveInfinity;
            bool shouldSnap = !_hasPose || phaseChanged || jump >= snapDistanceMeters;

            if (shouldSnap)
            {
                transform.position = target;
                _posVel = Vector3.zero;
                _hasPose = true;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref _posVel, positionSmoothTime);
            }

            Vector3 dir = waypoint - manager.CurrentUserCampusPosition;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                float yawTarget = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
                if (shouldSnap)
                {
                    _currentYaw = yawTarget;
                    _yawVel = 0f;
                }
                else
                {
                    _currentYaw = Mathf.SmoothDampAngle(_currentYaw, yawTarget, ref _yawVel, rotationSmoothTime);
                }
                transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            }

            ApplyHarmonyStyle();
        }

        private void ApplyHarmonyStyle()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            Color color = _harmonyTint;
            color.a *= _harmonyAlpha;
            foreach (Renderer childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                childRenderer.enabled =
                    _harmonyVisible && _harmonyAlpha > 0.001f;
                childRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_Color", color);
                _propertyBlock.SetColor("_BaseColor", color);
                childRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
