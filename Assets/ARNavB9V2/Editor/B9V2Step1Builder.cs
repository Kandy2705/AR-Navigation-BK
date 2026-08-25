using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ARNavB9V2.Data;
using ARNavB9V2.Scene;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ARNavB9V2.Editor
{
    public static class B9V2Step1Builder
    {
        private const string ReferenceScenePath = "Assets/Scenes/HybridGPSMap.unity";
        private const string OutputScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string DefinitionPath = "Assets/ARNavB9V2/Data/Buildings/B9BuildingDefinition.asset";
        private const string MapPrefabPath = "Assets/MultiSet/MapData/MAP_9LME2PB7Y3EN.prefab";
        private const string NavMeshPath = "Assets/Scenes/HybridGPSMap/NavMesh-MapB9.asset";
        private const double B9EntranceLatitude = 10.773456557544643d;
        private const double B9EntranceLongitude = 106.66042980794792d;
        private static readonly Regex RoomPattern = new Regex("^B9-[0-9]+$", RegexOptions.IgnoreCase);

        private sealed class Inventory
        {
            public Vector3 mapPosition = new Vector3(0f, 0f, -19.1f);
            public Quaternion mapRotation = Quaternion.identity;
            public Vector3 mapScale = Vector3.one;
            public Vector3 campusEntrance = new Vector3(153.83f, 0f, -3.19f);
            public Vector3 indoorEntrance;
            public Quaternion indoorEntranceRotation = Quaternion.identity;
            public readonly List<B9BuildingDefinition.RoomDefinition> rooms = new();
        }

        [MenuItem("Tools/AR Navigation V2/Step 1 - Build B9 Foundation")]
        public static void Build()
        {
            EnsureFolders();
            Inventory inventory = CaptureReferenceInventory();
            B9BuildingDefinition definition = CreateOrUpdateDefinition(inventory);
            CreateFoundationScene(definition, inventory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[B9V2 Step1] COMPLETE scene={OutputScenePath} rooms={inventory.rooms.Count} map=MAP_9LME2PB7Y3EN");
        }

        [MenuItem("Tools/AR Navigation V2/Sync MapB9 + POIs From HybridGPSMap")]
        public static void SyncMapB9FromReference()
        {
            Inventory inventory = CaptureReferenceInventory();
            B9BuildingDefinition definition = CreateOrUpdateDefinition(inventory);
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                OutputScenePath,
                OpenSceneMode.Single);
            B9SceneContext context = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            if (context == null)
                throw new InvalidOperationException("B9 V2 foundation context is missing.");
            if (!context.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 V2 foundation is invalid: " + failure);

            context.ModelRoot.localPosition = inventory.mapPosition;
            context.ModelRoot.localRotation = inventory.mapRotation;
            context.ModelRoot.localScale = inventory.mapScale;
            context.IndoorEntranceAnchor.localPosition = definition.IndoorEntranceMapLocalPosition;
            context.IndoorEntranceAnchor.localRotation = definition.IndoorEntranceMapLocalRotation;

            Transform anchorsRoot = context.RoomAnchors
                .Where(anchor => anchor != null)
                .Select(anchor => anchor.transform.parent)
                .FirstOrDefault(parent => parent != null);
            if (anchorsRoot == null)
            {
                GameObject anchorsGo = new GameObject("B9 Room Anchors");
                anchorsGo.transform.SetParent(context.MapSpace, false);
                anchorsRoot = anchorsGo.transform;
            }

            var existingAnchors = context.RoomAnchors
                .Where(anchor => anchor != null)
                .GroupBy(anchor => anchor.RoomId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var synchronizedAnchors = new List<B9RoomAnchor>(definition.Rooms.Count);
            var synchronizedRoomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (B9BuildingDefinition.RoomDefinition room in definition.Rooms)
            {
                if (!existingAnchors.TryGetValue(room.RoomId, out B9RoomAnchor anchor))
                {
                    GameObject anchorGo = new GameObject(room.RoomId);
                    anchorGo.transform.SetParent(anchorsRoot, false);
                    anchor = anchorGo.AddComponent<B9RoomAnchor>();
                }

                anchor.name = room.RoomId;
                anchor.transform.SetParent(anchorsRoot, false);
                anchor.transform.localPosition = room.MapLocalPosition;
                anchor.transform.localRotation = room.MapLocalRotation;
                anchor.Configure(room.RoomId, room.FloorId);
                EditorUtility.SetDirty(anchor);
                synchronizedAnchors.Add(anchor);
                synchronizedRoomIds.Add(room.RoomId);
            }

            foreach (B9RoomAnchor anchor in context.RoomAnchors.Where(anchor => anchor != null))
            {
                if (!synchronizedRoomIds.Contains(anchor.RoomId))
                    UnityEngine.Object.DestroyImmediate(anchor.gameObject);
            }

            context.Configure(
                definition,
                context.ArSession,
                context.XrOrigin,
                context.ArCamera,
                context.MinimapCamera,
                context.MapSpace,
                context.ModelRoot,
                context.NavMeshSurface,
                context.OutdoorEntranceAnchor,
                context.IndoorEntranceAnchor,
                synchronizedAnchors);
            EditorUtility.SetDirty(context);

            if (!context.ValidateConfiguration(out failure))
                throw new InvalidOperationException("Synced B9 V2 foundation is invalid: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OutputScenePath))
                throw new InvalidOperationException("Could not save B9 V2 scene after MapB9 synchronization.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            B9BuildingDefinition.RoomDefinition room104 = definition.Rooms.FirstOrDefault(
                room => string.Equals(room.RoomId, "B9-104", StringComparison.OrdinalIgnoreCase));
            Debug.Log(
                $"[B9V2 Sync] COMPLETE MapB9 + {synchronizedAnchors.Count} POIs from {ReferenceScenePath}. "
                + $"B9-104 mapLocal={room104?.MapLocalPosition:F3}");
        }

        private static Inventory CaptureReferenceInventory()
        {
            if (!System.IO.File.Exists(ReferenceScenePath))
                throw new InvalidOperationException($"Reference scene not found: {ReferenceScenePath}");

            UnityEngine.SceneManagement.Scene referenceScene =
                EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Single);
            var inventory = new Inventory();

            Transform mapSpace = FindTransform(referenceScene, "Map Space");
            Transform mapB9 = FindTransform(referenceScene, "MapB9");
            if (mapB9 != null)
            {
                CaptureRelativeTransform(
                    mapB9,
                    mapSpace,
                    out inventory.mapPosition,
                    out inventory.mapRotation,
                    out inventory.mapScale);
            }

            Transform entrance = FindTransform(referenceScene, "Entrance_B9");
            if (entrance != null)
                inventory.campusEntrance = entrance.position;

            Transform indoorStart = FindTransform(referenceScene, "Entrance_B9_IndoorStart");
            if (indoorStart != null && mapSpace != null)
            {
                inventory.indoorEntrance = mapSpace.InverseTransformPoint(indoorStart.position);
                inventory.indoorEntranceRotation =
                    Quaternion.Inverse(mapSpace.rotation) * indoorStart.rotation;
            }

            List<Transform> candidates = GetAllTransforms(referenceScene)
                .Where(t => RoomPattern.IsMatch(t.name))
                .ToList();

            foreach (IGrouping<string, Transform> group in candidates.GroupBy(
                         t => t.name.ToUpperInvariant(), StringComparer.Ordinal))
            {
                Transform room = group
                    .OrderByDescending(RoomCandidateScore)
                    .First();
                Vector3 localPosition = mapSpace != null
                    ? mapSpace.InverseTransformPoint(room.position)
                    : room.position;
                Quaternion localRotation = mapSpace != null
                    ? Quaternion.Inverse(mapSpace.rotation) * room.rotation
                    : room.rotation;
                inventory.rooms.Add(new B9BuildingDefinition.RoomDefinition(
                    group.Key,
                    group.Key,
                    ParseFloor(group.Key),
                    localPosition,
                    localRotation));
            }

            inventory.rooms.Sort((a, b) => string.CompareOrdinal(a.RoomId, b.RoomId));
            if (!inventory.rooms.Any(r => r.RoomId == "B9-104"))
                throw new InvalidOperationException("Reference scene does not contain a usable B9-104 room anchor.");

            Debug.Log(
                $"[B9V2 Step1] Inventory MapB9 local={inventory.mapPosition:F2}, " +
                $"entrance={inventory.campusEntrance:F2}, rooms={string.Join(", ", inventory.rooms.Select(r => r.RoomId))}");
            return inventory;
        }

        private static B9BuildingDefinition CreateOrUpdateDefinition(Inventory inventory)
        {
            B9BuildingDefinition definition =
                AssetDatabase.LoadAssetAtPath<B9BuildingDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<B9BuildingDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(
                B9EntranceLatitude,
                B9EntranceLongitude,
                inventory.campusEntrance,
                inventory.indoorEntrance,
                inventory.indoorEntranceRotation,
                inventory.rooms);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void CreateFoundationScene(
            B9BuildingDefinition definition,
            Inventory inventory)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            GameObject root = new GameObject("[B9 V2] Foundation");
            B9SceneContext context = root.AddComponent<B9SceneContext>();

            GameObject sessionGo = new GameObject("AR Session");
            sessionGo.transform.SetParent(root.transform, false);
            ARSession session = sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();

            GameObject originGo = new GameObject("XR Origin");
            originGo.transform.SetParent(root.transform, false);
            XROrigin xrOrigin = originGo.AddComponent<XROrigin>();
            xrOrigin.Origin = originGo;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.NotSpecified;

            GameObject offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);
            xrOrigin.CameraFloorOffsetObject = offsetGo;
            xrOrigin.CameraYOffset = 0f;

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(offsetGo.transform, false);
            Camera arCamera = cameraGo.AddComponent<Camera>();
            arCamera.nearClipPlane = 0.01f;
            arCamera.farClipPlane = 1000f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();
            ConfigureTrackedPoseDriver(cameraGo.AddComponent<TrackedPoseDriver>());
            xrOrigin.Camera = arCamera;

            GameObject contentGo = new GameObject("B9 Content");
            contentGo.transform.SetParent(root.transform, false);
            GameObject mapSpaceGo = new GameObject("Map Space");
            mapSpaceGo.transform.SetParent(contentGo.transform, false);

            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
            if (mapPrefab == null)
                throw new InvalidOperationException($"B9 map prefab missing: {MapPrefabPath}");
            GameObject model = PrefabUtility.InstantiatePrefab(mapPrefab, scene) as GameObject;
            if (model == null)
                throw new InvalidOperationException("Could not instantiate B9 map prefab.");
            model.name = "B9 Model (VPS + Minimap Only)";
            model.transform.SetParent(mapSpaceGo.transform, false);
            model.transform.localPosition = inventory.mapPosition;
            model.transform.localRotation = inventory.mapRotation;
            model.transform.localScale = inventory.mapScale;

            NavMeshData navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (navMeshData == null)
                throw new InvalidOperationException($"B9 NavMesh data missing: {NavMeshPath}");
            NavMeshSurface navMeshSurface = model.GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
                navMeshSurface = model.AddComponent<NavMeshSurface>();
            navMeshSurface.navMeshData = navMeshData;

            GameObject roomsGo = new GameObject("B9 Room Anchors");
            roomsGo.transform.SetParent(mapSpaceGo.transform, false);
            var anchors = new List<B9RoomAnchor>();
            foreach (B9BuildingDefinition.RoomDefinition room in definition.Rooms)
            {
                GameObject roomGo = new GameObject(room.RoomId);
                roomGo.transform.SetParent(roomsGo.transform, false);
                roomGo.transform.localPosition = room.MapLocalPosition;
                roomGo.transform.localRotation = room.MapLocalRotation;
                B9RoomAnchor anchor = roomGo.AddComponent<B9RoomAnchor>();
                anchor.Configure(room.RoomId, room.FloorId);
                anchors.Add(anchor);
            }

            GameObject referenceGo = new GameObject("B9 Reference Anchors");
            referenceGo.transform.SetParent(root.transform, false);
            GameObject outdoorEntranceGo = new GameObject("Outdoor Entrance B9");
            outdoorEntranceGo.transform.SetParent(referenceGo.transform, false);
            outdoorEntranceGo.transform.position = definition.EntranceCampusPosition;
            GameObject indoorEntranceGo = new GameObject("Indoor Entrance B9");
            indoorEntranceGo.transform.SetParent(mapSpaceGo.transform, false);
            indoorEntranceGo.transform.localPosition = definition.IndoorEntranceMapLocalPosition;
            indoorEntranceGo.transform.localRotation = definition.IndoorEntranceMapLocalRotation;

            Camera minimapCamera = CreateMinimapCamera(root.transform, model);
            B9MapVisibility visibility = root.AddComponent<B9MapVisibility>();
            visibility.Configure(model.transform, arCamera, minimapCamera);

            context.Configure(
                definition,
                session,
                xrOrigin,
                arCamera,
                minimapCamera,
                mapSpaceGo.transform,
                model.transform,
                navMeshSurface,
                outdoorEntranceGo.transform,
                indoorEntranceGo.transform,
                anchors);

            if (!context.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 V2 foundation validation failed: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, OutputScenePath))
                throw new InvalidOperationException($"Could not save scene: {OutputScenePath}");
        }

        private static Camera CreateMinimapCamera(Transform parent, GameObject model)
        {
            Bounds bounds = CalculateRendererBounds(model);
            float horizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);

            GameObject cameraGo = new GameObject("B9 Minimap Camera");
            cameraGo.transform.SetParent(parent, false);
            cameraGo.transform.position = bounds.center + Vector3.up * Mathf.Max(15f, horizontalExtent * 2f);
            cameraGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(8f, horizontalExtent * 1.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(50f, horizontalExtent * 5f);
            camera.enabled = false;
            return camera;
        }

        private static void ConfigureTrackedPoseDriver(TrackedPoseDriver driver)
        {
            var position = new InputAction("AR Device Position", InputActionType.Value, expectedControlType: "Vector3");
            position.AddBinding("<XRHMD>/centerEyePosition");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            driver.positionInput = new InputActionProperty(position);

            var rotation = new InputAction("AR Device Rotation", InputActionType.Value, expectedControlType: "Quaternion");
            rotation.AddBinding("<XRHMD>/centerEyeRotation");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = true;
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 10f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static int RoomCandidateScore(Transform candidate)
        {
            int score = 0;
            if (candidate.parent != null && candidate.parent.name == "POIs-B9") score += 100;
            if (HasAncestor(candidate, "POIs-B9")) score += 50;
            if (candidate.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "POI")) score += 25;
            return score;
        }

        private static string ParseFloor(string roomId)
        {
            int dash = roomId.IndexOf('-');
            if (dash >= 0 && dash + 1 < roomId.Length && char.IsDigit(roomId[dash + 1]))
                return "F" + roomId[dash + 1];
            return "F1";
        }

        private static bool HasAncestor(Transform child, string ancestorName)
        {
            for (Transform current = child.parent; current != null; current = current.parent)
            {
                if (current.name == ancestorName) return true;
            }
            return false;
        }

        private static Transform FindTransform(UnityEngine.SceneManagement.Scene scene, string exactName)
        {
            return GetAllTransforms(scene).FirstOrDefault(t => t.name == exactName);
        }

        private static List<Transform> GetAllTransforms(UnityEngine.SceneManagement.Scene scene)
        {
            var result = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Transform>(true));
            return result;
        }

        private static void CaptureRelativeTransform(
            Transform source,
            Transform relativeTo,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            if (relativeTo == null)
            {
                position = source.position;
                rotation = source.rotation;
                scale = source.lossyScale;
                return;
            }

            position = relativeTo.InverseTransformPoint(source.position);
            rotation = Quaternion.Inverse(relativeTo.rotation) * source.rotation;
            Vector3 parentScale = relativeTo.lossyScale;
            Vector3 worldScale = source.lossyScale;
            scale = new Vector3(
                SafeDivide(worldScale.x, parentScale.x),
                SafeDivide(worldScale.y, parentScale.y),
                SafeDivide(worldScale.z, parentScale.z));
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) < 0.0001f ? numerator : numerator / denominator;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "ARNavB9V2");
            EnsureFolder("Assets/ARNavB9V2", "Scenes");
            EnsureFolder("Assets/ARNavB9V2", "Data");
            EnsureFolder("Assets/ARNavB9V2/Data", "Buildings");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
