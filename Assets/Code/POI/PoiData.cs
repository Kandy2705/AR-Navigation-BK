using System;
using UnityEngine;

/// <summary>
/// DTO cho 1 POI — data model DÙNG CHUNG cho cả 2 nguồn:
///   1. ScriptableObject <see cref="PoiDatabase"/> (nhập tay trên Inspector)
///   2. API fetch sau này (JsonUtility.FromJson parse thẳng vào class này)
///
/// Plain [Serializable] class (không kế thừa MonoBehaviour) để:
///   - Unity serialize được trong list trên Inspector
///   - JsonUtility deserialize được từ response API
///
/// lat/lon dùng double (KHÔNG float) vì float mất độ chính xác ở mức tọa độ địa lý
/// (~1m sai số ở chữ số thứ 7) — phải khớp kiểu với TargetAnchor.targetLat/targetLon.
/// </summary>
[Serializable]
public class PoiData
{
    [Tooltip("Mã định danh ngắn, vd 'B9'. Dùng cho lookup / log.")]
    public string id;

    [Tooltip("Tên hiển thị, vd 'Tòa B9 - Khoa CNTT'.")]
    public string displayName;

    [Tooltip("Vĩ độ (latitude) WGS84, độ thập phân.")]
    public double latitude;

    [Tooltip("Kinh độ (longitude) WGS84, độ thập phân.")]
    public double longitude;

    [Tooltip("Loại POI — tùy chọn (building / canteen / gate...). Để filter/icon sau này.")]
    public string type;

    public PoiData() { }

    public PoiData(string id, string displayName, double latitude, double longitude, string type = "")
    {
        this.id = id;
        this.displayName = displayName;
        this.latitude = latitude;
        this.longitude = longitude;
        this.type = type;
    }
}

/// <summary>
/// Wrapper để JsonUtility parse mảng JSON từ API. JsonUtility không parse top-level array,
/// nên API nên trả về dạng: { "pois": [ {...}, {...} ] }.
/// Nếu API trả top-level array thuần, cần bọc lại trước khi parse.
/// </summary>
[Serializable]
public class PoiDataList
{
    public PoiData[] pois;
}
