using System.Collections.Generic;
using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Scene anchor đặt ngay tại cửa ra/vào của 1 tòa nhà.
    /// Outdoor route sẽ dẫn user tới đây, sau đó hybrid state machine bật MultiSet VPS cho đúng map/mapset.
    ///
    /// Cách dùng:
    ///   1. Tạo empty GameObject ở vị trí thực tế của cửa (theo campus world).
    ///   2. Gắn component này, set <see cref="buildingId"/>, <see cref="floorId"/>, <see cref="entranceType"/>.
    ///   3. Nếu cửa này thuộc 1 map cụ thể (B10 nhiều tầng), gán <see cref="linkedMapOrMapsetCode"/>.
    ///   4. Kéo Transform của waypoint indoor đầu tiên vào <see cref="linkedIndoorStartTransform"/>.
    ///   5. (Tuỳ chọn) cho phép auto-tạo trigger SphereCollider qua <see cref="autoCreateTrigger"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class EntranceAnchor : MonoBehaviour
    {
        public enum EntranceType
        {
            Entrance,
            Exit,
            Both,
        }

        private static readonly List<EntranceAnchor> _registry = new List<EntranceAnchor>();

        [Header("Identity")]
        [Tooltip("Tòa nhà mà cửa này dẫn vào.")]
        [SerializeField] private BuildingId buildingId = BuildingId.None;

        [Tooltip("Tầng tại cửa (vd 'F1', 'G', '1'). Dùng cho phòng nhiều tầng — leave free-form.")]
        [SerializeField] private string floorId = "F1";

        [Tooltip("Cửa này dùng để vào, ra, hay cả hai chiều.")]
        [SerializeField] private EntranceType entranceType = EntranceType.Both;

        [Header("Campus world position")]
        [Tooltip("Nếu true: lấy transform.position của GameObject làm campus world position (khuyến nghị).")]
        [SerializeField] private bool useTransformAsCampusPosition = true;

        [Tooltip("Override campus position khi không dùng transform. Bỏ qua nếu useTransformAsCampusPosition = true.")]
        [SerializeField] private Vector3 explicitCampusPosition;

        [Tooltip("Yaw (deg) của hướng nhìn 'vào nhà' tại cửa, trong campus world. Dùng cho VPS calibration.")]
        [SerializeField] private float facingYawDegrees;

        [Header("MultiSet binding")]
        [Tooltip("Mã MAP_/MSET_ MultiSet sẽ load khi user tới cửa này. Để trống = dùng mã chính trong BuildingLocalizationProfile.")]
        [SerializeField] private string linkedMapOrMapsetCode;

        [Header("Indoor handover")]
        [Tooltip("Transform indoor đầu tiên (waypoint/POI) ngay sau cửa — dùng cho route Phase 2.")]
        [SerializeField] private Transform linkedIndoorStartTransform;

        [Header("Trigger")]
        [Tooltip("Bán kính trigger (m). Khi user vào trong, state machine sẽ chuyển ApproachingEntrance → TransitionScanning.")]
        [SerializeField] private float triggerRadiusMeters = 5f;

        [Tooltip("Nếu chưa có SphereCollider, auto-tạo 1 trigger với bán kính trên ở Awake.")]
        [SerializeField] private bool autoCreateTrigger = true;

        // ---------------------------------------------------------------------
        // Public read-only API
        // ---------------------------------------------------------------------

        public BuildingId BuildingId => buildingId;
        public string FloorId => floorId;
        public EntranceType Type => entranceType;
        public string LinkedMapOrMapsetCode => linkedMapOrMapsetCode;
        public Transform LinkedIndoorStartTransform => linkedIndoorStartTransform;
        public float TriggerRadiusMeters => triggerRadiusMeters;
        public float FacingYawDegrees => facingYawDegrees;

        public Vector3 CampusWorldPosition =>
            useTransformAsCampusPosition ? transform.position : explicitCampusPosition;

        public bool CanEnter =>
            entranceType == EntranceType.Entrance || entranceType == EntranceType.Both;

        public bool CanExit =>
            entranceType == EntranceType.Exit || entranceType == EntranceType.Both;

        public static IReadOnlyList<EntranceAnchor> All => _registry;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (autoCreateTrigger)
            {
                EnsureTriggerCollider();
            }
        }

        private void OnEnable()
        {
            if (!_registry.Contains(this))
            {
                _registry.Add(this);
            }
        }

        private void OnDisable()
        {
            _registry.Remove(this);
        }

        private void EnsureTriggerCollider()
        {
            var col = GetComponent<SphereCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<SphereCollider>();
            }
            col.isTrigger = true;
            if (col.radius <= 0f || Mathf.Abs(col.radius - triggerRadiusMeters) > 0.01f)
            {
                col.radius = triggerRadiusMeters;
            }
        }

        // ---------------------------------------------------------------------
        // Queries
        // ---------------------------------------------------------------------

        /// <summary>Tìm entrance gần nhất theo XZ tới <paramref name="campusPos"/>, lọc theo building (None = bất kỳ).</summary>
        public static EntranceAnchor FindNearest(Vector3 campusPos, BuildingId filterBuilding, bool requireEntrance)
        {
            EntranceAnchor best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _registry.Count; i++)
            {
                var a = _registry[i];
                if (a == null) continue;
                if (filterBuilding != BuildingId.None && a.buildingId != filterBuilding) continue;
                if (requireEntrance && !a.CanEnter) continue;

                Vector3 d = a.CampusWorldPosition - campusPos;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = a;
                }
            }
            return best;
        }

        public static EntranceAnchor FindForBuilding(BuildingId id, bool requireEntrance)
        {
            for (int i = 0; i < _registry.Count; i++)
            {
                var a = _registry[i];
                if (a == null || a.buildingId != id) continue;
                if (requireEntrance && !a.CanEnter) continue;
                return a;
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = entranceType == EntranceType.Exit ? Color.red : Color.cyan;
            Vector3 p = useTransformAsCampusPosition ? transform.position : explicitCampusPosition;
            Gizmos.DrawWireSphere(p, triggerRadiusMeters);
            Vector3 dir = Quaternion.Euler(0f, facingYawDegrees, 0f) * Vector3.forward;
            Gizmos.DrawLine(p, p + dir * Mathf.Max(1f, triggerRadiusMeters * 0.6f));
        }
    }
}
