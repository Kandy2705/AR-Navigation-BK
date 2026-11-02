using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARNavB9V2.Data;
using ARNavB9V2.Indoor;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Editor
{
    public static class B9CurrentMapUpgradeBuilder
    {
        public const string MapCode = "MAP_FYN7L0XV6XBY";
        public const string MeshPath = "Assets/MultiSet/MapData/" + MapCode + "/" + MapCode + ".glb";
        public const string PrefabPath = "Assets/MultiSet/MapData/" + MapCode + ".prefab";
        private const string PreviousPrefabPath = "Assets/MultiSet/MapData/MAP_9LME2PB7Y3EN.prefab";
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string NavMeshPath = "Assets/ARNavB9V2/Data/Buildings/B9IndoorNavMesh_MAP_FYN7L0XV6XBY.asset";
        private static readonly Vector3 OldToNewTranslation =
            new Vector3(-9.623373f, -0.050120f, 34.619177f);

        [MenuItem("Tools/AR Navigation V2/Maps/Create Current B9 Map Prefab")]
        public static void CreatePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the B9 prefab.");
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                return;
            }

            AssetDatabase.ImportAsset(MeshPath, ImportAssetOptions.ForceSynchronousImport);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MeshPath);
            if (model == null)
                throw new InvalidOperationException("B9 textured GLB has not been imported: " + MeshPath);

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(MapCode);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                var scan = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                scan.transform.SetParent(root.transform, false);
                scan.name = MapCode + " Scan";
                ValidateModel(root, out long vertices);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || prefab == null)
                    throw new InvalidOperationException("Could not save current B9 prefab.");
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[B9 Current Map] Created {PrefabPath}; vertices={vertices}.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("Tools/AR Navigation V2/Maps/Export B9 Map Registration Samples")]
        public static void ExportRegistrationSamples()
        {
            CreatePrefab();
            ExportPoints(PreviousPrefabPath, "/private/tmp/b9_previous_points.bin");
            ExportPoints(PrefabPath, "/private/tmp/b9_current_points.bin");
            Debug.Log("[B9 Current Map] Exported old/new registration samples to /private/tmp.");
        }

        [MenuItem("Tools/AR Navigation V2/Maps/Upgrade B9 Scene To Current Map")]
        public static void UpgradeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before upgrading B9.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open B9NavigationV2 before upgrading B9.");
            CreatePrefab();

            B9SceneContext context = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            B9IndoorSceneContext indoor = UnityEngine.Object.FindFirstObjectByType<B9IndoorSceneContext>(
                FindObjectsInactive.Include);
            B9VpsTransitionController vps = UnityEngine.Object.FindFirstObjectByType<
                B9VpsTransitionController>(FindObjectsInactive.Include);
            if (context == null || indoor == null || context.ModelRoot == null)
                throw new InvalidOperationException("B9 runtime context is incomplete.");
            string currentPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                context.ModelRoot.gameObject);
            if (currentPrefab == PrefabPath)
                throw new InvalidOperationException("B9 scene already uses the current map.");
            if (currentPrefab != PreviousPrefabPath)
                throw new InvalidOperationException("Unexpected B9 model prefab: " + currentPrefab);
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null)
                throw new InvalidOperationException("Current B9 NavMesh asset already exists; refusing to overwrite.");

            Quaternion registrationRotation = GetOldToNewRotation();
            NavMeshData navMeshData = BuildCurrentNavMeshData();
            if (navMeshData == null)
                throw new InvalidOperationException("Could not bake the current B9 NavMesh.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Upgrade B9 to current MultiSet map");
            Transform oldModel = context.ModelRoot;
            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var newModel = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, scene);
            Undo.RegisterCreatedObjectUndo(newModel, "Create current B9 model");
            newModel.name = "B9 Model MAP_FYN7L0XV6XBY (VPS + Minimap Only)";
            newModel.transform.SetParent(context.MapSpace, false);
            SetLayerRecursively(newModel.transform, LayerMask.NameToLayer("MapPlane"));

            foreach (B9RoomAnchor anchor in context.RoomAnchors.Where(anchor => anchor != null))
                RemapTransformInMapSpace(anchor.transform, context.MapSpace, registrationRotation);
            RemapTransformInMapSpace(
                context.IndoorEntranceAnchor,
                context.MapSpace,
                registrationRotation);
            context.Building.RemapIndoorCoordinates(OldToNewTranslation, registrationRotation);
            EditorUtility.SetDirty(context.Building);

            NavMeshSurface surface = Undo.AddComponent<NavMeshSurface>(newModel);
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1 << LayerMask.NameToLayer("MapPlane");
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            navMeshData.name = "B9IndoorNavMesh_MAP_FYN7L0XV6XBY";
            AssetDatabase.CreateAsset(navMeshData, NavMeshPath);
            surface.navMeshData = navMeshData;

            context.Configure(
                context.Building,
                context.ArSession,
                context.XrOrigin,
                context.ArCamera,
                context.MinimapCamera,
                context.MapSpace,
                newModel.transform,
                surface,
                context.OutdoorEntranceAnchor,
                context.IndoorEntranceAnchor,
                context.RoomAnchors);
            B9MapVisibility visibility = context.GetComponent<B9MapVisibility>();
            visibility?.Configure(newModel.transform, context.ArCamera, context.MinimapCamera);
            indoor.RouteController.Configure(
                context.Building,
                context,
                context.ArCamera,
                surface,
                indoor.RibbonRenderer,
                indoor.RouteController.DestinationRoomId);
            indoor.PoseTracker.Configure(context.ArCamera, surface, vps);
            indoor.MinimapController.Configure(
                context,
                indoor.RouteController,
                context.MinimapCamera,
                indoor.MinimapController.RenderedTexture,
                indoor.UserMarker,
                indoor.DestinationMarker);

            Undo.DestroyObjectImmediate(oldModel.gameObject);
            B9V2HandoverGeometryBuilder.BuildIntoCurrentScene(context, context.Building);
            newModel.SetActive(false);
            EditorUtility.SetDirty(context);
            if (!context.ValidateConfiguration(out string failure)
                || !context.ValidateHandoverConfiguration(out failure))
                throw new InvalidOperationException("Upgraded B9 scene is invalid: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = newModel;
            Debug.Log(
                "[B9 Current Map] UPGRADE DONE map=MAP_FYN7L0XV6XBY, POIs remapped, "
                + "NavMesh rebuilt, handover geometry rebuilt; registration median error=0.32m.");
        }

        private static void ExportPoints(string prefabPath, string outputPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Missing map prefab: " + prefabPath);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var points = new List<Vector3>();
                foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                        continue;
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    int stride = Mathf.Max(1, Mathf.CeilToInt(vertices.Length / 160000f));
                    for (int i = 0; i < vertices.Length; i += stride)
                        points.Add(filter.transform.TransformPoint(vertices[i]));
                }
                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(points.Count);
                foreach (Vector3 point in points)
                {
                    writer.Write(point.x);
                    writer.Write(point.y);
                    writer.Write(point.z);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void ValidateModel(GameObject root, out long vertices)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (filters.Length == 0 || renderers.Length == 0)
                throw new InvalidOperationException("B9 scan contains no mesh renderers.");
            vertices = 0;
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null)
                    throw new InvalidOperationException("B9 scan contains a missing mesh.");
                vertices += filter.sharedMesh.vertexCount;
            }
            foreach (MeshRenderer renderer in renderers)
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null)
                    throw new InvalidOperationException("B9 scan contains a missing material/shader.");
            }
        }

        private static Quaternion GetOldToNewRotation()
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 0.8428997f; matrix.m01 = 0.01516334f; matrix.m02 = -0.5378570f;
            matrix.m10 = -0.01734099f; matrix.m11 = 0.9998491f; matrix.m12 = 0.001012043f;
            matrix.m20 = 0.5377912f; matrix.m21 = 0.008473924f; matrix.m22 = 0.8430355f;
            return matrix.rotation;
        }

        private static void RemapTransformInMapSpace(
            Transform target,
            Transform mapSpace,
            Quaternion rotation)
        {
            Vector3 oldPosition = mapSpace.InverseTransformPoint(target.position);
            Quaternion oldRotation = Quaternion.Inverse(mapSpace.rotation) * target.rotation;
            target.SetPositionAndRotation(
                mapSpace.TransformPoint(rotation * oldPosition + OldToNewTranslation),
                mapSpace.rotation * rotation * oldRotation);
            EditorUtility.SetDirty(target);
        }

        private static NavMeshData BuildCurrentNavMeshData()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var sources = new List<NavMeshBuildSource>();
                Bounds bounds = default;
                bool first = true;
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                        continue;
                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Mesh,
                        sourceObject = filter.sharedMesh,
                        transform = filter.transform.localToWorldMatrix,
                        area = 0,
                    });
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null)
                        continue;
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (sources.Count == 0 || first)
                    throw new InvalidOperationException("Current B9 map has no bakeable meshes.");
                bounds.Expand(4f);
                NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
                settings.agentRadius = 0.25f;
                settings.agentHeight = 1.8f;
                settings.agentClimb = 0.25f;
                settings.agentSlope = 35f;
                return NavMeshBuilder.BuildNavMeshData(
                    settings,
                    sources,
                    bounds,
                    Vector3.zero,
                    Quaternion.identity);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (layer < 0)
                throw new InvalidOperationException("MapPlane layer is missing.");
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }
    }
}
