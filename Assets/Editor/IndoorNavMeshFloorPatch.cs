#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tool — tự động tạo "Floor Patch" dưới mỗi tòa indoor (MapB9 / MapB10 / ...)
/// để NavMesh bake ra một đảo duy nhất kể cả khi mesh quét VPS bị rách hoặc gồm
/// nhiều sub-map lệch độ cao.
///
/// Floor Patch là một Cube mỏng (Y = 0.02m) phủ toàn bộ XZ-bounds của tòa, đặt
/// đúng cao độ sàn. MeshRenderer tắt (không hiển thị), MeshCollider giữ để bake
/// NavMesh include nó. Gắn NavMeshModifier (Walkable, override).
///
/// Menu:
///   Tools/Indoor/Floor Patch — Add or Update for All Buildings
///   Tools/Indoor/Floor Patch — Remove All
///   Tools/Indoor/Bake With Permissive Settings
/// </summary>
public static class IndoorNavMeshFloorPatch
{
    private const string PatchName = "_NavMeshFloorPatch";
    private const float PatchThickness = 0.02f;
    private const float PatchPadding = 1.0f; // mở rộng XZ thêm 1m mỗi cạnh

    [MenuItem("Tools/Indoor/Floor Patch — Add or Update for All Buildings")]
    public static void AddPatchAll()
    {
        var bindings = Object.FindFirstObjectByType<BuildingSceneBindings>(FindObjectsInactive.Include);
        if (bindings == null)
        {
            Debug.LogError("[FloorPatch] Không tìm thấy BuildingSceneBindings trong scene.");
            return;
        }

        int patched = 0;
        foreach (var b in bindings.Bindings)
        {
            if (b == null || b.buildingRoot == null) continue;
            if (AddOrUpdatePatch(b.buildingRoot)) patched++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[FloorPatch] Đã add/update floor patch cho {patched} tòa.");
        EditorUtility.DisplayDialog("Floor Patch",
            $"Đã add/update floor patch cho {patched} tòa.\n\nBước tiếp theo: chạy Tools/Indoor/Bake With Permissive Settings.",
            "OK");
    }

    [MenuItem("Tools/Indoor/Floor Patch — Remove All")]
    public static void RemovePatchAll()
    {
        var bindings = Object.FindFirstObjectByType<BuildingSceneBindings>(FindObjectsInactive.Include);
        if (bindings == null) { Debug.LogError("[FloorPatch] Không tìm thấy BuildingSceneBindings."); return; }

        int removed = 0;
        foreach (var b in bindings.Bindings)
        {
            if (b == null || b.buildingRoot == null) continue;
            var existing = b.buildingRoot.transform.Find(PatchName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                removed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[FloorPatch] Đã remove floor patch ở {removed} tòa.");
    }

    [MenuItem("Tools/Indoor/Bake With Permissive Settings")]
    public static void BakePermissive()
    {
        var bindings = Object.FindFirstObjectByType<BuildingSceneBindings>(FindObjectsInactive.Include);
        if (bindings == null) { Debug.LogError("[FloorPatch] Không tìm thấy BuildingSceneBindings."); return; }

        int baked = 0;
        foreach (var b in bindings.Bindings)
        {
            if (b == null || b.buildingRoot == null) continue;
            var surfaces = b.buildingRoot.GetComponentsInChildren<NavMeshSurface>(includeInactive: true);
            foreach (var s in surfaces)
            {
                ApplyPermissiveSettings(s);
                bool wasActive = s.gameObject.activeSelf;
                if (!wasActive) s.gameObject.SetActive(true);
                try
                {
                    s.BuildNavMesh();
                    EditorUtility.SetDirty(s);
                    baked++;
                    Debug.Log($"[FloorPatch] Baked '{s.gameObject.name}' under '{b.buildingRoot.name}'.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[FloorPatch] Bake fail {s.gameObject.name}: {ex.Message}");
                }
                finally
                {
                    if (!wasActive) s.gameObject.SetActive(false);
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[FloorPatch] Done. Baked {baked} surfaces with permissive settings.");
        EditorUtility.DisplayDialog("Bake Done",
            $"Đã bake {baked} NavMeshSurface với settings nới lỏng.\nKiểm tra Scene view (Show NavMesh) để xác nhận vùng xanh đã liền mạch.",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Core helpers
    // ─────────────────────────────────────────────────────────────────────

    private static bool AddOrUpdatePatch(GameObject buildingRoot)
    {
        var bounds = ComputeXZBounds(buildingRoot);
        if (!bounds.HasValue)
        {
            Debug.LogWarning($"[FloorPatch] '{buildingRoot.name}' không có Renderer/Collider con — bỏ qua.");
            return false;
        }

        var b = bounds.Value;
        Transform existing = buildingRoot.transform.Find(PatchName);
        GameObject patch;
        if (existing != null)
        {
            patch = existing.gameObject;
        }
        else
        {
            patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = PatchName;
            patch.transform.SetParent(buildingRoot.transform, worldPositionStays: false);
            Undo.RegisterCreatedObjectUndo(patch, "Add NavMesh Floor Patch");

            var renderer = patch.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        Undo.RecordObject(patch.transform, "Update Floor Patch Transform");

        // Y position = đáy bounds; XZ size = bounds + padding * 2
        Vector3 worldCenter = new Vector3(
            (b.min.x + b.max.x) * 0.5f,
            b.min.y + PatchThickness * 0.5f,
            (b.min.z + b.max.z) * 0.5f
        );
        Vector3 worldScale = new Vector3(
            (b.max.x - b.min.x) + PatchPadding * 2f,
            PatchThickness,
            (b.max.z - b.min.z) + PatchPadding * 2f
        );

        patch.transform.position = worldCenter;
        patch.transform.rotation = Quaternion.identity;
        patch.transform.localScale = patch.transform.parent != null
            ? Vector3.Scale(worldScale, InverseLossy(patch.transform.parent.lossyScale))
            : worldScale;

        // Đảm bảo có NavMeshModifier để override walkable.
        var modifier = patch.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = Undo.AddComponent<NavMeshModifier>(patch);
        modifier.overrideArea = true;
        modifier.area = 0; // 0 = Walkable

        EditorUtility.SetDirty(patch);
        Debug.Log($"[FloorPatch] '{buildingRoot.name}': patch size={worldScale}, Y={worldCenter.y:F2}");
        return true;
    }

    private static Bounds? ComputeXZBounds(GameObject root)
    {
        bool any = false;
        Bounds bounds = new Bounds();

        // Bao gồm tất cả Renderer + Collider con (trừ patch của chính chúng ta).
        foreach (var r in root.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            if (r.transform.IsChildOf(FindPatch(root)?.transform)) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else      { bounds.Encapsulate(r.bounds); }
        }
        foreach (var c in root.GetComponentsInChildren<Collider>(includeInactive: true))
        {
            if (FindPatch(root) != null && c.transform.IsChildOf(FindPatch(root).transform)) continue;
            if (!any) { bounds = c.bounds; any = true; }
            else      { bounds.Encapsulate(c.bounds); }
        }
        return any ? bounds : (Bounds?)null;
    }

    private static GameObject FindPatch(GameObject root)
    {
        var t = root.transform.Find(PatchName);
        return t != null ? t.gameObject : null;
    }

    private static Vector3 InverseLossy(Vector3 lossy)
    {
        return new Vector3(
            Mathf.Approximately(lossy.x, 0) ? 1 : 1f / lossy.x,
            Mathf.Approximately(lossy.y, 0) ? 1 : 1f / lossy.y,
            Mathf.Approximately(lossy.z, 0) ? 1 : 1f / lossy.z
        );
    }

    private static void ApplyPermissiveSettings(NavMeshSurface s)
    {
        Undo.RecordObject(s, "Apply Permissive NavMesh Settings");
        s.collectObjects = CollectObjects.Children;
        // Giữ default useGeometry (RenderMeshes) — bake sẽ nhận cả mesh quét VPS
        // lẫn floor patch (Cube primitive có MeshFilter mặc định).
        s.overrideVoxelSize = true;
        s.voxelSize = 0.08f;
        s.overrideTileSize = false;
        s.minRegionArea = 0.5f;
        EditorUtility.SetDirty(s);
    }
}
#endif
