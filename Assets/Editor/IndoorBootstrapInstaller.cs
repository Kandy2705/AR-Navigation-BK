#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utility: gắn các component thiết yếu cho Indoor Multiset.
/// Chỉ thêm 2 thứ:
///   1. MultisetIndoorBootstrap — patch ARCamera/ARCameraCollider qua reflection (fix NRE).
///   2. NavigationControllerSetup — preconditions cho NavigationController SDK.
///
/// Menu: Tools/Indoor/Install MultisetIndoorBootstrap
/// </summary>
public static class IndoorBootstrapInstaller
{
    [MenuItem("Tools/Indoor/Install MultisetIndoorBootstrap")]
    public static void Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[BootstrapInstaller] Mở scene trước."); return; }

        // 1. Tìm hoặc tạo GameObject "Indoor Bootstrap" ở scene root.
        var existing = Object.FindFirstObjectByType<MultisetIndoorBootstrap>(FindObjectsInactive.Include);
        GameObject host;
        if (existing != null)
        {
            host = existing.gameObject;
            Debug.Log($"[BootstrapInstaller] MultisetIndoorBootstrap đã tồn tại trên '{host.name}'.");
        }
        else
        {
            host = new GameObject("Indoor Bootstrap");
            host.AddComponent<MultisetIndoorBootstrap>();
            Undo.RegisterCreatedObjectUndo(host, "Create Indoor Bootstrap");
            Debug.Log("[BootstrapInstaller] Tạo 'Indoor Bootstrap' với MultisetIndoorBootstrap.");
        }

        // 2. NavigationControllerSetup vào GO chứa NavigationController.
        var navCtrl = Object.FindFirstObjectByType<NavigationController>(FindObjectsInactive.Include);
        if (navCtrl != null && navCtrl.GetComponent<NavigationControllerSetup>() == null)
        {
            Undo.AddComponent<NavigationControllerSetup>(navCtrl.gameObject);
            Debug.Log($"[BootstrapInstaller] Added NavigationControllerSetup vào '{navCtrl.gameObject.name}'.");
        }

        // Dọn các component cũ/thừa nếu có (bao gồm Missing Scripts).
        RemoveLegacyComponent(host, "RuntimeNavMeshRebaker");
        RemoveLegacyComponent(host, "AgentNavMeshKeeper");
        RemoveLegacyComponent(host, "DestinationListAutoWire");
        RemoveLegacyComponent(host, "IndoorBuildingToast");
        RemoveLegacyComponent(host, "HideMeshAfterLocalize");
        RemoveLegacyComponent(host, "HybridUnifiedHUD");
        RemoveMissingScripts(host);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[BootstrapInstaller] Done. Save scene rồi build.");
    }

    private static void RemoveLegacyComponent(GameObject host, string typeName)
    {
        foreach (var c in host.GetComponents<Component>())
        {
            if (c == null) continue;
            if (c.GetType().Name == typeName)
            {
                Object.DestroyImmediate(c);
                Debug.Log($"[BootstrapInstaller] Removed legacy component '{typeName}'.");
                return;
            }
        }
    }

    private static void RemoveMissingScripts(GameObject go)
    {
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        if (removed > 0) Debug.Log($"[BootstrapInstaller] Removed {removed} missing script(s) from '{go.name}'.");
    }
}
#endif
