using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using System.Text;

/// <summary>
/// Reads all ARPathFinder/SimpleGPSTracker/TargetAnchor inspector values from HybridGPSMap
/// and prints a diagnostic report to the console. Run from menu: Tools > GPS Navigation Diagnostic.
/// </summary>
public static class HybridGPSMapDiagnostic
{
    private const string HybridGPSMapPath = "Assets/Scenes/HybridGPSMap.unity";

    [MenuItem("Tools/GPS Navigation Diagnostic")]
    public static void RunDiagnostic()
    {
        var currentScene = EditorSceneManager.GetActiveScene();
        bool needsLoad = currentScene.path != HybridGPSMapPath;

        if (needsLoad)
        {
            bool ok = EditorUtility.DisplayDialog("GPS Nav Diagnostic",
                "This will load HybridGPSMap.unity. Any unsaved changes in current scene will be lost. Continue?",
                "Load & Run", "Cancel");
            if (!ok) return;
            EditorSceneManager.OpenScene(HybridGPSMapPath, OpenSceneMode.Single);
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== GPS Navigation Diagnostic: HybridGPSMap ===\n");

        // ── ARPathFinder ──────────────────────────────────────────────────────
        var finders = Object.FindObjectsByType<ARPathFinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"ARPathFinder count: {finders.Length}");
        foreach (var pf in finders)
        {
            sb.AppendLine($"\n  [{pf.gameObject.name}]  active={pf.gameObject.activeInHierarchy}  enabled={pf.enabled}");
            sb.AppendLine($"    pathGeometryMode          = {GetField(pf, "pathGeometryMode")}");
            sb.AppendLine($"    arCamera                  = {GetField(pf, "arCamera")}");
            sb.AppendLine($"    xrOrigin                  = {GetField(pf, "xrOrigin")}");
            sb.AppendLine($"    targetNode                = {GetField(pf, "targetNode")}");
            sb.AppendLine($"    navigationGpsTracker      = {GetField(pf, "navigationGpsTracker")}");
            sb.AppendLine($"    gateLineUntilNavGpsHealthy= {GetField(pf, "gateLineUntilNavigationGpsHealthy")}");
            sb.AppendLine($"    bypassGpsGateInEditor     = {GetField(pf, "bypassNavigationGpsGateInEditor")}");
            sb.AppendLine($"    prioritizePathVisibility  = {GetField(pf, "prioritizePathVisibility")}");
            sb.AppendLine($"    navMeshSampleRadius       = {GetField(pf, "navMeshSampleRadius")}");
            sb.AppendLine($"    navMeshSampleRadiusExpand = {GetField(pf, "navMeshSampleRadiusExpanded")}");
            sb.AppendLine($"    showStraightLineFallback  = {GetField(pf, "showStraightLineFallbackWhenNavMeshFails")}");
            sb.AppendLine($"    useMeshPath               = {GetField(pf, "useMeshPath")}");
            sb.AppendLine($"    pathBorderWidthMeters     = {GetField(pf, "pathBorderWidthMeters")}");
            sb.AppendLine($"    pathAlwaysOnTop           = {GetField(pf, "pathAlwaysOnTop")}");
            sb.AppendLine($"    pathCenterMaterial        = {GetField(pf, "pathCenterMaterial")}");
            sb.AppendLine($"    pathBorderMaterial        = {GetField(pf, "pathBorderMaterial")}");
            sb.AppendLine($"    pathUpdateInterval        = {GetField(pf, "pathUpdateInterval")}");
        }

        // ── SimpleGPSTracker ──────────────────────────────────────────────────
        var trackers = Object.FindObjectsByType<SimpleGPSTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nSimpleGPSTracker count: {trackers.Length}");
        foreach (var t in trackers)
        {
            sb.AppendLine($"\n  [{t.gameObject.name}]  active={t.gameObject.activeInHierarchy}  enabled={t.enabled}");
            sb.AppendLine($"    xrOrigin                  = {GetField(t, "xrOrigin")}");
            sb.AppendLine($"    arCamera                  = {GetField(t, "arCamera")}");
            sb.AppendLine($"    accuracyThresholdMeters   = {GetField(t, "accuracyThresholdMeters")}");
            sb.AppendLine($"    snapGpsToNavMesh          = {GetField(t, "snapGpsPositionsToNavMesh")}");
            sb.AppendLine($"    navMeshSnapRadius         = {GetField(t, "navMeshSnapSampleRadiusMeters")}");
            sb.AppendLine($"    averageFirstFix           = {GetField(t, "averageFirstFixWhileStationary")}");
            sb.AppendLine($"    firstFixMinSamples        = {GetField(t, "firstFixAverageMinSamples")}");
            sb.AppendLine($"    maxNavDistFromOriginM     = {GetField(t, "maxNavigationDistanceFromMapOriginMeters")}");
        }

        // ── MapOrigin ─────────────────────────────────────────────────────────
        var origins = Object.FindObjectsByType<MapOrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nMapOrigin count: {origins.Length}");
        foreach (var o in origins)
        {
            sb.AppendLine($"\n  [{o.gameObject.name}]  active={o.gameObject.activeInHierarchy}");
            sb.AppendLine($"    originLat = {GetField(o, "originLat")}");
            sb.AppendLine($"    originLon = {GetField(o, "originLon")}");
        }

        // ── TargetAnchor ──────────────────────────────────────────────────────
        var anchors = Object.FindObjectsByType<TargetAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nTargetAnchor count: {anchors.Length}");
        foreach (var a in anchors)
        {
            sb.AppendLine($"  [{a.gameObject.name}]  lat={GetField(a, "targetLat"):F6}  lon={GetField(a, "targetLon"):F6}  active={a.gameObject.activeInHierarchy}");
        }

        // ── NavMesh ───────────────────────────────────────────────────────────
        sb.AppendLine($"\nNavMesh loaded: {NavMesh.CalculateTriangulation().vertices.Length > 0}");
        var triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices.Length > 0)
        {
            Vector3 min = triangulation.vertices[0];
            Vector3 max = triangulation.vertices[0];
            foreach (var v in triangulation.vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            sb.AppendLine($"    NavMesh bounds: min={min}  max={max}");
            sb.AppendLine($"    NavMesh vertex count: {triangulation.vertices.Length}");
        }
        else
        {
            sb.AppendLine("    *** NavMesh NOT LOADED — no baked data in scene! ***");
        }

        // ── GPSStartupOverlay ─────────────────────────────────────────────────
        var overlays = Object.FindObjectsByType<GPSStartupOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nGPSStartupOverlay count: {overlays.Length}");
        foreach (var ov in overlays)
        {
            sb.AppendLine($"  [{ov.gameObject.name}]  destroyAfterFade={GetField(ov, "destroyAfterFade")}  active={ov.gameObject.activeInHierarchy}");
        }

        // ── HybridOutdoorNavigationRoot ────────────────────────────────────────
        var hybridRoots = Object.FindObjectsByType<HybridOutdoorNavigationRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nHybridOutdoorNavigationRoot count: {hybridRoots.Length}");
        foreach (var r in hybridRoots)
        {
            sb.AppendLine($"  [{r.gameObject.name}]  active={r.gameObject.activeInHierarchy}");
            sb.AppendLine($"    outdoorNavigationContentRoot = {GetField(r, "outdoorNavigationContentRoot")}");
            sb.AppendLine($"    outdoorHudVisualSubtree      = {GetField(r, "outdoorHudVisualSubtree")}");
        }

        // ── NavigationManager ─────────────────────────────────────────────────
        var navMgrs = Object.FindObjectsByType<NavigationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nNavigationManager count: {navMgrs.Length}  (>0 = AR gated behind login screen)");
        foreach (var nm in navMgrs)
        {
            sb.AppendLine($"  [{nm.gameObject.name}]  keepARPageDisabledOnStart={GetField(nm, "keepARPageDisabledOnStart")}  ARPageObject={GetField(nm, "ARPageObject")}");
        }

        string report = sb.ToString();
        Debug.Log(report);

        // Save to file for easy copy
        string logPath = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "gps-nav-diagnostic.txt");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
        System.IO.File.WriteAllText(logPath, report);
        Debug.Log($"[Diagnostic] Report saved to: {logPath}");
        EditorUtility.DisplayDialog("GPS Nav Diagnostic", "Done! See Console output.\nLog saved to: Logs/gps-nav-diagnostic.txt", "OK");
    }

    private static object GetField(object obj, string name)
    {
        if (obj == null) return "NULL_OBJ";
        var f = obj.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (f == null) return $"[field '{name}' not found]";
        var val = f.GetValue(obj);
        return val == null ? "<null>" : val.ToString();
    }
}
