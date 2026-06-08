#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FindSimulatorMode
{
    [MenuItem("Tools/Indoor/Find SimulatorModeController")]
    public static void Run()
    {
        var found = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(m => m != null && m.GetType().Name == "SimulatorModeController"
                        && m.gameObject.scene.IsValid())
            .ToArray();

        string report = $"=== SimulatorModeController ===\nFound {found.Length} instance(s):\n";
        foreach (var m in found)
        {
            var t = m.transform;
            var sb = new System.Text.StringBuilder(t.name);
            var cur = t.parent;
            while (cur != null) { sb.Insert(0, "/"); sb.Insert(0, cur.name); cur = cur.parent; }
            report += $"  - Path: {sb}\n";
            report += $"    active={m.gameObject.activeSelf}/{m.gameObject.activeInHierarchy}, enabled={m.enabled}\n";
            report += $"    pos={t.position}, rot={t.eulerAngles}\n";
        }
        Debug.Log(report);
    }
}
#endif
