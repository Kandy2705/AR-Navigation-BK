using System;
using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2Steps6To8Builder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string RootName = "[B9 V2] Steps 6-8 Completion";

        [MenuItem("Tools/AR Navigation V2/Steps 6-8 - Complete Indoor + Research + iOS")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9SceneContext foundation = Required<B9SceneContext>("B9 foundation");
            B9OutdoorSceneContext outdoor = Required<B9OutdoorSceneContext>("outdoor navigation");
            B9VpsSceneContext vps = Required<B9VpsSceneContext>("MultiSet VPS");
            B9IndoorSceneContext indoor = Required<B9IndoorSceneContext>("indoor navigation");
            if (!foundation.ValidateConfiguration(out string foundationFailure))
                throw new InvalidOperationException("B9 foundation invalid: " + foundationFailure);
            if (!outdoor.ValidateConfiguration(out string outdoorFailure))
                throw new InvalidOperationException("Outdoor navigation invalid: " + outdoorFailure);
            if (!vps.ValidateConfiguration(out string vpsFailure))
                throw new InvalidOperationException("VPS invalid: " + vpsFailure);
            if (!indoor.ValidateConfiguration(out string indoorFailure))
                throw new InvalidOperationException("Indoor navigation invalid: " + indoorFailure);

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject root = new GameObject(RootName);
            B9CompletionSceneContext context = root.AddComponent<B9CompletionSceneContext>();
            B9IosRuntimeGuard iosGuard = root.AddComponent<B9IosRuntimeGuard>();

            GameObject poseObject = new GameObject("Indoor AR + PDR Pose Fusion");
            poseObject.transform.SetParent(root.transform, false);
            B9IndoorPoseTracker poseTracker = poseObject.AddComponent<B9IndoorPoseTracker>();
            poseTracker.Configure(foundation.ArCamera, foundation.NavMeshSurface, vps.TransitionController);
            poseTracker.ConfigureTuning(
                stepLength: 0.68f,
                triggerAcceleration: 0.115f,
                releaseAcceleration: 0.045f,
                minimumStepInterval: 0.28f,
                navMeshRadius: 2.5f);
            indoor.AttachPoseTracking(poseTracker);
            EditorUtility.SetDirty(indoor);
            EditorUtility.SetDirty(indoor.RouteController);
            EditorUtility.SetDirty(indoor.MinimapController);

            GameObject loggerObject = new GameObject("Research Experiment Logger");
            loggerObject.transform.SetParent(root.transform, false);
            B9ExperimentLogger logger = loggerObject.AddComponent<B9ExperimentLogger>();
            logger.Configure(outdoor, vps.TransitionController, indoor, poseTracker, true);

            outdoor.NavigationHud.AttachResearchTools(poseTracker, logger);
            EditorUtility.SetDirty(outdoor.NavigationHud);
            context.Configure(poseTracker, logger, iosGuard);
            if (!context.ValidateConfiguration(out string completionFailure))
                throw new InvalidOperationException("Steps 6-8 invalid: " + completionFailure);

            ConfigureIosProject();
            EnsureOnlyV2SceneStartsApplication();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save B9 V2 completion scene.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[B9V2 Steps6-8] COMPLETE AR/PDR + NavMesh indoor tracking, research CSV "
                + "logger, compact HUD controls, and iPhone runtime/build policy.");
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
                var withV2 = new EditorBuildSettingsScene[scenes.Length + 1];
                withV2[0] = new EditorBuildSettingsScene(ScenePath, true);
                Array.Copy(scenes, 0, withV2, 1, scenes.Length);
                scenes = withV2;
            }
            EditorBuildSettings.scenes = scenes;
        }
    }
}
