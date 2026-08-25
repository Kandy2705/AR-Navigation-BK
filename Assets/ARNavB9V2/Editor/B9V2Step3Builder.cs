using System;
using System.Linq;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2Step3Builder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string SdkManagerPrefabPath =
            "Packages/com.multiset.sdk/Runtime/Prefabs/MultisetSdkManager.prefab";
        private const string LocalizerPrefabPath =
            "Packages/com.multiset.sdk/Runtime/Prefabs/MapLocalizationManager.prefab";
        private const string MultiSetConfigPath =
            "Assets/Samples/MultiSet-SDK/1.9.2/Sample Scenes/Resources/MultiSetConfig.asset";
        private const string Step3RootName = "[B9 V2] VPS Step 3";

        [MenuItem("Tools/AR Navigation V2/Step 3 - Build Automatic B9 VPS Handover")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9SceneContext foundation = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            B9OutdoorSceneContext outdoor = UnityEngine.Object.FindFirstObjectByType<B9OutdoorSceneContext>(
                FindObjectsInactive.Include);
            if (foundation == null)
                throw new InvalidOperationException("B9 foundation context is missing.");
            if (!foundation.ValidateConfiguration(out string foundationFailure))
                throw new InvalidOperationException("B9 foundation invalid: " + foundationFailure);
            if (outdoor == null)
                throw new InvalidOperationException("B9 outdoor context is missing.");
            if (!outdoor.ValidateConfiguration(out string outdoorFailure))
                throw new InvalidOperationException("B9 outdoor step invalid: " + outdoorFailure);

            GameObject oldRoot = GameObject.Find(Step3RootName);
            if (oldRoot != null)
                UnityEngine.Object.DestroyImmediate(oldRoot);

            ResetOutdoorPresentation(foundation, outdoor);
            outdoor.RibbonRenderer.ConfigureGroundReference(foundation.ArCamera);
            outdoor.RibbonRenderer.ConfigureMinimapPresentation(3.2f, 3f);
            outdoor.LocationProvider.ConfigurePresentationSmoothing(0.35f, 0.9f, 6f);
            outdoor.PoseController.ConfigurePositionSmoothing(0.75f, 8f);
            outdoor.RouteController.ConfigureRefreshSmoothing(0.18f, 0.3f);
            outdoor.MinimapController.ConfigureInteraction(22f, 105f, 0.22f);

            GameObject root = new GameObject(Step3RootName);
            B9VpsSceneContext context = root.AddComponent<B9VpsSceneContext>();

            GameObject sdkRoot = InstantiatePackagePrefab(
                SdkManagerPrefabPath,
                scene,
                root.transform,
                "MultiSet SDK Manager (B9 V2)");
            MonoBehaviour sdkManager = FindComponentByTypeName(sdkRoot, "MultisetSdkManager");
            if (sdkManager == null)
                throw new InvalidOperationException("MultisetSdkManager component is missing from SDK prefab.");
            ConfigureSdkAuthentication(sdkManager);

            GameObject localizerRoot = InstantiatePackagePrefab(
                LocalizerPrefabPath,
                scene,
                root.transform,
                "B9 VPS Localizer (Automatic)");
            MonoBehaviour localizer = FindComponentByTypeName(localizerRoot, "MapLocalizationManager");
            if (localizer == null)
                throw new InvalidOperationException("MapLocalizationManager component is missing from SDK prefab.");
            ConfigureB9Localizer(localizer, foundation);
            ConfigureSdkMapMesh(localizerRoot);

            GameObject runtime = new GameObject("Automatic Outdoor To Indoor Handover");
            runtime.transform.SetParent(root.transform, false);
            B9VpsTransitionController transition = runtime.AddComponent<B9VpsTransitionController>();
            B9MapVisibility visibility = foundation.GetComponent<B9MapVisibility>();
            transition.Configure(
                foundation.Building,
                sdkRoot,
                localizer,
                outdoor,
                foundation,
                visibility,
                invokeSdkInEditor: false);

            outdoor.NavigationHud.AttachVpsTransition(transition);
            context.Configure(sdkRoot, localizer, transition);
            if (!context.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 V2 VPS validation failed: " + failure);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save B9 V2 scene after Step 3.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[B9V2 Step3] COMPLETE automatic handover: entrance → "
                + foundation.Building.PrimaryMapCode
                + " VPS → indoor B9. Retry UI appears only after failure.");
        }

        private static GameObject InstantiatePackagePrefab(
            string path,
            UnityEngine.SceneManagement.Scene scene,
            Transform parent,
            string instanceName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Required MultiSet prefab missing: " + path);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate MultiSet prefab: " + path);
            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.SetActive(true);
            return instance;
        }

        private static void ConfigureSdkAuthentication(MonoBehaviour sdkManager)
        {
            UnityEngine.Object config = AssetDatabase.LoadMainAssetAtPath(MultiSetConfigPath);
            if (config == null)
                throw new InvalidOperationException("MultiSetConfig missing: " + MultiSetConfigPath);

            var source = new SerializedObject(config);
            var target = new SerializedObject(sdkManager);
            string clientId = source.FindProperty("clientId")?.stringValue;
            string clientSecret = source.FindProperty("clientSecret")?.stringValue;
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                throw new InvalidOperationException("MultiSetConfig does not contain client credentials.");

            SetString(target, "clientId", clientId);
            SetString(target, "clientSecret", clientSecret);
            SetBool(target, "runtimeAuthentication", false);
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureB9Localizer(
            MonoBehaviour localizer,
            B9SceneContext foundation)
        {
            var serialized = new SerializedObject(localizer);
            SetObject(serialized, "arCamera", foundation.ArCamera);
            SetObject(serialized, "mapSpace", foundation.MapSpace.gameObject);
            SetEnum(serialized, "localizationType", 0); // MultiSet.LocalizationType.Map
            SetString(serialized, "mapOrMapsetCode", foundation.Building.PrimaryMapCode);
            SetBool(serialized, "autoLocalize", false);
            SetBool(serialized, "backgroundLocalization", false);
            SetBool(serialized, "relocalization", false);
            SetInt(serialized, "numberOfFrames", 5);
            SetFloat(serialized, "frameCaptureInterval", 0.6f);
            SetBool(serialized, "enableBlurCheck", false);
            SetBool(serialized, "showAlert", false);
            SetBool(serialized, "firstLocalizationUntilSuccess", true);
            ClearPersistentEvent(serialized, "LocalizationInit");
            ClearPersistentEvent(serialized, "LocalizationRequested");
            ClearPersistentEvent(serialized, "LocalizationSuccess");
            ClearPersistentEvent(serialized, "LocalizationFailure");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSdkMapMesh(GameObject localizerRoot)
        {
            MonoBehaviour meshHandler = FindComponentByTypeName(localizerRoot, "MapMeshHandler");
            if (meshHandler == null)
                return;
            var serialized = new SerializedObject(meshHandler);
            SetEnum(serialized, "meshVisualizationOption", 2); // MultiSet.MeshVisualizationOption.NoMesh
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ResetOutdoorPresentation(
            B9SceneContext foundation,
            B9OutdoorSceneContext outdoor)
        {
            foundation.ModelRoot.gameObject.SetActive(false);
            outdoor.SchoolGround.gameObject.SetActive(true);
            outdoor.RouteController.enabled = true;
            outdoor.LocationProvider.enabled = true;
            outdoor.PoseController.enabled = true;
            outdoor.MinimapController.enabled = true;
            if (outdoor.UserMarker != null) outdoor.UserMarker.gameObject.SetActive(true);
            if (outdoor.EntranceMarker != null) outdoor.EntranceMarker.gameObject.SetActive(true);
            outdoor.RibbonRenderer.ClearPath();
        }

        private static MonoBehaviour FindComponentByTypeName(GameObject root, string typeName)
        {
            return root.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = RequiredProperty(serialized, propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            RequiredProperty(serialized, propertyName).stringValue = value;
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            RequiredProperty(serialized, propertyName).boolValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            RequiredProperty(serialized, propertyName).intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            RequiredProperty(serialized, propertyName).floatValue = value;
        }

        private static void SetEnum(SerializedObject serialized, string propertyName, int value)
        {
            RequiredProperty(serialized, propertyName).enumValueIndex = value;
        }

        private static void ClearPersistentEvent(SerializedObject serialized, string propertyName)
        {
            SerializedProperty eventProperty = RequiredProperty(serialized, propertyName);
            SerializedProperty calls = eventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls != null)
                calls.arraySize = 0;
        }

        private static SerializedProperty RequiredProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + " is missing serialized property " + propertyName);
            return property;
        }
    }
}
