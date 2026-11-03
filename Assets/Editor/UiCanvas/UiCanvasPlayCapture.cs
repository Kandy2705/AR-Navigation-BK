#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Runs the AppShell scene in Play mode and saves what the Canvas renders (Splash → Intro → Welcome)
    /// to Logs/UiPreview/play_*.png. The Canvas is redirected to a camera + RenderTexture so no Game view
    /// has to be open. Survives the domain reload that entering Play mode triggers.
    /// </summary>
    [InitializeOnLoad]
    public static class UiCanvasPlayCapture
    {
        private const string ActiveKey = "UiCanvasPlayCapture.active";
        private const int Width = 750;
        private const int Height = 1624;

        private static double startTime;
        private static int step;
        private static Camera camera;
        private static RenderTexture target;

        static UiCanvasPlayCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying)
            {
                Begin();
            }
        }

        [MenuItem("Tools/UI Canvas/Debug/Capture Play Screenshots")]
        public static void Start()
        {
            Directory.CreateDirectory(UiCanvasPaths.PreviewFolder);
            EditorSceneManager.OpenScene(UiCanvasPaths.ShellScene, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                Begin();
            }
        }

        private static void Begin()
        {
            startTime = EditorApplication.timeSinceStartup;
            step = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double t = EditorApplication.timeSinceStartup - startTime;
            if (step == 0 && t > 0.8)
            {
                Redirect();
                Save("splash");
                step = 1;
            }
            else if (step == 1 && t > 6.0)
            {
                Save("intro");
                Object.FindFirstObjectByType<NavigationManager>().Navigate(PageID.WelcomePage);
                step = 2;
            }
            else if (step == 2 && t > 7.0)
            {
                Save("welcome");
                step = 3;
            }
            else if (step == 3)
            {
                Finish();
            }
        }

        private static void Redirect()
        {
            Canvas canvas = null;
            foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (candidate.isRootCanvas && candidate.name.StartsWith("AppCanvas"))
                {
                    canvas = candidate;
                }
            }

            target = new RenderTexture(Width, Height, 24);
            var cameraGo = new GameObject("CaptureCamera", typeof(Camera));
            camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.Background;
            camera.targetTexture = target;
            camera.cullingMask = ~0;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
        }

        private static void Save(string name)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(UiCanvasPaths.PreviewFolder, "play_" + name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            Debug.Log($"[UiCanvas] Play capture: {name} (frame {Time.frameCount}, realtime {Time.realtimeSinceStartup:F2}s, " +
                      $"editor {EditorApplication.timeSinceStartup - startTime:F2}s, focused={UnityEditorInternal.InternalEditorUtility.isApplicationActive})");
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
