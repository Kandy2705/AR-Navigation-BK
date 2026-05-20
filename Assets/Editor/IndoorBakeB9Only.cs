#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bake NavMesh chỉ cho MapB9 và lưu data thành .asset file riêng
/// (NavMesh-MapB9.asset trong folder cạnh scene).
///
/// Khác <see cref="IndoorBakeNavMeshes"/>:
///   - Tìm NavMeshSurface qua <see cref="Resources.FindObjectsOfTypeAll{T}"/> để bypass
///     lỗi <c>GetComponentsInChildren</c> không trả về khi parent subtree inactive.
///   - Tạm thời activate toàn bộ chain parent của surface trước khi bake.
///   - Sau bake, gọi <c>AssetDatabase.CreateAsset</c> để lưu NavMeshData ra file
///     bên cạnh scene (mode persistent, build sẽ include).
///
/// Menu: Tools/Indoor/Bake B9 NavMesh (asset)
/// </summary>
public static class IndoorBakeB9Only
{
    [MenuItem("Tools/Indoor/Bake B9 NavMesh Only")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[BakeB9] Mở scene trước.");
            return;
        }

        // 1. Tìm MapB9 GameObject (bao gồm inactive).
        GameObject mapB9 = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == "MapB9" && t.gameObject.scene.IsValid())
            {
                mapB9 = t.gameObject;
                break;
            }
        }
        if (mapB9 == null)
        {
            Debug.LogError("[BakeB9] Không tìm thấy GameObject 'MapB9' trong scene.");
            return;
        }

        var surface = mapB9.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            Debug.LogError("[BakeB9] MapB9 không có NavMeshSurface — add component trước.");
            return;
        }

        // 2. Activate toàn bộ chain parent + chính nó để bake đúng world bounds.
        var deactivated = ActivateChain(mapB9);

        // Đảm bảo settings đúng.
        surface.collectObjects = CollectObjects.Children;
        EditorUtility.SetDirty(surface);

        try
        {
            // 3. Bake.
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[BakeB9] Bake xong nhưng navMeshData null — fail.");
                return;
            }

            // 4. Save NavMeshData ra asset file (persistent qua build).
            string sceneFolder = System.IO.Path.GetDirectoryName(scene.path);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            string folder = $"{sceneFolder}/{sceneName}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(sceneFolder, sceneName);
            }
            string assetPath = $"{folder}/NavMesh-MapB9.asset";

            // Nếu đã có asset cũ, xóa trước khi tạo mới.
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (existing != null && existing != surface.navMeshData)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            if (!AssetDatabase.Contains(surface.navMeshData))
            {
                AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
                Debug.Log($"[BakeB9] Saved NavMeshData → {assetPath}");
            }
            else
            {
                string currentPath = AssetDatabase.GetAssetPath(surface.navMeshData);
                Debug.Log($"[BakeB9] NavMeshData đã có asset tại: {currentPath}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BakeB9] Bake exception: {ex.Message}\n{ex.StackTrace}");
            return;
        }
        finally
        {
            // 5. Restore active state.
            foreach (var go in deactivated)
            {
                if (go != null) go.SetActive(false);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BakeB9] Done. Verify file NavMesh-MapB9.asset trong folder scene.");
    }

    /// <summary>
    /// Activate chain parent → child. Trả về list GameObject đã được activate
    /// (để restore lại sau).
    /// </summary>
    private static List<GameObject> ActivateChain(GameObject leaf)
    {
        var deactivated = new List<GameObject>();
        var chain = new List<GameObject>();
        Transform cur = leaf.transform;
        while (cur != null)
        {
            chain.Add(cur.gameObject);
            cur = cur.parent;
        }
        chain.Reverse();
        foreach (var go in chain)
        {
            if (!go.activeSelf)
            {
                go.SetActive(true);
                deactivated.Add(go);
            }
        }
        return deactivated;
    }
}
#endif
