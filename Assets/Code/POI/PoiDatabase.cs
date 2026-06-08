using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject chứa danh sách POI — nguồn data tĩnh, edit trên Inspector,
/// version-control được (asset text, không nhúng trong scene binary).
///
/// Tạo asset: Project window → Create → TestAR → POI Database.
/// Fill nhanh: right-click asset → "Fill — BK Campus 8 POIs".
///
/// Khi có API: vẫn dùng <see cref="PoiData"/> chung, chỉ đổi nguồn ở PoiSpawner.
/// </summary>
[CreateAssetMenu(fileName = "PoiDatabase", menuName = "TestAR/POI Database", order = 0)]
public class PoiDatabase : ScriptableObject
{
    [Tooltip("Danh sách POI. Mỗi entry là 1 PoiData (id, tên, lat, lon).")]
    public List<PoiData> pois = new List<PoiData>();

#if UNITY_EDITOR
    [ContextMenu("Fill — BK Campus 8 POIs")]
    private void FillBkCampus()
    {
        // 8 điểm khảo sát từ Google Earth Pro (BKGGEarth.kmz)
        pois = new List<PoiData>
        {
            new PoiData("A5",  "Tòa A5",  10.7731,    106.6598028, "building"),
            new PoiData("A2",  "Tòa A2",  10.7730139, 106.659975,  "building"),
            new PoiData("B9",  "Tòa B9",  10.7734,    106.660375,  "building"),
            new PoiData("B10", "Tòa B10", 10.773675,  106.6608861, "building"),
            new PoiData("B8",  "Tòa B8",  10.7737861, 106.6601667, "building"),
            new PoiData("A4",  "Tòa A4",  10.7732556, 106.6600972, "building"),
            new PoiData("A3",  "Tòa A3",  10.7733139, 106.6605583, "building"),
            new PoiData("B6",  "Tòa B6",  10.7737806, 106.6593139, "building"),
        };
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[PoiDatabase] Filled {pois.Count} BK campus POIs.");
    }
#endif
}
