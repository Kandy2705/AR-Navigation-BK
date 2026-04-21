using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshBatchDiagnose
{
    private const string ScenePath = "Assets/Samples/MultiSet-SDK/1.9.2/Sample Scenes/Navigation/Navigation.unity";

    [MenuItem("Tools/Nav/Run Batch Diagnose")]
    public static void Run()
    {
        try
        {
            Debug.Log("[NavDiag] ===== START =====");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[NavDiag] Scene opened: {scene.path}");

            var setNav = FindSceneObject<SetNavigation>(scene.path);
            if (setNav == null)
            {
                Debug.LogError("[NavDiag] SetNavigation not found in scene.");
                return;
            }

            var refs = ReadSetNavigationRefs(setNav);
            if (refs.markerObject == null || refs.navTargetObject == null)
            {
                Debug.LogError(
                    $"[NavDiag] Missing refs markerNull={refs.markerObject == null} targetNull={refs.navTargetObject == null}");
                return;
            }

            LogWorldContext("BEFORE", refs.markerObject.transform.position, refs.navTargetObject.transform.position);
            RunPathSweep("BEFORE", refs.markerObject.transform.position, refs.navTargetObject.transform.position);

            DisableIndoorNavMeshBranch(scene.path);
            DisableBetterCornersObstacles(scene.path);
            RebuildOutdoorSurface(scene.path);

            LogWorldContext("AFTER", refs.markerObject.transform.position, refs.navTargetObject.transform.position);
            RunPathSweep("AFTER", refs.markerObject.transform.position, refs.navTargetObject.transform.position);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[NavDiag] Scene saved after navmesh diagnose adjustments.");
            Debug.Log("[NavDiag] ===== END =====");
        }
        catch (Exception ex)
        {
            Debug.LogError("[NavDiag] Exception: " + ex);
            throw;
        }
    }

    private static (GameObject markerObject, GameObject navTargetObject) ReadSetNavigationRefs(SetNavigation setNavigation)
    {
        var so = new SerializedObject(setNavigation);
        var markerProp = so.FindProperty("markerObject");
        var targetProp = so.FindProperty("navTargetObject");

        var marker = markerProp != null ? markerProp.objectReferenceValue as GameObject : null;
        var target = targetProp != null ? targetProp.objectReferenceValue as GameObject : null;

        Debug.Log(
            $"[NavDiag] SetNavigation refs marker='{(marker != null ? marker.name : "null")}' target='{(target != null ? target.name : "null")}'");

        return (marker, target);
    }

    private static void DisableIndoorNavMeshBranch(string scenePath)
    {
        var navMeshRoot = FindByPath(scenePath, "SystemIndoor/UI Home Screen/Map Space/NavigationContent/NavMesh");
        if (navMeshRoot == null)
        {
            Debug.LogWarning("[NavDiag] Indoor NavMesh root not found, skip disable.");
            return;
        }

        if (navMeshRoot.activeSelf)
        {
            navMeshRoot.SetActive(false);
            Debug.Log("[NavDiag] Disabled indoor navmesh branch: SystemIndoor/UI Home Screen/Map Space/NavigationContent/NavMesh");
        }
        else
        {
            Debug.Log("[NavDiag] Indoor navmesh branch already disabled.");
        }
    }

    private static void DisableBetterCornersObstacles(string scenePath)
    {
        var betterCorners = FindByPath(scenePath, "SystemIndoor/UI Home Screen/Map Space/NavigationContent/NavMesh/Better Corners");
        if (betterCorners == null)
        {
            Debug.LogWarning("[NavDiag] Better Corners not found, skip obstacle disable.");
            return;
        }

        var obstacles = betterCorners.GetComponentsInChildren<NavMeshObstacle>(true);
        var countChanged = 0;
        foreach (var obstacle in obstacles)
        {
            if (obstacle.enabled)
            {
                obstacle.enabled = false;
                countChanged++;
            }
        }

        Debug.Log($"[NavDiag] Disabled Better Corners obstacles: changed={countChanged} total={obstacles.Length}");
    }

    private static void RebuildOutdoorSurface(string scenePath)
    {
        var mapBkCube = FindByPath(scenePath, "SystemOutdoor/Environment/MapBK/MapBKCube");
        if (mapBkCube == null)
        {
            Debug.LogWarning("[NavDiag] Outdoor MapBKCube not found, skip rebuild.");
            return;
        }

        var surface = mapBkCube.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            Debug.LogWarning("[NavDiag] Outdoor MapBKCube has no NavMeshSurface, skip rebuild.");
            return;
        }

        surface.BuildNavMesh();
        EditorUtility.SetDirty(surface);
        Debug.Log("[NavDiag] Rebuilt outdoor NavMeshSurface on SystemOutdoor/Environment/MapBK/MapBKCube.");
    }

    private static void LogWorldContext(string phase, Vector3 markerPos, Vector3 targetPos)
    {
        var surfaces = Resources.FindObjectsOfTypeAll<NavMeshSurface>()
            .Where(s => IsInScene(s.gameObject, ScenePath))
            .Select(s =>
            {
                var so = new SerializedObject(s);
                var navData = so.FindProperty("m_NavMeshData");
                var navName = navData != null && navData.objectReferenceValue != null
                    ? navData.objectReferenceValue.name
                    : "null";
                return $"{GetHierarchyPath(s.transform)} enabled={s.enabled} active={s.gameObject.activeInHierarchy} data={navName}";
            })
            .ToList();

        var obstacles = Resources.FindObjectsOfTypeAll<NavMeshObstacle>()
            .Where(o => IsInScene(o.gameObject, ScenePath) && o.enabled && o.carving)
            .Select(o => GetHierarchyPath(o.transform))
            .ToList();

        Debug.Log($"[NavDiag][{phase}] marker={markerPos} target={targetPos}");
        Debug.Log($"[NavDiag][{phase}] NavMeshSurface count={surfaces.Count}");
        foreach (var s in surfaces)
        {
            Debug.Log("[NavDiag][Surface] " + s);
        }

        Debug.Log($"[NavDiag][{phase}] Carving obstacle count={obstacles.Count}");
        foreach (var o in obstacles)
        {
            Debug.Log("[NavDiag][Obstacle] " + o);
        }
    }

    private static void RunPathSweep(string phase, Vector3 markerPos, Vector3 targetPos)
    {
        var distances = new[] { 0.1f, 1f, 2f, 3f, 5f, 10f, 20f, 40f, 80f };
        foreach (var d in distances)
        {
            bool haveStart = NavMesh.SamplePosition(markerPos, out var startHit, d, NavMesh.AllAreas);
            bool haveEnd = NavMesh.SamplePosition(targetPos, out var endHit, d, NavMesh.AllAreas);

            if (!haveStart || !haveEnd)
            {
                Debug.Log(
                    $"[NavDiag][{phase}] d={d:0.##} SAMPLE_FAIL haveStart={haveStart} haveEnd={haveEnd} startRaw={markerPos} endRaw={targetPos}");
                continue;
            }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);
            var corners = path.corners == null ? 0 : path.corners.Length;
            Debug.Log(
                $"[NavDiag][{phase}] d={d:0.##} status={path.status} corners={corners} start={startHit.position} end={endHit.position}");
        }

        bool startFarHit = NavMesh.SamplePosition(markerPos, out var startFar, 1000f, NavMesh.AllAreas);
        bool endFarHit = NavMesh.SamplePosition(targetPos, out var endFar, 1000f, NavMesh.AllAreas);
        var startDelta = startFarHit ? Vector3.Distance(markerPos, startFar.position) : -1f;
        var endDelta = endFarHit ? Vector3.Distance(targetPos, endFar.position) : -1f;

        Debug.Log(
            $"[NavDiag][{phase}] nearest(1000m) startHit={startFarHit} delta={startDelta:0.###} endHit={endFarHit} delta={endDelta:0.###}");
    }

    private static T FindSceneObject<T>(string scenePath) where T : UnityEngine.Object
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(obj =>
            {
                switch (obj)
                {
                    case Component c:
                        return IsInScene(c.gameObject, scenePath);
                    case GameObject go:
                        return IsInScene(go, scenePath);
                    default:
                        return false;
                }
            });
    }

    private static bool IsInScene(GameObject go, string scenePath)
    {
        return go != null && go.scene.IsValid() && go.scene.path == scenePath;
    }

    private static GameObject FindByPath(string scenePath, string path)
    {
        var roots = EditorSceneManager.GetSceneByPath(scenePath).GetRootGameObjects();
        var parts = path.Split('/');
        foreach (var root in roots)
        {
            if (root.name != parts[0])
            {
                continue;
            }

            var current = root.transform;
            for (int i = 1; i < parts.Length && current != null; i++)
            {
                current = current.Find(parts[i]);
            }

            if (current != null)
            {
                return current.gameObject;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(Transform t)
    {
        var nodes = new List<string>();
        var current = t;
        while (current != null)
        {
            nodes.Add(current.name);
            current = current.parent;
        }

        nodes.Reverse();
        return string.Join("/", nodes);
    }
}
