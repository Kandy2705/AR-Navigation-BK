#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Lists every <see cref="ARSession"/> in open scenes so you can spot duplicate / stray sessions
/// (code only suppresses sessions under <c>IndoorEnvironment</c>; a session under UI still runs).
/// </summary>
public static class HybridArSessionAudit
{
    private const string HybridScenePath = "Assets/Scenes/HybridGPSMap.unity";

    [MenuItem("Tools/TestAR/Hybrid/Log All ARSession In Open Scenes")]
    public static void LogAllArSessionsInOpenScenes()
    {
        var sessions = Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (sessions == null || sessions.Length == 0)
        {
            Debug.Log("[HybridArSessionAudit] No ARSession components in open scenes.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[HybridArSessionAudit] Found {sessions.Length} ARSession component(s):");
        int activeSimulated = 0;
        foreach (ARSession session in sessions)
        {
            if (session == null)
            {
                continue;
            }

            string path = GetHierarchyPath(session.transform);
            bool wouldRun = session.enabled && session.gameObject.activeInHierarchy;
            if (wouldRun)
            {
                activeSimulated++;
            }

            sb.AppendLine(
                $"  - {(wouldRun ? "[active]" : "[inactive]")} enabled={session.enabled} activeGO={session.gameObject.activeInHierarchy} :: {path}");
        }

        sb.AppendLine($"[HybridArSessionAudit] Count that would run at runtime (~enabled & activeInHierarchy): {activeSimulated}");
        if (activeSimulated > 1)
        {
            sb.AppendLine("[HybridArSessionAudit] WARNING: more than one session can run at once — high risk of AR/camera issues.");
        }

        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/TestAR/Hybrid/Fix HybridGPSMap Single ARSession")]
    public static void FixHybridGpsMapSingleArSession()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != HybridScenePath)
        {
            Debug.LogError(
                $"[HybridArSessionAudit] Open {HybridScenePath} before applying the single-session fix.");
            return;
        }

        ARSession[] sessions = Object.FindObjectsByType<ARSession>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        ARSession primary = null;
        foreach (ARSession session in sessions)
        {
            if (session != null &&
                GetHierarchyPath(session.transform).StartsWith("OutdoorEnvironment/"))
            {
                primary = session;
                break;
            }
        }

        if (primary == null)
        {
            Debug.LogError("[HybridArSessionAudit] OutdoorEnvironment AR Session was not found.");
            return;
        }

        int disabledDuplicates = 0;
        foreach (ARSession session in sessions)
        {
            if (session == null) continue;

            bool shouldEnable = ReferenceEquals(session, primary);
            if (session.enabled == shouldEnable) continue;

            Undo.RecordObject(session, "Fix HybridGPSMap single AR Session");
            session.enabled = shouldEnable;
            EditorUtility.SetDirty(session);
            if (!shouldEnable) disabledDuplicates++;
        }

        HybridModeController controller = Object.FindFirstObjectByType<HybridModeController>(
            FindObjectsInactive.Include);
        if (controller != null)
        {
            var serialized = new SerializedObject(controller);
            SetBool(serialized, "requestIOSCameraPermissionBeforeAR", true);
            SetBool(serialized, "enforceSingleARSessionAtRuntime", true);
            SetBool(serialized, "disableIndoorARSessionDuplicates", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[HybridArSessionAudit] HybridGPSMap saved with one startup AR Session. " +
            $"Primary='{GetHierarchyPath(primary.transform)}', disabled duplicates={disabledDuplicates}.");
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
        {
            return "(null)";
        }

        var stack = new System.Collections.Generic.List<string>();
        Transform walk = t;
        while (walk != null)
        {
            stack.Add(walk.name);
            walk = walk.parent;
        }

        stack.Reverse();
        return string.Join("/", stack);
    }
}
#endif
