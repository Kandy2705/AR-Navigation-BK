using System;
using System.Collections.Generic;
using System.Linq;
using ARNavB9V2.Data;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using ARNavB9V2.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ARNavB9V2.Editor
{
    public static class B9V2Step2Builder
    {
        private const double SchoolGroundOriginLatitude = 10.7734854d;
        private const double SchoolGroundOriginLongitude = 106.6590233d;
        private const string ReferenceScenePath = "Assets/Scenes/HybridGPSMap.unity";
        private const string V2ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string BuildingDefinitionPath = "Assets/ARNavB9V2/Data/Buildings/B9BuildingDefinition.asset";
        private const string OutdoorDefinitionPath = "Assets/ARNavB9V2/Data/Buildings/B9OutdoorMapDefinition.asset";
        private const string SchoolGroundPrefabPath = "Assets/ARNavB9V2/Art/Outdoor/SchoolGroundV2.prefab";
        private const string SchoolGroundNavMeshPath = "Assets/Scenes/HybridGPSMap/NavMesh-SchoolGround.asset";
        private const string PanelSettingsPath = "Assets/ARNavB9V2/UI/B9NavigationPanelSettings.asset";
        private const string MinimapTexturePath = "Assets/ARNavB9V2/Art/Outdoor/B9OutdoorMinimap.renderTexture";
        private const string LegacyChevronTexturePath = "Assets/Sprites/Line 1.png";
        private const string Step2RootName = "[B9 V2] Outdoor Step 2";

        private sealed class OutdoorInventory
        {
            public double originLatitude = SchoolGroundOriginLatitude;
            public double originLongitude = SchoolGroundOriginLongitude;
            public Vector3 entrancePosition = new Vector3(153.83f, 0f, -3.19f);
            public Bounds schoolGroundBounds;
            public Vector3 mockStartPosition;
        }

        [MenuItem("Tools/AR Navigation V2/Step 2 - Build SchoolGround Outdoor Route")]
        public static void Build()
        {
            EnsureFolders();
            OutdoorInventory inventory = CaptureReferenceAndSchoolGround();
            B9OutdoorMapDefinition outdoorDefinition = CreateOrUpdateOutdoorDefinition(inventory);
            BuildOutdoorScene(outdoorDefinition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[B9V2 Step2] COMPLETE scene={V2ScenePath} map=SchoolGround " +
                $"origin=({inventory.originLatitude:F7},{inventory.originLongitude:F7}) " +
                $"entranceGps=({outdoorDefinition.EntranceLatitude:F7},{outdoorDefinition.EntranceLongitude:F7}) " +
                $"mock={inventory.mockStartPosition:F2}");
        }

        [MenuItem("Tools/AR Navigation V2/Test/Toggle V2 Play Mode")]
        public static void ToggleV2PlayMode()
        {
            EditorApplication.isPlaying = !EditorApplication.isPlaying;
        }

        private static OutdoorInventory CaptureReferenceAndSchoolGround()
        {
            UnityEngine.SceneManagement.Scene reference = EditorSceneManager.OpenScene(
                ReferenceScenePath,
                OpenSceneMode.Single);

            Transform schoolGround = FindTransform(reference, "SchoolGround");
            if (schoolGround == null)
                throw new InvalidOperationException("SchoolGround was not found in the reference scene.");

            Renderer schoolGroundRenderer = schoolGround.GetComponent<Renderer>();
            if (schoolGroundRenderer == null)
                throw new InvalidOperationException("SchoolGround does not have a renderer.");

            Transform entrance = FindTransform(reference, "Entrance_B9");
            if (entrance == null)
                throw new InvalidOperationException("Entrance_B9 was not found in the reference scene.");

            var inventory = new OutdoorInventory
            {
                entrancePosition = entrance.position,
                schoolGroundBounds = schoolGroundRenderer.bounds,
            };
            NavMeshData navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(SchoolGroundNavMeshPath);
            if (navMeshData == null)
                throw new InvalidOperationException($"SchoolGround NavMesh missing: {SchoolGroundNavMeshPath}");

            inventory.mockStartPosition = FindUsefulMockStart(
                inventory.entrancePosition,
                inventory.schoolGroundBounds,
                navMeshData);
            SaveSchoolGroundPrefab(schoolGround.gameObject, navMeshData);

            Debug.Log(
                $"[B9V2 Step2] SchoolGround bounds={inventory.schoolGroundBounds.size:F1}, " +
                $"entrance={inventory.entrancePosition:F2}, mockStart={inventory.mockStartPosition:F2}");
            return inventory;
        }

        private static Vector3 FindUsefulMockStart(
            Vector3 entrance,
            Bounds schoolBounds,
            NavMeshData navMeshData)
        {
            NavMeshDataInstance temporaryInstance = default;
            bool addedTemporarily = !NavMesh.SamplePosition(
                entrance,
                out _,
                10f,
                NavMesh.AllAreas);
            if (addedTemporarily)
                temporaryInstance = NavMesh.AddNavMeshData(navMeshData);

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            Vector3 result = entrance + new Vector3(-55f, 0f, 65f);
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < triangulation.vertices.Length; i++)
            {
                Vector3 candidate = triangulation.vertices[i];
                float distance = Vector3.Distance(candidate, entrance);
                bool insideMap = candidate.x >= schoolBounds.min.x && candidate.x <= schoolBounds.max.x
                                 && candidate.z >= schoolBounds.min.z && candidate.z <= schoolBounds.max.z;
                if (!insideMap || Mathf.Abs(candidate.y - entrance.y) > 3f
                    || distance < 45f || distance > 140f)
                    continue;

                float score = Mathf.Abs(distance - 85f);
                if (score < bestScore)
                {
                    bestScore = score;
                    result = candidate;
                }
            }

            if (addedTemporarily && temporaryInstance.valid)
                temporaryInstance.Remove();
            return result;
        }

        private static void SaveSchoolGroundPrefab(GameObject source, NavMeshData navMeshData)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source);
            clone.name = "SchoolGround (Top-Down Outdoor Map)";
            clone.transform.SetParent(null, true);
            NavMeshSurface surface = clone.GetComponent<NavMeshSurface>();
            if (surface == null) surface = clone.AddComponent<NavMeshSurface>();
            surface.navMeshData = navMeshData;

            PrefabUtility.SaveAsPrefabAsset(clone, SchoolGroundPrefabPath, out bool success);
            UnityEngine.Object.DestroyImmediate(clone);
            if (!success)
                throw new InvalidOperationException($"Could not save SchoolGround prefab: {SchoolGroundPrefabPath}");
        }

        private static B9OutdoorMapDefinition CreateOrUpdateOutdoorDefinition(OutdoorInventory inventory)
        {
            B9OutdoorMapDefinition definition =
                AssetDatabase.LoadAssetAtPath<B9OutdoorMapDefinition>(OutdoorDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<B9OutdoorMapDefinition>();
                AssetDatabase.CreateAsset(definition, OutdoorDefinitionPath);
            }

            definition.Configure(
                inventory.originLatitude,
                inventory.originLongitude,
                inventory.entrancePosition,
                inventory.schoolGroundBounds,
                inventory.mockStartPosition,
                15f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void BuildOutdoorScene(B9OutdoorMapDefinition outdoorDefinition)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                V2ScenePath,
                OpenSceneMode.Single);
            B9SceneContext foundation = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            if (foundation == null)
                throw new InvalidOperationException("B9 foundation context is missing.");
            if (!foundation.ValidateConfiguration(out string foundationFailure))
                throw new InvalidOperationException("B9 foundation is invalid: " + foundationFailure);

            GameObject existing = GameObject.Find(Step2RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            foundation.ModelRoot.gameObject.SetActive(false);

            GameObject root = new GameObject(Step2RootName);
            B9OutdoorSceneContext context = root.AddComponent<B9OutdoorSceneContext>();

            GameObject content = new GameObject("SchoolGround Outdoor Content");
            content.transform.SetParent(root.transform, false);
            GameObject groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SchoolGroundPrefabPath);
            GameObject ground = PrefabUtility.InstantiatePrefab(groundPrefab, scene) as GameObject;
            if (ground == null)
                throw new InvalidOperationException("Could not instantiate SchoolGround V2 prefab.");
            ground.transform.SetParent(content.transform, false);
            NavMeshSurface outdoorSurface = ground.GetComponent<NavMeshSurface>();
            NavMeshData navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(SchoolGroundNavMeshPath);
            if (outdoorSurface == null) outdoorSurface = ground.AddComponent<NavMeshSurface>();
            outdoorSurface.navMeshData = navMeshData;

            GameObject runtime = new GameObject("Outdoor Runtime");
            runtime.transform.SetParent(root.transform, false);
            B9OutdoorLocationProvider location = runtime.AddComponent<B9OutdoorLocationProvider>();
            location.Configure(
                outdoorDefinition,
                enableEditorMock: true,
                outdoorDefinition.EditorMockStartCampusPosition,
                0f);
            B9OutdoorPoseController pose = runtime.AddComponent<B9OutdoorPoseController>();
            pose.Configure(location, foundation.XrOrigin, foundation.ArCamera);

            GameObject ribbonGo = new GameObject("Outdoor Route Ribbon");
            ribbonGo.transform.SetParent(root.transform, false);
            B9RouteRibbonRenderer ribbon = ribbonGo.AddComponent<B9RouteRibbonRenderer>();
            Material borderMaterial = CreateOrUpdateMaterial(
                "Assets/ARNavB9V2/Art/Outdoor/RouteBorder.mat",
                Color.white);
            Material routeMaterial = CreateOrUpdateMaterial(
                "Assets/ARNavB9V2/Art/Outdoor/RouteBlue.mat",
                Color.white);
            Texture2D chevronTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyChevronTexturePath);
            if (chevronTexture == null)
                throw new InvalidOperationException($"Legacy route artwork missing: {LegacyChevronTexturePath}");
            ConfigureRouteCenterMaterial(routeMaterial, chevronTexture);
            ribbon.Configure(borderMaterial, routeMaterial, foundation.ArCamera);
            ribbon.ConfigureMinimapPresentation(3.2f, 3f);

            B9OutdoorRouteController route = runtime.AddComponent<B9OutdoorRouteController>();
            B9BuildingDefinition building =
                AssetDatabase.LoadAssetAtPath<B9BuildingDefinition>(BuildingDefinitionPath);
            route.Configure(
                building,
                outdoorDefinition,
                location,
                foundation.OutdoorEntranceAnchor,
                outdoorSurface,
                ribbon,
                "B9-104");

            GameObject markerRoot = new GameObject("Outdoor Minimap Markers");
            markerRoot.transform.SetParent(root.transform, false);
            Material userMaterial = CreateOrUpdateMaterial(
                "Assets/ARNavB9V2/Art/Outdoor/UserMarker.mat",
                new Color(0.02f, 0.55f, 1f, 1f));
            Material entranceMaterial = CreateOrUpdateMaterial(
                "Assets/ARNavB9V2/Art/Outdoor/EntranceMarker.mat",
                new Color(1f, 0.18f, 0.12f, 1f));
            Transform userMarker = CreateUserMarker(markerRoot.transform, userMaterial);
            Transform entranceMarker = CreateEntranceMarker(markerRoot.transform, entranceMaterial);

            RenderTexture minimapTexture = CreateOrLoadRenderTexture();
            B9OutdoorMinimapController minimap = runtime.AddComponent<B9OutdoorMinimapController>();
            minimap.Configure(
                outdoorDefinition,
                location,
                foundation.MinimapCamera,
                minimapTexture,
                userMarker,
                entranceMarker);
            minimap.ConfigureInteraction(22f, 105f, 0.22f);

            ConfigureArCameraLayers(foundation.ArCamera);
            UIDocument uiDocument = CreateHudDocument(root.transform);
            B9NavigationHud hud = uiDocument.gameObject.AddComponent<B9NavigationHud>();
            hud.Configure(uiDocument, building, location, route, minimap);

            context.Configure(
                outdoorDefinition,
                ground.transform,
                outdoorSurface,
                location,
                pose,
                route,
                ribbon,
                minimap,
                hud,
                userMarker,
                entranceMarker);
            if (!context.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 V2 outdoor validation failed: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, V2ScenePath))
                throw new InvalidOperationException($"Could not save scene: {V2ScenePath}");
        }

        private static Transform CreateUserMarker(Transform parent, Material material)
        {
            GameObject root = new GameObject("User Position + Heading");
            root.transform.SetParent(parent, false);

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.name = "Position Dot";
            dot.transform.SetParent(root.transform, false);
            dot.transform.localScale = new Vector3(1.2f, 0.06f, 1.2f);
            AssignMarkerMaterial(dot, material);

            GameObject heading = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heading.name = "Heading Needle";
            heading.transform.SetParent(root.transform, false);
            heading.transform.localPosition = new Vector3(0f, 0.12f, 0.75f);
            heading.transform.localScale = new Vector3(0.28f, 0.08f, 1.25f);
            AssignMarkerMaterial(heading, material);
            SetLayerRecursively(root, LayerMask.NameToLayer("MinimapOnly"));
            return root.transform;
        }

        private static Transform CreateEntranceMarker(Transform parent, Material material)
        {
            GameObject root = new GameObject("B9 Entrance Marker");
            root.transform.SetParent(parent, false);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Entrance Dot";
            marker.transform.SetParent(root.transform, false);
            marker.transform.localScale = new Vector3(1.5f, 0.08f, 1.5f);
            AssignMarkerMaterial(marker, material);
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

        private static UIDocument CreateHudDocument(Transform parent)
        {
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "B9 Navigation Panel Settings";
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1170, 2532);
                settings.match = 0.5f;
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            GameObject go = new GameObject("B9 Navigation HUD");
            go.transform.SetParent(parent, false);
            UIDocument document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.sortingOrder = 100;
            return document;
        }

        private static RenderTexture CreateOrLoadRenderTexture()
        {
            RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(MinimapTexturePath);
            if (texture != null)
                return texture;

            texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "B9 Outdoor Minimap",
                antiAliasing = 2,
                useMipMap = false,
                autoGenerateMips = false,
            };
            AssetDatabase.CreateAsset(texture, MinimapTexturePath);
            return texture;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    throw new InvalidOperationException("No unlit shader is available for the V2 route.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.renderQueue = 2450;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureRouteCenterMaterial(Material material, Texture2D chevronTexture)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", chevronTexture);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", chevronTexture);
                material.SetTextureScale("_MainTex", Vector2.one);
                material.SetTextureOffset("_MainTex", Vector2.zero);
            }
            material.mainTexture = chevronTexture;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureArCameraLayers(Camera arCamera)
        {
            int mapLayer = LayerMask.NameToLayer("MapPlane");
            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            if (mapLayer >= 0) arCamera.cullingMask &= ~(1 << mapLayer);
            if (minimapLayer >= 0) arCamera.cullingMask &= ~(1 << minimapLayer);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0) return;
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private static List<Component> GetAllComponents(UnityEngine.SceneManagement.Scene scene)
        {
            var result = new List<Component>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    result.AddRange(child.GetComponents<Component>().Where(component => component != null));
            }
            return result;
        }

        private static Transform FindTransform(UnityEngine.SceneManagement.Scene scene, string exactName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == exactName);
                if (found != null) return found;
            }
            return null;
        }

        private static bool HasAncestor(Transform child, string ancestorName)
        {
            for (Transform current = child; current != null; current = current.parent)
            {
                if (current.name == ancestorName) return true;
            }
            return false;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/ARNavB9V2", "Art");
            EnsureFolder("Assets/ARNavB9V2/Art", "Outdoor");
            EnsureFolder("Assets/ARNavB9V2", "UI");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
