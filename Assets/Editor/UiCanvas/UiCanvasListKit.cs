#if UNITY_EDITOR
using System.IO;
using Game.Features.Chat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Game.Foundation.UI.EditorTools.UiCanvasBuilderKit;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Building blocks for the home, history, chat and settings screens: title bar, chip rows, scroll lists,
    /// history rows, chat bubbles, the bottom tab bar, and the shared confirm sheet and busy overlay prefabs.
    /// Positions are Figma 375 x 812 coordinates, measured from the thesis mock-ups (Hình 6.7 - 6.10).
    /// </summary>
    public static class UiCanvasListKit
    {
        public const string BottomNavPrefab = UiCanvasPaths.CommonPrefabs + "/BottomNav.prefab";
        public const string ConfirmSheetPrefab = UiCanvasPaths.CommonPrefabs + "/ConfirmSheet.prefab";
        public const string BusyOverlayPrefab = UiCanvasPaths.CommonPrefabs + "/BusyOverlay.prefab";

        public const float NavHeight = 84f;
        public const float RowHeight = 94f;
        public const float SideMargin = 16f;
        public const float ContentWidth = 343f;
        public const float TitleCenterY = 80f;

        public struct ChipStyle
        {
            public float Height;
            public float Padding;
            public float Spacing;
            public float FontSize;
            public string Weight;
            public Color NormalFill;
            public Color SelectedFill;
            public Color NormalText;
            public Color SelectedText;
            public bool Outline;
            public bool AllowDeselect;
            public float LeftInset;
        }

        public static readonly ChipStyle HistoryChips = new ChipStyle
        {
            Height = 32f, Padding = 14f, Spacing = 9f, FontSize = 12f, Weight = "Medium",
            NormalFill = Color.clear, SelectedFill = UiTheme.Primary,
            NormalText = UiTheme.ChipText, SelectedText = Color.white,
            Outline = true, AllowDeselect = true, LeftInset = 8f,
        };

        public static readonly ChipStyle SupportChips = new ChipStyle
        {
            Height = 46f, Padding = 17f, Spacing = 9f, FontSize = 16f, Weight = "Regular",
            NormalFill = UiTheme.Surface, SelectedFill = UiTheme.Primary,
            NormalText = Color.white, SelectedText = Color.white,
            Outline = false, AllowDeselect = false, LeftInset = 16f,
        };

        public static Sprite Icon(string fileName)
        {
            return LoadSprite(UiCanvasPaths.CommonArt(fileName));
        }

        public static Sprite Rounded16()
        {
            return Icon(UiCanvasPaths.RoundedR16File);
        }

        // ---------------------------------------------------------------- headers and buttons

        /// <summary>Back arrow with the screen title beside it, both centered on y=80 (history, profile, support).</summary>
        public static Button AddTitleBar(Transform parent, string title)
        {
            Button back = AddIconButton(parent, "BackButton", Icon(UiCanvasPaths.BackIconFile),
                TopLeft, new Vector2(9.5f, -(TitleCenterY - 20f)), new Vector2(40f, 40f), new Vector2(24f, 24f), Color.white);
            AddText(parent, "Title", title, "SemiBold", 20f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(60f, -(TitleCenterY - 20f)), new Vector2(240f, 40f));
            return back;
        }

        /// <summary>Round-ended button smaller than the 48 px pill (the AR page's top buttons).</summary>
        public static Button AddSmallPill(Transform parent, string name, string label, Color fill, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            Button button = AddPillButton(parent, name, label, fill, anchor, position, size.x);
            ((RectTransform)button.transform).sizeDelta = size;
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = fontSize;
            return button;
        }

        public static Button AddCornerIconButton(Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 position)
        {
            return AddIconButton(parent, name, icon, anchor, position, new Vector2(40f, 40f), new Vector2(22f, 22f), Color.white);
        }

        // ---------------------------------------------------------------- lists

        /// <summary>Vertical list filling the page between two insets. Returns the content that rows are added to.</summary>
        public static RectTransform AddListArea(Transform parent, string name, float topInset, float bottomInset, float spacing, float sidePadding, float topPadding = 0f, float bottomPadding = 16f)
        {
            RectTransform area = NewBand(parent, name, new Vector2(0f, bottomInset), new Vector2(0f, -topInset));
            var scroll = area.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = NewStretch(area, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, Color.clear).raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero; // a new RectTransform is 100 x 100; with stretched anchors that would widen the list

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)sidePadding, (int)sidePadding, (int)topPadding, (int)bottomPadding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        public static ScrollRect ScrollOf(RectTransform listContent)
        {
            return listContent.GetComponentInParent<ScrollRect>();
        }

        /// <summary>Single-line chips that scroll sideways, pinned under the title bar.</summary>
        public static UiChipRow AddChipRow(Transform parent, string name, float top, ChipStyle style)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -top);
            root.sizeDelta = new Vector2(0f, style.Height);

            var row = root.gameObject.AddComponent<UiChipRow>();
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = NewStretch(root, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, Color.clear).raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)style.LeftInset, 16, 0, 0);
            layout.spacing = style.Spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            // Hidden template cloned per label.
            var chip = new GameObject("ChipTemplate", typeof(RectTransform)).GetComponent<RectTransform>();
            chip.SetParent(root, false);
            Image fill = AddImage(chip, Rounded16(), style.NormalFill, Image.Type.Sliced);
            fill.raycastTarget = true;
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            var chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset((int)style.Padding, (int)style.Padding, 0, 0);
            chipLayout.childAlignment = TextAnchor.MiddleCenter;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;
            chipLayout.childForceExpandHeight = false;
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = style.Height;

            if (style.Outline)
            {
                RectTransform outline = NewStretch(chip, "Outline");
                outline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                AddImage(outline, Icon(UiCanvasPaths.OutlineR16File), UiTheme.ChipBorder, Image.Type.Sliced);
            }

            RectTransform label = NewRect(chip, "Label", UiCanvasBuilderKit.Middle, Vector2.zero, new Vector2(60f, style.Height));
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LoadFont(style.Weight);
            text.fontSize = style.FontSize;
            text.color = style.NormalText;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            chip.gameObject.SetActive(false);

            SetReference(row, "content", content);
            SetReference(row, "chipTemplate", button);
            var serialized = new SerializedObject(row);
            serialized.FindProperty("allowDeselect").boolValue = style.AllowDeselect;
            serialized.FindProperty("normalFill").colorValue = style.NormalFill;
            serialized.FindProperty("selectedFill").colorValue = style.SelectedFill;
            serialized.FindProperty("normalText").colorValue = style.NormalText;
            serialized.FindProperty("selectedText").colorValue = style.SelectedText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        /// <summary>The hidden row the history and search lists clone: title, date, chevron, and a delete button behind it.</summary>
        public static UiHistoryRow AddHistoryRowTemplate(RectTransform listContent)
        {
            var root = new GameObject("RowTemplate", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(listContent, false);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var row = root.gameObject.AddComponent<UiHistoryRow>();

            RectTransform deleteRect = NewStretch(root, "DeleteButton");
            Image deleteImage = AddImage(deleteRect, Rounded16(), UiTheme.SwipeDelete, Image.Type.Sliced);
            deleteImage.raycastTarget = true;
            var deleteButton = deleteRect.gameObject.AddComponent<Button>();
            deleteButton.targetGraphic = deleteImage;
            deleteButton.transition = Selectable.Transition.None;
            RectTransform bin = NewRect(deleteRect, "Icon", MiddleRight, new Vector2(-21f, 0f), new Vector2(22f, 22f));
            AddImage(bin, Icon(UiCanvasIconGenerator.Trash), Color.white);
            deleteRect.gameObject.SetActive(false); // shown by UiSwipeRow once the row slides

            RectTransform content = NewStretch(root, "Content");
            Image surface = AddImage(content, Rounded16(), UiTheme.Surface, Image.Type.Sliced);
            surface.raycastTarget = true;
            var open = content.gameObject.AddComponent<Button>();
            open.targetGraphic = surface;
            open.transition = Selectable.Transition.None;
            var swipe = content.gameObject.AddComponent<UiSwipeRow>();

            RectTransform block = NewStretch(content, "Text");
            block.offsetMin = new Vector2(16f, 0f);
            block.offsetMax = new Vector2(-52f, 0f);
            var layout = block.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI title = AddText(block, "Title", "Tòa A4", "SemiBold", 14f, UiTheme.TextTitle,
                TextAlignmentOptions.TopLeft, TopLeft, Vector2.zero, new Vector2(200f, 22f));
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.maxVisibleLines = 2;
            TextMeshProUGUI time = AddText(block, "Time", "08-03-2024 | 03:23 AM", "Regular", 12f, UiTheme.TextSubtitle,
                TextAlignmentOptions.TopLeft, TopLeft, Vector2.zero, new Vector2(200f, 18f));

            RectTransform chevron = NewRect(content, "Chevron", MiddleRight, new Vector2(-14f, 0f), new Vector2(20f, 20f));
            AddImage(chevron, Icon(UiCanvasIconGenerator.ChevronRight), new Color(0.74f, 0.74f, 0.74f, 1f));

            SetReference(swipe, "content", content);
            SetReference(swipe, "deleteButton", deleteButton);
            SetReference(row, "openButton", open);
            SetReference(row, "titleLabel", title);
            SetReference(row, "timeLabel", time);
            SetReference(row, "swipe", swipe);
            root.gameObject.SetActive(false);
            return row;
        }

        /// <summary>A hidden chat row (bubble aligned to one side) the chat page clones for every message.</summary>
        public static UiChatBubble AddBubbleTemplate(RectTransform content, string name, bool fromUser)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(content, false);
            var bubble = root.gameObject.AddComponent<UiChatBubble>();

            var rowLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 0, 0);
            rowLayout.childAlignment = fromUser ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var body = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(root, false);
            AddImage(body, Rounded16(), fromUser ? UiTheme.BubbleUser : UiTheme.BubbleBot, Image.Type.Sliced);
            var bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(14, 14, 10, 10);
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = false;

            TextMeshProUGUI label = AddText(body, "Label", "Tin nhắn", "Regular", 14f, Color.white,
                TextAlignmentOptions.TopLeft, TopLeft, Vector2.zero, new Vector2(200f, 22f));
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();

            SetReference(bubble, "label", label);
            SetReference(bubble, "labelLayout", labelLayout);
            var serialized = new SerializedObject(bubble);
            serialized.FindProperty("maxTextWidth").floatValue = 265f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            return bubble;
        }

        // ---------------------------------------------------------------- shared prefabs

        public static UiBottomNav AddBottomNav(Transform page)
        {
            EnsureBottomNav();
            return Instantiate<UiBottomNav>(BottomNavPrefab, page);
        }

        public static UiModal AddConfirmSheet(Transform page)
        {
            EnsureConfirmSheet();
            return Instantiate<UiModal>(ConfirmSheetPrefab, page);
        }

        public static UiBusyOverlay AddBusyOverlay(Transform page)
        {
            EnsureBusyOverlay();
            return Instantiate<UiBusyOverlay>(BusyOverlayPrefab, page);
        }

        private static T Instantiate<T>(string prefabPath, Transform parent) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetAsLastSibling();
            return instance.GetComponent<T>();
        }

        public static void EnsureBottomNav()
        {
            var root = new GameObject("BottomNav", typeof(RectTransform)).GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = BottomCenter;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0f, NavHeight);
            var nav = root.gameObject.AddComponent<UiBottomNav>();

            string[] labels = { "AR chỉ đường", "Lịch sử", "Cài đặt" };
            string[] icons = { UiCanvasIconGenerator.Phone, UiCanvasIconGenerator.Clock, UiCanvasIconGenerator.Gear };
            var buttons = new Button[3];
            var images = new Image[3];
            var texts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var tab = new GameObject("Tab" + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
                tab.SetParent(root, false);
                tab.anchorMin = new Vector2(i / 3f, 0f);
                tab.anchorMax = new Vector2((i + 1) / 3f, 1f);
                tab.offsetMin = Vector2.zero;
                tab.offsetMax = Vector2.zero;
                Image hit = AddImage(tab, null, Color.clear);
                hit.raycastTarget = true;
                buttons[i] = tab.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = hit;
                buttons[i].transition = Selectable.Transition.None;

                RectTransform icon = NewRect(tab, "Icon", TopCenter, new Vector2(0f, -7f), new Vector2(24f, 24f));
                images[i] = AddImage(icon, Icon(icons[i]), Color.white);
                texts[i] = AddText(tab, "Label", labels[i], "Regular", 12f, Color.white, TextAlignmentOptions.Center,
                    TopCenter, new Vector2(0f, -33f), new Vector2(120f, 16f));
            }

            AssignArray(nav, "tabs", buttons);
            AssignArray(nav, "icons", images);
            AssignArray(nav, "labels", texts);
            SavePrefab(root.gameObject, BottomNavPrefab, false);
        }

        public static void EnsureConfirmSheet()
        {
            var root = new GameObject("ConfirmSheet", typeof(RectTransform)).GetComponent<RectTransform>();
            Stretch(root);
            var modal = root.gameObject.AddComponent<UiModal>();

            RectTransform scrim = NewStretch(root, "Scrim");
            Image scrimImage = AddImage(scrim, null, new Color(0.04f, 0.04f, 0.045f, 0.78f));
            scrimImage.raycastTarget = true;
            var backdrop = scrim.gameObject.AddComponent<Button>();
            backdrop.targetGraphic = scrimImage;
            backdrop.transition = Selectable.Transition.None;

            // The panel reaches 32 below the screen so its bottom corners never show.
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root, false);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = BottomCenter;
            panel.anchoredPosition = new Vector2(0f, -32f);
            panel.sizeDelta = new Vector2(0f, 267f);
            AddImage(panel, Icon(UiCanvasPaths.RoundedR32File), UiTheme.BackgroundDeep, Image.Type.Sliced).raycastTarget = true;

            TextMeshProUGUI title = AddText(panel, "Title", "Tiêu đề", "SemiBold", 20f, UiTheme.TextTitle, TextAlignmentOptions.Center,
                TopCenter, new Vector2(0f, -20f), new Vector2(ContentWidth, 32f));
            RectTransform divider = NewRect(panel, "Divider", TopCenter, new Vector2(0f, -70f), new Vector2(ContentWidth - 16f, 1f));
            AddImage(divider, null, new Color(1f, 1f, 1f, 0.1f));
            TextMeshProUGUI message = AddText(panel, "Message", "Nội dung", "Light", 14f, UiTheme.TextSubtitle, TextAlignmentOptions.Top,
                TopCenter, new Vector2(0f, -86f), new Vector2(ContentWidth - 16f, 52f));

            Button cancel = AddPillButton(panel, "CancelButton", "Hủy", UiTheme.Surface, BottomLeft, new Vector2(SideMargin, 64f), 164f);
            Button confirm = AddPillButton(panel, "ConfirmButton", "Xác nhận", UiTheme.Primary, new Vector2(1f, 0f), new Vector2(-SideMargin, 64f), 163f);

            SetReference(modal, "titleLabel", title);
            SetReference(modal, "messageLabel", message);
            SetReference(modal, "confirmButton", confirm);
            SetReference(modal, "confirmLabel", confirm.GetComponentInChildren<TextMeshProUGUI>());
            SetReference(modal, "cancelButton", cancel);
            SetReference(modal, "cancelLabel", cancel.GetComponentInChildren<TextMeshProUGUI>());
            SetReference(modal, "backdropButton", backdrop);
            SavePrefab(root.gameObject, ConfirmSheetPrefab, true);
        }

        public static void EnsureBusyOverlay()
        {
            var root = new GameObject("BusyOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
            Stretch(root);
            root.gameObject.AddComponent<UiBusyOverlay>();
            AddImage(root, null, new Color(0.07f, 0.07f, 0.07f, 0.8f)).raycastTarget = true;

            RectTransform spinner = NewRect(root, "Spinner", Middle, Vector2.zero, new Vector2(48f, 48f));
            AddImage(spinner, LoadSprite(UiCanvasPaths.CommonArt("spinner_arc.png")), UiTheme.Accent);
            spinner.gameObject.AddComponent<UiSpinner>();
            SavePrefab(root.gameObject, BusyOverlayPrefab, true);
        }

        private static void AssignArray(Object target, string property, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root, string path, bool startInactive)
        {
            root.SetActive(!startInactive);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
