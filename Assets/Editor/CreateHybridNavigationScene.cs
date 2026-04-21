using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CreateHybridNavigationScene
{
    private const string OutdoorSourceScenePath = "Assets/Scenes/ManScene.unity";
    private const string IndoorSourceScenePath = "Assets/Samples/MultiSet-SDK/1.9.2/Sample Scenes/Navigation/Navigation.unity";
    private const string HybridScenePath = "Assets/Scenes/Hybrid Navigation.unity";
    private const string OutdoorRootName = "OutdoorEnvironment";
    private const string IndoorRootName = "IndoorEnvironment";

    [MenuItem("Tools/Nav/Create Hybrid Navigation Scene")]
    public static void CreateHybridScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[HybridNav] Stop Play Mode before creating Hybrid Navigation scene.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[HybridNav] Aborted by user.");
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(OutdoorSourceScenePath))
        {
            Debug.LogError("[HybridNav] Outdoor source scene not found: " + OutdoorSourceScenePath);
            return;
        }

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(IndoorSourceScenePath))
        {
            Debug.LogError("[HybridNav] Indoor source scene not found: " + IndoorSourceScenePath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HybridScenePath) != null)
        {
            if (!AssetDatabase.DeleteAsset(HybridScenePath))
            {
                Debug.LogError("[HybridNav] Could not replace existing hybrid scene: " + HybridScenePath);
                return;
            }
        }

        if (!AssetDatabase.CopyAsset(OutdoorSourceScenePath, HybridScenePath))
        {
            Debug.LogError("[HybridNav] Failed to create hybrid scene base from ManScene.");
            return;
        }

        AssetDatabase.Refresh();

        Scene hybridScene = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);

        var outdoorRoot = EnsureRoot(hybridScene, OutdoorRootName);
        MoveAllRootsUnder(hybridScene, outdoorRoot);

        Scene indoorSourceScene = EditorSceneManager.OpenScene(IndoorSourceScenePath, OpenSceneMode.Additive);
        try
        {
            var indoorRoot = EnsureRoot(hybridScene, IndoorRootName);
            CloneRootsToHybrid(indoorSourceScene, hybridScene, indoorRoot);
        }
        finally
        {
            EditorSceneManager.CloseScene(indoorSourceScene, true);
        }

        EditorSceneManager.MarkSceneDirty(hybridScene);
        EditorSceneManager.SaveScene(hybridScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[HybridNav] Created scene: " + HybridScenePath);
    }

    [MenuItem("Tools/Nav/Create Hybrid Navigation Scene", true)]
    private static bool ValidateCreateHybridScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static GameObject EnsureRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == name)
            {
                return go;
            }
        }

        var created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static void MoveAllRootsUnder(Scene scene, GameObject parentRoot)
    {
        var toMove = new List<GameObject>();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go == parentRoot)
            {
                continue;
            }
            toMove.Add(go);
        }

        foreach (var go in toMove)
        {
            go.transform.SetParent(parentRoot.transform, true);
        }
    }

    private static void CloneRootsToHybrid(Scene source, Scene target, GameObject indoorRoot)
    {
        foreach (var root in source.GetRootGameObjects())
        {
            var cloned = Object.Instantiate(root);
            cloned.name = root.name;
            SceneManager.MoveGameObjectToScene(cloned, target);
            cloned.transform.SetParent(indoorRoot.transform, true);
        }
    }
}
