#if UNITY_EDITOR
using System.IO;
using Game.Features.Home;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Game.Foundation.UI.EditorTools.UiCanvasBuilderKit;
using static Game.Foundation.UI.EditorTools.UiCanvasListKit;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Builds NavigationHudUI, the Canvas version of the B9 guidance overlay that used to be drawn with UI Toolkit:
    /// status card, round minimap, destination panel and the research panel. The prefab carries its own Canvas
    /// (sorted under AppCanvas) because it lives in the navigation scene, not in the app shell.
    /// </summary>
    public static class UiCanvasNavigationHudBuilder
    {
        public const string HudName = "NavigationHudUI";
        public const int SortingOrder = 50;

        /// <summary>Large circle for the minimap mask: the stencil cuts hard edges, so a small sprite stair-steps at 300 units.</summary>
        public const string MinimapMaskFile = "ui_minimap_mask.png";
        private const int MinimapMaskPixels = 512;

        public const float CardHeight = 92f;
        private const float ShellTop = 104f;
        private const float ShellBottom = 124f;
        private const float MinimapSize = 96f;
        private const float ButtonHeight = 44f;

        public static readonly Color Glass = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color Warning = UiTheme.Hex("#F27A14");
        public static readonly Color Success = UiTheme.Hex("#1F8554");
        public static readonly Color Muted = UiTheme.Hex("#21405A");
        public static readonly Color LogText = UiTheme.Hex("#8CEBB8");
        public static readonly Color North = UiTheme.Hex("#FFD133");

        [MenuItem("Tools/UI Canvas/Build Navigation HUD")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the navigation HUD.");
                return;
            }

            WriteMinimapMask();

            var root = new GameObject(HudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiEventSystemEnsurer));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiTheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            Stretch((RectTransform)root.transform);
            var hud = root.AddComponent<NavigationHudUI>();

            RectTransform content = NewStretch(root.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            BuildStatusCard(content, hud);
            BuildMinimap(content, hud);
            BuildDestinationPanel(content, hud);
            BuildResearchPanel(content, hud);

            var pickerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasRegisterBuilder.PickerSheetPrefab);
            var picker = (GameObject)PrefabUtility.InstantiatePrefab(pickerPrefab, content);
            picker.transform.SetAsLastSibling();
            picker.SetActive(false);

            SetReference(hud, "canvas", canvas);
            SetReference(hud, "picker", picker.GetComponent<UiPickerSheet>());

            string path = UiCanvasPaths.ScreenPrefab(HudName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiCanvas] " + path + " built");
        }

        // ---------------------------------------------------------------- status

        private static void BuildStatusCard(RectTransform content, NavigationHudUI hud)
        {
            RectTransform card = NewTopBand(content, "StatusCard", ShellTop, CardHeight);
            AddImage(card, Rounded16(), Glass, Image.Type.Sliced);

            TextMeshProUGUI status = AddLine(card, "Status", "Đang chuẩn bị GPS…", "SemiBold", 15f, 11f, Color.white, 12f, 22f, 48f);
            TextMeshProUGUI detail = AddLine(card, "Detail", "SchoolGround · GPS", "Regular", 12f, 9f, UiTheme.TextBody, 38f, 18f, 16f);
            TextMeshProUGUI summary = AddLine(card, "Summary", "Ngoài trời → cửa B9 → phòng đã chọn", "Light", 12f, 9f, UiTheme.TextSecondary, 60f, 18f, 16f);

            Button research = AddIconButton(card, "ResearchButton", Icon(UiCanvasIconGenerator.Flask),
                new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(40f, 40f), new Vector2(18f, 18f), UiTheme.TextSecondary);

            Button retry = AddIconPill(content, "RetryButton", Icon(UiCanvasIconGenerator.Refresh), "Quét lại VPS", UiTheme.Accent,
                TopLeft, new Vector2(SideMargin, -(ShellTop + CardHeight + 12f)), new Vector2(170f, 36f), 13f);
            retry.gameObject.SetActive(false);

            SetReference(hud, "statusCard", card);
            SetReference(hud, "statusLabel", status);
            SetReference(hud, "detailLabel", detail);
            SetReference(hud, "summaryLabel", summary);
            SetReference(hud, "researchButton", research);
            SetReference(hud, "retryButton", retry);
        }

        // ---------------------------------------------------------------- minimap

        private static void BuildMinimap(RectTransform content, NavigationHudUI hud)
        {
            RectTransform frame = NewRect(content, "Minimap", new Vector2(1f, 1f),
                new Vector2(-SideMargin, -(ShellTop + CardHeight + 12f)), new Vector2(MinimapSize, MinimapSize));
            Image ring = AddImage(frame, LoadSprite(UiCanvasPaths.CircleSprite), new Color(0.04f, 0.09f, 0.14f, 0.94f));
            ring.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = ring;
            button.transition = Selectable.Transition.None;

            RectTransform mask = NewBand(frame, "Mask", new Vector2(4f, 4f), new Vector2(-4f, -4f));
            AddImage(mask, LoadSprite(UiCanvasPaths.ScreenArt(HudName, MinimapMaskFile)), Color.white);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform map = NewStretch(mask, "Map");
            var raw = map.gameObject.AddComponent<RawImage>();
            raw.color = UiTheme.Surface;
            raw.raycastTarget = false;

            TextMeshProUGUI north = AddText(frame, "North", "N", "Bold", 13f, North, TextAlignmentOptions.Center,
                TopCenter, new Vector2(0f, -5f), new Vector2(20f, 16f));
            north.raycastTarget = false;

            TextMeshProUGUI hint = AddText(frame, "Hint", "CHẠM XEM TỔNG QUAN", "Medium", 8f, new Color(0.85f, 0.94f, 1f, 0.9f),
                TextAlignmentOptions.Center, BottomCenter, new Vector2(0f, -4f), new Vector2(120f, 12f));
            ((RectTransform)hint.transform).pivot = TopCenter;
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            SetReference(hud, "minimapFrame", frame);
            SetReference(hud, "minimapButton", button);
            SetReference(hud, "minimapImage", raw);
            SetReference(hud, "minimapHint", hint);
        }

        // ---------------------------------------------------------------- destination

        private static void BuildDestinationPanel(RectTransform content, NavigationHudUI hud)
        {
            var panel = new GameObject("DestinationPanel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(content, false);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = BottomCenter;
            panel.offsetMin = new Vector2(SideMargin, 0f);
            panel.offsetMax = new Vector2(-SideMargin, 0f);
            panel.anchoredPosition = new Vector2(0f, ShellBottom);
            AddImage(panel, Rounded16(), Glass, Image.Type.Sliced);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = AddText(panel, "Title", "Điểm đến · khuôn viên / B9 / B10", "Medium", 11f, UiTheme.TextSecondary,
                TextAlignmentOptions.MidlineLeft, TopLeft, Vector2.zero, new Vector2(300f, 16f));
            Height(title.gameObject, 16f);

            // Field: pin, selected destination, chevron. Tapping opens the picker sheet.
            RectTransform field = NewRect(panel, "DestinationField", TopLeft, Vector2.zero, new Vector2(300f, ButtonHeight));
            Height(field.gameObject, ButtonHeight);
            Image fieldImage = AddImage(field, Rounded16(), UiTheme.SurfaceAlt, Image.Type.Sliced);
            fieldImage.raycastTarget = true;
            var fieldButton = field.gameObject.AddComponent<Button>();
            fieldButton.targetGraphic = fieldImage;
            fieldButton.transition = Selectable.Transition.ColorTint;
            RectTransform pin = NewRect(field, "Icon", MiddleLeft, new Vector2(12f, 0f), new Vector2(18f, 18f));
            AddImage(pin, Icon(UiCanvasIconGenerator.Pin), UiTheme.Accent);
            TextMeshProUGUI label = AddStretchText(field, "Label", NavigationHudUI.NoDestinationLabel, "Regular", 13f, Color.white, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)label.transform).offsetMin = new Vector2(40f, 0f);
            ((RectTransform)label.transform).offsetMax = new Vector2(-36f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform chevron = NewRect(field, "Chevron", MiddleRight, new Vector2(-12f, 0f), new Vector2(18f, 18f));
            AddImage(chevron, Icon(UiCanvasPaths.ChevronDownFile), UiTheme.TextSecondary);

            Button start = AddIconPill(panel, "StartButton", Icon(UiCanvasIconGenerator.Navigate), "Bắt đầu / đổi điểm đến", UiTheme.Accent,
                TopLeft, Vector2.zero, new Vector2(300f, ButtonHeight), 14f);
            Height(start.gameObject, ButtonHeight);

            var row = new GameObject("Actions", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(panel, false);
            Height(row.gameObject, 40f);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            Button cancel = AddIconPill(row, "CancelButton", Icon(UiCanvasIconGenerator.Cross), "Hủy chỉ đường", UiTheme.Danger,
                TopLeft, Vector2.zero, new Vector2(140f, 40f), 13f);
            Button exit = AddIconPill(row, "ExitButton", Icon(UiCanvasIconGenerator.ExitDoor), "Dẫn ra ngoài", Warning,
                TopLeft, Vector2.zero, new Vector2(140f, 40f), 13f);
            exit.gameObject.SetActive(false);

            SetReference(hud, "destinationPanel", panel);
            SetReference(hud, "destinationField", fieldButton);
            SetReference(hud, "destinationLabel", label);
            SetReference(hud, "startButton", start);
            SetReference(hud, "actionsRow", row.gameObject);
            SetReference(hud, "cancelButton", cancel);
            SetReference(hud, "exitButton", exit);
        }

        // ---------------------------------------------------------------- research

        private static void BuildResearchPanel(RectTransform content, NavigationHudUI hud)
        {
            RectTransform panel = NewStretch(content, "ResearchPanel");
            RectTransform scrim = NewStretch(panel, "Scrim");
            Image scrimImage = AddImage(scrim, null, new Color(0f, 0f, 0f, 0.6f));
            scrimImage.raycastTarget = true;
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.targetGraphic = scrimImage;
            scrimButton.transition = Selectable.Transition.None;

            var card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(panel, false);
            card.anchorMin = new Vector2(0f, 0.5f);
            card.anchorMax = new Vector2(1f, 0.5f);
            card.pivot = Middle;
            card.offsetMin = new Vector2(SideMargin, -135f);
            card.offsetMax = new Vector2(-SideMargin, 135f);
            Image cardImage = AddImage(card, Rounded16(), UiTheme.Background, Image.Type.Sliced);
            cardImage.raycastTarget = true; // taps on the card must not reach the scrim

            AddText(card, "Title", "Công cụ nghiên cứu", "SemiBold", 16f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(16f, -14f), new Vector2(240f, 24f));
            Button close = AddIconButton(card, "CloseButton", Icon(UiCanvasIconGenerator.Cross),
                new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 40f), new Vector2(18f, 18f), UiTheme.TextSecondary);

            TextMeshProUGUI harmony = AddLine(card, "Harmony", "HARMONY V5 · Full HARMONY", "Regular", 11f, 9f, UiTheme.TextBody, 46f, 32f, 16f);
            harmony.textWrappingMode = TextWrappingModes.Normal;
            harmony.enableAutoSizing = false;

            RectTransform grid = NewTopBand(card, "Versions", 84f, 72f);
            grid.offsetMin = new Vector2(16f, grid.offsetMin.y);
            grid.offsetMax = new Vector2(-16f, grid.offsetMax.y);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(57f, 32f);
            gridLayout.spacing = new Vector2(6f, 8f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;
            Button template = AddSmallPill(grid, "VersionTemplate", "V5", UiTheme.Surface, TopLeft, Vector2.zero, new Vector2(57f, 32f), 12f);
            template.GetComponentInChildren<TextMeshProUGUI>().text = "V5";

            TextMeshProUGUI log = AddLine(card, "Experiment", "LOG · đang chuẩn bị", "Medium", 11f, 9f, LogText, 166f, 32f, 16f);
            log.textWrappingMode = TextWrappingModes.Normal;
            log.enableAutoSizing = false;

            RectTransform buttons = NewTopBand(card, "LogButtons", 208f, 40f);
            buttons.offsetMin = new Vector2(16f, buttons.offsetMin.y);
            buttons.offsetMax = new Vector2(-16f, buttons.offsetMax.y);
            var buttonsLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 6f;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = true;
            Button marker = AddIconPill(buttons, "MarkerButton", Icon(UiCanvasIconGenerator.Flag), "Đánh dấu", Muted, TopLeft, Vector2.zero, new Vector2(100f, 40f), 11f);
            Button trial = AddIconPill(buttons, "TrialButton", Icon(UiCanvasIconGenerator.Record), "Lưu lần thử", UiTheme.Accent, TopLeft, Vector2.zero, new Vector2(100f, 40f), 11f);
            Button export = AddIconPill(buttons, "ExportButton", Icon(UiCanvasIconGenerator.Download), "Xuất 3 CSV", Success, TopLeft, Vector2.zero, new Vector2(100f, 40f), 11f);

            panel.gameObject.SetActive(false);

            SetReference(hud, "researchPanel", panel.gameObject);
            SetReference(hud, "researchCloseButton", close);
            SetReference(hud, "harmonyLabel", harmony);
            SetReference(hud, "versionGrid", grid);
            SetReference(hud, "versionTemplate", template);
            SetReference(hud, "experimentLabel", log);
            SetReference(hud, "markerButton", marker);
            SetReference(hud, "trialButton", trial);
            SetReference(hud, "trialLabel", trial.transform.Find("Label").GetComponent<TMP_Text>());
            SetReference(hud, "exportButton", export);

            // The scrim closes the panel like the X does.
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(scrimButton.onClick, hud.CloseResearch);
        }

        // ---------------------------------------------------------------- helpers

        private static void WriteMinimapMask()
        {
            string path = UiCanvasPaths.ScreenArt(HudName, MinimapMaskFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(MinimapMaskPixels, MinimapMaskPixels, TextureFormat.RGBA32, false);
            var pixels = new Color32[MinimapMaskPixels * MinimapMaskPixels];
            float half = MinimapMaskPixels * 0.5f;
            for (int y = 0; y < MinimapMaskPixels; y++)
            {
                for (int x = 0; x < MinimapMaskPixels; x++)
                {
                    float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - (half - 1f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * MinimapMaskPixels + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Full-width band (16 from each side) pinned to the top of <paramref name="parent"/>.</summary>
        private static RectTransform NewTopBand(Transform parent, string name, float top, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = TopCenter;
            rect.offsetMin = new Vector2(SideMargin, 0f);
            rect.offsetMax = new Vector2(-SideMargin, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = new Vector2(0f, -top);
            return rect;
        }

        /// <summary>One line of the card: left aligned, shrinks (down to <paramref name="minSize"/>) rather than wrapping.</summary>
        private static TextMeshProUGUI AddLine(RectTransform card, string name, string text, string weight, float size, float minSize,
            Color color, float top, float height, float rightInset)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(card, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = TopCenter;
            rect.offsetMin = new Vector2(14f, 0f);
            rect.offsetMax = new Vector2(-rightInset, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = LoadFont(weight);
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = size;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Round-ended button with a white icon left of its label, both centered.</summary>
        private static Button AddIconPill(Transform parent, string name, Sprite icon, string label, Color fill,
            Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            Button button = AddSmallPill(parent, name, label, fill, anchor, position, size, fontSize);
            var rect = (RectTransform)button.transform;
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6f;
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform iconRect = NewRect(rect, "Icon", Middle, Vector2.zero, new Vector2(16f, 16f));
            AddImage(iconRect, icon, Color.white);
            var iconSize = iconRect.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = 16f;
            iconSize.preferredHeight = 16f;
            iconRect.SetAsFirstSibling();

            TextMeshProUGUI text = rect.Find("Label").GetComponent<TextMeshProUGUI>();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return button;
        }

        private static void Height(GameObject target, float height)
        {
            // No "??": in the editor a missing component comes back as a fake null that "??" treats as present.
            var element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredHeight = height;
            element.minHeight = height;
        }
    }
}
#endif
