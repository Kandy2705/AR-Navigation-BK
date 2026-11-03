#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Builds the AppCanvas prefab that hosts every migrated screen:
    /// AppCanvas (Canvas + CanvasScaler 375x812 + raycaster) / Pages / Modals.
    /// Pages are full-screen; each page puts <see cref="UiSafeArea"/> on its own content child so
    /// backgrounds can still bleed behind the notch.
    /// </summary>
    public static class UiCanvasPrefabBuilder
    {
        public const string PagesName = "Pages";
        public const string ModalsName = "Modals";

        // Above the legacy UI Toolkit panels' default sort; revisit when the AR HUD canvases migrate (phase 4).
        private const int SortingOrder = 100;

        [MenuItem("Tools/UI Canvas/Build AppCanvas Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(UiCanvasPaths.CommonPrefabs);

            var root = new GameObject(
                "AppCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UiEventSystemEnsurer));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Figma frames are 375 wide: match width so horizontal layout is identical on every phone,
            // taller screens simply get more vertical room.
            scaler.matchWidthOrHeight = 0f;

            CreateStretchChild(root.transform, PagesName);
            CreateStretchChild(root.transform, ModalsName);

            PrefabUtility.SaveAsPrefabAsset(root, UiCanvasPaths.AppCanvasPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiCanvas] AppCanvas prefab saved to " + UiCanvasPaths.AppCanvasPrefab);
        }

        private static RectTransform CreateStretchChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
#endif
