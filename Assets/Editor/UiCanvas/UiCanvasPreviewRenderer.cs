#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Renders a screen prefab to a PNG at 2x of the 375x812 Figma frame, so a layout can be compared with
    /// the design without entering Play mode, at several screen heights to check the layout stretches.
    /// Output: Logs/UiPreview/&lt;Name&gt;_&lt;height&gt;.png. Edit mode only: building a preview scene is not allowed in Play mode.
    /// </summary>
    public static class UiCanvasPreviewRenderer
    {
        private const int Scale = 2;

        [MenuItem("Tools/UI Canvas/Render Page Previews")]
        public static void RenderAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before rendering previews.");
                return;
            }

            Directory.CreateDirectory(UiCanvasPaths.PreviewFolder);
            foreach (string name in new[]
            {
                UiCanvasOnboardingBuilder.SplashName,
                UiCanvasOnboardingBuilder.IntroName,
                UiCanvasOnboardingBuilder.WelcomeName,
            })
            {
                // 812 is the Figma frame; 667 is an iPhone SE / 16:9 screen (width stays 375); 900 is a tall phone.
                foreach (float height in new[] { 667f, 812f, 900f })
                {
                    Render(name, height);
                }
            }
        }

        public static void Render(string screenName, float frameHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(screenName));
            if (prefab == null)
            {
                Debug.LogError("[UiCanvas] Missing prefab for " + screenName);
                return;
            }

            Vector2 frame = new Vector2(UiTheme.ReferenceResolution.x, frameHeight);
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary((int)frame.x * Scale, (int)frame.y * Scale, 24);
            try
            {
                // World-space canvas sized exactly like the Figma frame; an orthographic camera frames it.
                var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraGo, preview);
                var camera = cameraGo.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = frame.y * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.scene = preview;

                var canvasGo = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasGo, preview);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = frame;
                canvasRect.position = Vector3.zero;

                var page = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
                foreach (TMP_Text text in page.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                }

                // Dynamic font atlases fill on first use, so render twice.
                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                }

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string file = Path.Combine(UiCanvasPaths.PreviewFolder, screenName + "_" + (int)frameHeight + ".png");
                File.WriteAllBytes(file, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log("[UiCanvas] Preview written: " + file);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
#endif
