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

        // ---------------------------------------------------------------- auth screens (phase 2)

        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 MiddleRight = new Vector2(1f, 0.5f);
        public static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);

        private const float InputRowHeight = 53f;
        private const float ScreenMargin = 16f;
        private const float ContentWidth = 343f;

        public static void AddBackground(Transform page, Color color)
        {
            RectTransform background = NewStretch(page, "Background");
            AddImage(background, null, color);
        }

        /// <summary>Transparent tap target with a centered icon. The hit area is larger than the icon so it is easy to press.</summary>
        public static Button AddIconButton(Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 position, Vector2 hitSize, Vector2 iconSize, Color iconColor)
        {
            RectTransform rect = NewRect(parent, name, anchor, position, hitSize);
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            RectTransform iconRect = NewRect(rect, "Icon", Middle, Vector2.zero, iconSize);
            AddImage(iconRect, icon, iconColor);
            return button;
        }

        /// <summary>
        /// Back arrow, title and subtitle shared by the auth screens. One-line titles are centered at y=165 with the
        /// subtitle from y=198; two-line titles start higher (y=128) and push the subtitle to y=211 (Figma).
        /// </summary>
        public static Button AddAuthHeader(Transform content, string title, string subtitle, float subtitleHeight = 70f, bool twoLineTitle = false)
        {
            Button back = AddIconButton(content, "BackButton", LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.BackIconFile)),
                TopLeft, new Vector2(9.5f, -63f), new Vector2(40f, 40f), new Vector2(24f, 24f), Color.white);

            if (twoLineTitle)
            {
                // Figma sets the 28px title at 120% line height; Poppins' natural line is 150%.
                AddText(content, "Title", title, "SemiBold", 28f, UiTheme.TextTitle, TextAlignmentOptions.TopLeft,
                    TopLeft, new Vector2(ScreenMargin, -128f), new Vector2(ContentWidth, 72f), lineSpacing: -20f);
            }
            else
            {
                AddText(content, "Title", title, "SemiBold", 28f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                    TopLeft, new Vector2(ScreenMargin, -145f), new Vector2(ContentWidth, 40f));
            }

            float subtitleTop = twoLineTitle ? 211f : 198f;
            // Figma sets 16px body at 140% line height; Poppins' natural line is 150%.
            AddText(content, "Subtitle", subtitle, "Light", 16f, UiTheme.TextSubtitle, TextAlignmentOptions.TopLeft,
                TopLeft, new Vector2(ScreenMargin, -subtitleTop), new Vector2(ContentWidth, subtitleHeight), lineSpacing: -8f);
            return back;
        }

        public static TextMeshProUGUI AddErrorLabel(Transform parent, float top)
        {
            TextMeshProUGUI label = AddText(parent, "ErrorLabel", string.Empty, "Regular", 12f, UiTheme.Danger,
                TextAlignmentOptions.TopLeft, TopLeft, new Vector2(ScreenMargin, -top), new Vector2(ContentWidth, 18f));
            label.gameObject.SetActive(false);
            return label;
        }

        /// <summary>Bordered card holding <paramref name="rows"/> input rows separated by hairlines.</summary>
        public static RectTransform AddInputCard(Transform parent, string name, float top, int rows, float rowHeight = InputRowHeight)
        {
            RectTransform card = NewRect(parent, name, TopLeft, new Vector2(ScreenMargin, -top), new Vector2(ContentWidth, rowHeight * rows));
            AddImage(card, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.OutlineR16File)), UiTheme.Border, Image.Type.Sliced);
            for (int i = 1; i < rows; i++)
            {
                RectTransform line = NewRect(card, "Divider" + i, TopLeft, new Vector2(1f, -rowHeight * i), new Vector2(ContentWidth - 2f, 1f));
                AddImage(line, null, UiTheme.Divider);
            }

            return card;
        }

        /// <summary>One row of an input card: a text input plus either a trailing icon or a show/hide-password toggle.</summary>
        public static UiTextField AddInputRow(RectTransform card, int row, string name, string placeholder, Sprite trailingIcon, bool password,
            TouchScreenKeyboardType keyboard = TouchScreenKeyboardType.Default, float rowHeight = InputRowHeight)
        {
            RectTransform rowRect = NewRect(card, name, TopLeft, new Vector2(0f, -rowHeight * row), new Vector2(ContentWidth, rowHeight));
            var field = rowRect.gameObject.AddComponent<UiTextField>();
            TMP_InputField input = AddTmpInput(rowRect, "Input", placeholder, "Light", 16f, new Vector2(14f, 56f), TextAlignmentOptions.MidlineLeft);

            SetReference(field, "input", input);
            var serialized = new SerializedObject(field);
            serialized.FindProperty("isPasswordField").boolValue = password;
            SetEnum(serialized.FindProperty("keyboardType"), keyboard.ToString());

            if (password)
            {
                Sprite hidden = LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.EyeOffIconFile));
                Sprite shown = LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.EyeIconFile));
                Button toggle = AddIconButton(rowRect, "VisibilityToggle", hidden, MiddleRight, new Vector2(-4f, 0f),
                    new Vector2(44f, 44f), new Vector2(20f, 18f), Color.white);
                serialized.FindProperty("visibilityToggle").objectReferenceValue = toggle;
                serialized.FindProperty("visibilityIcon").objectReferenceValue = toggle.transform.Find("Icon").GetComponent<Image>();
                serialized.FindProperty("iconHidden").objectReferenceValue = hidden;
                serialized.FindProperty("iconShown").objectReferenceValue = shown;
            }
            else if (trailingIcon != null)
            {
                RectTransform icon = NewRect(rowRect, "Icon", MiddleRight, new Vector2(-16f, 0f), new Vector2(20f, 16f));
                AddImage(icon, trailingIcon, Color.white);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return field;
        }

        /// <summary>Single-line TextMeshPro input. <paramref name="insets"/> is (left, right) padding of the text area.</summary>
        public static TMP_InputField AddTmpInput(RectTransform parent, string name, string placeholderText, string weight, float size, Vector2 insets, TextAlignmentOptions alignment)
        {
            RectTransform root = NewStretch(parent, name);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            RectTransform area = NewStretch(root, "Text Area");
            area.offsetMin = new Vector2(insets.x, 0f);
            area.offsetMax = new Vector2(-insets.y, 0f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = AddStretchText(area, "Placeholder", placeholderText, weight, size, UiTheme.Placeholder, alignment);
            TextMeshProUGUI text = AddStretchText(area, "Text", string.Empty, weight, size, UiTheme.TextPrimary, alignment);

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = hit;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = LoadFont(weight);
            input.pointSize = size;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.selectionColor = new Color(UiTheme.Primary.r, UiTheme.Primary.g, UiTheme.Primary.b, 0.6f);
            return input;
        }

        public static TextMeshProUGUI AddStretchText(Transform parent, string name, string content, string weight, float size, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewStretch(parent, name);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            Fill(label, content, weight, size, color, alignment);
            return label;
        }

        /// <summary>Text-only button, e.g. the "Quên mật khẩu ?" link.</summary>
        public static Button AddLinkButton(Transform parent, string name, string label, string weight, float size, Color color, Vector2 anchor, Vector2 position, Vector2 hitSize)
        {
            RectTransform rect = NewRect(parent, name, anchor, position, hitSize);
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            AddStretchText(rect, "Label", label, weight, size, color, TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>Checkbox with a label ("Ghi nhớ tôi"). The whole row is the tap target.</summary>
        public static Toggle AddCheckboxRow(Transform parent, string name, string label, Vector2 position)
        {
            RectTransform row = NewRect(parent, name, TopLeft, position, new Vector2(170f, 40f));
            Image hit = AddImage(row, null, Color.clear);
            hit.raycastTarget = true;

            RectTransform box = NewRect(row, "Box", MiddleLeft, new Vector2(2f, 0f), new Vector2(20f, 20f));
            AddImage(box, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.CheckboxOutlineFile)), UiTheme.Primary, Image.Type.Sliced);
            RectTransform check = NewStretch(box, "Check");
            Image checkImage = AddImage(check, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.CheckboxFillFile)), UiTheme.Primary, Image.Type.Sliced);

            AddText(row, "Label", label, "Regular", 14f, UiTheme.TextBody, TextAlignmentOptions.MidlineLeft,
                MiddleLeft, new Vector2(36f, 0f), new Vector2(130f, 24f));

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = hit;
            toggle.graphic = checkImage;
            toggle.transition = Selectable.Transition.None;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>Four digit boxes (73.75 x 69, 16 apart) for a one-time code.</summary>
        public static UiOtpInput AddOtpRow(Transform parent, float top)
        {
            RectTransform row = NewRect(parent, "OtpInput", TopLeft, new Vector2(ScreenMargin, -top), new Vector2(ContentWidth, 69f));
            var otp = row.gameObject.AddComponent<UiOtpInput>();

            const int count = 4;
            const float boxWidth = 73.75f;
            const float gap = 16f;
            var inputs = new TMP_InputField[count];
            var fills = new Image[count];
            for (int i = 0; i < count; i++)
            {
                RectTransform box = NewRect(row, "Digit" + (i + 1), TopLeft, new Vector2(i * (boxWidth + gap), 0f), new Vector2(boxWidth, 69f));
                fills[i] = AddImage(box, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR16File)), UiTheme.OtpBox, Image.Type.Sliced);
                AddImage(NewStretch(box, "Outline"), LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.OutlineR16File)), UiTheme.Border, Image.Type.Sliced);

                inputs[i] = AddTmpInput(box, "Input", string.Empty, "Bold", 24f, Vector2.zero, TextAlignmentOptions.Center);
                inputs[i].textComponent.color = UiTheme.OtpDigit;
            }

            var serialized = new SerializedObject(otp);
            SerializedProperty digits = serialized.FindProperty("digits");
            SerializedProperty boxes = serialized.FindProperty("boxes");
            digits.arraySize = count;
            boxes.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                digits.GetArrayElementAtIndex(i).objectReferenceValue = inputs[i];
                boxes.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return otp;
        }

        // ---------------------------------------------------------------- register screens (phase 3)

        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        /// <summary>Selects an enum value on a serialized property by its name.</summary>
        public static void SetEnum(SerializedProperty property, string valueName)
        {
            int index = System.Array.IndexOf(property.enumNames, valueName);
            if (index < 0)
            {
                throw new System.InvalidOperationException(property.name + " has no value " + valueName);
            }

            property.enumValueIndex = index;
        }

        /// <summary>Error line pinned just above the bottom button, for screens whose form scrolls.</summary>
        public static TextMeshProUGUI AddBottomErrorLabel(Transform parent)
        {
            TextMeshProUGUI label = AddText(parent, "ErrorLabel", string.Empty, "Regular", 12f, UiTheme.Danger,
                TextAlignmentOptions.BottomLeft, BottomLeft, new Vector2(ScreenMargin, 85f), new Vector2(ContentWidth, 18f));
            label.gameObject.SetActive(false);
            return label;
        }

        /// <summary>A form row that opens a picker: gray hint, white once chosen, chevron on the right.</summary>
        public static UiSelectField AddSelectRow(RectTransform card, int row, string name, string hint, float rowHeight)
        {
            RectTransform rowRect = NewRect(card, name, TopLeft, new Vector2(0f, -rowHeight * row), new Vector2(ContentWidth, rowHeight));
            Image hit = AddImage(rowRect, null, Color.clear);
            hit.raycastTarget = true;
            var button = rowRect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            var field = rowRect.gameObject.AddComponent<UiSelectField>();

            RectTransform labelRect = NewStretch(rowRect, "Label");
            labelRect.offsetMin = new Vector2(14f, 0f);
            labelRect.offsetMax = new Vector2(-56f, 0f);
            TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            Fill(label, hint, "Light", 16f, UiTheme.Placeholder, TextAlignmentOptions.MidlineLeft);

            RectTransform chevron = NewRect(rowRect, "Chevron", MiddleRight, new Vector2(-16f, 0f), new Vector2(24f, 24f));
            AddImage(chevron, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.ChevronDownFile)), Color.white);

            SetReference(field, "button", button);
            SetReference(field, "label", label);
            var serialized = new SerializedObject(field);
            serialized.FindProperty("hint").stringValue = hint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return field;
        }

        /// <summary>Profile picture tile (104 x 104) with the edit badge on its bottom-right corner.</summary>
        public static RectTransform AddAvatar(Transform parent, float top)
        {
            RectTransform tile = NewRect(parent, "Avatar", TopCenter, new Vector2(0f, -top), new Vector2(104f, 104f));
            AddImage(tile, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR32File)), UiTheme.Surface, Image.Type.Sliced);

            RectTransform icon = NewRect(tile, "Icon", Middle, Vector2.zero, new Vector2(44f, 44f));
            AddImage(icon, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.UserIconFile)), Color.white);

            RectTransform badge = NewRect(tile, "EditBadge", new Vector2(1f, 0f), Vector2.zero, new Vector2(40f, 40f));
            badge.pivot = Middle;
            badge.anchoredPosition = new Vector2(-13f, 12f);
            AddImage(badge, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.PenBadgeFile)), Color.white);
            return tile;
        }

        /// <summary>
        /// Vertical scroll area filling the page above <paramref name="bottomInset"/>. Children of the returned content
        /// are positioned from its top edge, which is the top of the screen, so Figma y values apply unchanged.
        /// </summary>
        public static RectTransform AddScrollArea(Transform parent, string name, float bottomInset, float contentHeight)
        {
            RectTransform area = NewBand(parent, name, new Vector2(0f, bottomInset), Vector2.zero);
            var scroll = area.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = NewStretch(area, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = AddImage(viewport, null, Color.clear);
            catcher.raycastTarget = true; // lets a drag that starts on empty space scroll the form

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, contentHeight);

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;
            return content;
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
