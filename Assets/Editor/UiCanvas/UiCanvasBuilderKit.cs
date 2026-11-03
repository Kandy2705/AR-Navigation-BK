#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Small helpers for building screen prefabs in code. Positions are in Figma frame units (375x812),
    /// measured from the top-left corner of the frame.
    /// </summary>
    public static class UiCanvasBuilderKit
    {
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        public static T NewPage<T>(string name) where T : UiPage
        {
            var go = new GameObject(name, typeof(RectTransform));
            Stretch((RectTransform)go.transform);
            return go.AddComponent<T>();
        }

        public static RectTransform NewStretch(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        /// <summary>Stretches over the parent with fixed insets (left/bottom in <paramref name="offsetMin"/>, right/top in <paramref name="offsetMax"/>).</summary>
        public static RectTransform NewBand(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = NewStretch(parent, name);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>Full-width block pinned to the bottom edge. Children anchor to it, so it keeps its height on any screen.</summary>
        public static RectTransform NewBottomGroup(Transform parent, string name, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = BottomCenter;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

        /// <summary>Image scaled to fit inside <paramref name="area"/> keeping <paramref name="aspect"/> (width / height), centered.</summary>
        public static Image AddFittedImage(RectTransform area, string name, Sprite sprite, float aspect)
        {
            RectTransform rect = NewStretch(area, name);
            Image image = AddImage(rect, sprite, Color.white);
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = aspect;
            return image;
        }

        public static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image AddImage(RectTransform rect, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI AddText(
            Transform parent,
            string name,
            string text,
            string fontWeight,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            float lineSpacing = 0f)
        {
            RectTransform rect = NewRect(parent, name, anchor, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            Fill(label, text, fontWeight, fontSize, color, alignment);
            label.lineSpacing = lineSpacing;
            return label;
        }

        /// <summary>Pill button: tinted rounded 9-slice with a centered label.</summary>
        public static Button AddPillButton(
            Transform parent,
            string name,
            string label,
            Color fill,
            Vector2 anchor,
            Vector2 position,
            float width)
        {
            RectTransform rect = NewRect(parent, name, anchor, position, new Vector2(width, UiTheme.PillHeight));
            Image image = AddImage(rect, LoadSprite(UiCanvasPaths.RoundedSprite), fill, Image.Type.Sliced);
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f),
                pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(1f, 1f, 1f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };

            RectTransform labelRect = NewStretch(rect, "Label");
            var text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            Fill(text, label, "Medium", 16f, UiTheme.TextPrimary, TextAlignmentOptions.Center);
            return button;
        }

        public static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Missing sprite " + path + " — run Tools/UI Canvas/Run Phase 0 Setup");
            }

            return sprite;
        }

        public static TMP_FontAsset LoadFont(string weight)
        {
            string path = UiCanvasPaths.PoppinsFontAsset(weight);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                throw new System.InvalidOperationException("Missing font " + path + " — run Tools/UI Canvas/Run Phase 0 Setup");
            }

            return font;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Sets a private [SerializeField] by property name.</summary>
        public static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException(target.GetType().Name + " has no serialized field " + propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetPageId(UiPage page, PageID id)
        {
            var serialized = new SerializedObject(page);
            serialized.FindProperty("pageId").enumValueIndex = (int)id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Fill(
            TextMeshProUGUI label,
            string text,
            string fontWeight,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            label.font = LoadFont(fontWeight);
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }
    }
}
#endif
