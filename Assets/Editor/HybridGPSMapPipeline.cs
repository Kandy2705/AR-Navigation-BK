using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces Hybrid <c>OutdoorEnvironment</c> navigation content with (almost) all of
/// <see cref="GpsScenePath"/>: same stack as standalone GPSMapPlane (SimpleGPSTracker, ARPathFinder,
/// Ground, minimap, targets, obstacles, etc.). Hybrid shell is kept: one XR Origin, one AR Session,
/// outdoor UI canvas, sun. GPSMapPlane's own XR / AR Session / sun are skipped to avoid duplicates;
/// rigs are rewired to Hybrid's XR Origin and Main Camera.
/// </summary>
public static class HybridGPSMapPipeline
{
    internal const string HybridScenePath = "Assets/Scenes/HybridGPSMap.unity";
    internal const string GpsScenePath = "Assets/Scenes/GPSMapPlane.unity";

    /// <summary>Direct children of OutdoorEnvironment that must survive the wipe (Hybrid + AR).</summary>
    private static readonly HashSet<string> OutdoorShellPreservedChildNames = new HashSet<string>
    {
        "XR Origin",
        "AR Session",
        "UI",
        "Directional Light"
    };

    /// <summary>GPSMapPlane scene roots we do not paste (Hybrid already owns these).</summary>
    private static readonly HashSet<string> GpsMapPlaneRootsSkipped = new HashSet<string>
    {
        "XR Origin",
        "AR Session",
        "Directional Light"
    };

    [MenuItem("Tools/TestAR/HybridGPSMap/Replace Outdoor With GPSMapPlane (merge)")]
    public static bool MenuMergeOutdoor()
    {
        if (!EnsureNotPlaying())
            return false;
        return MergeOutdoorFromGpsMapPlane(saveScene: true);
    }

    [MenuItem("Tools/TestAR/HybridGPSMap/Rebake Outdoor Ground NavMesh")]
    public static bool MenuBakeOutdoorNavMesh()
    {
        if (!EnsureNotPlaying())
            return false;

        EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        bool ok = RebakeOutdoorGroundNavMesh_Internal();
        if (ok)
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[HybridGPSMap] Outdoor Ground NavMesh baked and scene saved.");
        }

        return ok;
    }

    [MenuItem("Tools/TestAR/HybridGPSMap/Replace Outdoor + Rebake Ground NavMesh")]
    public static void MenuMergeAndBake()
    {
        if (!EnsureNotPlaying())
            return;

        if (!MergeOutdoorFromGpsMapPlane(saveScene: true))
            return;

        RebakeOutdoorGroundNavMesh_Internal();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[HybridGPSMap] Replace outdoor + NavMesh bake complete.");
    }

    /// <summary>
    /// Batch: <c>-batchmode -quit -projectPath ... -executeMethod HybridGPSMapPipeline.MergeAndBakeFromCli</c>
    /// </summary>
    public static void MergeAndBakeFromCli()
    {
        MergeOutdoorFromGpsMapPlane(saveScene: false);
        RebakeOutdoorGroundNavMesh_Internal();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[HybridGPSMap] CLI merge+bake finished.");
        EditorApplication.Exit(0);
    }

    internal static bool MergeOutdoorFromGpsMapPlane(bool saveScene)
    {
        if (!System.IO.File.Exists(HybridScenePath))
        {
            Debug.LogError($"[HybridGPSMap] Missing scene: {HybridScenePath}");
            return false;
        }

        if (!System.IO.File.Exists(GpsScenePath))
        {
            Debug.LogError($"[HybridGPSMap] Missing scene: {GpsScenePath}");
            return false;
        }

        Scene hybridScene = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        Transform outdoor = GameObject.Find("OutdoorEnvironment")?.transform;
        if (outdoor == null)
        {
            Debug.LogError("[HybridGPSMap] OutdoorEnvironment not found.");
            return false;
        }

        RemoveNonShellOutdoorChildren(outdoor);

        Scene gpsScene = EditorSceneManager.OpenScene(GpsScenePath, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(hybridScene);

        int merged = 0;
        foreach (GameObject src in gpsScene.GetRootGameObjects())
        {
            if (src == null || GpsMapPlaneRootsSkipped.Contains(src.name))
                continue;

            GameObject clone = Object.Instantiate(src);
            SceneManager.MoveGameObjectToScene(clone, hybridScene);
            clone.name = src.name;
            clone.transform.SetParent(outdoor, true);
            EditorUtility.SetDirty(clone);
            merged++;
        }

        GameObject gpsXrRoot = gpsScene.GetRootGameObjects()
            .FirstOrDefault(g => g != null && g.name == "XR Origin");
        Transform hybridXrTf = outdoor.Find("XR Origin");
        CopySimpleGPSTrackerFromGpsXrToHybridXr(gpsXrRoot, hybridXrTf != null ? hybridXrTf.gameObject : null);

        EditorSceneManager.CloseScene(gpsScene, removeScene: true);

        RewireOutdoorGpsReferences(hybridScene, outdoor);

        EditorSceneManager.MarkSceneDirty(hybridScene);
        if (saveScene)
            EditorSceneManager.SaveScene(hybridScene);

        Debug.Log(
            $"[HybridGPSMap] Outdoor replaced: {merged} root object(s) from GPSMapPlane " +
            $"(skipped roots: {string.Join(", ", GpsMapPlaneRootsSkipped)}).");
        return true;
    }

    /// <summary>
    /// On GPSMapPlane, <see cref="SimpleGPSTracker"/> lives on the XR Origin GameObject, which we do not paste.
    /// Copy component settings onto Hybrid's outdoor XR Origin, then <see cref="RewireOutdoorGpsReferences"/> fixes refs.
    /// </summary>
    private static void CopySimpleGPSTrackerFromGpsXrToHybridXr(GameObject gpsXrRoot, GameObject hybridXrRoot)
    {
        if (gpsXrRoot == null || hybridXrRoot == null)
        {
            Debug.LogError("[HybridGPSMap] Cannot copy SimpleGPSTracker: missing XR Origin in GPS or Hybrid outdoor.");
            return;
        }

        SimpleGPSTracker src = gpsXrRoot.GetComponent<SimpleGPSTracker>();
        if (src == null)
        {
            Debug.LogError("[HybridGPSMap] GPSMapPlane XR Origin has no SimpleGPSTracker.");
            return;
        }

        SimpleGPSTracker dst = hybridXrRoot.GetComponent<SimpleGPSTracker>();
        if (dst == null)
            dst = hybridXrRoot.AddComponent<SimpleGPSTracker>();

        EditorUtility.CopySerialized(src, dst);
        EditorUtility.SetDirty(dst);
        Debug.Log("[HybridGPSMap] Copied SimpleGPSTracker from GPSMapPlane XR Origin onto Hybrid outdoor XR Origin.");
    }

    /// <summary>
    /// Removes legacy outdoor navigation (GPSMarker-era minimap, lines, managers, prefab stubs, …).
    /// Keeps XR / Session / Hybrid UI / sun only.
    /// </summary>
    private static void RemoveNonShellOutdoorChildren(Transform outdoor)
    {
        for (int i = outdoor.childCount - 1; i >= 0; i--)
        {
            Transform ch = outdoor.GetChild(i);
            if (OutdoorShellPreservedChildNames.Contains(ch.name))
                continue;

            Object.DestroyImmediate(ch.gameObject);
        }
    }

    private static void RewireOutdoorGpsReferences(Scene hybridScene, Transform outdoor)
    {
        Transform xrTf = outdoor.Find("XR Origin")?.transform;
        if (xrTf == null)
        {
            Debug.LogError("[HybridGPSMap] Could not find OutdoorEnvironment/XR Origin for rewiring.");
            return;
        }

        Camera mainArCam = xrTf.GetComponentsInChildren<Camera>(true)
                .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"))
            ?? xrTf.GetComponentInChildren<Camera>(true);

        if (mainArCam == null)
            Debug.LogError("[HybridGPSMap] No MainCamera-tagged camera under XR Origin.");

        SimpleGPSTracker tracker = xrTf != null ? xrTf.GetComponent<SimpleGPSTracker>() : null;
        if (tracker == null)
            tracker = outdoor.GetComponentInChildren<SimpleGPSTracker>(true);
        if (tracker != null)
        {
            tracker.xrOrigin = xrTf;
            SerializedObject sto = new SerializedObject(tracker);
            SerializedProperty arProp = sto.FindProperty("arCamera");
            if (arProp != null && mainArCam != null)
            {
                arProp.objectReferenceValue = mainArCam;
                sto.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(tracker);
        }
        else
        {
            Debug.LogWarning("[HybridGPSMap] No SimpleGPSTracker under OutdoorEnvironment after merge.");
        }

        ARPathFinder pathFinder = outdoor.GetComponentInChildren<ARPathFinder>(true);
        if (pathFinder != null)
        {
            pathFinder.xrOrigin = xrTf;
            pathFinder.arCamera = mainArCam;

            SerializedObject spo = new SerializedObject(pathFinder);
            SerializedProperty gpsProp = spo.FindProperty("navigationGpsTracker");
            if (gpsProp != null && tracker != null)
                gpsProp.objectReferenceValue = tracker;

            spo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pathFinder);
        }

        foreach (MinimapTopDownCamera mmc in outdoor.GetComponentsInChildren<MinimapTopDownCamera>(true))
        {
            SerializedObject smo = new SerializedObject(mmc);
            SerializedProperty fo = smo.FindProperty("followCameraOverride");
            if (fo != null && mainArCam != null && fo.objectReferenceValue == null)
            {
                fo.objectReferenceValue = mainArCam;
                smo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mmc);
            }
        }

        HybridModeController hmc = Resources.FindObjectsOfTypeAll<HybridModeController>()
            .FirstOrDefault(h => h != null && h.gameObject.scene == hybridScene);

        if (hmc != null && mainArCam != null)
        {
            SerializedObject shm = new SerializedObject(hmc);
            SerializedProperty camProp = shm.FindProperty("outdoorMainCamera");
            if (camProp != null)
            {
                camProp.objectReferenceValue = mainArCam;
                shm.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(hmc);
        }
    }

    internal static bool RebakeOutdoorGroundNavMesh_Internal()
    {
        Transform outdoor = GameObject.Find("OutdoorEnvironment")?.transform;
        if (outdoor == null)
        {
            Debug.LogError("[HybridGPSMap] OutdoorEnvironment not found for NavMesh bake.");
            return false;
        }

        Transform groundTf = null;
        foreach (Transform t in outdoor.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Ground")
            {
                groundTf = t;
                break;
            }
        }

        if (groundTf == null)
        {
            Debug.LogError("[HybridGPSMap] Rebake aborted: Ground not found under OutdoorEnvironment.");
            return false;
        }

        NavMeshSurface surface = groundTf.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = groundTf.GetComponentInChildren<NavMeshSurface>(true);

        if (surface == null)
        {
            Debug.LogError("[HybridGPSMap] Ground has no NavMeshSurface.");
            return false;
        }

        surface.BuildNavMesh();
        EditorUtility.SetDirty(surface);
        EditorUtility.SetDirty(groundTf.gameObject);
        return true;
    }

    private static bool EnsureNotPlaying()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[HybridGPSMap] Stop Play Mode before running this menu.");
            return false;
        }

        return true;
    }
}
