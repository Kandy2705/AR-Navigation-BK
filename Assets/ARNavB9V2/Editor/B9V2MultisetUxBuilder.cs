using System;
using System.Linq;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARNavB9V2.Editor
{
    public static class B9V2MultisetUxBuilder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string LoaderPrefabPath =
            "Packages/com.multiset.sdk/Runtime/Assets/PrefabsUI/LoaderPanel.prefab";
        private const string RootName = "[B9 V2] MultiSet Localization UX";

        [MenuItem("Tools/AR Navigation V2/Step 5 - Build MultiSet Localization UX")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9VpsSceneContext vps = UnityEngine.Object.FindFirstObjectByType<B9VpsSceneContext>(
                FindObjectsInactive.Include);
            if (vps == null)
                throw new InvalidOperationException("B9 VPS context is missing.");
            if (!vps.ValidateConfiguration(out string failure))
                throw new InvalidOperationException("B9 VPS context is invalid: " + failure);

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            vps.TransitionController.ConfigureLocalizationTiming(0.35f);
            EditorUtility.SetDirty(vps.TransitionController);

            GameObject root = new GameObject(RootName);
            GameObject canvasGo = new GameObject("MultiSet Localization Canvas");
            canvasGo.transform.SetParent(root.transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170f, 2532f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject loaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoaderPrefabPath);
            if (loaderPrefab == null)
                throw new InvalidOperationException("Official MultiSet LoaderPanel prefab is missing.");
            GameObject loader = PrefabUtility.InstantiatePrefab(loaderPrefab, scene) as GameObject;
            if (loader == null)
                throw new InvalidOperationException("Could not instantiate MultiSet LoaderPanel.");
            loader.name = "MultiSet VPS Loader (Official UX)";
            loader.transform.SetParent(canvasGo.transform, false);
            LocalizeLoaderText(loader);
            loader.SetActive(false);

            B9MultisetLocalizationUx ux = root.AddComponent<B9MultisetLocalizationUx>();
            ux.Configure(vps.TransitionController, loader);
            EditorUtility.SetDirty(ux);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "Could not save B9 V2 scene after MultiSet localization UX build.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[B9V2 Step5] COMPLETE official MultiSet LoaderPanel UX + resilient "
                + "localization using the official MultiSet defaults. "
                + "Custom indoor route/minimap remain unchanged.");
        }

        private static void LocalizeLoaderText(GameObject loader)
        {
            Text[] labels = loader.GetComponentsInChildren<Text>(true);
            Text info = labels.FirstOrDefault(label => label.name == "InfoText");
            Text loading = labels.FirstOrDefault(label => label.name == "LoadingText");
            if (info != null)
                info.text = "Giữ máy ổn định\nlia chậm qua cửa, tường và biển phòng";
            if (loading != null)
                loading.text = "MULTISET VPS · ĐANG QUÉT KỸ VỊ TRÍ…";
        }

    }
}
