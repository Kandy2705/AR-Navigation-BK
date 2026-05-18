using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tự động gán tất cả Inspector references còn null trong HybridGPSMap scene.
/// Menu: Tools → Fix AR → Auto Wire All References
/// </summary>
public static class AutoWireReferences
{
    [MenuItem("Tools/Fix AR/Auto Wire All References")]
    public static void Execute()
    {
        int fixed_count = 0;
        string log = "";

        // ── 1. SimpleGPSTracker ───────────────────────────────────────────────
        SimpleGPSTracker gpsTracker = Object.FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
        if (gpsTracker != null)
        {
            SerializedObject gpsSO = new SerializedObject(gpsTracker);

            SerializedProperty xrOriginProp = gpsSO.FindProperty("xrOrigin");
            if (xrOriginProp != null && xrOriginProp.objectReferenceValue == null)
            {
                xrOriginProp.objectReferenceValue = gpsTracker.transform;
                gpsSO.ApplyModifiedProperties();
                log += "✅ SimpleGPSTracker.xrOrigin → self transform\n";
                fixed_count++;
            }

            Camera mainCam = FindCameraByName("Main Camera");
            SerializedProperty arCamProp = gpsSO.FindProperty("arCamera");
            if (arCamProp != null && arCamProp.objectReferenceValue == null && mainCam != null)
            {
                arCamProp.objectReferenceValue = mainCam;
                gpsSO.ApplyModifiedProperties();
                log += $"✅ SimpleGPSTracker.arCamera → {mainCam.name}\n";
                fixed_count++;
            }
        }
        else log += "⚠️ SimpleGPSTracker not found\n";

        // ── 2. ARPathFinder ───────────────────────────────────────────────────
        ARPathFinder pathFinder = Object.FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
        if (pathFinder != null)
        {
            if (gpsTracker != null)
            {
                SerializedObject so = new SerializedObject(pathFinder);
                SerializedProperty prop = so.FindProperty("navigationGpsTracker");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = gpsTracker;
                    so.ApplyModifiedProperties();
                    log += "✅ ARPathFinder.navigationGpsTracker → SimpleGPSTracker\n";
                    fixed_count++;
                }
            }
        }
        else log += "⚠️ ARPathFinder not found\n";

        // ── 3. HybridOutdoorNavigationRoot ────────────────────────────────────
        HybridOutdoorNavigationRoot outdoorRoot = Object.FindFirstObjectByType<HybridOutdoorNavigationRoot>(FindObjectsInactive.Include);
        if (outdoorRoot != null)
        {
            SerializedObject so = new SerializedObject(outdoorRoot);

            // outdoorNavigationContentRoot = OutdoorNavigationUI
            SerializedProperty contentProp = so.FindProperty("outdoorNavigationContentRoot");
            if (contentProp != null && contentProp.objectReferenceValue == null)
            {
                GameObject outdoorNavUI = GameObject.Find("OutdoorNavigationUI");
                if (outdoorNavUI != null)
                {
                    contentProp.objectReferenceValue = outdoorNavUI;
                    log += "✅ HybridOutdoorNavigationRoot.outdoorNavigationContentRoot → OutdoorNavigationUI\n";
                    fixed_count++;
                }
                else log += "⚠️ OutdoorNavigationUI not found for HybridOutdoorNavigationRoot\n";
            }

            so.ApplyModifiedProperties();
        }
        else log += "⚠️ HybridOutdoorNavigationRoot not found\n";

        // ── 4. HybridModeController: indoorMainCamera + outdoorMainCamera ────
        HybridModeController hybridCtrl = Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybridCtrl != null)
        {
            SerializedObject so = new SerializedObject(hybridCtrl);

            SerializedProperty indoorCamProp = so.FindProperty("indoorMainCamera");
            if (indoorCamProp != null && indoorCamProp.objectReferenceValue == null)
            {
                Camera arCam = FindCameraByName("ARCamera");
                if (arCam != null)
                {
                    indoorCamProp.objectReferenceValue = arCam;
                    log += $"✅ HybridModeController.indoorMainCamera → {arCam.name}\n";
                    fixed_count++;
                }
            }

            SerializedProperty outdoorCamProp = so.FindProperty("outdoorMainCamera");
            if (outdoorCamProp != null && outdoorCamProp.objectReferenceValue == null)
            {
                Camera mainCam = FindCameraByName("Main Camera");
                if (mainCam != null)
                {
                    outdoorCamProp.objectReferenceValue = mainCam;
                    log += $"✅ HybridModeController.outdoorMainCamera → {mainCam.name}\n";
                    fixed_count++;
                }
            }

            so.ApplyModifiedProperties();
        }

        // ── 5. NavigationManager.hybridModeController ─────────────────────────
        NavigationManager navMgr = Object.FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include);
        if (navMgr != null && hybridCtrl != null)
        {
            SerializedObject so = new SerializedObject(navMgr);
            SerializedProperty prop = so.FindProperty("hybridModeController");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = hybridCtrl;
                so.ApplyModifiedProperties();
                log += "✅ NavigationManager.hybridModeController → HybridModeController\n";
                fixed_count++;
            }
        }

        // ── 6. ARPageController.hybridModeController ──────────────────────────
        ARPageController arPageCtrl = Object.FindFirstObjectByType<ARPageController>(FindObjectsInactive.Include);
        if (arPageCtrl != null && hybridCtrl != null)
        {
            SerializedObject so = new SerializedObject(arPageCtrl);
            SerializedProperty prop = so.FindProperty("hybridModeController");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = hybridCtrl;
                so.ApplyModifiedProperties();
                log += "✅ ARPageController.hybridModeController → HybridModeController\n";
                fixed_count++;
            }
        }

        // ── 7. NavigationControllerSetup: add if missing ──────────────────────
        NavigationController sdkNavCtrl = Object.FindFirstObjectByType<NavigationController>(FindObjectsInactive.Include);
        if (sdkNavCtrl != null)
        {
            if (sdkNavCtrl.GetComponent<NavigationControllerSetup>() == null)
            {
                sdkNavCtrl.gameObject.AddComponent<NavigationControllerSetup>();
                EditorUtility.SetDirty(sdkNavCtrl.gameObject);
                log += "✅ NavigationControllerSetup added to NavigationController GO\n";
                fixed_count++;
            }
            else
            {
                log += "ℹ️  NavigationControllerSetup đã có rồi\n";
            }
        }
        else log += "⚠️ NavigationController (SDK) not found\n";

        // ── Save ──────────────────────────────────────────────────────────────
        if (fixed_count > 0)
        {
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        string summary = fixed_count > 0
            ? $"Đã sửa {fixed_count} references. Nhớ Ctrl+S save scene.\n\n"
            : "Tất cả references đã được assign trước đó.\n\n";

        EditorUtility.DisplayDialog(
            fixed_count > 0 ? "Auto Wire — Hoàn thành" : "Auto Wire — Không có gì cần sửa",
            summary + log,
            "OK");

        Debug.Log("[AutoWireReferences] Kết quả:\n" + log);
    }

    [MenuItem("Tools/Fix AR/Auto Wire All References", true)]
    public static bool Validate() =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().IsValid();

    private static Camera FindCameraByName(string name)
    {
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam.name == name) return cam;
        return null;
    }
}
