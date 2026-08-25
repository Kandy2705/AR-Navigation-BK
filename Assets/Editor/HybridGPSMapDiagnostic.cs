using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.ARFoundation;
using System.Text;
using ARNav.Hybrid;

/// <summary>
/// Reads all ARPathFinder/SimpleGPSTracker/TargetAnchor inspector values from HybridGPSMap
/// and prints a diagnostic report to the console. Run from menu: Tools > GPS Navigation Diagnostic.
/// </summary>
public static class HybridGPSMapDiagnostic
{
    private const string HybridGPSMapPath = "Assets/Scenes/HybridGPSMap.unity";

    [MenuItem("Tools/TestAR/HybridGPSMap/Fix Outdoor Path On Device")]
    public static void FixOutdoorPathOnDevice()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != HybridGPSMapPath)
        {
            Debug.LogError($"[OutdoorPathFix] Open {HybridGPSMapPath} before applying the fix.");
            return;
        }

        var tracker = Object.FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
        var pathFinder = Object.FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
        if (tracker == null || pathFinder == null)
        {
            Debug.LogError($"[OutdoorPathFix] Missing SimpleGPSTracker={tracker != null}, ARPathFinder={pathFinder != null}.");
            return;
        }

        Undo.RecordObject(tracker, "Fix outdoor GPS and compass thresholds");
        var trackerSerialized = new SerializedObject(tracker);
        SetFloat(trackerSerialized, "accuracyThresholdMeters", 30f);
        SetFloat(trackerSerialized, "maxAcceptableHeadingAccuracy", 30f);
        SetFloat(trackerSerialized, "relaxedHeadingAccuracyLimit", 60f);
        SetBool(trackerSerialized, "lockXrOriginYawAfterNorthAlign", false);
        trackerSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tracker);

        var gpsMarkers = Object.FindObjectsByType<GPSMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var gpsMarker in gpsMarkers)
        {
            Undo.RecordObject(gpsMarker, "Use real compass on device");
            var markerSerialized = new SerializedObject(gpsMarker);
            SetBool(markerSerialized, "useMockCompass", false);
            markerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gpsMarker);
        }

        Undo.RecordObject(pathFinder, "Fix outdoor path visibility");
        var pathSerialized = new SerializedObject(pathFinder);
        SetBool(pathSerialized, "gateLineUntilNavigationGpsHealthy", false);
        SetBool(pathSerialized, "prioritizePathVisibility", true);
        SetBool(pathSerialized, "showStraightLineFallbackWhenNavMeshFails", true);
        SetBool(pathSerialized, "clampPathYToCameraFoot", true);
        SetFloat(pathSerialized, "pathWidth", 0.42f);
        SetFloat(pathSerialized, "pathStartTrimMeters", 1.2f);
        pathSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pathFinder);

        int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
        Camera displayCamera = GetObjectField<Camera>(pathFinder, "arCamera");
        if (displayCamera != null && minimapLayer >= 0)
        {
            Undo.RecordObject(displayCamera, "Hide minimap path mirror from AR camera");
            displayCamera.cullingMask &= ~(1 << minimapLayer);
            EditorUtility.SetDirty(displayCamera);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "[OutdoorPathFix] HybridGPSMap saved: GPS≤30m, compass≤30° (fallback≤60°), " +
            "real compass enabled, continuous XR yaw lock disabled, path gate off, " +
            "ribbon width=0.42m, start trim=1.2m, " +
            "MinimapOnly hidden from AR camera.");
    }

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
            sb.AppendLine($"    hierarchy                 = {GetHierarchyPath(pf.transform)}");
            sb.AppendLine($"    lossyScale                = {pf.transform.lossyScale}");
            sb.AppendLine($"    layer                     = {LayerMask.LayerToName(pf.gameObject.layer)} ({pf.gameObject.layer})");
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
            sb.AppendLine($"    pathWidth                = {GetField(pf, "pathWidth")}");
            sb.AppendLine($"    pathHeightOffset         = {GetField(pf, "pathHeightOffset")}");
            sb.AppendLine($"    clampPathYToCameraFoot   = {GetField(pf, "clampPathYToCameraFoot")}");
            sb.AppendLine($"    cameraEyeToFootMeters    = {GetField(pf, "cameraEyeToFootMeters")}");
            sb.AppendLine($"    showPathOnMinimap        = {GetField(pf, "showPathOnMinimap")}");
            sb.AppendLine($"    minimapPathLiftMeters    = {GetField(pf, "minimapPathLiftMeters")}");
            sb.AppendLine($"    pathBorderWidthMeters     = {GetField(pf, "pathBorderWidthMeters")}");
            sb.AppendLine($"    pathAlwaysOnTop           = {GetField(pf, "pathAlwaysOnTop")}");
            sb.AppendLine($"    pathCenterMaterial        = {GetField(pf, "pathCenterMaterial")}");
            sb.AppendLine($"    pathBorderMaterial        = {GetField(pf, "pathBorderMaterial")}");
            sb.AppendLine($"    pathUpdateInterval        = {GetField(pf, "pathUpdateInterval")}");
        }

        // ── Hybrid route chain ───────────────────────────────────────────────────
        var coordinators = Object.FindObjectsByType<HybridRouteCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var bridges = Object.FindObjectsByType<HybridArPathFinderBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var entrances = Object.FindObjectsByType<EntranceAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nHybridRouteCoordinator count: {coordinators.Length}");
        foreach (var c in coordinators)
            sb.AppendLine($"  [{GetHierarchyPath(c.transform)}] active={c.gameObject.activeInHierarchy} enabled={c.enabled}");
        sb.AppendLine($"HybridArPathFinderBridge count: {bridges.Length}");
        foreach (var b in bridges)
            sb.AppendLine($"  [{GetHierarchyPath(b.transform)}] active={b.gameObject.activeInHierarchy} enabled={b.enabled}");
        sb.AppendLine($"EntranceAnchor count: {entrances.Length}");
        foreach (var e in entrances)
        {
            sb.AppendLine(
                $"  [{GetHierarchyPath(e.transform)}] building={e.BuildingId} type={e.Type} " +
                $"active={e.gameObject.activeInHierarchy} position={e.CampusWorldPosition} " +
                $"linkedStart={(e.LinkedIndoorStartTransform != null ? GetHierarchyPath(e.LinkedIndoorStartTransform) : "<null>")}");
        }

        // ── AR plane debug visual ────────────────────────────────────────────────────
        var planeManagers = Object.FindObjectsByType<ARPlaneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nARPlaneManager count: {planeManagers.Length}");
        foreach (var p in planeManagers)
        {
            sb.AppendLine(
                $"  [{GetHierarchyPath(p.transform)}] active={p.gameObject.activeInHierarchy} enabled={p.enabled} " +
                $"requestedMode={p.requestedDetectionMode} planePrefab={(p.planePrefab != null ? p.planePrefab.name : "<null>")}");
        }

        int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"\nCamera count: {cameras.Length}; MinimapOnly layer={minimapLayer}");
        foreach (var camera in cameras)
        {
            bool seesMirror = minimapLayer >= 0 && (camera.cullingMask & (1 << minimapLayer)) != 0;
            sb.AppendLine(
                $"  [{GetHierarchyPath(camera.transform)}] active={camera.gameObject.activeInHierarchy} enabled={camera.enabled} " +
                $"tag={camera.tag} seesMinimapOnly={seesMirror} mask={camera.cullingMask}");
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
            sb.AppendLine($"    lockXrOriginYawAfterNorth = {GetField(t, "lockXrOriginYawAfterNorthAlign")}");
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

    private static T GetObjectField<T>(object obj, string name) where T : Object
    {
        return GetField(obj, name) as T;
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "<null>";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
