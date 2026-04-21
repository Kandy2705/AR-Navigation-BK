using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class ManSceneHealthCheck
{
    private const string ScenePath = "Assets/Scenes/ManScene.unity";

    [MenuItem("Tools/Nav/Run ManScene Health Check")]
    public static void Run()
    {
        try
        {
            Debug.Log("[ManSceneCheck] ===== START =====");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[ManSceneCheck] Scene opened: {scene.path}");

            var setNavigations = Resources.FindObjectsOfTypeAll<SetNavigation>()
                .Where(s => IsInScene(s.gameObject, scene.path))
                .ToList();

            Debug.Log($"[ManSceneCheck] SetNavigation count={setNavigations.Count}");
            if (setNavigations.Count == 0)
            {
                Debug.LogWarning("[ManSceneCheck] No SetNavigation found.");
            }

            var globals = Resources.FindObjectsOfTypeAll<GlobalProperties>()
                .Where(g => IsInScene(g.gameObject, scene.path))
                .ToList();
            Debug.Log($"[ManSceneCheck] GlobalProperties count={globals.Count}");

            var surfaces = Resources.FindObjectsOfTypeAll<NavMeshSurface>()
                .Where(s => IsInScene(s.gameObject, scene.path))
                .ToList();
            Debug.Log($"[ManSceneCheck] NavMeshSurface count={surfaces.Count}");

            foreach (var surface in surfaces)
            {
                var so = new SerializedObject(surface);
                int collectObjects = FindInt(so, "m_CollectObjects", -1);
                int useGeometry = FindInt(so, "m_UseGeometry", -1);
                int layerMask = FindInt(so, "m_LayerMask", 0);
                var navDataName = FindObjectName(so, "m_NavMeshData");

                Debug.Log(
                    $"[ManSceneCheck][Surface] path={GetHierarchyPath(surface.transform)} " +
                    $"enabled={surface.enabled} active={surface.gameObject.activeInHierarchy} " +
                    $"collectObjects={collectObjects} useGeometry={useGeometry} layerMask={layerMask} navData={navDataName}");
            }

            foreach (var setNavigation in setNavigations)
            {
                InspectSetNavigation(scene.path, setNavigation, surfaces);
            }

            Debug.Log("[ManSceneCheck] ===== END =====");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ManSceneCheck] Exception: " + ex);
            throw;
        }
    }

    private static void InspectSetNavigation(string scenePath, SetNavigation setNavigation, List<NavMeshSurface> surfaces)
    {
        var so = new SerializedObject(setNavigation);
        var marker = FindObject<GameObject>(so, "markerObject");
        var target = FindObject<GameObject>(so, "navTargetObject");
        var topDown = FindObject<Camera>(so, "topDownCamera");
        var sampleDistance = FindFloat(so, "navMeshSampleDistance", 10f);
        var debugPath = FindBool(so, "debugPathState", false);

        Debug.Log(
            $"[ManSceneCheck][SetNav] path={GetHierarchyPath(setNavigation.transform)} " +
            $"marker={(marker != null ? GetHierarchyPath(marker.transform) : "null")} " +
            $"target={(target != null ? GetHierarchyPath(target.transform) : "null")} " +
            $"topDown={(topDown != null ? GetHierarchyPath(topDown.transform) : "null")} " +
            $"sampleDistance={sampleDistance:0.##} debugPathState={debugPath}");

        if (marker == null || target == null)
        {
            Debug.LogWarning("[ManSceneCheck][SetNav] Missing marker/target reference.");
            return;
        }

        foreach (var surface in surfaces)
        {
            bool markerInside = IsDescendantOf(marker.transform, surface.transform);
            bool targetInside = IsDescendantOf(target.transform, surface.transform);
            if (markerInside || targetInside)
            {
                Debug.Log(
                    $"[ManSceneCheck][SetNav] markerOrTargetUnderSurface surface={GetHierarchyPath(surface.transform)} " +
                    $"markerInside={markerInside} targetInside={targetInside}");
            }
        }

        var markerPos = marker.transform.position;
        var targetPos = target.transform.position;
        Debug.Log($"[ManSceneCheck][SetNav] markerPos={markerPos} targetPos={targetPos}");

        RunPathSweep(markerPos, targetPos);
    }

    private static void RunPathSweep(Vector3 markerPos, Vector3 targetPos)
    {
        var distances = new[] { 0.1f, 0.5f, 1f, 2f, 5f, 10f, 20f };
        foreach (var distance in distances)
        {
            bool haveStart = NavMesh.SamplePosition(markerPos, out var startHit, distance, NavMesh.AllAreas);
            bool haveEnd = NavMesh.SamplePosition(targetPos, out var endHit, distance, NavMesh.AllAreas);
            if (!haveStart || !haveEnd)
            {
                Debug.Log(
                    $"[ManSceneCheck][Path] d={distance:0.##} SAMPLE_FAIL haveStart={haveStart} haveEnd={haveEnd} " +
                    $"markerRaw={markerPos} targetRaw={targetPos}");
                continue;
            }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);
            int corners = path.corners == null ? 0 : path.corners.Length;
            Debug.Log(
                $"[ManSceneCheck][Path] d={distance:0.##} status={path.status} corners={corners} " +
                $"start={startHit.position} end={endHit.position}");
        }

        bool startFar = NavMesh.SamplePosition(markerPos, out var startFarHit, 1000f, NavMesh.AllAreas);
        bool endFar = NavMesh.SamplePosition(targetPos, out var endFarHit, 1000f, NavMesh.AllAreas);
        float startDelta = startFar ? Vector3.Distance(markerPos, startFarHit.position) : -1f;
        float endDelta = endFar ? Vector3.Distance(targetPos, endFarHit.position) : -1f;

        Debug.Log(
            $"[ManSceneCheck][Path] nearest(1000m) startHit={startFar} delta={startDelta:0.###} " +
            $"endHit={endFar} delta={endDelta:0.###}");
    }

    private static bool IsInScene(GameObject go, string scenePath)
    {
        return go != null && go.scene.IsValid() && go.scene.path == scenePath;
    }

    private static bool IsDescendantOf(Transform child, Transform root)
    {
        if (child == null || root == null)
        {
            return false;
        }

        var current = child;
        while (current != null)
        {
            if (current == root)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static T FindObject<T>(SerializedObject so, string propertyName) where T : UnityEngine.Object
    {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.objectReferenceValue as T : null;
    }

    private static float FindFloat(SerializedObject so, string propertyName, float fallback)
    {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.floatValue : fallback;
    }

    private static int FindInt(SerializedObject so, string propertyName, int fallback)
    {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.intValue : fallback;
    }

    private static bool FindBool(SerializedObject so, string propertyName, bool fallback)
    {
        var prop = so.FindProperty(propertyName);
        return prop != null ? prop.boolValue : fallback;
    }

    private static string FindObjectName(SerializedObject so, string propertyName)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null || prop.objectReferenceValue == null)
        {
            return "null";
        }

        return prop.objectReferenceValue.name;
    }

    private static string GetHierarchyPath(Transform t)
    {
        var names = new List<string>();
        var current = t;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
