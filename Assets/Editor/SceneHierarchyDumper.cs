#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// testOutdoorBK / hybrid scenes are often saved as binary YAML — Cursor cannot read hierarchy from disk.
/// Run in Unity: Tools/TestAR/Debug/Dump Scene Hierarchy To File (output: Assets/Editor/scene-hierarchy-dump.txt)
/// </summary>
public static class SceneHierarchyDumper
{
    [MenuItem("Tools/TestAR/Debug/Dump Active Scene Hierarchy To File")]
    public static void DumpActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[SceneHierarchyDumper] No active scene.");
            return;
        }

        string path = Path.Combine(Application.dataPath, "Editor", "scene-hierarchy-dump.txt");

        var sb = new StringBuilder(16_384);
        sb.AppendLine($"Scene: {scene.name} | path: {scene.path}");
        sb.AppendLine($"Time: {System.DateTime.Now:O}");
        sb.AppendLine(new string('=', 72));

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DumpTransformRecursive(root.transform, sb, 0);
        }

        sb.AppendLine(new string('-', 72));
        sb.AppendLine("Components (search): HybridModeController, HybridOutdoorNavigationRoot, ARPathFinder, SimpleGPSTracker, MobileNavigationHUD");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DumpKeyComponentsRecursive(root.transform, sb);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log($"[SceneHierarchyDumper] Wrote {path}");
        EditorUtility.RevealInFinder(path);
    }

    private static void DumpTransformRecursive(Transform t, StringBuilder sb, int depth)
    {
        if (t == null)
        {
            return;
        }

        bool active = t.gameObject.activeSelf;
        bool inHierarchy = t.gameObject.activeInHierarchy;
        string state = active ? (inHierarchy ? "on" : "selfOn_parentOff") : "off";
        sb.AppendLine($"{new string(' ', depth * 2)}- [{state}] {t.name}");

        for (int i = 0; i < t.childCount; i++)
        {
            DumpTransformRecursive(t.GetChild(i), sb, depth + 1);
        }
    }

    private static void DumpKeyComponentsRecursive(Transform t, StringBuilder sb)
    {
        if (t == null)
        {
            return;
        }

        GameObject go = t.gameObject;
        var path = GetTransformPath(t);

        var hmc = go.GetComponent<HybridModeController>();
        if (hmc != null)
        {
            sb.AppendLine($"[HybridModeController] {path} | activeInHierarchy={go.activeInHierarchy}");
        }

        var hon = go.GetComponent<HybridOutdoorNavigationRoot>();
        if (hon != null)
        {
            sb.AppendLine($"[HybridOutdoorNavigationRoot] {path} | activeInHierarchy={go.activeInHierarchy}");
        }

        var pf = go.GetComponent<ARPathFinder>();
        if (pf != null)
        {
            sb.AppendLine($"[ARPathFinder] {path} | enabled={pf.enabled} activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}");
        }

        var tr = go.GetComponent<SimpleGPSTracker>();
        if (tr != null)
        {
            sb.AppendLine($"[SimpleGPSTracker] {path} | enabled={tr.enabled} activeInHierarchy={go.activeInHierarchy}");
        }

        var hud = go.GetComponent<MobileNavigationHUD>();
        if (hud != null)
        {
            sb.AppendLine($"[MobileNavigationHUD] {path} | activeInHierarchy={go.activeInHierarchy}");
        }

        for (int i = 0; i < t.childCount; i++)
        {
            DumpKeyComponentsRecursive(t.GetChild(i), sb);
        }
    }

    private static string GetTransformPath(Transform t)
    {
        if (t.parent == null)
        {
            return t.name;
        }

        return GetTransformPath(t.parent) + "/" + t.name;
    }
}
#endif
