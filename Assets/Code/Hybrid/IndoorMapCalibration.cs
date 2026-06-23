using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Calibration giữa hệ tọa độ MultiSet map-local và Unity campus world.
    ///
    /// Mục đích: VPS trả pose trong hệ tọa độ của map (gốc tại map origin, North/East tuỳ map),
    /// trong khi outdoor và NavMesh outdoor đều dùng campus world. Để indoor pose dùng được
    /// chung 1 navigation pipeline, ta cần ánh xạ:
    ///   (mapLocal pos, rot) → (campus pos, rot)
    ///
    /// Mô hình: 1 cặp điểm tham chiếu (entrance) + yaw offset + scale.
    ///   - <see cref="mapLocalEntrancePoint"/>: vị trí cửa trong hệ map (đo trong scene MultiSet hoặc tool quét).
    ///   - <see cref="campusEntrancePoint"/>: vị trí cửa thực tế trong campus world (Unity scene).
    ///   - <see cref="yawOffsetDegrees"/>: xoay quanh Y để hướng "vào nhà" của map khớp campus.
    ///   - <see cref="scale"/>: default 1; chỉ đổi nếu map có scale khác.
    ///
    /// Một component có thể serve nhiều cửa nếu cùng building share frame; nếu mỗi cửa lệch nhau
    /// thì gắn nhiều IndoorMapCalibration với <see cref="targetBuilding"/> + <see cref="floorId"/>
    /// khác nhau và resolve qua <see cref="FindFor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class IndoorMapCalibration : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<IndoorMapCalibration> _registry =
            new System.Collections.Generic.List<IndoorMapCalibration>();

        [Header("Target")]
        [Tooltip("Building mà calibration này phục vụ.")]
        [SerializeField] private BuildingId targetBuilding = BuildingId.None;

        [Tooltip("Floor id (free-form, khớp EntranceAnchor.floorId). Để trống = áp cho mọi tầng.")]
        [SerializeField] private string floorId = "";

        [Header("Reference points")]
        [Tooltip("Vị trí cửa trong hệ map MultiSet (đo offline). XYZ trong map local.")]
        [SerializeField] private Vector3 mapLocalEntrancePoint = Vector3.zero;

        [Tooltip("Vị trí cửa trong campus world (Unity scene). Thường = transform.position của EntranceAnchor.")]
        [SerializeField] private Vector3 campusEntrancePoint = Vector3.zero;

        [Tooltip("Nếu gán, runtime sẽ override campusEntrancePoint = anchor.CampusWorldPosition.")]
        [SerializeField] private EntranceAnchor entranceAnchorReference;

        [Header("Transform")]
        [Tooltip("Xoay quanh Y (deg) để hướng forward map-local khớp campus world.")]
        [SerializeField] private float yawOffsetDegrees;

        [Tooltip("Scale map→campus. Mặc định 1.")]
        [SerializeField] private float scale = 1f;

        // ---------------------------------------------------------------------
        // Public read-only
        // ---------------------------------------------------------------------

        public BuildingId TargetBuilding => targetBuilding;
        public string FloorId => floorId;
        public float YawOffsetDegrees => yawOffsetDegrees;
        public float Scale => scale;
        public Vector3 MapLocalEntrancePoint => mapLocalEntrancePoint;

        public Vector3 CampusEntrancePoint =>
            entranceAnchorReference != null ? entranceAnchorReference.CampusWorldPosition : campusEntrancePoint;

        public static System.Collections.Generic.IReadOnlyList<IndoorMapCalibration> All => _registry;

        // ---------------------------------------------------------------------
        // Conversion
        // ---------------------------------------------------------------------

        /// <summary>
        /// Convert pose từ map-local sang campus world.
        ///
        /// Công thức:
        ///   delta = (localPos - mapLocalEntrancePoint) * scale
        ///   rotated = Quaternion.Euler(0, yawOffset, 0) * delta
        ///   campusPos = campusEntrancePoint + rotated
        ///   campusRot = Quaternion.Euler(0, yawOffset, 0) * localRot
        /// </summary>
        public void MapLocalToCampusWorld(Vector3 localPos, Quaternion localRot,
            out Vector3 campusPos, out Quaternion campusRot)
        {
            Quaternion yaw = Quaternion.Euler(0f, yawOffsetDegrees, 0f);
            Vector3 delta = (localPos - mapLocalEntrancePoint) * scale;
            Vector3 rotated = yaw * delta;
            campusPos = CampusEntrancePoint + rotated;
            campusRot = yaw * localRot;
        }

        /// <summary>Convert chỉ position. Convenience.</summary>
        public Vector3 MapLocalToCampusWorld(Vector3 localPos)
        {
            MapLocalToCampusWorld(localPos, Quaternion.identity, out var p, out _);
            return p;
        }

        // ---------------------------------------------------------------------
        // Lifecycle / registry
        // ---------------------------------------------------------------------

        private void OnEnable()
        {
            if (!_registry.Contains(this)) _registry.Add(this);
        }

        private void OnDisable()
        {
            _registry.Remove(this);
        }

        private void OnValidate()
        {
            if (scale <= 0f) scale = 1f;
        }

        /// <summary>Resolve calibration cho 1 (building, floor). Floor empty trong calib = wildcard.</summary>
        public static IndoorMapCalibration FindFor(BuildingId building, string floorId)
        {
            IndoorMapCalibration wildcard = null;
            for (int i = 0; i < _registry.Count; i++)
            {
                var c = _registry[i];
                if (c == null || c.targetBuilding != building) continue;
                if (string.IsNullOrEmpty(c.floorId))
                {
                    wildcard = c;
                    continue;
                }
                if (c.floorId == floorId) return c;
            }
            return wildcard;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 campus = CampusEntrancePoint;
            Gizmos.DrawSphere(campus, 0.25f);
            Vector3 dir = Quaternion.Euler(0f, yawOffsetDegrees, 0f) * Vector3.forward;
            Gizmos.DrawLine(campus, campus + dir * 2f);
        }
    }
}
