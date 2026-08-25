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
    public static class B9V2ReliabilitySteps2To4Builder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string RootName = "[B9 V2] Reliability Steps 2-4";

        [MenuItem("Tools/AR Navigation V2/Reliability Steps 2-4 - GPS PDR VPS + 3 Logs")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9SceneContext foundation = Required<B9SceneContext>("B9 foundation");
            B9OutdoorSceneContext outdoor = Required<B9OutdoorSceneContext>("outdoor navigation");
            B9VpsSceneContext vps = Required<B9VpsSceneContext>("MultiSet VPS");
            B9IndoorSceneContext indoor = Required<B9IndoorSceneContext>("indoor navigation");
            B9ExperimentLogger logger = Required<B9ExperimentLogger>("three-file experiment logger");

            if (!foundation.ValidateConfiguration(out string reason))
                throw new InvalidOperationException("B9 foundation invalid: " + reason);
            if (!foundation.ValidateHandoverConfiguration(out reason))
                throw new InvalidOperationException("B9 handover geometry invalid: " + reason);
            if (!outdoor.ValidateConfiguration(out reason))
                throw new InvalidOperationException("Outdoor navigation invalid: " + reason);
            if (!vps.ValidateConfiguration(out reason))
                throw new InvalidOperationException("MultiSet VPS invalid: " + reason);
            if (!indoor.ValidateConfiguration(out reason))
                throw new InvalidOperationException("Indoor navigation invalid: " + reason);

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            GameObject root = new GameObject(RootName);
            GameObject pdrObject = new GameObject("Entrance Transition PDR");
            pdrObject.transform.SetParent(root.transform, false);
            B9TransitionPdrTracker pdr = pdrObject.AddComponent<B9TransitionPdrTracker>();
            pdr.Configure(foundation.ArCamera);

            GameObject controllerObject = new GameObject("GPS PDR VPS Reliability State Machine");
            controllerObject.transform.SetParent(root.transform, false);
            B9ReliableNavigationController controller =
                controllerObject.AddComponent<B9ReliableNavigationController>();
            controller.Configure(
                foundation,
                outdoor,
                vps.TransitionController,
                indoor,
                foundation.HandoverGeometry,
                pdr);

            vps.TransitionController.SetExternalHandoverControl(true);
            outdoor.NavigationHud.AttachReliability(controller);
            logger.AttachReliability(controller, pdr);
            EditorUtility.SetDirty(vps.TransitionController);
            EditorUtility.SetDirty(outdoor.NavigationHud);
            EditorUtility.SetDirty(logger);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(pdr);

            if (!controller.ValidateConfiguration(out reason))
                throw new InvalidOperationException("Reliability Steps 2-4 invalid: " + reason);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save B9 reliability Steps 2-4.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log(
                "[B9V2 Reliability Steps2-4] COMPLETE outer collider -> PDR, "
                + "inner collider -> MultiSet VPS, continuous route preview, and "
                + "events/samples/summary CSV bundle.");
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
