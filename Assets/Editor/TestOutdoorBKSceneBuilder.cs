#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds <c>Assets/Scenes/testOutdoorBK.unity</c> from a hybrid template scene for outdoor-only testing (no indoor hierarchy).
/// Run once in Unity: Tools/TestAR/Scenes/Create testOutdoorBK from hybrid template.
/// </summary>
public static class TestOutdoorBKSceneBuilder
{
    private static readonly string[] SourceSceneCandidates =
    {
        "Assets/Scenes/HybridGPSMap.unity",
        "Assets/Scenes/Hybrid Navigation.unity",
    };

    private const string TargetScenePath = "Assets/Scenes/testOutdoorBK.unity";

    [MenuItem("Tools/TestAR/Scenes/Create testOutdoorBK from hybrid template")]
    public static void CreateTestOutdoorBkFromHybridGpsMap()
    {
        string sourceScenePath = ResolveSourceScenePath();
        if (string.IsNullOrEmpty(sourceScenePath))
        {
            Debug.LogError("[TestOutdoorBK] No hybrid source scene found. Expected one of: " +
                string.Join(", ", SourceSceneCandidates));
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(TargetScenePath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Overwrite testOutdoorBK?",
                    $"{TargetScenePath} already exists. Replace with a fresh copy from{System.Environment.NewLine}{sourceScenePath}?",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(TargetScenePath);
            AssetDatabase.Refresh();
        }

        if (!AssetDatabase.CopyAsset(sourceScenePath, TargetScenePath))
        {
            Debug.LogError("[TestOutdoorBK] CopyAsset failed.");
            return;
        }

        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        DestroyRootIfPresent(scene, "IndoorEnvironment");

        HybridModeController hmc = Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc != null)
        {
            Undo.RecordObject(hmc, "TestOutdoorBK strip indoor refs");
            SerializedObject so = new SerializedObject(hmc);
            SetObjectReference(so, "indoorEnvironment", null);
            SetObjectReference(so, "indoorVisualRoot", null);
            SetObjectReference(so, "indoorMainCamera", null);
            ClearArrayProperty(so, "indoorOnlyVisualRoots");
            ClearArrayProperty(so, "indoorAudioSources");
            ClearArrayProperty(so, "indoorAudioListeners");

            SetEnumIndex(so, "initialMode", (int)HybridModeController.HybridMode.Outdoor);
            SetBoolProp(so, "activateInitialModeOnStart", true);
            SetBoolProp(so, "createRuntimeModeSwitcher", false);
            SetBoolProp(so, "autoSwitchEnabled", false);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hmc);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TestOutdoorBK] Done: {TargetScenePath} (from {sourceScenePath}) — outdoor-only; indoor stripped. Open and Play.");
    }

    private static string ResolveSourceScenePath()
    {
        foreach (string candidate in SourceSceneCandidates)
        {
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void DestroyRootIfPresent(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root != null && root.name == rootName)
            {
                Undo.DestroyObjectImmediate(root);
                Debug.Log($"[TestOutdoorBK] Removed root GameObject '{rootName}'.");
                return;
            }
        }
    }

    private static void SetObjectReference(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = value;
        }
    }

    private static void ClearArrayProperty(SerializedObject so, string propertyName)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && prop.isArray)
        {
            prop.ClearArray();
        }
    }

    private static void SetEnumIndex(SerializedObject so, string propertyName, int enumValueIndex)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
        {
            prop.enumValueIndex = enumValueIndex;
        }
    }

    private static void SetBoolProp(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
        {
            prop.boolValue = value;
        }
    }
}
#endif
