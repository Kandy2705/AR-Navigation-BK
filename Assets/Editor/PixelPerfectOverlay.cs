// Pixel Perfect Overlay — compare Unity UI against a design image, pixel by pixel.
//
// Panel: Scene view → ⋮ (Overlays menu) → "Pixel Overlay".  Toggle: press 1 (Scene view focused).
//
// - Image loads straight from a file OUTSIDE the project (PNG/JPG) — nothing is
//   imported into Assets, no .meta, nothing touches the repo. Path is kept in
//   EditorPrefs and the texture auto-reloads whenever the file changes on disk.
// - An editor-only RawImage (HideAndDontSave) is attached to the Canvas of the
//   current scene OR prefab stage automatically — native pixel size, anchored
//   top-center (y = 0). It never shows in the Hierarchy, never gets saved into
//   the scene/prefab, never ships in a build. raycastTarget = false.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SunStudio.EditorTools
{
    [InitializeOnLoad]
    internal static class PixelOverlayState
    {
        const string GO_NAME = "__PixelPerfectOverlay__";
        static readonly string KeyPrefix =
            "PixelPerfectOverlay." + Application.dataPath.GetHashCode() + ".";

        public static bool Enabled;
        public static float Alpha = 0.5f;
        public static string ImagePath = "";
        public static Vector2 CanvasOffset; // +x = right, +y = down (design-tool style)
        public static Texture2D Texture { get; private set; }

        static RawImage _canvasImage;
        static DateTime _fileTime;
        static double _nextFileCheck;

        static PixelOverlayState()
        {
            Load();
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredEditMode)
                    Refresh();
            };
            PrefabStage.prefabStageOpened += _ => Refresh();
            PrefabStage.prefabStageClosing += _ => EditorApplication.delayCall += Refresh;
            EditorSceneManager.sceneOpened += (_, _) => Refresh();
            EditorApplication.hierarchyChanged += () =>
            {
                if (Enabled && _canvasImage == null)
                    Refresh(); // canvas xuất hiện muộn
            };
            EditorApplication.update += WatchImageFile;
            EditorApplication.delayCall += () =>
            {
                CleanupOrphans();
                Refresh();
            };
        }

        // ---------- persistence ----------

        static void Load()
        {
            Enabled = EditorPrefs.GetBool(KeyPrefix + "enabled", false);
            Alpha = EditorPrefs.GetFloat(KeyPrefix + "alpha", 0.5f);
            ImagePath = EditorPrefs.GetString(KeyPrefix + "path", "");
            CanvasOffset.x = EditorPrefs.GetFloat(KeyPrefix + "coffx", 0f);
            CanvasOffset.y = EditorPrefs.GetFloat(KeyPrefix + "coffy", 0f);
        }

        public static void Save()
        {
            EditorPrefs.SetBool(KeyPrefix + "enabled", Enabled);
            EditorPrefs.SetFloat(KeyPrefix + "alpha", Alpha);
            EditorPrefs.SetString(KeyPrefix + "path", ImagePath ?? "");
            EditorPrefs.SetFloat(KeyPrefix + "coffx", CanvasOffset.x);
            EditorPrefs.SetFloat(KeyPrefix + "coffy", CanvasOffset.y);
        }

        // ---------- image ----------

        public static bool LoadTexture(string path)
        {
            if (!LoadTextureInternal(path))
                return false;
            Enabled = true; // nạp ảnh xong là bật luôn
            Save();
            Refresh();
            return true;
        }

        static bool LoadTextureInternal(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
            };
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                return false;
            }
            if (Texture != null)
                Object.DestroyImmediate(Texture);
            Texture = tex;
            ImagePath = path;
            _fileTime = File.GetLastWriteTimeUtc(path);
            return true;
        }

        // File ảnh đổi trên đĩa (Figma re-export…) → tự nạp lại, không cần nhấn gì
        static void WatchImageFile()
        {
            if (!Enabled || string.IsNullOrEmpty(ImagePath))
                return;
            if (EditorApplication.timeSinceStartup < _nextFileCheck)
                return;
            _nextFileCheck = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(ImagePath))
                return;
            var t = File.GetLastWriteTimeUtc(ImagePath);
            if (t != _fileTime && Texture != null)
            {
                LoadTextureInternal(ImagePath);
                Refresh();
            }
        }

        // ---------- canvas overlay ----------

        // Tự tìm Canvas: đang mở prefab stage thì lấy canvas trong stage
        // (kể cả "Canvas (Environment)" Unity tự tạo), không thì lấy trong scene.
        static Canvas ResolveCanvas()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                foreach (var root in stage.scene.GetRootGameObjects())
                {
                    var c = root.GetComponentInChildren<Canvas>(true);
                    if (c != null)
                        return c;
                }
                return null;
            }
            return Object.FindAnyObjectByType<Canvas>();
        }

        public static void Refresh()
        {
            // Lazy-load để không bao giờ phải nhấn Reload tay
            if (Enabled && Texture == null)
                LoadTextureInternal(ImagePath);

            if (!Enabled || Texture == null)
            {
                DestroyCanvasImage();
            }
            else
            {
                var canvas = ResolveCanvas();
                if (canvas == null)
                {
                    DestroyCanvasImage();
                }
                else
                {
                    EnsureCanvasImage(canvas);
                    ForceRepaint();
                }
            }
            SceneView.RepaintAll();
        }

        static void EnsureCanvasImage(Canvas canvas)
        {
            if (_canvasImage == null || _canvasImage.transform.parent != canvas.transform)
            {
                DestroyCanvasImage();
                // CreateGameObjectWithHideFlags: gắn hideFlags ngay từ lúc tạo —
                // không đánh dấu scene dirty như new GameObject()
                var go = EditorUtility.CreateGameObjectWithHideFlags(
                    GO_NAME,
                    HideFlags.HideAndDontSave,
                    typeof(RectTransform),
                    typeof(RawImage)
                );
                go.transform.SetParent(canvas.transform, false);
                _canvasImage = go.GetComponent<RawImage>();
                _canvasImage.raycastTarget = false;
            }
            // Vẽ đè lên mọi UI — chỉ reorder khi cần, tránh chạm hierarchy mỗi Refresh
            var tr = _canvasImage.transform;
            if (tr.GetSiblingIndex() != tr.parent.childCount - 1)
                tr.SetAsLastSibling();
            _canvasImage.texture = Texture;
            _canvasImage.color = new Color(1f, 1f, 1f, Alpha);

            // Native size, offset (0,0) = nằm chính giữa canvas
            var rt = (RectTransform)_canvasImage.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(CanvasOffset.x, -CanvasOffset.y);
            rt.sizeDelta = new Vector2(Texture.width, Texture.height);
        }

        // Edit mode không tự vẽ lại Game/Scene view khi đổi thuộc tính bằng code —
        // ép repaint mọi view. TUYỆT ĐỐI không SetDirty ở đây: SetDirty lên object
        // trong scene sẽ đánh dấu scene "changed" mỗi lần mở, kéo người dùng phải
        // Save Project → flush luôn cả các asset dirty sẵn (AddressableAssetSettings…)
        static void ForceRepaint()
        {
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }

        static void DestroyCanvasImage()
        {
            if (_canvasImage != null)
            {
                Object.DestroyImmediate(_canvasImage.gameObject);
                ForceRepaint();
            }
            _canvasImage = null;
        }

        static void CleanupOrphans()
        {
            // Dọn RawImage sót lại sau domain reload (mất tham chiếu static)
            foreach (
                var canvas in Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            )
            {
                var t = canvas.transform.Find(GO_NAME);
                if (t != null && t.GetComponent<RawImage>() != _canvasImage)
                    Object.DestroyImmediate(t.gameObject);
            }
        }

        // ---------- shortcut ----------

        // Rebind: Edit → Shortcuts… → search "Pixel Overlay"
        [Shortcut("Tools/Pixel Overlay/Toggle Enabled", KeyCode.Alpha1)]
        public static void ToggleEnabled()
        {
            Enabled = !Enabled;
            Save();
            Refresh();
        }
    }

    [Overlay(typeof(SceneView), "Pixel Overlay")]
    internal class PixelOverlayPanel : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            return new IMGUIContainer(DrawGUI) { style = { minWidth = 220 } };
        }

        static void DrawGUI()
        {
            EditorGUIUtility.labelWidth = 46f;   // panel hẹp → label gọn
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                PixelOverlayState.Enabled = EditorGUILayout.ToggleLeft(
                    "On (1)",
                    PixelOverlayState.Enabled,
                    GUILayout.Width(60)
                );
                DrawDropZone();
            }

            string info =
                PixelOverlayState.Texture == null
                    ? "(no image)"
                    : $"{Path.GetFileName(PixelOverlayState.ImagePath)} "
                        + $"({PixelOverlayState.Texture.width}×{PixelOverlayState.Texture.height}px)";
            EditorGUILayout.LabelField(info, EditorStyles.miniLabel);

            PixelOverlayState.Alpha = EditorGUILayout.Slider(
                "Alpha",
                PixelOverlayState.Alpha,
                0f,
                1f
            );

            using (new EditorGUILayout.HorizontalScope())
            {
                PixelOverlayState.CanvasOffset = EditorGUILayout.Vector2Field(
                    "Offset",
                    PixelOverlayState.CanvasOffset
                );
                if (GUILayout.Button("0", GUILayout.Width(24)))
                    PixelOverlayState.CanvasOffset = Vector2.zero;
            }

            if (EditorGUI.EndChangeCheck())
            {
                PixelOverlayState.Save();
                PixelOverlayState.Refresh();
            }
        }

        static void DrawDropZone()
        {
            var rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "⬇ Drop PNG/JPG", EditorStyles.helpBox);

            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;
            if (!rect.Contains(e.mousePosition))
                return;

            string path = null;
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                path = DragAndDrop.paths[0];
            if (
                string.IsNullOrEmpty(path)
                && DragAndDrop.objectReferences.Length > 0
                && DragAndDrop.objectReferences[0] is Texture2D t
            )
                path = AssetDatabase.GetAssetPath(t);
            if (string.IsNullOrEmpty(path))
                return;

            string lower = path.ToLowerInvariant();
            if (!lower.EndsWith(".png") && !lower.EndsWith(".jpg") && !lower.EndsWith(".jpeg"))
                return;

            // Project-relative asset path ("Assets/...") → absolute
            if (path.StartsWith("Assets"))
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                PixelOverlayState.LoadTexture(path);
            }
            e.Use();
        }
    }
}
