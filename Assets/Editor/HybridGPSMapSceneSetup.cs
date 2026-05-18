#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// One-click setup for <c>HybridGPSMap.unity</c>: clone <c>MainScreen</c> from Khanh-UI (app shell),
/// build outdoor navigation under <c>OutdoorNavigationUI</c>, wire <see cref="HybridOutdoorNavigationRoot"/>,
/// and tune <see cref="HybridModeController"/> flags. Run from Unity: Tools/TestAR/HybridGPSMap/...
/// Hybrid rig: if AR Session and XROrigin live on different objects, set <see cref="HybridModeController"/>
/// <c>outdoorXrRigRootOverride</c> to a root that includes both so one rig survives mode switching.
/// Startup: tool sets <see cref="HybridModeController.initialMode"/> to Outdoor and enables
/// <c>activateInitialModeOnStart</c>. When <see cref="NavigationManager"/> is active in the scene,
/// <see cref="HybridModeController"/> now calls <c>EnterARPage()</c> after one frame (same as tapping AR)
/// so the AR stack is not left off (avoids a black screen).
/// </summary>
public static class HybridGPSMapSceneSetup
{
    private const string HybridScenePath = "Assets/Scenes/HybridGPSMap.unity";
    private const string KhanhUiTemplatePath = "Assets/Scene/Khanh-UI.unity";

    [MenuItem("Tools/TestAR/HybridGPSMap/Setup App Shell + Outdoor Navigation Hierarchy")]
    public static void SetupAppShellAndOutdoorHierarchy()
    {
        Scene hybrid = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        Undo.IncrementCurrentGroup();

        CopyMainScreenIfMissing(hybrid);
        EnsureHybridModeFlags();
        EnsureGpsMarkerDebugUiOffForCleanHud();
        EnsureEventSystem();
        EnsureOutdoorNavigationStack();

        EditorSceneManager.MarkSceneDirty(hybrid);
        EditorSceneManager.SaveScene(hybrid);
        Debug.Log("[HybridGPSMapSceneSetup] HybridGPSMap saved (MainScreen if added, OutdoorNavigationUI, gate, HybridMode flags).");
    }

    /// <summary>Open HybridGPSMap and save: Outdoor on launch; <see cref="HybridModeController"/> enters AR via NavigationManager when shell is active.</summary>
    [MenuItem("Tools/TestAR/HybridGPSMap/Set Startup: Outdoor On Launch")]
    public static void SetStartupOutdoorOnLaunch()
    {
        Scene hybrid = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        HybridModeController hmc =
            Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc == null)
        {
            Debug.LogWarning("[HybridGPSMapSceneSetup] HybridModeController not found — cannot set startup mode.");
            return;
        }

        SerializedObject so = new SerializedObject(hmc);
        ApplyOutdoorStartupHybridFlags(so);
        so.ApplyModifiedProperties();
        Undo.RecordObject(hmc, "HybridGPSMap startup Outdoor");
        EditorUtility.SetDirty(hmc);
        EditorSceneManager.MarkSceneDirty(hybrid);
        EditorSceneManager.SaveScene(hybrid);
        Debug.Log("[HybridGPSMapSceneSetup] Saved: initialMode=Outdoor, activateInitialModeOnStart=true.");
    }

    private static void CopyMainScreenIfMissing(Scene hybrid)
    {
        if (FindRootNamed(hybrid, "MainScreen") != null)
        {
            return;
        }

        Scene khanh = EditorSceneManager.OpenScene(KhanhUiTemplatePath, OpenSceneMode.Additive);
        GameObject template = FindRootNamed(khanh, "MainScreen");

        if (template == null)
        {
            Debug.LogError($"[HybridGPSMapSceneSetup] No root GameObject named 'MainScreen' in {KhanhUiTemplatePath}");
            EditorSceneManager.CloseScene(khanh, true);
            return;
        }

        GameObject clone = Object.Instantiate(template);
        clone.name = "MainScreen";
        Undo.RegisterCreatedObjectUndo(clone, "HybridGPSMap MainScreen shell");
        SceneManager.MoveGameObjectToScene(clone, hybrid);
        EditorSceneManager.CloseScene(khanh, true);
    }

    private static GameObject FindRootNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root.gameObject;
            }
        }

        return null;
    }

    private static void EnsureHybridModeFlags()
    {
        HybridModeController hmc =
            Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc == null)
        {
            Debug.LogWarning("[HybridGPSMapSceneSetup] HybridModeController not found — skipping flags.");
            return;
        }

        SerializedObject so = new SerializedObject(hmc);

        ApplyOutdoorStartupHybridFlags(so);

        SetBool(so, "createSharedOutdoorHud", false);
        SetBool(so, "createRuntimeModeSwitcher", true);
        SetBool(so, "anchorRuntimeModeSwitcherAtBottom", true);
        SetBool(so, "showRuntimeModeSwitcherStatusLine", false);
        SetVector2(so, "runtimeModeSwitcherOffset", new Vector2(0f, 22f));
        SetBool(so, "disableIndoorXROriginDuplicates", true);
        SetBool(so, "disableIndoorARSessionDuplicates", true);

        so.ApplyModifiedProperties();
        Undo.RecordObject(hmc, "HybridMode HybridGPSMap");
        EditorUtility.SetDirty(hmc);
    }

    /// <summary>Outdoor first on app open: user asked path/GPS stack to run immediately instead of Indoor/Transition.</summary>
    private static void ApplyOutdoorStartupHybridFlags(SerializedObject so)
    {
        SerializedProperty modeProp = so.FindProperty("initialMode");
        if (modeProp != null)
        {
            modeProp.enumValueIndex = (int)HybridModeController.HybridMode.Outdoor;
        }

        SetBool(so, "activateInitialModeOnStart", true);
    }

    private static void SetBool(SerializedObject so, string propName, bool value)
    {
        SerializedProperty p = so.FindProperty(propName);
        if (p != null && p.propertyType == SerializedPropertyType.Boolean)
        {
            p.boolValue = value;
        }
    }

    private static void SetVector2(SerializedObject so, string propName, Vector2 value)
    {
        SerializedProperty p = so.FindProperty(propName);
        if (p != null && p.propertyType == SerializedPropertyType.Vector2)
        {
            p.vector2Value = value;
        }
    }

    private static void EnsureGpsMarkerDebugUiOffForCleanHud()
    {
        GPSMarker[] markers = Object.FindObjectsByType<GPSMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (GPSMarker marker in markers)
        {
            SerializedObject gso = new SerializedObject(marker);
            SetBool(gso, "showEnvironmentTransformOverlay", false);
            SetBool(gso, "showRuntimeUpdateIndicator", false);
            SetBool(gso, "appendTransformDiagToGpsHud", false);
            gso.ApplyModifiedProperties();
            Undo.RecordObject(marker, "HybridGPSMap GPSMarker clean HUD");
            EditorUtility.SetDirty(marker);
        }
    }

    /// <summary>
    /// Parent <c>OutdoorNavigationUI</c> under <c>OutdoorEnvironment</c> — valid; child deactivates when parent is off in Transition.
    /// </summary>
    private static void TryParentOutdoorNavigationUnderEnvironment(GameObject outdoorNavUi)
    {
        if (outdoorNavUi == null)
        {
            return;
        }

        GameObject outdoorEnv = GameObject.Find("OutdoorEnvironment");
        if (outdoorEnv == null ||
            outdoorNavUi.transform.parent == outdoorEnv.transform)
        {
            return;
        }

        Undo.RecordObject(outdoorNavUi.transform, "OutdoorNavigationUI under OutdoorEnvironment");
        outdoorNavUi.transform.SetParent(outdoorEnv.transform, false);
        EditorUtility.SetDirty(outdoorNavUi);
    }

    private static void EnsureOutdoorNavigationStack()
    {
        HybridModeController hmc =
            Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc == null)
        {
            return;
        }

        GameObject outdoorRoot = GameObject.Find("OutdoorNavigationUI");
        if (outdoorRoot == null)
        {
            outdoorRoot = new GameObject("OutdoorNavigationUI");
            Undo.RegisterCreatedObjectUndo(outdoorRoot, "OutdoorNavigationUI");
        }

        TryParentOutdoorNavigationUnderEnvironment(outdoorRoot);

        outdoorRoot.SetActive(false);

        HybridOutdoorNavigationRoot gate = hmc.GetComponent<HybridOutdoorNavigationRoot>();
        if (gate == null)
        {
            gate = Undo.AddComponent<HybridOutdoorNavigationRoot>(hmc.gameObject);
        }

        SerializedObject gateSo = new SerializedObject(gate);
        SerializedProperty hmProp = gateSo.FindProperty("hybridModeController");
        if (hmProp != null)
        {
            hmProp.objectReferenceValue = hmc;
        }

        SerializedProperty outdoorProp = gateSo.FindProperty("outdoorNavigationContentRoot");
        if (outdoorProp != null)
        {
            outdoorProp.objectReferenceValue = outdoorRoot;
        }

        gateSo.ApplyModifiedProperties();
        Undo.RecordObject(gate, "HybridOutdoorNavigationRoot wires");

        BuildOutdoorNavigationHierarchy(outdoorRoot);

        MobileNavigationHUD navHud =
            outdoorRoot.GetComponentInChildren<MobileNavigationHUD>(true);
        if (navHud != null)
        {
            SerializedObject hudSo = new SerializedObject(navHud);
            SetBool(hudSo, "showProximityRefinementHint", false);
            hudSo.ApplyModifiedProperties();
            Undo.RecordObject(navHud, "MobileNavigationHUD compact");
            EditorUtility.SetDirty(navHud);
        }

        Undo.RecordObject(outdoorRoot, "Outdoor navigation stack");

        EnsureNavigationProximity();
    }

    private static void EnsureNavigationProximity()
    {
        SimpleGPSTracker tracker =
            Object.FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
        if (tracker == null ||
            tracker.GetComponent<NavigationProximityRefinement>() != null)
        {
            return;
        }

        Undo.AddComponent<NavigationProximityRefinement>(tracker.gameObject);
        EditorUtility.SetDirty(tracker.gameObject);
    }

    private static void BuildOutdoorNavigationHierarchy(GameObject outdoorRoot)
    {
        if (outdoorRoot.GetComponentInChildren<MobileNavigationHUD>(true) == null)
        {
            MobileNavigationHUD hud = MobileNavigationHUD.InstantiateOutdoorHudInHierarchy(
                outdoorRoot.transform,
                outdoorRoot.transform);
            Undo.RegisterCreatedObjectUndo(hud.gameObject, "Outdoor MobileNavigationHUD");
            EditorUtility.SetDirty(hud.gameObject);
        }

        if (outdoorRoot.GetComponentInChildren<GPSStartupOverlay>(true) == null)
        {
            GPSStartupOverlay overlay = GPSStartupOverlay.CreateOutdoorOverlayInHierarchy(
                outdoorRoot.transform,
                false);
            Undo.RegisterCreatedObjectUndo(overlay.gameObject, "Outdoor GPSStartupOverlay");
            EditorUtility.SetDirty(overlay.gameObject);
        }

        MinimapTopDownCamera topCam =
            Object.FindFirstObjectByType<MinimapTopDownCamera>(FindObjectsInactive.Include);

        if (outdoorRoot.GetComponentInChildren<MinimapHUD>(true) == null && topCam == null)
        {
            GameObject minimapHolder = new GameObject("Minimap HUD");
            Undo.RegisterCreatedObjectUndo(minimapHolder, "Outdoor MinimapHUD");
            minimapHolder.transform.SetParent(outdoorRoot.transform, false);
            Undo.AddComponent<MinimapHUD>(minimapHolder);
            EditorUtility.SetDirty(minimapHolder);
        }

        EditorUtility.SetDirty(outdoorRoot);
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────────
    // Map switch: BKMAP → MYPHUMAP + hybrid flags for always-active ARPathFinder + no black screen
    // ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates MYPHUMAP, deactivates BKMAP, sets keepOutdoorActiveWhileIndoor=true so
    /// ARPathFinder never loses activeInHierarchy during Indoor mode, and wires MYPHUMAP into
    /// outdoorOnlyVisualRoots so its 3-D geometry hides in Indoor while the path ribbon keeps
    /// updating. Also verifies the dual-AR-Session guard flags.
    /// Run once from Unity: Tools/TestAR/HybridGPSMap/Switch Map to MYPHUMAP (hybrid setup)
    /// </summary>
    [MenuItem("Tools/TestAR/HybridGPSMap/Switch Map to MYPHUMAP (hybrid setup)")]
    public static void SwitchToMyPhuMap()
    {
        Scene hybrid = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        Undo.IncrementCurrentGroup();

        // 1. Toggle map GameObjects
        ToggleMapObject("BKMAP",    active: false);
        ToggleMapObject("MYPHUMAP", active: true);

        // 2. Wire HybridModeController flags
        HybridModeController hmc =
            Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc == null)
        {
            Debug.LogError("[HybridGPSMapSetup] HybridModeController not found.");
            return;
        }

        SerializedObject so = new SerializedObject(hmc);

        // Path ribbon stays alive: keep OutdoorEnvironment (+ NavigationManager) always active.
        SetBool(so, "keepOutdoorActiveWhileIndoor", true);

        // Single-AR-Session guard: disable indoor AR Session & XR Origin when outdoor stack is up.
        SetBool(so, "disableIndoorARSessionDuplicates", true);
        SetBool(so, "disableIndoorXROriginDuplicates",  true);

        // XR Origin must survive OutdoorEnvironment.SetActive(false) in Indoor mode.
        SetBool(so, "detachOutdoorXrRigFromEnvironment", true);

        // 3. Add MYPHUMAP to outdoorOnlyVisualRoots so its 3-D geometry hides in Indoor mode
        //    while ARPathFinder (under NavigationManager, NOT in this list) keeps updating.
        GameObject myPhuMap = GameObject.Find("MYPHUMAP");
        if (myPhuMap != null)
        {
            SerializedProperty listProp = so.FindProperty("outdoorOnlyVisualRoots");
            if (listProp != null && listProp.isArray)
            {
                bool alreadyInList = false;
                for (int i = 0; i < listProp.arraySize; i++)
                {
                    if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == myPhuMap)
                    {
                        alreadyInList = true;
                        break;
                    }
                }

                if (!alreadyInList)
                {
                    listProp.arraySize++;
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = myPhuMap;
                    Debug.Log("[HybridGPSMapSetup] Added MYPHUMAP to outdoorOnlyVisualRoots.");
                }
                else
                {
                    Debug.Log("[HybridGPSMapSetup] MYPHUMAP already in outdoorOnlyVisualRoots.");
                }
            }
        }
        else
        {
            Debug.LogWarning("[HybridGPSMapSetup] MYPHUMAP not found in scene — add it manually to outdoorOnlyVisualRoots.");
        }

        so.ApplyModifiedProperties();
        Undo.RecordObject(hmc, "HybridGPSMap switch to MYPHUMAP");
        EditorUtility.SetDirty(hmc);

        EditorSceneManager.MarkSceneDirty(hybrid);
        EditorSceneManager.SaveScene(hybrid);

        Debug.Log("[HybridGPSMapSetup] Done: MYPHUMAP active, BKMAP off, keepOutdoorActiveWhileIndoor=true, dual-session guards set.");
    }

    /// <summary>
    /// Remove BKMAP from outdoorOnlyVisualRoots (in case it was previously added) and clean up.
    /// </summary>
    [MenuItem("Tools/TestAR/HybridGPSMap/Remove BKMAP from outdoorOnlyVisualRoots")]
    public static void RemoveBkMapFromVisualRoots()
    {
        Scene hybrid = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        HybridModeController hmc =
            Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc == null) return;

        SerializedObject so = new SerializedObject(hmc);
        SerializedProperty listProp = so.FindProperty("outdoorOnlyVisualRoots");
        if (listProp == null || !listProp.isArray) return;

        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            Object entry = listProp.GetArrayElementAtIndex(i).objectReferenceValue;
            if (entry != null && entry.name == "BKMAP")
            {
                listProp.DeleteArrayElementAtIndex(i);
                Debug.Log("[HybridGPSMapSetup] Removed BKMAP from outdoorOnlyVisualRoots.");
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(hmc);
        EditorSceneManager.MarkSceneDirty(hybrid);
        EditorSceneManager.SaveScene(hybrid);
    }

    private static void ToggleMapObject(string objectName, bool active)
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            // Also search inactive objects
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                go = FindChildRecursive(root.transform, objectName);
                if (go != null) break;
            }
        }

        if (go == null)
        {
            Debug.LogWarning($"[HybridGPSMapSetup] '{objectName}' not found in scene.");
            return;
        }

        Undo.RecordObject(go, $"Toggle {objectName}");
        go.SetActive(active);
        EditorUtility.SetDirty(go);
        Debug.Log($"[HybridGPSMapSetup] {objectName}.SetActive({active})");
    }

    private static GameObject FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject result = FindChildRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────────
    // Restructure to single shared AR rig
    // ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-click restructure: moves XR Origin + AR Session out of both environments into a single
    /// SharedARRig at scene root. Removes indoor AR duplicates, wires all references, sets
    /// HybridModeController flags so the shared rig is never deactivated by mode switching.
    ///
    /// Run ONCE on HybridGPSMap: Tools/TestAR/HybridGPSMap/Restructure → Single Shared AR Rig
    /// Safe to re-run: skips steps already done.
    /// </summary>
    [MenuItem("Tools/TestAR/HybridGPSMap/Restructure → Single Shared AR Rig")]
    public static void RestructureToSingleSharedARRig()
    {
        Scene hybrid = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        Undo.IncrementCurrentGroup();

        // ── 1. Find or create SharedARRig at scene root ──────────────────────────────────────────
        GameObject sharedRig = FindRootNamed(hybrid, "SharedARRig");
        if (sharedRig == null)
        {
            sharedRig = new GameObject("SharedARRig");
            Undo.RegisterCreatedObjectUndo(sharedRig, "Create SharedARRig");
            SceneManager.MoveGameObjectToScene(sharedRig, hybrid);
        }

        // ── 2. Move AR Session to SharedARRig ────────────────────────────────────────────────────
        // Search everywhere (OutdoorEnvironment, IndoorEnvironment, scene root)
        ARSession[] allSessions = Object.FindObjectsByType<ARSession>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        ARSession primarySession = null;
        foreach (ARSession s in allSessions)
        {
            // Prefer the one already under SharedARRig, else first found
            if (s.transform.IsChildOf(sharedRig.transform)) { primarySession = s; break; }
            if (primarySession == null) primarySession = s;
        }

        if (primarySession != null && primarySession.transform.parent != sharedRig.transform)
        {
            Undo.SetTransformParent(primarySession.transform, sharedRig.transform, "Move AR Session to SharedARRig");
            Debug.Log($"[HybridSetup] Moved AR Session '{primarySession.name}' → SharedARRig");
        }

        // Disable any remaining AR Sessions not in SharedARRig
        foreach (ARSession s in allSessions)
        {
            if (s == primarySession) continue;
            Undo.RecordObject(s, "Disable duplicate ARSession");
            s.enabled = false;
            EditorUtility.SetDirty(s);
            Debug.Log($"[HybridSetup] Disabled duplicate AR Session on '{s.gameObject.name}'");
        }

        // ── 3. Move XR Origin to SharedARRig ─────────────────────────────────────────────────────
        XROrigin[] allXrOrigins = Object.FindObjectsByType<XROrigin>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        XROrigin primaryXr = null;
        foreach (XROrigin xr in allXrOrigins)
        {
            if (xr.transform.IsChildOf(sharedRig.transform)) { primaryXr = xr; break; }
            // Prefer the one currently under OutdoorEnvironment (has SimpleGPSTracker / AR camera set up)
            GameObject outdoorEnv = GameObject.Find("OutdoorEnvironment") ??
                FindInactiveRoot(hybrid, "OutdoorEnvironment");
            if (outdoorEnv != null && xr.transform.IsChildOf(outdoorEnv.transform))
            {
                primaryXr = xr;
                break;
            }
            if (primaryXr == null) primaryXr = xr;
        }

        if (primaryXr != null && primaryXr.transform.parent != sharedRig.transform)
        {
            Undo.SetTransformParent(primaryXr.transform, sharedRig.transform, "Move XR Origin to SharedARRig");
            Debug.Log($"[HybridSetup] Moved XR Origin '{primaryXr.name}' → SharedARRig");
        }

        // Disable any remaining XR Origins not in SharedARRig
        foreach (XROrigin xr in allXrOrigins)
        {
            if (xr == primaryXr) continue;
            Undo.RecordObject(xr, "Disable duplicate XROrigin");
            xr.enabled = false;
            EditorUtility.SetDirty(xr);
            Debug.Log($"[HybridSetup] Disabled duplicate XR Origin on '{xr.gameObject.name}'");
        }

        // ── 4. Resolve shared camera ─────────────────────────────────────────────────────────────
        Camera sharedCam = null;
        if (primaryXr != null)
        {
            sharedCam = primaryXr.GetComponentInChildren<Camera>(true);
            if (sharedCam != null && !sharedCam.CompareTag("MainCamera"))
            {
                Undo.RecordObject(sharedCam.gameObject, "Tag MainCamera");
                sharedCam.tag = "MainCamera";
                EditorUtility.SetDirty(sharedCam.gameObject);
            }
        }

        // ── 5. Set environments inactive ─────────────────────────────────────────────────────────
        SetEnvironmentInactive(hybrid, "OutdoorEnvironment");
        SetEnvironmentInactive(hybrid, "IndoorEnvironment");

        // ── 6. Activate MYPHUMAP, deactivate BKMAP ───────────────────────────────────────────────
        ToggleMapObject("BKMAP",    active: false);
        ToggleMapObject("MYPHUMAP", active: true);

        // ── 7. Wire HybridModeController ─────────────────────────────────────────────────────────
        HybridModeController hmc =
            Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hmc == null)
        {
            Debug.LogError("[HybridSetup] HybridModeController not found.");
        }
        else
        {
            SerializedObject so = new SerializedObject(hmc);

            // Do NOT auto-start outdoor on launch — app has a login screen.
            // GPS activates when the user explicitly navigates to it (call hmc.ForceOutdoor()
            // or hmc.ApplyInitialMode() from the menu button that launches GPS navigation).
            SetBool(so, "activateInitialModeOnStart", false);
            SerializedProperty modeProp = so.FindProperty("initialMode");
            if (modeProp != null)
                modeProp.enumValueIndex = (int)HybridModeController.HybridMode.Outdoor;

            // Shared rig is always at scene root — no need to detach at runtime
            SetBool(so, "detachOutdoorXrRigFromEnvironment", false);

            // No duplicates left → guards no longer needed
            SetBool(so, "disableIndoorARSessionDuplicates", false);
            SetBool(so, "disableIndoorXROriginDuplicates",  false);

            // Keep outdoor active so ARPathFinder never stops
            SetBool(so, "keepOutdoorActiveWhileIndoor", true);

            // Point HMC to the shared outdoor camera
            if (sharedCam != null)
            {
                SerializedProperty camProp = so.FindProperty("outdoorMainCamera");
                if (camProp != null) camProp.objectReferenceValue = sharedCam;
            }

            // Add SharedARRig to alwaysActiveRoots
            SerializedProperty alwaysProp = so.FindProperty("alwaysActiveRoots");
            if (alwaysProp != null && alwaysProp.isArray)
            {
                bool already = false;
                for (int i = 0; i < alwaysProp.arraySize; i++)
                    if (alwaysProp.GetArrayElementAtIndex(i).objectReferenceValue == sharedRig)
                    { already = true; break; }
                if (!already)
                {
                    alwaysProp.arraySize++;
                    alwaysProp.GetArrayElementAtIndex(alwaysProp.arraySize - 1).objectReferenceValue = sharedRig;
                }
            }

            // Remove outdoorXrRigRootOverride (no longer needed)
            SerializedProperty overrideProp = so.FindProperty("outdoorXrRigRootOverride");
            if (overrideProp != null) overrideProp.objectReferenceValue = null;

            // Add MYPHUMAP to outdoorOnlyVisualRoots
            GameObject myPhuMap = FindInactiveRoot(hybrid, "MYPHUMAP") ?? GameObject.Find("MYPHUMAP");
            if (myPhuMap != null)
            {
                SerializedProperty listProp = so.FindProperty("outdoorOnlyVisualRoots");
                if (listProp != null && listProp.isArray)
                {
                    bool found = false;
                    for (int i = 0; i < listProp.arraySize; i++)
                        if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == myPhuMap)
                        { found = true; break; }
                    if (!found)
                    {
                        listProp.arraySize++;
                        listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = myPhuMap;
                    }
                }
            }

            so.ApplyModifiedProperties();
            Undo.RecordObject(hmc, "HybridGPSMap shared rig restructure");
            EditorUtility.SetDirty(hmc);
        }

        // ── 8. Rewire SimpleGPSTracker + ARPathFinder to shared rig ─────────────────────────────
        if (primaryXr != null && sharedCam != null)
        {
            foreach (SimpleGPSTracker tracker in Object.FindObjectsByType<SimpleGPSTracker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(tracker, "Rewire SimpleGPSTracker");
                tracker.xrOrigin = primaryXr.transform;
                SerializedObject tso = new SerializedObject(tracker);
                SerializedProperty arCamProp = tso.FindProperty("arCamera");
                if (arCamProp != null) arCamProp.objectReferenceValue = sharedCam;
                tso.ApplyModifiedProperties();
                EditorUtility.SetDirty(tracker);
                Debug.Log($"[HybridSetup] Rewired SimpleGPSTracker on '{tracker.gameObject.name}'");
            }

            foreach (ARPathFinder pf in Object.FindObjectsByType<ARPathFinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(pf, "Rewire ARPathFinder");
                pf.xrOrigin  = primaryXr.transform;
                pf.arCamera  = sharedCam;
                EditorUtility.SetDirty(pf);
                Debug.Log($"[HybridSetup] Rewired ARPathFinder on '{pf.gameObject.name}'");
            }
        }

        EditorSceneManager.MarkSceneDirty(hybrid);
        EditorSceneManager.SaveScene(hybrid);

        Debug.Log("[HybridSetup] Done — single SharedARRig. Hierarchy:\n" +
                  "  SharedARRig/AR Session\n" +
                  "  SharedARRig/XR Origin/Camera Offset/Main Camera\n" +
                  "  OutdoorEnvironment (inactive, no XR rig)\n" +
                  "  IndoorEnvironment  (inactive, no XR rig)");
    }

    private static void SetEnvironmentInactive(Scene scene, string name)
    {
        GameObject env = FindRootNamed(scene, name) ?? FindInactiveRoot(scene, name);
        if (env == null) { Debug.LogWarning($"[HybridSetup] '{name}' not found."); return; }
        if (!env.activeSelf) { Debug.Log($"[HybridSetup] '{name}' already inactive."); return; }
        Undo.RecordObject(env, $"Deactivate {name}");
        env.SetActive(false);
        EditorUtility.SetDirty(env);
        Debug.Log($"[HybridSetup] {name} → inactive");
    }

    private static GameObject FindInactiveRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = FindChildRecursive(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                Undo.AddComponent<InputSystemUIInputModule>(existing.gameObject)
                    .AssignDefaultActions();
            }
            else
            {
                existing.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Undo.RecordObject(existing, "HybridGPSMap EventSystem");
            EditorUtility.SetDirty(existing);
            return;
        }

        GameObject esGo = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGo, "HybridGPSMap EventSystem");
        esGo.AddComponent<EventSystem>();
        Undo.AddComponent<InputSystemUIInputModule>(esGo).AssignDefaultActions();
    }
}
#endif
