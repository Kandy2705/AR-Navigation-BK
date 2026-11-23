#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    /// Renders the motion design as filmstrips (Logs/UiPreview/motion_*.png): each strip shows one transition frozen
    /// at several moments, using the same pose functions the app runs. Batch:
    /// <c>Unity -batchmode -executeMethod Game.Foundation.UI.EditorTools.UiMotionPreview.RenderAll</c> (needs a GPU, so no -nographics).
    /// </summary>
    public static class UiMotionPreview
    {
        private static readonly float[] Moments = { 0f, 0.1f, 0.2f, 0.35f, 0.55f, 1f };
        private const int Gap = 12;

        [MenuItem("Tools/UI Canvas/Debug/Render Motion Filmstrips")]
        public static void RenderAll()
        {
            Directory.CreateDirectory(UiCanvasPaths.PreviewFolder);
            PageStrip("motion_push", UiCanvasAuthBuilder.LoginName, UiCanvasAuthBuilder.ForgotEmailName, PageTransition.Push);
            PageStrip("motion_pop", UiCanvasAuthBuilder.LoginName, UiCanvasAuthBuilder.ForgotEmailName, PageTransition.Pop);
            PageStrip("motion_fade", UiCanvasHomeBuilder.HistoryName, UiCanvasSettingsBuilder.SettingsName, PageTransition.Fade);

            OverlayStrip("motion_sheet", UiCanvasSettingsBuilder.SettingsName, page =>
            {
                var modal = page.GetComponentInChildren<UiModal>(true);
                modal.Show("Đăng xuất", "Bạn có chắc chắn muốn đăng xuất không?", "Đăng xuất", "Hủy");
                var so = new SerializedObject(modal);
                var backdrop = (Button)so.FindProperty("backdropButton").objectReferenceValue;
                return new UiOverlayMotion(modal, OverlayStyle.SlideUp, (RectTransform)so.FindProperty("panel").objectReferenceValue, backdrop.targetGraphic);
            });
            OverlayStrip("motion_picker", "RegisterUI", page =>
            {
                var sheet = page.GetComponentInChildren<UiPickerSheet>(true);
                var options = new List<string> { "Nam", "Nữ", "Khác" };
                sheet.Show("Chọn giới tính", new[] { new UiPickerSheet.Column { Options = options, Selected = 0 } }, null);
                var so = new SerializedObject(sheet);
                var scrim = (Button)so.FindProperty("scrimButton").objectReferenceValue;
                return new UiOverlayMotion(sheet, OverlayStyle.SlideUp, (RectTransform)so.FindProperty("panel").objectReferenceValue, scrim.targetGraphic);
            });
            OverlayStrip("motion_status", UiCanvasAuthBuilder.LoginName, page =>
            {
                var overlay = page.GetComponentInChildren<UiStatusOverlay>(true);
                overlay.Show("Đăng nhập thành công!", "Chào mừng bạn quay trở lại.");
                var card = (RectTransform)new SerializedObject(overlay).FindProperty("card").objectReferenceValue;
                return new UiOverlayMotion(overlay, OverlayStyle.Pop, card);
            });
        }

        /// <summary>Two pages: <paramref name="under"/> is the one left behind on push (and returned to on pop).</summary>
        private static void PageStrip(string file, string under, string over, PageTransition kind)
        {
            Strip(file, canvas =>
            {
                UiPage bottom = Instantiate(under, canvas);
                UiPage top = Instantiate(over, canvas);
                float width = ((RectTransform)canvas).rect.width;
                // Push and fade: "over" arrives. Pop: "over" leaves and "under" comes back.
                bool overEntering = kind != PageTransition.Pop;
                return t =>
                {
                    bottom.ApplyTransitionPose(UiMotion.Page(kind, !overEntering, t, width));
                    top.ApplyTransitionPose(UiMotion.Page(kind, overEntering, t, width));
                };
            });
        }

        private static void OverlayStrip(string file, string screen, Func<GameObject, UiOverlayMotion> open)
        {
            Strip(file, canvas =>
            {
                UiPage page = Instantiate(screen, canvas);
                UiOverlayMotion motion = open(page.gameObject);
                return t => motion.Pose(t, true);
            });
        }

        private static UiPage Instantiate(string screen, Transform canvas)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(screen));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            go.SetActive(true);
            return go.GetComponent<UiPage>();
        }

        private static void Strip(string file, Func<Transform, Action<float>> build)
        {
            Vector2 frame = new Vector2(UiTheme.ReferenceResolution.x, UiTheme.ReferenceResolution.y);
            int w = (int)frame.x;
            int h = (int)frame.y;
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary(w, h, 24);
            var strip = new Texture2D(Moments.Length * (w + Gap) - Gap, h, TextureFormat.RGBA32, false);
            var shot = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraGo, preview);
                var camera = cameraGo.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = frame.y * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.5f, 0f, 0.5f); // magenta-ish: anything see-through shows up
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

                Action<float> pose = build(canvasGo.transform);
                Color32[] fill = new Color32[strip.width * strip.height];
                for (int i = 0; i < fill.Length; i++)
                {
                    fill[i] = new Color32(255, 255, 255, 255);
                }

                strip.SetPixels32(fill);
                for (int i = 0; i < Moments.Length; i++)
                {
                    pose(Moments[i]);
                    foreach (TMP_Text text in canvasGo.GetComponentsInChildren<TMP_Text>(true))
                    {
                        text.ForceMeshUpdate();
                    }

                    for (int pass = 0; pass < 2; pass++)
                    {
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                    }

                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    shot.Apply();
                    RenderTexture.active = previous;
                    strip.SetPixels(i * (w + Gap), 0, w, h, shot.GetPixels());
                }

                strip.Apply();
                string path = Path.Combine(UiCanvasPaths.PreviewFolder, file + ".png");
                File.WriteAllBytes(path, strip.EncodeToPNG());
                Debug.Log("[UiMotionPreview] Written: " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(strip);
                UnityEngine.Object.DestroyImmediate(shot);
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
#endif
