using System;
using ARNavB9V2.Experiment;
using ARNavB9V2.Reliability;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2ReliabilitySteps5To7Builder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string RootName = "[B9 V2] Reliability Steps 5-7";

        [MenuItem("Tools/AR Navigation V2/Reliability Steps 5-7 - Exit + UI + Log Export")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9OutdoorSceneContext outdoor = Required<B9OutdoorSceneContext>("outdoor navigation");
            B9ReliableNavigationController reliability =
                Required<B9ReliableNavigationController>("reliability controller");
            B9ExperimentLogger logger = Required<B9ExperimentLogger>("three-file logger");

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject root = new GameObject(RootName);
            B9ExperimentLogExporter exporter = root.AddComponent<B9ExperimentLogExporter>();
            exporter.Configure(logger);
            outdoor.NavigationHud.AttachReliability(reliability);
            outdoor.NavigationHud.AttachLogExporter(exporter);

            EditorUtility.SetDirty(exporter);
            EditorUtility.SetDirty(outdoor.NavigationHud);
            EditorUtility.SetDirty(reliability);
            ConfigureIosProject();
            EnsureOnlyV2SceneStartsApplication();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save reliability Steps 5-7.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log(
                "[B9V2 Reliability Steps5-7] COMPLETE nearest-exit indoor route, "
                + "PDR-to-stable-GPS handover, cancel/change/continue UI, and "
                + "three-CSV iPhone export bundle.");
        }

        private static T Required<T>(string label) where T : UnityEngine.Object
        {
            T value = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (value == null)
                throw new InvalidOperationException("Missing " + label + ".");
            return value;
        }

        private static void ConfigureIosProject()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.iOS,
                ScriptingImplementation.IL2CPP);
        }

        private static void EnsureOnlyV2SceneStartsApplication()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool found = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                bool isV2 = string.Equals(
                    scenes[i].path,
                    ScenePath,
                    StringComparison.OrdinalIgnoreCase);
                scenes[i].enabled = isV2;
                found |= isV2;
            }

            if (!found)
            {
                var result = new EditorBuildSettingsScene[scenes.Length + 1];
                result[0] = new EditorBuildSettingsScene(ScenePath, true);
                Array.Copy(scenes, 0, result, 1, scenes.Length);
                scenes = result;
            }
            EditorBuildSettings.scenes = scenes;
        }
    }
}
