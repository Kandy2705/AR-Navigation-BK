using System;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2Step4Builder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string Step4RootName = "[B9 V2] Indoor Navigation Step 4";
        private const string CenterMaterialPath = "Assets/ARNavB9V2/Art/Outdoor/RouteBlue.mat";
        private const string BorderMaterialPath = "Assets/ARNavB9V2/Art/Outdoor/RouteBorder.mat";
        private const string UserMaterialPath = "Assets/ARNavB9V2/Art/Outdoor/UserMarker.mat";
        private const string DestinationMaterialPath = "Assets/ARNavB9V2/Art/Outdoor/EntranceMarker.mat";

        [MenuItem("Tools/AR Navigation V2/Step 4 - Build Indoor Room Navigation")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9SceneContext foundation = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            B9OutdoorSceneContext outdoor = UnityEngine.Object.FindFirstObjectByType<B9OutdoorSceneContext>(
                FindObjectsInactive.Include);
            B9VpsSceneContext vps = UnityEngine.Object.FindFirstObjectByType<B9VpsSceneContext>(
                FindObjectsInactive.Include);
            if (foundation == null)
                throw new InvalidOperationException("B9 foundation context is missing.");
            if (!foundation.ValidateConfiguration(out string foundationFailure))
                throw new InvalidOperationException("B9 foundation invalid: " + foundationFailure);
            if (outdoor == null)
                throw new InvalidOperationException("B9 outdoor context is missing.");
            if (!outdoor.ValidateConfiguration(out string outdoorFailure))
                throw new InvalidOperationException("B9 outdoor invalid: " + outdoorFailure);
            if (vps == null)
                throw new InvalidOperationException("B9 VPS context is missing.");
            if (!vps.ValidateConfiguration(out string vpsFailure))
                throw new InvalidOperationException("B9 VPS invalid: " + vpsFailure);

            GameObject oldRoot = GameObject.Find(Step4RootName);
            if (oldRoot != null)
                UnityEngine.Object.DestroyImmediate(oldRoot);

            GameObject root = new GameObject(Step4RootName);
            B9IndoorSceneContext context = root.AddComponent<B9IndoorSceneContext>();

            B9RouteRibbonRenderer ribbon = CreateRouteRenderer(root.transform, foundation.ArCamera);
            GameObject runtime = new GameObject("Indoor Navigation Runtime");
            runtime.transform.SetParent(root.transform, false);
            B9IndoorRouteController route = runtime.AddComponent<B9IndoorRouteController>();
            string defaultRoom = !string.IsNullOrWhiteSpace(outdoor.RouteController.SelectedRoomId)
                ? outdoor.RouteController.SelectedRoomId
                : "B9-104";
            route.Configure(
                foundation.Building,
                foundation,
                foundation.ArCamera,
                foundation.NavMeshSurface,
                ribbon,
                defaultRoom);

            GameObject markerRoot = new GameObject("Indoor Minimap Markers");
            markerRoot.transform.SetParent(root.transform, false);
            Material userMaterial = RequiredMaterial(UserMaterialPath);
            Material destinationMaterial = RequiredMaterial(DestinationMaterialPath);
            Transform userMarker = CreateUserMarker(markerRoot.transform, userMaterial);
            Transform destinationMarker = CreateDestinationMarker(
                markerRoot.transform,
                destinationMaterial);

            B9IndoorMinimapController minimap = runtime.AddComponent<B9IndoorMinimapController>();
            minimap.Configure(
                foundation,
                route,
                foundation.MinimapCamera,
                outdoor.MinimapController.RenderedTexture,
                userMarker,
                destinationMarker);

            context.Configure(route, ribbon, minimap, userMarker, destinationMarker);
            vps.TransitionController.AttachIndoorNavigation(context);
            outdoor.NavigationHud.AttachIndoorNavigation(route, minimap);
            context.PrepareForLocalization();

            if (!context.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 indoor navigation invalid: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save B9 V2 scene after indoor navigation build.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[B9V2 Step4] COMPLETE indoor room navigation: VPS pose → B9 NavMesh → "
                + defaultRoom
                + ". AR ribbon and follow/overview minimap are ready.");
        }

        private static B9RouteRibbonRenderer CreateRouteRenderer(
            Transform parent,
            Camera arCamera)
        {
            GameObject routeGo = new GameObject("Indoor Route Ribbon");
            routeGo.transform.SetParent(parent, false);
            B9RouteRibbonRenderer ribbon = routeGo.AddComponent<B9RouteRibbonRenderer>();
            ribbon.Configure(
                RequiredMaterial(BorderMaterialPath),
                RequiredMaterial(CenterMaterialPath),
                arCamera);
            ribbon.ConfigureRouteStyle(
                widthMeters: 0.65f,
                sideBorderMeters: 0.08f,
                verticalOffsetMeters: 0.04f,
                useEstimatedCameraGround: false);
            ribbon.ConfigureMinimapPresentation(3.2f, 12f);
            return ribbon;
        }

        private static Transform CreateUserMarker(Transform parent, Material material)
        {
            GameObject root = new GameObject("Indoor User Position + Heading");
            root.transform.SetParent(parent, false);

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.name = "Indoor Position Dot";
            dot.transform.SetParent(root.transform, false);
            dot.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
            AssignMarkerMaterial(dot, material);

            GameObject heading = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heading.name = "Indoor Heading Needle";
            heading.transform.SetParent(root.transform, false);
            heading.transform.localPosition = new Vector3(0f, 0.12f, 1.2f);
            heading.transform.localScale = new Vector3(0.45f, 0.1f, 1.7f);
            AssignMarkerMaterial(heading, material);
            SetLayerRecursively(root, LayerMask.NameToLayer("MinimapOnly"));
            return root.transform;
        }

        private static Transform CreateDestinationMarker(Transform parent, Material material)
        {
            GameObject root = new GameObject("Indoor Destination Marker");
            root.transform.SetParent(parent, false);

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.name = "Indoor Destination Dot";
            dot.transform.SetParent(root.transform, false);
            dot.transform.localScale = new Vector3(2.1f, 0.08f, 2.1f);
            AssignMarkerMaterial(dot, material);
            SetLayerRecursively(root, LayerMask.NameToLayer("MinimapOnly"));
            return root.transform;
        }

        private static void AssignMarkerMaterial(GameObject marker, Material material)
        {
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material RequiredMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new InvalidOperationException("Required B9 V2 material missing: " + path);
            return material;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
                return;
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }
    }
}
