using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject metadata cho các tòa nhà (chỉ data, KHÔNG reference GameObject của scene).
/// Tham chiếu GameObject scene phải đặt trong <see cref="BuildingSceneBindings"/> (MonoBehaviour).
///
/// Lý do: ScriptableObject asset sống ngoài scene → không lưu được reference tới scene GO
/// (Unity sẽ hiện "Type mismatch" khi drag GameObject vào).
/// </summary>
[CreateAssetMenu(fileName = "BuildingRegistry", menuName = "Indoor/Building Registry")]
public class BuildingRegistry : ScriptableObject
{
    /// <summary>Khớp với <see cref="MapLocalizationManager.localizationType"/> của Multiset SDK.</summary>
    public enum MultisetLocalizationKind
    {
        /// <summary>1 single map (vd "MAP_xxx").</summary>
        Map,
        /// <summary>Mapset gồm nhiều map đã merge (vd "MSET_xxx").</summary>
        MapSet,
    }

    [Serializable]
    public class Entry
    {
        [Tooltip("Định danh tòa nhà.")]
        public BuildingId id = BuildingId.None;

        [Tooltip("Tên hiển thị cho UI (vd \"Tòa B10\").")]
        public string displayName;

        [Tooltip("Mã map/mapset trên cloud Multiset (vd MAP_xxx hoặc MSET_xxx).")]
        public string mapsetCode;

        [Tooltip("Single Map hay MapSet — tương ứng MapLocalizationManager.localizationType.")]
        public MultisetLocalizationKind kind = MultisetLocalizationKind.Map;

        [Header("Outdoor entry point (GPS coords)")]
        public double entranceLatitude;
        public double entranceLongitude;

        [Tooltip("Bán kính (m) quanh entrance để app coi user 'đã đến tòa' và gợi ý chuyển indoor.")]
        public float entranceTriggerRadiusMeters = 30f;
    }

    [SerializeField] private List<Entry> buildings = new List<Entry>();

    public IReadOnlyList<Entry> Buildings => buildings;

    public Entry Find(BuildingId id)
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null && buildings[i].id == id) return buildings[i];
        }
        return null;
    }
}
