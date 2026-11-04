#if UNITY_EDITOR
using System.IO;
using Game.Features.Auth;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Game.Foundation.UI.EditorTools.UiCanvasBuilderKit;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Phase 3: builds the RegisterUI and CreatePasswordUI prefabs and the PickerSheet (gender and birthday)
    /// from the Figma register flow. The OTP step reuses OtpUI and the success card reuses StatusOverlay.
    /// </summary>
    public static class UiCanvasRegisterBuilder
    {
        public const string RegisterName = "RegisterUI";
        public const string CreatePasswordName = "CreatePasswordUI";
        public const string PickerSheetPrefab = UiCanvasPaths.CommonPrefabs + "/PickerSheet.prefab";

        private const float ButtonWidth = 343f;
        private const float ButtonBottomMargin = 33f;

        // Profile card: 5 rows of 57 starting at y=415.5. The form scrolls above a reserved strip
        // (button 33 + 48, gap, error line) so on short screens the button never covers a field.
        private const float ProfileRowHeight = 57f;
        private const float ProfileCardTop = 415.5f;
        private const float ProfileScrollInset = 105f;
        private const float ProfileContentHeight = 704f;

        private const float PasswordCardTop = 305.5f;

        [MenuItem("Tools/UI Canvas/Build Register Screens (Phase 3)")]
        public static void Build()
        {
            // In Play mode AddComponent runs Awake at once, before the fields are wired.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the register screens.");
                return;
            }

            UiCanvasAuthBuilder.EnsureStatusOverlay();
            BuildPickerSheet();
            BuildRegister();
            BuildCreatePassword();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Register prefabs built");
        }

        private static void BuildRegister()
        {
            var page = NewPage<RegisterUI>(RegisterName);
            SetPageId(page, PageID.Register);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            RectTransform form = AddScrollArea(content, "Form", ProfileScrollInset, ProfileContentHeight);
            Button back = AddAuthHeader(form, "Hoàn thiện hồ sơ của bạn!", "Để được tham gia thì bạn hãy hoàn thiện hồ sơ bên dưới.", 50f, twoLineTitle: true);
            AddAvatar(form, 271f);

            RectTransform card = AddInputCard(form, "Card", ProfileCardTop, 5, ProfileRowHeight);
            UiTextField name = AddInputRow(card, 0, "NameField", "Tên người dùng", null, false, TouchScreenKeyboardType.Default, ProfileRowHeight);
            UiTextField phone = AddInputRow(card, 1, "PhoneField", "Số điện thoại", null, false, TouchScreenKeyboardType.PhonePad, ProfileRowHeight);
            UiTextField email = AddInputRow(card, 2, "EmailField", "Email", null, false, TouchScreenKeyboardType.EmailAddress, ProfileRowHeight);
            UiSelectField gender = AddSelectRow(card, 3, "GenderField", "Giới tính", ProfileRowHeight);
            UiSelectField birthday = AddSelectRow(card, 4, "BirthdayField", "Ngày sinh", ProfileRowHeight);

            TextMeshProUGUI error = AddBottomErrorLabel(content);
            Button next = AddPillButton(content, "ContinueButton", "Tiếp tục", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);
            UiPickerSheet sheet = AddSheet(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "nameField", name);
            SetReference(page, "phoneField", phone);
            SetReference(page, "emailField", email);
            SetReference(page, "genderField", gender);
            SetReference(page, "birthdayField", birthday);
            SetReference(page, "continueButton", next);
            SetReference(page, "errorLabel", error);
            SetReference(page, "pickerSheet", sheet);
            SavePrefab(page.gameObject, RegisterName);
        }

        private static void BuildCreatePassword()
        {
            var page = NewPage<CreatePasswordUI>(CreatePasswordName);
            SetPageId(page, PageID.PasswordConfirm);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Tuyệt vời gần xong rồi, Đặt mật khẩu nào!",
                "Hãy đặt mật khẩu cho tài khoản của bạn nào. Mật khẩu phải có ít nhất 8 ký tự, gồm chữ và số.", 70f, twoLineTitle: true);

            // Figma draws the eye on the second row only; both rows get it so either password can be checked.
            RectTransform card = AddInputCard(content, "Card", PasswordCardTop, 2, ProfileRowHeight);
            UiTextField password = AddInputRow(card, 0, "PasswordField", "Mật khẩu", null, true, TouchScreenKeyboardType.Default, ProfileRowHeight);
            UiTextField confirm = AddInputRow(card, 1, "ConfirmField", "Nhập lại mật khẩu", null, true, TouchScreenKeyboardType.Default, ProfileRowHeight);
            TextMeshProUGUI error = AddErrorLabel(content, PasswordCardTop + ProfileRowHeight * 2 + 8f);
            Button next = AddPillButton(content, "ContinueButton", "Tiếp tục", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);

            UiStatusOverlay overlay = AddStatusOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "passwordField", password);
            SetReference(page, "confirmField", confirm);
            SetReference(page, "continueButton", next);
            SetReference(page, "errorLabel", error);
            SetReference(page, "successOverlay", overlay);
            SavePrefab(page.gameObject, CreatePasswordName);
        }

        /// <summary>
        /// The bottom sheet: dim scrim, a rounded panel with a title, up to three option columns and "Xong".
        /// Not in the Figma (only the closed dropdown rows are), so the look follows the other cards.
        /// </summary>
        private static void BuildPickerSheet()
        {
            var root = new GameObject("PickerSheet", typeof(RectTransform));
            Stretch((RectTransform)root.transform);
            var sheet = root.AddComponent<UiPickerSheet>();

            RectTransform scrim = NewStretch(root.transform, "Scrim");
            Image scrimImage = AddImage(scrim, null, new Color(0f, 0f, 0f, 0.6f));
            scrimImage.raycastTarget = true;
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.targetGraphic = scrimImage;
            scrimButton.transition = Selectable.Transition.None;

            // The panel starts 16 below the screen so its bottom corners are never visible.
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, -16f);
            panel.sizeDelta = new Vector2(0f, 396f);
            Image panelImage = AddImage(panel, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR16File)), UiTheme.SurfaceAlt, Image.Type.Sliced);
            panelImage.raycastTarget = true; // taps on the panel must not reach the scrim

            TextMeshProUGUI title = AddText(panel, "Title", string.Empty, "SemiBold", 18f, UiTheme.TextTitle,
                TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -16f), new Vector2(ButtonWidth, 28f));

            RectTransform columnsRow = NewRect(panel, "Columns", TopCenter, new Vector2(0f, -60f), new Vector2(ButtonWidth, 228f));
            var layout = columnsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            const int columnCount = 3;
            var columns = new UiOptionColumn[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                columns[i] = BuildColumn(columnsRow, i);
            }

            Button done = AddPillButton(panel, "DoneButton", "Xong", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin + 16f), ButtonWidth);

            SetReference(sheet, "titleLabel", title);
            SetReference(sheet, "doneButton", done);
            SetReference(sheet, "scrimButton", scrimButton);
            var serialized = new SerializedObject(sheet);
            SerializedProperty list = serialized.FindProperty("columns");
            list.arraySize = columnCount;
            for (int i = 0; i < columnCount; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = columns[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            Directory.CreateDirectory(UiCanvasPaths.CommonPrefabs);
            PrefabUtility.SaveAsPrefabAsset(root, PickerSheetPrefab);
            Object.DestroyImmediate(root);
        }

        private static UiOptionColumn BuildColumn(RectTransform parent, int index)
        {
            RectTransform root = NewStretch(parent, "Column" + (index + 1));
            var column = root.gameObject.AddComponent<UiOptionColumn>();
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = NewStretch(root, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, null, Color.clear).raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // Hidden template the column clones for every option.
            RectTransform item = NewStretch(root, "ItemTemplate");
            Image highlight = AddImage(item, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR16File)), Color.clear, Image.Type.Sliced);
            highlight.raycastTarget = true;
            var button = item.gameObject.AddComponent<Button>();
            button.targetGraphic = highlight;
            button.transition = Selectable.Transition.None;
            var size = item.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = 44f;
            AddStretchText(item, "Label", string.Empty, "Regular", 18f, UiTheme.Placeholder, TextAlignmentOptions.Center);
            item.gameObject.SetActive(false);

            SetReference(column, "scroll", scroll);
            SetReference(column, "content", content);
            SetReference(column, "itemTemplate", button);
            return column;
        }

        private static UiPickerSheet AddSheet(Transform page)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickerSheetPrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, page);
            instance.transform.SetAsLastSibling();
            return instance.GetComponent<UiPickerSheet>();
        }

        private static UiStatusOverlay AddStatusOverlay(Transform page)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasAuthBuilder.StatusOverlayPrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, page);
            instance.transform.SetAsLastSibling();
            return instance.GetComponent<UiStatusOverlay>();
        }

        private static void SavePrefab(GameObject root, string screenName)
        {
            string path = UiCanvasPaths.ScreenPrefab(screenName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
