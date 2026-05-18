#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-click fix for HybridGPSMap.unity after switching from MYPHUMAP to BKMAP and after
/// the per-environment AR rig restructure. Wires references that MCP could not set,
/// updates HybridModeController.outdoorOnlyVisualRoots to use BKMAP, and prints a
/// minimap RenderTexture sanity report.
/// Run from Unity: Tools/TestAR/HybridGPSMap/Fix References And Map Switch
/// </summary>
public static class HybridGPSMapFixReferences
{
    private const string HybridScenePath = "Assets/Scenes/HybridGPSMap.unity";

    [MenuItem("Tools/TestAR/HybridGPSMap/Fix References And Map Switch")]
    public static void FixReferencesAndMapSwitch()
    {
        Scene scene = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        Undo.IncrementCurrentGroup();

        GameObject hybridRuntime = FindRoot(scene, "HybridRuntime");
        GameObject mainScreen    = FindRoot(scene, "MainScreen");
        GameObject indoorEnv     = FindRoot(scene, "IndoorEnvironment");
        GameObject outdoorEnv    = FindRoot(scene, "OutdoorEnvironment");

        if (hybridRuntime == null || outdoorEnv == null || indoorEnv == null || mainScreen == null)
        {
            Debug.LogError("[FixReferences] Missing one of root objects: HybridRuntime / MainScreen / IndoorEnvironment / OutdoorEnvironment.");
            return;
        }

        Camera indoorCam  = FindCameraByName(indoorEnv,  "ARCamera");
        Camera outdoorCam = FindCameraByName(outdoorEnv, "Main Camera");

        GameObject bkmap                = FindChildByName(outdoorEnv, "BKMAP");
        GameObject myphumap             = FindChildByName(outdoorEnv, "MYPHUMAP");
        GameObject outdoorNavigationUI  = FindChildByName(outdoorEnv, "OutdoorNavigationUI");

        // --- 1. HybridModeController references + map list swap ---
        HybridModeController hmc = hybridRuntime.GetComponent<HybridModeController>();
        if (hmc != null)
        {
            SerializedObject so = new SerializedObject(hmc);

            if (indoorCam != null)
                so.FindProperty("indoorMainCamera").objectReferenceValue = indoorCam;
            else
                Debug.LogWarning("[FixReferences] indoor ARCamera not found under IndoorEnvironment.");

            if (outdoorCam != null)
                so.FindProperty("outdoorMainCamera").objectReferenceValue = outdoorCam;
            else
                Debug.LogWarning("[FixReferences] outdoor Main Camera not found under OutdoorEnvironment.");

            // outdoorOnlyVisualRoots: remove MYPHUMAP, add BKMAP.
            SerializedProperty list = so.FindProperty("outdoorOnlyVisualRoots");
            if (list != null && list.isArray)
            {
                ReplaceInGameObjectList(list, myphumap, bkmap);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[FixReferences] HybridModeController references wired (indoorMainCamera, outdoorMainCamera, outdoorOnlyVisualRoots).");
        }

        // --- 2. HybridOutdoorNavigationRoot.outdoorNavigationContentRoot ---
        HybridOutdoorNavigationRoot honr = hybridRuntime.GetComponent<HybridOutdoorNavigationRoot>();
        if (honr != null && outdoorNavigationUI != null)
        {
            SerializedObject so = new SerializedObject(honr);
            so.FindProperty("outdoorNavigationContentRoot").objectReferenceValue = outdoorNavigationUI;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[FixReferences] HybridOutdoorNavigationRoot.outdoorNavigationContentRoot wired to OutdoorNavigationUI.");
        }

        // --- 3. NavigationManager.hybridModeController on MainScreen ---
        NavigationManager nav = mainScreen.GetComponent<NavigationManager>();
        if (nav != null && hmc != null)
        {
            SerializedObject so = new SerializedObject(nav);
            SerializedProperty field = so.FindProperty("hybridModeController");
            if (field != null)
            {
                field.objectReferenceValue = hmc;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[FixReferences] NavigationManager.hybridModeController wired to HybridRuntime.");
            }
        }

        // --- 4. ARPageController.hybridModeController if present ---
        GameObject arPageCtrlGO = FindRoot(scene, "ARPageController");
        if (arPageCtrlGO != null)
        {
            ARPageController arpc = arPageCtrlGO.GetComponent<ARPageController>();
            if (arpc != null && hmc != null)
            {
                SerializedObject so = new SerializedObject(arpc);
                SerializedProperty field = so.FindProperty("hybridModeController");
                if (field != null)
                {
                    field.objectReferenceValue = hmc;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[FixReferences] ARPageController.hybridModeController wired to HybridRuntime.");
                }
            }
        }

        // --- 5. Toggle BKMAP active / MYPHUMAP inactive (idempotent) ---
        if (bkmap != null && !bkmap.activeSelf)
        {
            bkmap.SetActive(true);
            Debug.Log("[FixReferences] BKMAP set active.");
        }
        if (myphumap != null && myphumap.activeSelf)
        {
            myphumap.SetActive(false);
            Debug.Log("[FixReferences] MYPHUMAP set inactive.");
        }

        // --- 6. Minimap sanity check ---
        ReportMinimapPipeline(outdoorEnv);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixReferences] HybridGPSMap saved.");
    }

    private static void ReplaceInGameObjectList(SerializedProperty list, GameObject remove, GameObject add)
    {
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            Object cur = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (cur != null && remove != null && cur == remove)
                list.DeleteArrayElementAtIndex(i);
        }

        if (add == null) return;

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == add)
                return;
        }

        int idx = list.arraySize;
        list.InsertArrayElementAtIndex(idx);
        list.GetArrayElementAtIndex(idx).objectReferenceValue = add;
        Debug.Log($"[FixReferences] outdoorOnlyVisualRoots updated: removed MYPHUMAP, added {add.name}.");
    }

    private static void ReportMinimapPipeline(GameObject outdoorEnv)
    {
        Camera minimapCam = null;
        foreach (Camera c in outdoorEnv.GetComponentsInChildren<Camera>(true))
        {
            if (c.gameObject.name.Contains("Minimap")) { minimapCam = c; break; }
        }

        List<RawImage> minimapRaws = new List<RawImage>();
        foreach (RawImage r in outdoorEnv.GetComponentsInChildren<RawImage>(true))
        {
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("minimap") || n.Contains("mini map") || n == "minimap view") minimapRaws.Add(r);
        }

        if (minimapCam == null)
        {
            Debug.LogWarning("[FixReferences/Minimap] Minimap Camera not found inside OutdoorEnvironment.");
        }
        else
        {
            if (minimapCam.targetTexture == null)
                Debug.LogWarning($"[FixReferences/Minimap] '{minimapCam.name}'.targetTexture is NULL.");
            else
                Debug.Log($"[FixReferences/Minimap] '{minimapCam.name}'.targetTexture = {minimapCam.targetTexture.name}");

            int mapPlaneLayer = LayerMask.NameToLayer("MapPlane");
            if (mapPlaneLayer >= 0 && (minimapCam.cullingMask & (1 << mapPlaneLayer)) == 0)
                Debug.LogWarning("[FixReferences/Minimap] Minimap Camera culling mask does NOT include 'MapPlane' layer.");
        }

        if (minimapRaws.Count == 0)
        {
            Debug.LogWarning("[FixReferences/Minimap] No Minimap RawImage found.");
        }
        foreach (RawImage r in minimapRaws)
        {
            string path = GetHierarchyPath(r.transform);
            if (r.texture == null)
                Debug.LogWarning($"[FixReferences/Minimap] RawImage '{path}'.texture is NULL.");
            else
                Debug.Log($"[FixReferences/Minimap] RawImage '{path}'.texture = {r.texture.name}");
        }

        if (minimapCam != null && minimapCam.targetTexture != null && minimapRaws.Count > 0)
        {
            bool anyMatches = false;
            foreach (RawImage r in minimapRaws)
                if (r.texture == minimapCam.targetTexture) { anyMatches = true; break; }
            if (!anyMatches)
                Debug.LogWarning("[FixReferences/Minimap] No RawImage texture matches Minimap Camera.targetTexture. Verify both reference the same RenderTexture asset.");
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    private static GameObject FindChildByName(GameObject parent, string name)
    {
        Transform[] all = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
            if (t.name == name) return t.gameObject;
        return null;
    }

    private static Camera FindCameraByName(GameObject parent, string name)
    {
        Camera[] cams = parent.GetComponentsInChildren<Camera>(true);
        foreach (Camera c in cams)
            if (c.gameObject.name == name) return c;
        return null;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
#endif
