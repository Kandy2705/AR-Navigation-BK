using System;
using System.Collections.Generic;
using System.Linq;
using ARNavB9V2.Handover;
using ARNavB9V2.Scene;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B10HandoverSetupBuilder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string DataPath = "Assets/ARNavB9V2/Data/Buildings/B10IndoorNavMesh.asset";
        private const float CellSize = 0.75f;
        private const float Padding = 3f;

        [MenuItem("Tools/AR Navigation V2/Maps/Build B10 Handover Setup")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open B9NavigationV2 in Edit Mode first.");
            if (UnityEngine.Object.FindObjectsByType<B10BuildingAuthoring>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Any())
                throw new InvalidOperationException("B10 setup already exists; edit its authored volumes instead of replacing them.");
            var placed = scene.GetRootGameObjects().SingleOrDefault(
                go => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == B10MapPrefabBuilder.PrefabPath);
            if (placed == null)
                throw new InvalidOperationException("Expected one placed B10 prefab at scene root.");
            if ((placed.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("B10 must keep metric scale (1,1,1).");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath) != null)
                throw new InvalidOperationException("B10 NavMesh asset already exists; refusing to overwrite.");

            // Bake only this scan in an isolated scene; never rebake the campus/B9 surfaces.
            var preview = EditorSceneManager.NewPreviewScene();
            NavMeshData data = null;
            float floorY;
            HashSet<Vector2Int> cells;
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(B10MapPrefabBuilder.PrefabPath), preview);
                var sources = new List<NavMeshBuildSource>();
                Bounds bounds = new Bounds();
                bool first = true;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh,
                        sourceObject = filter.sharedMesh, transform = filter.transform.localToWorldMatrix, area = 0 });
                    var renderer = filter.GetComponent<Renderer>();
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (sources.Count == 0) throw new InvalidOperationException("No B10 scan geometry.");
                bounds.Expand(4f);
                var settings = NavMesh.GetSettingsByIndex(0);
                settings.agentRadius = 0.25f;
                settings.agentHeight = 1.8f;
                settings.agentClimb = 0.25f;
                settings.agentSlope = 35f;
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data == null) throw new InvalidOperationException("B10 NavMesh bake failed.");
                cells = GetGroundCells(data, bounds, out floorY);
                if (cells.Count < 10) throw new InvalidOperationException("Insufficient walkable B10 ground cells.");
            }
            catch
            {
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            int mapLayer = LayerMask.NameToLayer("MapPlane");
            if (mapLayer < 0) throw new InvalidOperationException("MapPlane layer is missing.");
            Vector3 placementPosition = placed.transform.position;
            Quaternion placementRotation = placed.transform.rotation;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create B10 handover authoring");
            var root = Create("[B10] Navigation Setup", null);
            var campus = Create("B10 Campus Placement (MOVE THIS)", root.transform);
            campus.transform.SetPositionAndRotation(placementPosition, placementRotation);
            Undo.SetTransformParent(placed.transform, campus.transform, "Group placed B10 scan");
            SetLayer(placed.transform, mapLayer);

            var inner = Create("Inner Localization Volume (VPS Allowed)", campus.transform)
                .AddComponent<B9HandoverVolume>();
            var outer = Create("Outer Handover Volume (PDR)", campus.transform)
                .AddComponent<B9HandoverVolume>();
            var innerBoxes = new List<BoxCollider>();
            var outerBoxes = new List<BoxCollider>();
            foreach (var row in cells.GroupBy(c => c.y).OrderBy(g => g.Key))
            {
                int[] xs = row.Select(c => c.x).OrderBy(x => x).ToArray();
                int start = xs[0], end = start;
                for (int i = 1; i <= xs.Length; i++)
                {
                    if (i < xs.Length && xs[i] == end + 1) { end = xs[i]; continue; }
                    Vector3 center = new Vector3((start + end + 1) * CellSize * 0.5f,
                        floorY + 1.25f, (row.Key + 0.5f) * CellSize);
                    Vector3 size = new Vector3((end - start + 1) * CellSize + 0.02f, 3f, CellSize + 0.02f);
                    innerBoxes.Add(CreateBox(inner.transform, innerBoxes.Count, center, size));
                    outerBoxes.Add(CreateBox(outer.transform, outerBoxes.Count, center,
                        size + new Vector3(Padding * 2f, 1f, Padding * 2f)));
                    if (i < xs.Length) start = end = xs[i];
                }
            }
            inner.Configure(B9HandoverVolume.VolumeKind.InnerLocalization, innerBoxes);
            outer.Configure(B9HandoverVolume.VolumeKind.OuterHandover, outerBoxes);
            var portals = Create("B10 Portal Anchors (PLACE ENTRANCES HERE)", campus.transform);
            var space = Create("B10 Indoor Map Space (VPS - NOT CONNECTED)", root.transform);
            space.SetActive(false);
            var scan = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(B10MapPrefabBuilder.PrefabPath), scene);
            Undo.RegisterCreatedObjectUndo(scan, "Create B10 indoor scan");
            scan.transform.SetParent(space.transform, false);
            SetLayer(scan.transform, mapLayer);
            var rooms = Create("B10 Room Anchors (PLACE POIS HERE)", space.transform);
            var surface = space.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1 << mapLayer;
            data.name = "B10IndoorNavMesh";
            AssetDatabase.CreateAsset(data, DataPath);
            surface.navMeshData = data;
            var authoring = root.AddComponent<B10BuildingAuthoring>();
            authoring.Configure(campus.transform, placed.transform, space.transform,
                scan.transform, surface, outer, inner, portals.transform, rooms.transform, floorY);
            if (Vector3.Distance(placed.transform.position, placementPosition) > 0.001f
                || Quaternion.Angle(placed.transform.rotation, placementRotation) > 0.01f)
                throw new InvalidOperationException("B10 placement changed unexpectedly.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = campus;
            Debug.Log($"[B10 Setup] DONE groundY={floorY:F2}, cells={cells.Count}, "
                + $"segments={innerBoxes.Count}, outerPadding={Padding}; placed scan transform preserved. "
                + "Geometry draft only: entrance/room anchors and per-account runtime integration still required.");
        }

        private static HashSet<Vector2Int> GetGroundCells(NavMeshData data, Bounds bounds, out float floorY)
        {
            Vector3 offset = new Vector3(10000f, 10000f, 10000f);
            var instance = NavMesh.AddNavMeshData(data, offset, Quaternion.identity);
            try
            {
                var mesh = NavMesh.CalculateTriangulation();
                var triangles = new List<(Vector3 a, Vector3 b, Vector3 c)>();
                var heights = new Dictionary<int, float>();
                for (int i = 0; i < mesh.indices.Length; i += 3)
                {
                    Vector3 a = mesh.vertices[mesh.indices[i]] - offset;
                    Vector3 b = mesh.vertices[mesh.indices[i + 1]] - offset;
                    Vector3 c = mesh.vertices[mesh.indices[i + 2]] - offset;
                    if (!bounds.Contains(a) || !bounds.Contains(b) || !bounds.Contains(c)) continue;
                    float area = Mathf.Abs(Cross(b - a, c - a)) * 0.5f;
                    if (area < 0.01f) continue;
                    triangles.Add((a, b, c));
                    int key = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 5f);
                    heights[key] = (heights.TryGetValue(key, out float value) ? value : 0f) + area;
                }
                if (heights.Count == 0) throw new InvalidOperationException("No walkable ground in B10 bake.");
                floorY = heights.OrderByDescending(pair => pair.Value).First().Key / 5f;
                var cells = new HashSet<Vector2Int>();
                foreach (var t in triangles)
                {
                    if (Mathf.Abs((t.a.y + t.b.y + t.c.y) / 3f - floorY) > 0.6f) continue;
                    int minX = Mathf.FloorToInt(Mathf.Min(t.a.x, t.b.x, t.c.x) / CellSize);
                    int maxX = Mathf.FloorToInt(Mathf.Max(t.a.x, t.b.x, t.c.x) / CellSize);
                    int minZ = Mathf.FloorToInt(Mathf.Min(t.a.z, t.b.z, t.c.z) / CellSize);
                    int maxZ = Mathf.FloorToInt(Mathf.Max(t.a.z, t.b.z, t.c.z) / CellSize);
                    for (int x = minX; x <= maxX; x++)
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Vector3 p = new Vector3((x + 0.5f) * CellSize, floorY, (z + 0.5f) * CellSize);
                        float ab = Cross(t.b - t.a, p - t.a);
                        float bc = Cross(t.c - t.b, p - t.b);
                        float ca = Cross(t.a - t.c, p - t.c);
                        if ((ab >= -0.001f && bc >= -0.001f && ca >= -0.001f)
                            || (ab <= 0.001f && bc <= 0.001f && ca <= 0.001f)) cells.Add(new Vector2Int(x, z));
                    }
                }
                return cells;
            }
            finally { instance.Remove(); }
        }

        private static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
        private static GameObject Create(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create B10 setup");
            go.transform.SetParent(parent, false);
            return go;
        }
        private static BoxCollider CreateBox(Transform parent, int index, Vector3 center, Vector3 size)
        {
            var go = Create($"Segment {index + 1:000}", parent);
            go.transform.localPosition = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return box;
        }
        private static void SetLayer(Transform root, int layer)
        {
            Undo.RecordObject(root.gameObject, "B10 minimap visibility");
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
    }
}
