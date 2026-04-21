using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupManScenePlanePathVariant
{
    private const string SourceScene = "Assets/Scenes/ManScene.unity";
    private const string VariantScene = "Assets/Scenes/ManScene_PlanePath.unity";

    [MenuItem("Tools/Nav/Create+Setup ManScene PlanePath Variant")]
    public static void CreateAndSetupVariant()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[PlanePathSetup] Cannot run while Play Mode is active. Stop Play Mode and run again.");
            EditorUtility.DisplayDialog(
                "PlanePath Setup",
                "Tool nay khong chay duoc khi dang Play Mode.\nHay Stop Play roi bam menu lai.",
                "OK");
            return;
        }

        try
        {
            EnsureSceneCopyExists();

            var scene = EditorSceneManager.OpenScene(VariantScene, OpenSceneMode.Single);
            Debug.Log($"[PlanePathSetup] Opened scene: {scene.path}");

            var global = EnsureGlobalProperties(scene.path);
            if (global != null)
            {
                global.IsShowNavigation = true;
                EditorUtility.SetDirty(global);
                Debug.Log("[PlanePathSetup] GlobalProperties ensured and IsShowNavigation=true.");
            }

            var setNavigation = Resources.FindObjectsOfTypeAll<SetNavigation>()
                .FirstOrDefault(s => s != null && s.gameObject.scene.path == scene.path);
            if (setNavigation == null)
            {
                Debug.LogError("[PlanePathSetup] SetNavigation not found in variant scene.");
                return;
            }

            var userIcon = FindByName(scene.path, "UserIcon");
            var target = FindByName(scene.path, "Target");
            var marker = FindByName(scene.path, "Marker");
            var mapBkCube = FindByName(scene.path, "MapBKCube");

            if (userIcon == null || target == null)
            {
                Debug.LogError(
                    $"[PlanePathSetup] Missing refs userIconNull={userIcon == null} targetNull={target == null}.");
                return;
            }

            var userNavPoint = EnsureFootAnchor(userIcon.transform, "UserNavPoint");
            var targetNavPoint = EnsureFootAnchor(target.transform, "TargetNavPoint");

            ExcludeFromNavMeshBuild(userIcon);
            ExcludeFromNavMeshBuild(target);
            if (marker != null)
            {
                ExcludeFromNavMeshBuild(marker);
            }

            if (mapBkCube != null)
            {
                var surface = mapBkCube.GetComponent<NavMeshSurface>();
                if (surface != null)
                {
                    var surfaceSo = new SerializedObject(surface);
                    var useGeometryProp = surfaceSo.FindProperty("m_UseGeometry");
                    var layerMaskProp = surfaceSo.FindProperty("m_LayerMask");
                    var collectObjectsProp = surfaceSo.FindProperty("m_CollectObjects");

                    // Build from render mesh of the map plane layer only.
                    // This avoids baking runtime cubes/line objects into tiny islands.
                    if (useGeometryProp != null)
                    {
                        // 0: RenderMeshes
                        useGeometryProp.intValue = 0;
                    }

                    if (collectObjectsProp != null)
                    {
                        // 0: All (keep default collect mode)
                        collectObjectsProp.intValue = 0;
                    }

                    if (layerMaskProp != null)
                    {
                        int groundLayer = mapBkCube.layer;
                        int maskBits = 1 << groundLayer;
                        layerMaskProp.intValue = maskBits;
                        Debug.Log($"[PlanePathSetup] Surface LayerMask set to MapBKCube layer={groundLayer} bits={maskBits}.");
                    }

                    surfaceSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(surface);
                    surface.BuildNavMesh();
                    Debug.Log("[PlanePathSetup] Rebuilt NavMeshSurface on MapBKCube using RenderMeshes (plane layer only).");
                }
            }

            ApplySetNavigationConfig(setNavigation, userNavPoint.gameObject, targetNavPoint.gameObject);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[PlanePathSetup] SUCCESS: Variant scene has been configured for plane-based path rendering.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[PlanePathSetup] Exception: " + ex);
            // Do not rethrow: keep Console readable for iterative editor setup.
        }
    }

    [MenuItem("Tools/Nav/Create+Setup ManScene PlanePath Variant", true)]
    private static bool ValidateCreateAndSetupVariant()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void EnsureSceneCopyExists()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VariantScene) != null)
        {
            return;
        }

        if (!AssetDatabase.CopyAsset(SourceScene, VariantScene))
        {
            throw new InvalidOperationException("Could not copy ManScene to ManScene_PlanePath.");
        }
    }

    private static GameObject FindByName(string scenePath, string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t != null && t.gameObject.scene.path == scenePath)
            .Select(t => t.gameObject)
            .FirstOrDefault(go => go.name.Equals(name, StringComparison.Ordinal));
    }

    private static Transform EnsureFootAnchor(Transform owner, string anchorName)
    {
        var existing = owner.Find(anchorName);
        if (existing != null)
        {
            return existing;
        }

        var anchor = new GameObject(anchorName).transform;
        anchor.SetParent(owner, worldPositionStays: true);
        anchor.position = GetFootWorldPoint(owner.gameObject);
        return anchor;
    }

    private static Vector3 GetFootWorldPoint(GameObject go)
    {
        var world = go.transform.position;
        float y = world.y;

        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            y = col.bounds.min.y + 0.02f;
        }
        else
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                y = rend.bounds.min.y + 0.02f;
            }
        }

        return new Vector3(world.x, y, world.z);
    }

    private static void ExcludeFromNavMeshBuild(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        var modifier = go.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = go.AddComponent<NavMeshModifier>();
        }

        modifier.ignoreFromBuild = true;
        modifier.overrideArea = false;
        modifier.enabled = true;
        EditorUtility.SetDirty(modifier);
    }

    private static GlobalProperties EnsureGlobalProperties(string scenePath)
    {
        var existing = Resources.FindObjectsOfTypeAll<GlobalProperties>()
            .FirstOrDefault(g => g != null && g.gameObject.scene.path == scenePath);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("GlobalProperties");
        var created = go.AddComponent<GlobalProperties>();
        return created;
    }

    private static void ApplySetNavigationConfig(SetNavigation setNavigation, GameObject markerRef, GameObject targetRef)
    {
        var so = new SerializedObject(setNavigation);

        var markerProp = so.FindProperty("markerObject");
        var targetProp = so.FindProperty("navTargetObject");
        var sampleProp = so.FindProperty("navMeshSampleDistance");
        var heightProp = so.FindProperty("lineHeightOffset");
        var debugProp = so.FindProperty("debugPathState");

        if (markerProp != null) markerProp.objectReferenceValue = markerRef;
        if (targetProp != null) targetProp.objectReferenceValue = targetRef;
        if (sampleProp != null) sampleProp.floatValue = 2f;
        if (heightProp != null) heightProp.floatValue = 0.03f;
        if (debugProp != null) debugProp.boolValue = true;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(setNavigation);

        Debug.Log(
            $"[PlanePathSetup] SetNavigation configured marker={markerRef.name} target={targetRef.name} " +
            "sampleDistance=2 lineHeightOffset=0.03");
    }
}
