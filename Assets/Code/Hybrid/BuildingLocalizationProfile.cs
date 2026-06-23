using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Profile MultiSet VPS cho 1 tòa nhà. Chỉ chứa data — KHÔNG reference scene GameObject
    /// (ScriptableObject sống ngoài scene).
    ///
    /// Cách dùng:
    ///   1. Create → Hybrid → Building Localization Profile.
    ///   2. Set buildingId + kind (Map vs MapSet).
    ///   3. Set primaryMapCode (vd MAP_9LME2PB7Y3EN cho B9, MSET_61GWPDRA2MJG cho B10).
    ///   4. Thêm acceptedMapIds = danh sách map ID mà SDK có thể trả về (B10 MSET sẽ trả MAP_xxx con).
    ///   5. (Optional) thêm floor metadata để UI hiển thị tầng.
    ///   6. EntranceAnchor sống trong scene; resolve runtime qua <see cref="EntranceAnchor.FindForBuilding"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingLocProfile", menuName = "Hybrid/Building Localization Profile")]
    public class BuildingLocalizationProfile : ScriptableObject
    {
        public enum LocalizationKind
        {
            Map,
            MapSet,
        }

        [Serializable]
        public class FloorMetadata
        {
            [Tooltip("Tên hiển thị (vd 'Tầng 1', 'Tầng trệt').")]
            public string displayName;

            [Tooltip("Free-form id khớp với EntranceAnchor.floorId.")]
            public string floorId;

            [Tooltip("Map ID cụ thể nếu mapset có 1 map per floor. Bỏ trống nếu floor share map chung.")]
            public string mapIdHint;

            [Tooltip("Y campus world ước lượng của sàn tầng này (để snap pose Y nếu cần).")]
            public float campusFloorY;
        }

        [Header("Identity")]
        [Tooltip("Tòa nhà mà profile này đại diện.")]
        [SerializeField] private BuildingId buildingId = BuildingId.None;

        [Tooltip("Tên hiển thị (vd 'Toà B10'). Optional.")]
        [SerializeField] private string displayName;

        [Header("MultiSet VPS")]
        [Tooltip("Single Map hay MapSet (khớp MapLocalizationManager.localizationType).")]
        [SerializeField] private LocalizationKind kind = LocalizationKind.Map;

        [Tooltip("Mã chính nạp vào MapLocalizationManager.mapOrMapsetCode (vd MAP_xxx hoặc MSET_xxx).")]
        [SerializeField] private string primaryMapCode;

        [Tooltip("Danh sách map ID hợp lệ. SDK có thể trả về map ID con của 1 MSET — dùng để verify confidence.")]
        [SerializeField] private List<string> acceptedMapIds = new List<string>();

        [Header("Localization gate")]
        [Tooltip("Confidence tối thiểu để chấp nhận pose VPS (range tuỳ SDK; thường 0..1).")]
        [SerializeField] private float minAcceptedConfidence = 0.5f;

        [Tooltip("Số lần localize success liên tiếp trước khi accept pose ổn định.")]
        [SerializeField] private int requiredConsecutiveSuccesses = 2;

        [Header("Floors (optional)")]
        [SerializeField] private List<FloorMetadata> floors = new List<FloorMetadata>();

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public BuildingId BuildingId => buildingId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? buildingId.ToString() : displayName;
        public LocalizationKind Kind => kind;
        public string PrimaryMapCode => primaryMapCode;
        public IReadOnlyList<string> AcceptedMapIds => acceptedMapIds;
        public float MinAcceptedConfidence => minAcceptedConfidence;
        public int RequiredConsecutiveSuccesses => Mathf.Max(1, requiredConsecutiveSuccesses);
        public IReadOnlyList<FloorMetadata> Floors => floors;

        /// <summary>True nếu mapId thuộc set chấp nhận (primary code hoặc 1 trong accepted list).</summary>
        public bool IsAcceptedMapId(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return false;
            if (!string.IsNullOrEmpty(primaryMapCode) && primaryMapCode == mapId) return true;
            for (int i = 0; i < acceptedMapIds.Count; i++)
            {
                if (acceptedMapIds[i] == mapId) return true;
            }
            return false;
        }

        public FloorMetadata FindFloor(string floorId)
        {
            if (string.IsNullOrEmpty(floorId)) return null;
            for (int i = 0; i < floors.Count; i++)
            {
                if (floors[i] != null && floors[i].floorId == floorId) return floors[i];
            }
            return null;
        }

        private void OnValidate()
        {
            if (minAcceptedConfidence < 0f) minAcceptedConfidence = 0f;
            if (requiredConsecutiveSuccesses < 1) requiredConsecutiveSuccesses = 1;
        }
    }
}
