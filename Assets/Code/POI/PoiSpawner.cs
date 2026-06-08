using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instantiate POI capsules từ data thay vì gắn tay từng cái.
///
/// Thay thế flow cũ:
///   (cũ) Đặt tay N capsule → mỗi capsule nhập lat/lon vào TargetAnchor
///   (mới) 1 prefab + 1 nguồn data (PoiDatabase / API) → spawn tự động N capsule
///
/// Timing:
///   Spawn trong Awake → các TargetAnchor được set lat/lon TRƯỚC khi Start() của chúng
///   chạy (Unity chạy mọi Awake xong mới tới Start). TargetAnchor.Start() đọc lat/lon
///   đã set để tính vị trí. MobileNavigationHUD.Start() auto-find các anchor này nếu
///   field 'targets' để trống.
///
/// API tương lai:
///   Gọi SpawnFromData(apiPoiList) sau khi fetch — cùng logic, khác nguồn.
/// </summary>
public class PoiSpawner : MonoBehaviour
{
    [Header("Data source (ưu tiên database; nếu null dùng inlinePois)")]
    [Tooltip("ScriptableObject chứa danh sách POI. Tạo qua Create → TestAR → POI Database.")]
    [SerializeField] private PoiDatabase database;

    [Tooltip("Fallback / test nhanh: nhập POI thẳng đây khi chưa tạo database.")]
    [SerializeField] private List<PoiData> inlinePois = new List<PoiData>();

    [Header("Spawn target")]
    [Tooltip("Prefab capsule có sẵn component TargetAnchor (+ POISign/collider nếu cần). " +
             "Tạo bằng cách kéo 1 capsule POI hiện có vào Project để thành prefab.")]
    [SerializeField] private GameObject poiPrefab;

    [Tooltip("Parent cho các POI spawn ra. Để trống = spawn dưới chính object này.")]
    [SerializeField] private Transform poiParent;

    [Header("Lifecycle")]
    [Tooltip("Tự spawn trong Awake. Tắt nếu muốn gọi SpawnFromData() thủ công (vd sau API fetch).")]
    [SerializeField] private bool spawnOnAwake = true;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Số POI đã spawn (read-only).</summary>
    public int SpawnedCount => _spawned.Count;

    private void Awake()
    {
        if (spawnOnAwake)
            SpawnFromConfiguredSource();
    }

    /// <summary>Spawn từ database nếu có, ngược lại từ inlinePois.</summary>
    public void SpawnFromConfiguredSource()
    {
        if (database != null && database.pois != null && database.pois.Count > 0)
            SpawnFromData(database.pois);
        else
            SpawnFromData(inlinePois);
    }

    /// <summary>
    /// Spawn từ bất kỳ nguồn data nào (database / API / test). Clear lứa cũ trước.
    /// Đây là entry point cho API tương lai: PoiSpawner.SpawnFromData(deserializedList).
    /// </summary>
    public void SpawnFromData(IEnumerable<PoiData> pois)
    {
        if (poiPrefab == null)
        {
            Debug.LogError("[PoiSpawner] poiPrefab chưa gán — không spawn được.");
            return;
        }
        if (pois == null)
        {
            Debug.LogWarning("[PoiSpawner] Nguồn data null — không có gì để spawn.");
            return;
        }

        ClearSpawned();

        Transform parent = poiParent != null ? poiParent : transform;
        int count = 0;

        foreach (PoiData poi in pois)
        {
            if (poi == null) continue;

            GameObject go = Instantiate(poiPrefab, parent);
            go.name = string.IsNullOrEmpty(poi.displayName) ? $"POI_{poi.id}" : poi.displayName;

            TargetAnchor anchor = go.GetComponent<TargetAnchor>();
            if (anchor == null) anchor = go.GetComponentInChildren<TargetAnchor>(true);
            if (anchor == null)
            {
                Debug.LogWarning($"[PoiSpawner] Prefab thiếu TargetAnchor — bỏ qua POI '{go.name}'.");
                Destroy(go);
                continue;
            }

            // Set lat/lon TRƯỚC khi TargetAnchor.Start() chạy (next frame) → nó tự tính vị trí.
            anchor.displayName = poi.displayName;
            anchor.targetLat = poi.latitude;
            anchor.targetLon = poi.longitude;

            // Đẩy tên trực tiếp vào label (authoritative) — tránh label đọc nhầm displayName baked
            // trong prefab (vd prefab tạo từ capsule "B9" → label hiện B9 dù đây là A5).
            PoiLabel label = go.GetComponent<PoiLabel>();
            if (label == null) label = go.GetComponentInChildren<PoiLabel>(true);
            if (label != null) label.SetDisplayName(poi.displayName);

            _spawned.Add(go);
            count++;
        }

        Debug.Log($"[PoiSpawner] Spawned {count} POI(s) từ data source.");
    }

    /// <summary>Hủy toàn bộ POI đã spawn (gọi trước khi spawn lại từ nguồn mới, vd API refresh).</summary>
    public void ClearSpawned()
    {
        foreach (GameObject go in _spawned)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        _spawned.Clear();
    }
}
