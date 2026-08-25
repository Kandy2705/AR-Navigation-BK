using System;
using System.Collections.Generic;
using System.Linq;
using ARNavB9V2.Data;
using ARNavB9V2.Handover;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2HandoverGeometryBuilder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string MapPrefabPath = "Assets/MultiSet/MapData/MAP_9LME2PB7Y3EN.prefab";
        private const string RootName = "[B9 V2] Handover Geometry Step 1";
        private const string CampusProxyName = "B9 Campus Georeferenced Proxy";
        private const string CampusModelName = "B9 Scan Campus Proxy (Minimap Only)";
        private const string PrimaryPortalId = "B9-MAIN";
        private const float InnerWidthMeters = 5.2f;
        private const float VolumeHeightMeters = 5.6f;
        private const float VolumeVerticalCenterMeters = 0.4f;
        private const float OuterPaddingMeters = 3f;
        private const float MaximumEndpointExtensionMeters = 10f;
        private const float SegmentOverlapMeters = 0.4f;

        [MenuItem("Tools/AR Navigation V2/Reliability Step 1 - Build B9 Handover Geometry")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9SceneContext context = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            if (context == null)
                throw new InvalidOperationException("B9 V2 foundation context is missing.");
            if (!context.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 V2 foundation is invalid: " + failure);

            B9BuildingTransitionGeometry geometry = BuildIntoCurrentScene(
                context,
                context.Building);
            if (!geometry.ValidateConfiguration(out failure))
                throw new InvalidOperationException("B9 handover geometry is invalid: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save B9 V2 handover geometry scene.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = geometry.gameObject;
            Debug.Log(
                $"[B9V2 Reliability Step1] COMPLETE segments={context.Building.HandoverSegments.Count}, "
                + $"portals={context.Building.Portals.Count}, innerWidth={InnerWidthMeters:0.0}m, "
                + $"outerPadding={OuterPaddingMeters:0.0}m. "
                + "MapB9 campus proxy is aligned to the measured entrance. "
                + "Cyan=VPS allowed, orange=PDR handover buffer.");
        }

        public static B9BuildingTransitionGeometry BuildIntoCurrentScene(
            B9SceneContext context,
            B9BuildingDefinition definition)
        {
            if (context == null || definition == null)
                throw new ArgumentNullException(context == null ? nameof(context) : nameof(definition));
            if (context.MapSpace == null || context.ModelRoot == null)
                throw new InvalidOperationException("Map Space or MapB9 model is missing.");
            if (context.OutdoorEntranceAnchor == null || context.IndoorEntranceAnchor == null)
                throw new InvalidOperationException("Measured B9 entrance anchor pair is missing.");

            DestroyExistingGeometry();

            List<Vector3> centerline = BuildCenterline(context);
            List<B9BuildingDefinition.HandoverSegmentDefinition> segmentDefinitions =
                BuildSegmentDefinitions(centerline);
            List<B9BuildingDefinition.PortalDefinition> portalDefinitions =
                BuildPortalDefinitions(context, definition);
            definition.ConfigureHandoverGeometry(
                OuterPaddingMeters,
                segmentDefinitions,
                portalDefinitions);
            EditorUtility.SetDirty(definition);

            GameObject root = new GameObject(RootName);
            root.transform.SetParent(context.transform, false);
            B9BuildingTransitionGeometry geometry =
                root.AddComponent<B9BuildingTransitionGeometry>();

            Transform campusProxy = CreateCampusProxy(root.transform, context);
            Transform campusModelProxy = CreateCampusModelProxy(campusProxy, context);

            B9HandoverVolume outer = CreateVolume(
                campusProxy,
                "Outer Handover Volume (PDR)",
                B9HandoverVolume.VolumeKind.OuterHandover,
                definition.HandoverSegments,
                definition.OuterPaddingMeters);
            B9HandoverVolume inner = CreateVolume(
                campusProxy,
                "Inner Localization Volume (VPS Allowed)",
                B9HandoverVolume.VolumeKind.InnerLocalization,
                definition.HandoverSegments,
                0f);
            List<B9PortalAnchor> portals = CreatePortalRegistry(
                root.transform,
                context,
                definition.Portals);

            geometry.Configure(
                context.MapSpace,
                campusProxy,
                campusModelProxy,
                outer,
                inner,
                portals);
            context.AttachHandoverGeometry(geometry);
            EditorUtility.SetDirty(geometry);
            EditorUtility.SetDirty(context);

            if (!context.ValidateHandoverConfiguration(out string failure))
                throw new InvalidOperationException("Generated handover geometry is invalid: " + failure);
            return geometry;
        }

        private static Transform CreateCampusProxy(Transform parent, B9SceneContext context)
        {
            GameObject proxyGo = new GameObject(CampusProxyName);
            proxyGo.transform.SetParent(parent, false);

            Vector3 indoorMapLocalPosition = context.MapSpace.InverseTransformPoint(
                context.IndoorEntranceAnchor.position);
            Quaternion indoorMapLocalRotation = Quaternion.Inverse(context.MapSpace.rotation)
                                                   * context.IndoorEntranceAnchor.rotation;
            Vector3 proxyScale = context.MapSpace.lossyScale;
            Quaternion proxyRotation = context.OutdoorEntranceAnchor.rotation
                                       * Quaternion.Inverse(indoorMapLocalRotation);
            Vector3 proxyPosition = context.OutdoorEntranceAnchor.position
                                    - proxyRotation
                                    * Vector3.Scale(indoorMapLocalPosition, proxyScale);

            proxyGo.transform.SetPositionAndRotation(proxyPosition, proxyRotation);
            proxyGo.transform.localScale = proxyScale;
            return proxyGo.transform;
        }

        private static Transform CreateCampusModelProxy(
            Transform campusProxy,
            B9SceneContext context)
        {
            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);
            if (mapPrefab == null)
                throw new InvalidOperationException($"B9 map prefab missing: {MapPrefabPath}");

            GameObject proxyModel = PrefabUtility.InstantiatePrefab(
                mapPrefab,
                campusProxy.gameObject.scene) as GameObject;
            if (proxyModel == null)
                throw new InvalidOperationException("Could not instantiate the B9 campus scan proxy.");

            proxyModel.name = CampusModelName;
            proxyModel.transform.SetParent(campusProxy, false);
            proxyModel.transform.localPosition = context.ModelRoot.localPosition;
            proxyModel.transform.localRotation = context.ModelRoot.localRotation;
            proxyModel.transform.localScale = context.ModelRoot.localScale;
            proxyModel.SetActive(true);

            int mapLayer = LayerMask.NameToLayer("MapPlane");
            if (mapLayer < 0)
                throw new InvalidOperationException("Required layer 'MapPlane' is missing.");
            SetLayerRecursively(proxyModel.transform, mapLayer);
            return proxyModel.transform;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private static List<Vector3> BuildCenterline(B9SceneContext context)
        {
            var floorPoints = context.RoomAnchors
                .Where(anchor => anchor != null && anchor.FloorId == "F1")
                .Select(anchor => context.MapSpace.InverseTransformPoint(anchor.transform.position))
                .ToList();
            floorPoints.Add(context.MapSpace.InverseTransformPoint(
                context.IndoorEntranceAnchor.position));
            if (floorPoints.Count < 2)
                throw new InvalidOperationException("At least two F1 reference points are required.");

            FindFarthestPair(floorPoints, out Vector3 first, out Vector3 last);
            Vector3 axis = last - first;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f)
                throw new InvalidOperationException("B9 corridor axis cannot be determined.");
            axis.Normalize();

            Vector3 mean = Vector3.zero;
            for (int i = 0; i < floorPoints.Count; i++)
                mean += new Vector3(floorPoints[i].x, 0f, floorPoints[i].z);
            mean /= floorPoints.Count;

            float roomMin = float.PositiveInfinity;
            float roomMax = float.NegativeInfinity;
            for (int i = 0; i < floorPoints.Count; i++)
            {
                float projection = Vector3.Dot(floorPoints[i] - mean, axis);
                roomMin = Mathf.Min(roomMin, projection);
                roomMax = Mathf.Max(roomMax, projection);
            }

            GetModelProjectionRange(
                context.ModelRoot,
                context.MapSpace,
                mean,
                axis,
                out float modelMin,
                out float modelMax);
            float startProjection = Mathf.Max(
                modelMin,
                roomMin - MaximumEndpointExtensionMeters);
            float endProjection = Mathf.Min(
                modelMax,
                roomMax + MaximumEndpointExtensionMeters);

            var ordered = floorPoints
                .Select(point => new Vector3(point.x, 0f, point.z))
                .OrderBy(point => Vector3.Dot(point - mean, axis))
                .ToList();
            ordered.Insert(0, mean + axis * startProjection);
            ordered.Add(mean + axis * endProjection);

            var result = new List<Vector3>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3 point = ordered[i];
                if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], point) >= 1f)
                    result.Add(point);
            }

            return result;
        }

        private static List<B9BuildingDefinition.HandoverSegmentDefinition>
            BuildSegmentDefinitions(IReadOnlyList<Vector3> centerline)
        {
            var result = new List<B9BuildingDefinition.HandoverSegmentDefinition>(
                Mathf.Max(0, centerline.Count - 1));
            for (int i = 0; i < centerline.Count - 1; i++)
            {
                result.Add(new B9BuildingDefinition.HandoverSegmentDefinition(
                    centerline[i],
                    centerline[i + 1],
                    InnerWidthMeters,
                    VolumeHeightMeters,
                    VolumeVerticalCenterMeters));
            }

            return result;
        }

        private static List<B9BuildingDefinition.PortalDefinition> BuildPortalDefinitions(
            B9SceneContext context,
            B9BuildingDefinition definition)
        {
            var result = new List<B9BuildingDefinition.PortalDefinition>
            {
                new B9BuildingDefinition.PortalDefinition(
                    PrimaryPortalId,
                    "Cửa chính B9",
                    "F1",
                    true,
                    context.OutdoorEntranceAnchor.position,
                    context.OutdoorEntranceAnchor.rotation,
                    context.MapSpace.InverseTransformPoint(context.IndoorEntranceAnchor.position),
                    Quaternion.Inverse(context.MapSpace.rotation)
                    * context.IndoorEntranceAnchor.rotation),
            };

            for (int i = 0; i < definition.Portals.Count; i++)
            {
                B9BuildingDefinition.PortalDefinition existing = definition.Portals[i];
                if (existing == null || string.Equals(
                        existing.PortalId,
                        PrimaryPortalId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(new B9BuildingDefinition.PortalDefinition(
                    existing.PortalId,
                    existing.DisplayName,
                    existing.FloorId,
                    false,
                    existing.OutdoorCampusPosition,
                    existing.OutdoorCampusRotation,
                    existing.IndoorMapLocalPosition,
                    existing.IndoorMapLocalRotation));
            }

            return result;
        }

        private static B9HandoverVolume CreateVolume(
            Transform parent,
            string name,
            B9HandoverVolume.VolumeKind kind,
            IReadOnlyList<B9BuildingDefinition.HandoverSegmentDefinition> definitions,
            float padding)
        {
            GameObject volumeGo = new GameObject(name);
            volumeGo.transform.SetParent(parent, false);
            B9HandoverVolume volume = volumeGo.AddComponent<B9HandoverVolume>();
            var colliders = new List<BoxCollider>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                B9BuildingDefinition.HandoverSegmentDefinition definition = definitions[i];
                Vector3 start = definition.StartMapLocalPosition;
                Vector3 end = definition.EndMapLocalPosition;
                start.y = definition.VerticalCenterMeters;
                end.y = definition.VerticalCenterMeters;
                Vector3 direction = end - start;
                direction.y = 0f;
                float length = direction.magnitude;
                if (length < 0.1f)
                    continue;

                GameObject segmentGo = new GameObject($"{kind} Segment {i + 1:00}");
                segmentGo.layer = 2;
                segmentGo.transform.SetParent(volumeGo.transform, false);
                segmentGo.transform.localPosition = (start + end) * 0.5f;
                segmentGo.transform.localRotation = Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
                BoxCollider box = segmentGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(
                    definition.InnerWidthMeters + padding * 2f,
                    definition.HeightMeters + padding * 2f,
                    length + SegmentOverlapMeters + padding * 2f);
                colliders.Add(box);
            }

            volume.Configure(kind, colliders);
            EditorUtility.SetDirty(volume);
            return volume;
        }

        private static List<B9PortalAnchor> CreatePortalRegistry(
            Transform parent,
            B9SceneContext context,
            IReadOnlyList<B9BuildingDefinition.PortalDefinition> definitions)
        {
            GameObject registryGo = new GameObject("B9 Portal Registry");
            registryGo.transform.SetParent(parent, false);
            var result = new List<B9PortalAnchor>(definitions.Count);

            for (int i = 0; i < definitions.Count; i++)
            {
                B9BuildingDefinition.PortalDefinition definition = definitions[i];
                Transform outdoorAnchor;
                Transform indoorAnchor;
                if (string.Equals(
                        definition.PortalId,
                        PrimaryPortalId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    outdoorAnchor = context.OutdoorEntranceAnchor;
                    indoorAnchor = context.IndoorEntranceAnchor;
                }
                else
                {
                    outdoorAnchor = CreateOutdoorAnchor(context, definition);
                    indoorAnchor = CreateIndoorAnchor(context, definition);
                }

                GameObject portalGo = new GameObject(
                    $"{definition.PortalId} · {definition.DisplayName}");
                portalGo.transform.SetParent(registryGo.transform, false);
                B9PortalAnchor portal = portalGo.AddComponent<B9PortalAnchor>();
                portal.Configure(
                    definition.PortalId,
                    definition.DisplayName,
                    definition.FloorId,
                    definition.Primary,
                    outdoorAnchor,
                    indoorAnchor);
                result.Add(portal);
            }

            return result;
        }

        private static Transform CreateOutdoorAnchor(
            B9SceneContext context,
            B9BuildingDefinition.PortalDefinition definition)
        {
            Transform parent = context.OutdoorEntranceAnchor.parent;
            Transform existing = parent != null
                ? parent.Find("Outdoor Portal " + definition.PortalId)
                : null;
            GameObject anchorGo = existing != null
                ? existing.gameObject
                : new GameObject("Outdoor Portal " + definition.PortalId);
            anchorGo.transform.SetParent(parent, true);
            anchorGo.transform.SetPositionAndRotation(
                definition.OutdoorCampusPosition,
                definition.OutdoorCampusRotation);
            return anchorGo.transform;
        }

        private static Transform CreateIndoorAnchor(
            B9SceneContext context,
            B9BuildingDefinition.PortalDefinition definition)
        {
            Transform existing = context.MapSpace.Find("Indoor Portal " + definition.PortalId);
            GameObject anchorGo = existing != null
                ? existing.gameObject
                : new GameObject("Indoor Portal " + definition.PortalId);
            anchorGo.transform.SetParent(context.MapSpace, false);
            anchorGo.transform.localPosition = definition.IndoorMapLocalPosition;
            anchorGo.transform.localRotation = definition.IndoorMapLocalRotation;
            return anchorGo.transform;
        }

        private static void FindFarthestPair(
            IReadOnlyList<Vector3> points,
            out Vector3 first,
            out Vector3 second)
        {
            first = points[0];
            second = points[1];
            float best = 0f;
            for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                Vector3 delta = points[j] - points[i];
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance > best)
                {
                    best = distance;
                    first = points[i];
                    second = points[j];
                }
            }
        }

        private static void GetModelProjectionRange(
            Transform model,
            Transform mapSpace,
            Vector3 mean,
            Vector3 axis,
            out float minimum,
            out float maximum)
        {
            minimum = float.PositiveInfinity;
            maximum = float.NegativeInfinity;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds bounds = renderers[i].bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 worldCorner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 localCorner = mapSpace.InverseTransformPoint(worldCorner);
                    float projection = Vector3.Dot(localCorner - mean, axis);
                    minimum = Mathf.Min(minimum, projection);
                    maximum = Mathf.Max(maximum, projection);
                }
            }

            if (!float.IsFinite(minimum) || !float.IsFinite(maximum))
            {
                minimum = -20f;
                maximum = 20f;
            }
        }

        private static void DestroyExistingGeometry()
        {
            B9BuildingTransitionGeometry[] existing =
                UnityEngine.Object.FindObjectsByType<B9BuildingTransitionGeometry>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                    UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
            }

            GameObject namedRoot = GameObject.Find(RootName);
            if (namedRoot != null)
                UnityEngine.Object.DestroyImmediate(namedRoot);
        }
    }
}
