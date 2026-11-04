#if UNITY_EDITOR
using System.IO;
using Game.Features.Chat;
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
    /// Phase 4-6: the AR page overlay (ArHudUI), history, search and chat screens, built from the thesis
    /// mock-ups (Hình 6.7 - 6.9). The tab bar, confirm sheet and busy overlay are shared prefabs.
    /// </summary>
    public static class UiCanvasHomeBuilder
    {
        public const string ArHudName = "ArHudUI";
        public const string HistoryName = "HistoryUI";
        public const string ChatName = "ChatUI";
        public const string SearchName = "SearchUI";

        private const float HistoryChipsTop = 124f;
        private const float HistoryListTop = 171f;
        private const float ChatListTop = 112f;
        private const float ChatInputBottom = 100f;

        [MenuItem("Tools/UI Canvas/Build Home, History and Chat Screens")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the home screens.");
                return;
            }

            EnsureBottomNav();
            EnsureConfirmSheet();
            EnsureBusyOverlay();
            BuildArHud();
            BuildHistory();
            BuildChat();
            BuildSearch();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Home, history and chat prefabs built");
        }

        /// <summary>The overlay on the camera view. No background: the AR scene shows through.</summary>
        private static void BuildArHud()
        {
            var page = NewPage<ArHudUI>(ArHudName);
            SetPageId(page, PageID.ARPage);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button clear = AddSmallPill(content, "ClearRouteButton", "Xóa chỉ đường", UiTheme.Danger,
                TopLeft, new Vector2(16f, -61f), new Vector2(163f, 32f), 12f);
            Button menu = AddSmallPill(content, "MenuButton", "Lịch sử/Cài đặt", UiTheme.Accent,
                new Vector2(1f, 1f), new Vector2(-18f, -61f), new Vector2(162f, 32f), 12f);

            // The assistant's picture is a placeholder until the 3D character render is exported.
            RectTransform avatar = NewRect(content, "Avatar", BottomLeft, new Vector2(8f, 18f), new Vector2(96f, 96f));
            AddImage(avatar, LoadSprite(UiCanvasPaths.CircleSprite), UiTheme.Surface);
            RectTransform face = NewRect(avatar, "Icon", Middle, Vector2.zero, new Vector2(44f, 44f));
            AddImage(face, Icon(UiCanvasPaths.UserIconFile), Color.white);

            RectTransform bubble = NewRect(content, "PromptBubble", BottomLeft, new Vector2(112f, 70f), new Vector2(110f, 24f));
            AddImage(bubble, Rounded16(), new Color(0f, 0f, 0f, 0.75f), Image.Type.Sliced);
            var bubbleLayout = bubble.gameObject.AddComponent<HorizontalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(14, 14, 4, 4);
            bubbleLayout.childControlWidth = true;
            bubbleLayout.childControlHeight = true;
            bubbleLayout.childForceExpandWidth = false;
            bubbleLayout.childForceExpandHeight = false;
            var bubbleFit = bubble.gameObject.AddComponent<ContentSizeFitter>();
            bubbleFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bubbleFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI prompt = AddText(bubble, "Label", ArHudUI.AskPrompt, "Regular", 11f, Color.white,
                TextAlignmentOptions.Center, Middle, Vector2.zero, new Vector2(80f, 16f));
            prompt.textWrappingMode = TextWrappingModes.NoWrap;

            var bar = new GameObject("SearchBar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(content, false);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = BottomCenter;
            bar.offsetMin = new Vector2(112f, 26f);
            bar.offsetMax = new Vector2(-16f, 62f);
            Image barImage = AddImage(bar, Rounded16(), new Color(0.07f, 0.07f, 0.07f, 0.92f), Image.Type.Sliced);
            barImage.raycastTarget = true;
            var barButton = bar.gameObject.AddComponent<Button>();
            barButton.targetGraphic = barImage;
            barButton.transition = Selectable.Transition.None;
            TextMeshProUGUI barLabel = AddStretchText(bar, "Label", ArHudUI.SearchPlaceholder, "Light", 12f, UiTheme.Placeholder, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)barLabel.transform).offsetMin = new Vector2(16f, 0f);
            ((RectTransform)barLabel.transform).offsetMax = new Vector2(-16f, 0f);
            barLabel.textWrappingMode = TextWrappingModes.NoWrap;
            barLabel.overflowMode = TextOverflowModes.Ellipsis;

            UiBusyOverlay busy = AddBusyOverlay(page.transform);

            SetReference(page, "menuButton", menu);
            SetReference(page, "clearRouteButton", clear);
            SetReference(page, "searchBar", barButton);
            SetReference(page, "searchLabel", barLabel);
            SetReference(page, "promptLabel", prompt);
            SetReference(page, "busy", busy);
            Save(page.gameObject, ArHudName);
        }

        private static void BuildHistory()
        {
            var page = NewPage<HistoryUI>(HistoryName);
            SetPageId(page, PageID.HistoryPage);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddTitleBar(content, "Lịch sử");
            Button search = AddCornerIconButton(content, "SearchButton", Icon(UiCanvasIconGenerator.Search),
                new Vector2(1f, 1f), new Vector2(-53f, -(TitleCenterY - 20f)));
            Button clearAll = AddCornerIconButton(content, "ClearAllButton", Icon(UiCanvasIconGenerator.Trash),
                new Vector2(1f, 1f), new Vector2(-13f, -(TitleCenterY - 20f)));

            UiChipRow chips = AddChipRow(content, "RangeChips", HistoryChipsTop, HistoryChips);
            RectTransform list = AddListArea(content, "List", HistoryListTop, NavHeight, 12f, SideMargin);
            UiHistoryRow template = AddHistoryRowTemplate(list);

            TextMeshProUGUI empty = AddText(content, "EmptyLabel", HistoryUI.EmptyMessage, "Light", 16f, UiTheme.Placeholder,
                TextAlignmentOptions.Center, Middle, new Vector2(0f, 40f), new Vector2(300f, 24f));
            empty.gameObject.SetActive(false);

            UiBottomNav nav = AddBottomNav(content);
            UiModal confirm = AddConfirmSheet(page.transform);
            UiBusyOverlay busy = AddBusyOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "searchButton", search);
            SetReference(page, "clearAllButton", clearAll);
            SetReference(page, "rangeChips", chips);
            SetReference(page, "listContent", list);
            SetReference(page, "rowTemplate", template);
            SetReference(page, "emptyLabel", empty);
            SetReference(page, "bottomNav", nav);
            SetReference(page, "confirmModal", confirm);
            SetReference(page, "busy", busy);
            Save(page.gameObject, HistoryName);
        }

        private static void BuildChat()
        {
            var page = NewPage<ChatUI>(ChatName);
            SetPageId(page, PageID.Chatbox);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddTitleBar(content, "Chatbot");
            TextMeshProUGUI title = content.Find("Title").GetComponent<TextMeshProUGUI>();
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform list = AddListArea(content, "Messages", ChatListTop, ChatInputBottom, 12f, 0f, 8f, 16f);
            UiChatBubble user = AddBubbleTemplate(list, "UserTemplate", true);
            UiChatBubble bot = AddBubbleTemplate(list, "BotTemplate", false);

            // Input bar: rounded field with an outline and a round send button.
            var box = new GameObject("InputBox", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(content, false);
            box.anchorMin = new Vector2(0f, 0f);
            box.anchorMax = new Vector2(1f, 0f);
            box.pivot = BottomCenter;
            box.offsetMin = new Vector2(16f, 24f);
            box.offsetMax = new Vector2(-68f, 72f);
            AddImage(box, Rounded16(), UiTheme.SurfaceAlt, Image.Type.Sliced);
            RectTransform outline = NewStretch(box, "Outline");
            AddImage(outline, Icon(UiCanvasPaths.OutlineR16File), UiTheme.Border, Image.Type.Sliced);
            TMP_InputField input = AddTmpInput(box, "Input", "Ask me anything...", "Light", 14f, new Vector2(16f, 16f), TextAlignmentOptions.MidlineLeft);

            RectTransform sendRect = NewRect(content, "SendButton", new Vector2(1f, 0f), new Vector2(-16f, 28f), new Vector2(40f, 40f));
            Image sendImage = AddImage(sendRect, LoadSprite(UiCanvasPaths.CircleSprite), UiTheme.Primary);
            sendImage.raycastTarget = true;
            var send = sendRect.gameObject.AddComponent<Button>();
            send.targetGraphic = sendImage;
            send.transition = Selectable.Transition.ColorTint;
            RectTransform plane = NewRect(sendRect, "Icon", Middle, Vector2.zero, new Vector2(20f, 20f));
            AddImage(plane, Icon(UiCanvasIconGenerator.Send), Color.white);

            SetReference(page, "backButton", back);
            SetReference(page, "titleLabel", title);
            SetReference(page, "scroll", ScrollOf(list));
            SetReference(page, "messageContent", list);
            SetReference(page, "userBubble", user);
            SetReference(page, "botBubble", bot);
            SetReference(page, "input", input);
            SetReference(page, "sendButton", send);
            Save(page.gameObject, ChatName);
        }

        private static void BuildSearch()
        {
            var page = NewPage<SearchUI>(SearchName);
            SetPageId(page, PageID.SearchPage);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddIconButton(content, "BackButton", Icon(UiCanvasPaths.BackIconFile),
                TopLeft, new Vector2(9.5f, -(TitleCenterY - 20f)), new Vector2(40f, 40f), new Vector2(24f, 24f), Color.white);

            RectTransform box = NewRect(content, "QueryBox", TopLeft, new Vector2(60f, -(TitleCenterY - 28f)), new Vector2(299f, 56f));
            AddImage(box, Rounded16(), UiTheme.SurfaceAlt, Image.Type.Sliced);
            RectTransform outline = NewStretch(box, "Outline");
            AddImage(outline, Icon(UiCanvasPaths.OutlineR16File), UiTheme.Border, Image.Type.Sliced);
            TMP_InputField query = AddTmpInput(box, "Input", "Tên tiêu đề", "Light", 14f, new Vector2(16f, 16f), TextAlignmentOptions.MidlineLeft);

            RectTransform list = AddListArea(content, "Results", 128f, 0f, 12f, SideMargin);
            UiHistoryRow template = AddHistoryRowTemplate(list);

            // "Không tìm thấy": a gray tile with a cross, then a title and a hint (Hình 6.8, third frame).
            RectTransform empty = NewStretch(content, "EmptyState");
            RectTransform tile = NewRect(empty, "Tile", TopCenter, new Vector2(0f, -187f), new Vector2(ContentWidth, 340f));
            AddImage(tile, Icon(UiCanvasPaths.RoundedR32File), new Color(0.15f, 0.15f, 0.15f, 1f), Image.Type.Sliced);
            RectTransform cross = NewRect(tile, "Cross", Middle, Vector2.zero, new Vector2(64f, 64f));
            AddImage(cross, Rounded16(), new Color(0.29f, 0.29f, 0.29f, 1f), Image.Type.Sliced);
            RectTransform mark = NewRect(cross, "Icon", Middle, Vector2.zero, new Vector2(30f, 30f));
            AddImage(mark, Icon(UiCanvasIconGenerator.Cross), new Color(0.1f, 0.1f, 0.1f, 1f));
            AddText(empty, "Title", "Không tìm thấy", "SemiBold", 24f, UiTheme.TextTitle, TextAlignmentOptions.Center,
                TopCenter, new Vector2(0f, -562f), new Vector2(ContentWidth, 36f));
            AddText(empty, "Message", "Rất tiếc, không có tin nhắn nào khớp với tìm kiếm của bạn", "Regular", 14f, UiTheme.TextSubtitle,
                TextAlignmentOptions.Top, TopCenter, new Vector2(0f, -610f), new Vector2(300f, 44f));
            empty.gameObject.SetActive(false);

            UiBusyOverlay busy = AddBusyOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "queryInput", query);
            SetReference(page, "listContent", list);
            SetReference(page, "rowTemplate", template);
            SetReference(page, "emptyState", empty.gameObject);
            SetReference(page, "busy", busy);
            Save(page.gameObject, SearchName);
        }

        private static void Save(GameObject root, string screenName)
        {
            string path = UiCanvasPaths.ScreenPrefab(screenName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
