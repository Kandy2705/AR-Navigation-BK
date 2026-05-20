#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utility: bake NavMesh offline cho mỗi NavMeshSurface dưới Map Space.
/// Mỗi tòa cần có NavMeshSurface trên building root (MapB9, MapB10) với CollectObjects=Children.
///
/// Sau khi bake, NavMesh data được lưu vào file asset .asset trong scene folder.
/// Khi mesh quét tag EditorOnly bị strip ra khỏi build, NavMesh data VẪN còn — vì asset file
/// được include vào build qua scene reference.
///
/// Lưu ý cho multi-tòa: Unity 6 hỗ trợ nhiều NavMeshSurface mà không ghi đè nhau.
/// Mỗi surface có data riêng. NavMesh.CalculatePath dùng tất cả surface đang active.
/// → Khi switch sang B10, IndoorMapSwitcher tắt MapB9 → NavMesh của B9 bị unbind →
///   chỉ còn NavMesh B10 active.
///
/// Menu: Tools/Indoor/Bake All NavMeshes
/// </summary>
public static class IndoorBakeNavMeshes
{
    [MenuItem("Tools/Indoor/Bake All NavMeshes")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[IndoorBake] Mở scene trước.");
            return;
        }

        // Tìm Map Space (bao gồm inactive).
        Transform mapSpace = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == "Map Space" && t.gameObject.scene.IsValid())
            { mapSpace = t; break; }
        }
        if (mapSpace == null)
        {
            Debug.LogError("[IndoorBake] Không tìm thấy 'Map Space' trong scene.");
            return;
        }

        var surfaces = mapSpace.GetComponentsInChildren<NavMeshSurface>(includeInactive: true);
        if (surfaces.Length == 0)
        {
            Debug.LogWarning("[IndoorBake] Không có NavMeshSurface dưới Map Space. Add component vào MapB9/MapB10 trước.");
            return;
        }

        int success = 0;
        foreach (var s in surfaces)
        {
            // Đảm bảo settings đúng cho mỗi tòa.
            s.collectObjects = CollectObjects.Children;

            bool wasActive = s.gameObject.activeSelf;
            if (!wasActive) s.gameObject.SetActive(true);

            try
            {
                s.BuildNavMesh();
                EditorUtility.SetDirty(s);
                success++;
                Debug.Log($"[IndoorBake] Baked NavMesh on '{s.gameObject.name}' (parent='{s.transform.parent?.name}').");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[IndoorBake] Bake fail on '{s.gameObject.name}': {ex.Message}");
            }
            finally
            {
                if (!wasActive) s.gameObject.SetActive(false);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[IndoorBake] Done. Baked {success}/{surfaces.Length} surfaces.");
    }
}
#endif
