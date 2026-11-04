#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Builds the AppShell scene: camera, AppCanvas and a NavigationManager that routes to every Canvas screen
    /// whose prefab exists. Screens are tried in <see cref="UiCanvasShellRoutes.All"/> order and the first one is the start page.
    /// </summary>
    public static class UiCanvasShellSceneBuilder
    {
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the shell scene.");
                return;
            }

            var routes = new List<UiCanvasShellRoutes.Route>();
            foreach (UiCanvasShellRoutes.Route route in UiCanvasShellRoutes.All)
            {
                if (AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(route.Screen)) != null)
                {
                    routes.Add(route);
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.Background;

            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.AppCanvasPrefab);
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
            var pages = (RectTransform)canvas.transform.Find(UiCanvasPrefabBuilder.PagesName);

            var navigationGo = new GameObject("NavigationManager");
            var navigation = navigationGo.AddComponent<NavigationManager>();

            var serialized = new SerializedObject(navigation);
            SerializedProperty list = serialized.FindProperty("pages");
            list.arraySize = routes.Count;
            for (int i = 0; i < routes.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").enumValueIndex = (int)routes[i].Id;
                entry.FindPropertyRelative("canvasPage").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(routes[i].Screen));
            }

            serialized.FindProperty("firstPage").enumValueIndex = (int)PageID.SplashPage;
            serialized.FindProperty("canvasRoot").objectReferenceValue = canvas;
            serialized.FindProperty("canvasPageContainer").objectReferenceValue = pages;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(UiCanvasPaths.ShellScene));
            EditorSceneManager.SaveScene(scene, UiCanvasPaths.ShellScene);
            Debug.Log("[UiCanvas] " + UiCanvasPaths.ShellScene + " built with " + routes.Count + " routes");
        }
    }
}
#endif
