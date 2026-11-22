#if UNITY_EDITOR
using System.Collections.Generic;
using ARNavB9V2.UI;
using Game.Features.Home;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Puts the Canvas guidance overlay into the B9 navigation scene in place of the UI Toolkit document:
    /// removes the UIDocument from "B9 Navigation HUD" (the B9NavigationHud logic and its references stay),
    /// adds one NavigationHudUI instance wired to it, deletes the unused panel settings, and lists the scenes
    /// in the build (AppShell first, the navigation scene loaded on top of it).
    /// </summary>
    public static class UiCanvasNavigationSceneInstaller
    {
        public const string NavigationScene = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";

        private static readonly string[] LegacyUiToolkitAssets =
        {
            "Assets/ARNavB9V2/UI/B9NavigationPanelSettings.asset",
            "Assets/ARNavB9V2/UI/UI Login.uss",
        };

        [MenuItem("Tools/UI Canvas/Install Navigation HUD In B9 Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before installing the navigation HUD.");
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != NavigationScene)
            {
                Debug.LogError("[UiCanvas] Save " + active.path + " first: installing opens the navigation scene.");
                return;
            }

            Scene scene = active.path == NavigationScene ? active : EditorSceneManager.OpenScene(NavigationScene, OpenSceneMode.Single);
            B9NavigationHud hud = FindInScene<B9NavigationHud>(scene);
            if (hud == null)
            {
                Debug.LogError("[UiCanvas] No B9NavigationHud in " + NavigationScene + "; run the AR Navigation V2 builders first.");
                return;
            }

            // The logic component keeps its serialized references; only the UI Toolkit document goes.
            foreach (Component component in hud.GetComponents<Component>())
            {
                if (component != null && component.GetType().FullName == "UnityEngine.UIElements.UIDocument")
                {
                    Object.DestroyImmediate(component, true);
                }
            }

            NavigationHudUI view = FindInScene<NavigationHudUI>(scene);
            if (view == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(UiCanvasNavigationHudBuilder.HudName));
                if (prefab == null)
                {
                    Debug.LogError("[UiCanvas] Build the navigation HUD prefab first (Tools/UI Canvas/Build Navigation HUD).");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                view = instance.GetComponent<NavigationHudUI>();
            }

            var serialized = new SerializedObject(view);
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            foreach (string path in LegacyUiToolkitAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

            EnsureBuildScenes();
            Debug.Log("[UiCanvas] NavigationHudUI installed in " + NavigationScene);
        }

        /// <summary>AppShell starts the app; the navigation scene is loaded on top when the AR page opens.</summary>
        public static void EnsureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(UiCanvasPaths.ShellScene, true),
                new EditorBuildSettingsScene(NavigationScene, true),
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != UiCanvasPaths.ShellScene && existing.path != NavigationScene)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
#endif
