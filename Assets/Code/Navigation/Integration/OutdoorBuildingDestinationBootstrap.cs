using System.Collections.Generic;
using ARNav.Hybrid;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Đảm bảo outdoor destination cho <b>Tòa B9</b> và <b>Tòa B10</b> luôn có
/// <see cref="TargetAnchor"/> trên HybridGPSMap — hiện trong dropdown/search
/// (<see cref="HybridDestinationService"/> / <see cref="MobileNavigationHUD"/>).
///
/// Nguồn tọa độ (ưu tiên):
///   1. <see cref="EntranceAnchor"/> B9/B10 trong scene → inverse MapOrigin → lat/lon
///   2. <see cref="PoiDatabase"/> (id B9 / B10) nếu gán
///   3. Fallback hardcode khảo sát BK (cùng số trong PoiDatabase.asset)
///
/// Không đụng scene binary: spawn runtime dưới parent "Outdoor Destinations (B9/B10)".
/// </summary>
[DefaultExecutionOrder(-100)]
public class OutdoorBuildingDestinationBootstrap : MonoBehaviour
{
    public const string ParentName = "Outdoor Destinations (B9/B10)";
    public const string AnchorB9Name = "Outdoor_B9";
    public const string AnchorB10Name = "Outdoor_B10";

    // Khảo sát BK (PoiDatabase) — fallback khi scene/registry chưa có số.
    private const double FallbackB9Lat = 10.7734;
    private const double FallbackB9Lon = 106.660375;
    private const double FallbackB10Lat = 10.773675;
    private const double FallbackB10Lon = 106.6608861;

    [Header("Data")]
    [Tooltip("Optional: lấy lat/lon id=B9, B10 từ asset PoiDatabase.")]
    [SerializeField] private PoiDatabase poiDatabase;

    [Tooltip("Chỉ chạy trên các scene này (tên exact).")]
    [SerializeField] private string[] allowedSceneNames = { "HybridGPSMap", "Hybrid Navigation" };

    [Header("Visual")]
    [Tooltip("Y world của marker outdoor (m).")]
    [SerializeField] private float markerY = 0.5f;

    [Tooltip("Tạo capsule visual đơn giản nếu TargetAnchor mới spawn (dễ thấy trên map).")]
    [SerializeField] private bool createSimpleVisual = true;

    [Header("Behavior")]
    [SerializeField] private bool runOnAwake = true;
    [SerializeField] private bool verboseLog = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene != "HybridGPSMap" && scene != "Hybrid Navigation") return;
        if (FindFirstObjectByType<OutdoorBuildingDestinationBootstrap>(FindObjectsInactive.Include) != null)
            return;

        var go = new GameObject("OutdoorBuildingDestinationBootstrap");
        go.AddComponent<OutdoorBuildingDestinationBootstrap>();
    }

    private void Awake()
    {
        if (runOnAwake) EnsureOutdoorBuildingAnchors();
    }

    [ContextMenu("Ensure Outdoor B9/B10 Anchors")]
    public void EnsureOutdoorBuildingAnchors()
    {
        if (!IsAllowedScene())
        {
            if (verboseLog) Debug.Log($"[OutdoorBuildingDest] Skip scene '{SceneManager.GetActiveScene().name}'.");
            return;
        }

        if (poiDatabase == null)
        {
            // Try load default asset by name (Resources optional — also FindObjectsOfTypeAll in Editor/play).
            var all = Resources.FindObjectsOfTypeAll<PoiDatabase>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "PoiDatabase")
                {
                    poiDatabase = all[i];
                    break;
                }
            }
            if (poiDatabase == null && all.Length > 0) poiDatabase = all[0];
        }

        Transform parent = EnsureParent();
        EnsureOne(BuildingId.B9, AnchorB9Name, "Tòa B9", parent);
        EnsureOne(BuildingId.B10, AnchorB10Name, "Tòa B10", parent);

        // Refresh hybrid catalog + outdoor HUD dropdown.
        var svc = HybridDestinationService.Instance ?? FindFirstObjectByType<HybridDestinationService>(FindObjectsInactive.Include);
        if (svc != null) svc.RefreshCatalog();

        var hud = FindFirstObjectByType<MobileNavigationHUD>(FindObjectsInactive.Include);
        if (hud != null) hud.RebuildDestinationList();

        if (verboseLog) Debug.Log("[OutdoorBuildingDest] Outdoor B9 + B10 TargetAnchors ready.");
    }

    private bool IsAllowedScene()
    {
        string n = SceneManager.GetActiveScene().name;
        if (allowedSceneNames == null || allowedSceneNames.Length == 0) return true;
        for (int i = 0; i < allowedSceneNames.Length; i++)
        {
            if (allowedSceneNames[i] == n) return true;
        }
        return false;
    }

    private Transform EnsureParent()
    {
        var existing = GameObject.Find(ParentName);
        if (existing != null) return existing.transform;

        // Prefer under OutdoorEnvironment / MYPHUMAP if present.
        Transform parent = null;
        var outdoor = GameObject.Find("OutdoorEnvironment");
        if (outdoor != null)
        {
            var myphu = outdoor.transform.Find("MYPHUMAP");
            parent = myphu != null ? myphu : outdoor.transform;
        }

        var go = new GameObject(ParentName);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    private void EnsureOne(BuildingId building, string goName, string displayName, Transform parent)
    {
        TargetAnchor existing = FindAnchorByName(goName) ?? FindAnchorByDisplayName(displayName);
        if (existing != null)
        {
            // Already in scene — still refresh lat/lon if zero-ish and we have better data.
            if (IsUnsetGps(existing.targetLat, existing.targetLon))
            {
                ApplyCoords(existing, building, displayName);
                existing.Recalculate();
            }
            if (verboseLog) Debug.Log($"[OutdoorBuildingDest] Reuse existing '{existing.name}' ({existing.TargetName}).");
            return;
        }

        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, markerY, 0f);

        if (createSimpleVisual)
            AttachSimpleMarker(go, building == BuildingId.B9
                ? new Color(0.2f, 0.55f, 1f, 0.95f)
                : new Color(1f, 0.45f, 0.15f, 0.95f));

        var anchor = go.AddComponent<TargetAnchor>();
        anchor.displayName = displayName;
        ApplyCoords(anchor, building, displayName);

        // TargetAnchor.Awake ẩn renderer; Recalculate khi MapOrigin sẵn sàng.
        // Gọi ngay; GPSStartupOverlay cũng sẽ Recalculate sau.
        anchor.Recalculate();

        if (verboseLog)
            Debug.Log($"[OutdoorBuildingDest] Created {goName} → {displayName} " +
                      $"GPS ({anchor.targetLat:F7}, {anchor.targetLon:F7})");
    }

    private void ApplyCoords(TargetAnchor anchor, BuildingId building, string displayName)
    {
        // 1) EntranceAnchor world → GPS
        var entrance = EntranceAnchor.FindForBuilding(building, requireEntrance: true);
        if (entrance == null)
            entrance = EntranceAnchor.FindForBuilding(building, requireEntrance: false);

        MapOrigin mapOrigin = MapOrigin.FindPrimary();
        if (entrance != null && mapOrigin != null)
        {
            Vector3 campus = entrance.CampusWorldPosition;
            mapOrigin.GetGPSFromUnityPosition(campus, out double lat, out double lon);
            anchor.targetLat = lat;
            anchor.targetLon = lon;
            // Snap Y to marker height; XZ set by Recalculate from GPS (should match entrance XZ closely).
            if (verboseLog)
                Debug.Log($"[OutdoorBuildingDest] {displayName} GPS from EntranceAnchor '{entrance.name}' @ {campus}");
            return;
        }

        // 2) PoiDatabase
        if (TryGetFromDatabase(building, out double dLat, out double dLon))
        {
            anchor.targetLat = dLat;
            anchor.targetLon = dLon;
            return;
        }

        // 3) Fallback survey
        if (building == BuildingId.B9)
        {
            anchor.targetLat = FallbackB9Lat;
            anchor.targetLon = FallbackB9Lon;
        }
        else
        {
            anchor.targetLat = FallbackB10Lat;
            anchor.targetLon = FallbackB10Lon;
        }
    }

    private bool TryGetFromDatabase(BuildingId building, out double lat, out double lon)
    {
        lat = 0; lon = 0;
        if (poiDatabase == null || poiDatabase.pois == null) return false;
        string id = building == BuildingId.B9 ? "B9" : building == BuildingId.B10 ? "B10" : null;
        if (id == null) return false;
        for (int i = 0; i < poiDatabase.pois.Count; i++)
        {
            var p = poiDatabase.pois[i];
            if (p == null) continue;
            if (string.Equals(p.id, id, System.StringComparison.OrdinalIgnoreCase)
                || (p.displayName != null && p.displayName.IndexOf(id, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (p.latitude == 0 && p.longitude == 0) return false;
                lat = p.latitude;
                lon = p.longitude;
                return true;
            }
        }
        return false;
    }

    private static bool IsUnsetGps(double lat, double lon) =>
        System.Math.Abs(lat) < 1e-8 && System.Math.Abs(lon) < 1e-8;

    private static TargetAnchor FindAnchorByName(string goName)
    {
        var all = FindObjectsByType<TargetAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == goName) return all[i];
        }
        return null;
    }

    private static TargetAnchor FindAnchorByDisplayName(string displayName)
    {
        var all = FindObjectsByType<TargetAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (string.Equals(all[i].displayName, displayName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(all[i].TargetName, displayName, System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        }
        return null;
    }

    private static void AttachSimpleMarker(GameObject host, Color color)
    {
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Visual";
        capsule.transform.SetParent(host.transform, false);
        capsule.transform.localPosition = Vector3.up * 0.5f;
        capsule.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        var col = capsule.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        var r = capsule.GetComponent<MeshRenderer>();
        if (r != null)
        {
            // URP/Lit or Unlit
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                r.sharedMaterial = mat;
            }
        }

        // Label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(host.transform, false);
        labelGo.transform.localPosition = Vector3.up * 2.2f;
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = host.name.Replace("Outdoor_", "Tòa ");
        tm.characterSize = 0.25f;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
    }
}
