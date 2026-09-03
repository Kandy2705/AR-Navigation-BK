using System;
using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using ARNavB9V2.Reliability;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2HarmonyVersionsBuilder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string RootName = "[B9 V2] HARMONY V1-V5 Experiment";

        [MenuItem("Tools/AR Navigation V2/Add HARMONY V1-V5 Experiment Selector")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9OutdoorSceneContext outdoor = Required<B9OutdoorSceneContext>("outdoor navigation");
            B9VpsSceneContext vps = Required<B9VpsSceneContext>("MultiSet VPS");
            B9IndoorSceneContext indoor = Required<B9IndoorSceneContext>("indoor navigation");
            B9ReliableNavigationController reliability =
                Required<B9ReliableNavigationController>("reliability controller");
            B9TransitionPdrTracker pdr = Required<B9TransitionPdrTracker>("transition PDR");
            B9ExperimentLogger logger = Required<B9ExperimentLogger>("three-file experiment logger");
            B9IndoorPoseTracker pose = indoor.PoseTracker != null
                ? indoor.PoseTracker
                : Required<B9IndoorPoseTracker>("indoor pose tracker");

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject root = new GameObject(RootName);
            B9HarmonyExperimentController controller =
                root.AddComponent<B9HarmonyExperimentController>();
            controller.Configure(
                reliability,
                vps.TransitionController,
                outdoor.LocationProvider,
                pdr,
                pose,
                outdoor.RibbonRenderer,
                indoor.RibbonRenderer,
                logger);

            outdoor.NavigationHud.AttachHarmonyExperiment(controller);
            logger.SetExperimentProfile(
                B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony),
                restartActiveTrial: false);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(outdoor.NavigationHud);
            EditorUtility.SetDirty(logger);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save HARMONY V1-V5 selector.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log(
                "[B9V2 HARMONY] COMPLETE V1 Fixed Geometric, V2 Reliable Handover, "
                + "V3 No Dwell, V4 No Map-ID, V5 Full HARMONY; selector and CSV flags connected.");
        }

        private static T Required<T>(string label) where T : UnityEngine.Object
        {
            T value = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (value == null)
                throw new InvalidOperationException("Missing " + label + ".");
            return value;
        }
    }
}
