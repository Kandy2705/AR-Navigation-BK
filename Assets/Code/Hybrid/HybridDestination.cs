using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Destination cuối cùng người dùng muốn tới. Đủ thông tin để
    /// <see cref="HybridRouteCoordinator"/> quyết định route phase.
    ///
    /// - Outdoor POI: <see cref="isIndoor"/> = false, <see cref="building"/> = None.
    /// - Indoor POI: <see cref="isIndoor"/> = true + <see cref="building"/> + <see cref="floorId"/>.
    ///
    /// Position lấy theo thứ tự: <see cref="targetTransform"/> (nếu có), rồi
    /// <see cref="explicitCampusPosition"/>.
    /// </summary>
    [System.Serializable]
    public class HybridDestination
    {
        [Tooltip("Hiển thị cho UI/log.")]
        public string displayName;

        [Tooltip("True nếu POI nằm trong tòa.")]
        public bool isIndoor;

        [Tooltip("Tòa chứa POI (chỉ dùng khi isIndoor=true).")]
        public BuildingId building = BuildingId.None;

        [Tooltip("Tầng (free-form). Chỉ dùng khi isIndoor=true.")]
        public string floorId = "";

        [Tooltip("Ưu tiên đọc transform.position nếu khác null. Cho phép POI di chuyển hoặc dùng Anchor.")]
        public Transform targetTransform;

        [Tooltip("Vị trí campus world (fallback nếu targetTransform null).")]
        public Vector3 explicitCampusPosition;

        public Vector3 CampusPosition =>
            targetTransform != null ? targetTransform.position : explicitCampusPosition;

        public bool IsValid =>
            !string.IsNullOrEmpty(displayName) || targetTransform != null || explicitCampusPosition != Vector3.zero
            || isIndoor;
    }
}
