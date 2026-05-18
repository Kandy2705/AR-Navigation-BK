#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Lists every <see cref="ARSession"/> in open scenes so you can spot duplicate / stray sessions
/// (code only suppresses sessions under <c>IndoorEnvironment</c>; a session under UI still runs).
/// </summary>
public static class HybridArSessionAudit
{
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
