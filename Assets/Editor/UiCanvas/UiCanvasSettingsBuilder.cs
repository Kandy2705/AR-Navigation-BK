#if UNITY_EDITOR
using System.IO;
using Game.Features.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Game.Foundation.UI.EditorTools.UiCanvasBuilderKit;
using static Game.Foundation.UI.EditorTools.UiCanvasListKit;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Phase 7: settings, profile, change email, change password, support center and contact, from the thesis
    /// mock-up (Hình 6.10). The OTP step and the success card are the shared auth prefabs.
    /// </summary>
    public static class UiCanvasSettingsBuilder
    {
        public const string SettingsName = "SettingsUI";
        public const string ProfileName = "ProfileUI";
        public const string ChangeEmailName = "ChangeEmailUI";
        public const string ChangePasswordName = "ChangePasswordUI";
        public const string SupportCenterName = "SupportCenterUI";
        public const string ContactName = "ContactUI";

        private const float ButtonBottomMargin = 32f;
        private const float FormCardTop = 274f;
        private const float ProfileRowHeight = 57f;
        private const float ProfileCardTop = 415.5f;
        private static readonly Color Hairline = new Color(0.125f, 0.125f, 0.125f, 1f);

        [MenuItem("Tools/UI Canvas/Build Settings Screens")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the settings screens.");
                return;
            }

            UiCanvasAuthBuilder.EnsureStatusOverlay();
            EnsureBottomNav();
            EnsureConfirmSheet();
            BuildSettings();
            BuildProfile();
            BuildChangeEmail();
            BuildChangePassword();
            BuildSupportCenter();
            BuildContact();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Settings prefabs built");
        }

        private static void BuildSettings()
        {
            var page = NewPage<SettingsUI>(SettingsName);
            SetPageId(page, PageID.MainSettings);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            AddText(content, "Title", "Cài đặt", "SemiBold", 20f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(24f, -(TitleCenterY - 20f)), new Vector2(240f, 40f));

            AddSectionLabel(content, "AccountLabel", "Về tài khoản", 137f);
            Button profile = AddMenuRow(content, "ProfileButton", "Thông tin cá nhân", Icon(UiCanvasIconGenerator.User), 180f, Color.white);

            AddSectionLabel(content, "AboutLabel", "Về chúng tôi", 225.5f);
            Button support = AddMenuRow(content, "SupportButton", "Trung tâm hỗ trợ", Icon(UiCanvasIconGenerator.Help), 268f, Color.white);
            AddHelpMark(support.transform);
            Button contact = AddMenuRow(content, "ContactButton", "Thông tin liên hệ", Icon(UiCanvasIconGenerator.Doc), 312f, Color.white);
            Button logout = AddMenuRow(content, "LogoutButton", "Logout", Icon(UiCanvasIconGenerator.Logout), 360f, UiTheme.DangerText);

            UiBottomNav nav = AddBottomNav(content);
            UiModal modal = AddConfirmSheet(page.transform);

            SetReference(page, "profileButton", profile);
            SetReference(page, "supportButton", support);
            SetReference(page, "contactButton", contact);
            SetReference(page, "logoutButton", logout);
            SetReference(page, "bottomNav", nav);
            SetReference(page, "logoutModal", modal);
            Save(page.gameObject, SettingsName);
        }

        private static void BuildProfile()
        {
            var page = NewPage<ProfileUI>(ProfileName);
            SetPageId(page, PageID.Profile);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            // The form scrolls above the two buttons, so short screens never hide a field behind them.
            RectTransform form = AddScrollArea(content, "Form", 152f, 660f);
            Button back = AddTitleBar(form, "Thông tin cá nhân");
            AddAvatar(form, 270f);

            RectTransform card = AddInputCard(form, "Card", ProfileCardTop, 4, ProfileRowHeight);
            UiTextField name = AddInputRow(card, 0, "NameField", "Tên người dùng", null, false, TouchScreenKeyboardType.Default, ProfileRowHeight);
            UiTextField phone = AddInputRow(card, 1, "PhoneField", "Số điện thoại", null, false, TouchScreenKeyboardType.PhonePad, ProfileRowHeight);
            UiSelectField gender = AddSelectRow(card, 2, "GenderField", "Giới tính", ProfileRowHeight);
            UiSelectField birthday = AddSelectRow(card, 3, "BirthdayField", "Ngày sinh", ProfileRowHeight);

            Button email = AddPillButton(content, "ChangeEmailButton", "Đổi Email", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin + 56f), ContentWidth);
            Button password = AddPillButton(content, "ChangePasswordButton", "Đổi mật khẩu", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ContentWidth);

            SetReference(page, "backButton", back);
            SetReference(page, "nameField", name);
            SetReference(page, "phoneField", phone);
            SetReference(page, "genderField", gender);
            SetReference(page, "birthdayField", birthday);
            SetReference(page, "changeEmailButton", email);
            SetReference(page, "changePasswordButton", password);
            Save(page.gameObject, ProfileName);
        }

        private static void BuildChangeEmail()
        {
            var page = NewPage<ChangeEmailUI>(ChangeEmailName);
            SetPageId(page, PageID.EmailChange);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Thay đổi Email",
                "Vui lòng nhập email mới của bạn và chúng tôi sẽ gửi mã OTP ở bước tiếp theo để xác nhận thay đổi.");
            RectTransform card = AddInputCard(content, "Card", FormCardTop, 1);
            UiTextField email = AddInputRow(card, 0, "EmailField", "Email", Icon(UiCanvasPaths.MailIconFile), false, TouchScreenKeyboardType.EmailAddress);
            TextMeshProUGUI error = AddErrorLabel(content, FormCardTop + 53f + 8f);
            Button next = AddPillButton(content, "ContinueButton", "Tiếp tục", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ContentWidth);
            UiStatusOverlay overlay = AddStatusOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "emailField", email);
            SetReference(page, "continueButton", next);
            SetReference(page, "errorLabel", error);
            SetReference(page, "successOverlay", overlay);
            Save(page.gameObject, ChangeEmailName);
        }

        private static void BuildChangePassword()
        {
            var page = NewPage<ChangePasswordUI>(ChangePasswordName);
            SetPageId(page, PageID.PasswordChange);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Thay đổi mật khẩu",
                "Vui lòng nhập mật khẩu cũ và mật khẩu mới của bạn và chúng tôi sẽ xác nhận lại xem có phù hợp và chấp nhận thay đổi.");
            RectTransform card = AddInputCard(content, "Card", FormCardTop, 3);
            UiTextField oldField = AddInputRow(card, 0, "OldField", "Mật khẩu cũ", null, true);
            UiTextField newField = AddInputRow(card, 1, "NewField", "Mật khẩu mới", null, true);
            UiTextField confirmField = AddInputRow(card, 2, "ConfirmField", "Nhập lại mật khẩu mới", null, true);
            TextMeshProUGUI error = AddErrorLabel(content, FormCardTop + 53f * 3f + 8f);

            RectTransform divider = NewRect(content, "Divider", TopLeft, new Vector2(SideMargin, -473f), new Vector2(ContentWidth, 1f));
            AddImage(divider, null, new Color(0.3f, 0.3f, 0.3f, 1f));
            Button forgot = AddLinkButton(content, "ForgotButton", "Quên mật khẩu ?", "SemiBold", 16f, UiTheme.Primary,
                TopCenter, new Vector2(0f, -488f), new Vector2(220f, 40f));
            Button change = AddPillButton(content, "ChangeButton", "Thay đổi", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ContentWidth);
            UiStatusOverlay overlay = AddStatusOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "oldField", oldField);
            SetReference(page, "newField", newField);
            SetReference(page, "confirmField", confirmField);
            SetReference(page, "forgotButton", forgot);
            SetReference(page, "changeButton", change);
            SetReference(page, "errorLabel", error);
            SetReference(page, "successOverlay", overlay);
            Save(page.gameObject, ChangePasswordName);
        }

        private static void BuildSupportCenter()
        {
            var page = NewPage<SupportCenterUI>(SupportCenterName);
            SetPageId(page, PageID.SupportCenter);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddTitleBar(content, "Trung tâm hỗ trợ");
            UiChipRow chips = AddChipRow(content, "CategoryChips", 115f, SupportChips);
            RectTransform list = AddListArea(content, "List", 185f, 0f, 12f, SideMargin);
            UiAccordionItem template = AddAccordionTemplate(list);

            SetReference(page, "backButton", back);
            SetReference(page, "categoryChips", chips);
            SetReference(page, "listContent", list);
            SetReference(page, "itemTemplate", template);
            Save(page.gameObject, SupportCenterName);
        }

        private static void BuildContact()
        {
            var page = NewPage<ContactUI>(ContactName);
            SetPageId(page, PageID.Contact);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Thông tin liên hệ", string.Empty, 10f);

            AddContactIcon(content, "AddressIcon", UiCanvasIconGenerator.Pin, 227f);
            AddText(content, "AddressTitle", "Địa chỉ", "Regular", 14f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(56f, -207f), new Vector2(300f, 20f));
            TextMeshProUGUI address = AddText(content, "AddressValue", ContactInfo.Address, "Light", 16f, UiTheme.TextSubtitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(56f, -228f), new Vector2(300f, 24f));
            AddHairline(content, "AddressLine", 261f);

            AddContactIcon(content, "PhoneIcon", UiCanvasIconGenerator.Handset, 290f);
            AddText(content, "PhoneTitle", "Số điện thoại", "Regular", 14f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(56f, -278f), new Vector2(150f, 24f));
            TextMeshProUGUI phone = AddText(content, "PhoneValue", ContactInfo.Phone, "SemiBold", 14f, Color.white, TextAlignmentOptions.MidlineRight,
                new Vector2(1f, 1f), new Vector2(-SideMargin, -278f), new Vector2(180f, 24f));
            AddHairline(content, "PhoneLine", 318f);

            AddContactIcon(content, "EmailIcon", UiCanvasPaths.MailIconFile, 346f);
            AddText(content, "EmailTitle", "Email", "Regular", 14f, UiTheme.TextTitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(56f, -334f), new Vector2(100f, 24f));
            TextMeshProUGUI email = AddText(content, "EmailValue", ContactInfo.Email, "SemiBold", 14f, Color.white, TextAlignmentOptions.MidlineRight,
                new Vector2(1f, 1f), new Vector2(-SideMargin, -334f), new Vector2(220f, 24f));
            AddHairline(content, "EmailLine", 374f);

            Button send = AddPillButton(content, "SendEmailButton", "Gửi Email", UiTheme.Primary,
                TopCenter, new Vector2(0f, -398f), ContentWidth);

            SetReference(page, "backButton", back);
            SetReference(page, "addressLabel", address);
            SetReference(page, "phoneLabel", phone);
            SetReference(page, "emailLabel", email);
            SetReference(page, "sendEmailButton", send);
            Save(page.gameObject, ContactName);
        }

        // ---------------------------------------------------------------- pieces

        private static void AddSectionLabel(Transform parent, string name, string text, float centerY)
        {
            AddText(parent, name, text, "Regular", 12f, UiTheme.TextSubtitle, TextAlignmentOptions.MidlineLeft,
                TopLeft, new Vector2(24f, -(centerY - 10f)), new Vector2(240f, 20f));
        }

        /// <summary>Icon and label on one 44 px row; the whole row is the button.</summary>
        private static Button AddMenuRow(Transform parent, string name, string label, Sprite icon, float centerY, Color color)
        {
            RectTransform row = NewRect(parent, name, TopLeft, new Vector2(SideMargin, -(centerY - 22f)), new Vector2(ContentWidth, 44f));
            Image hit = AddImage(row, null, Color.clear);
            hit.raycastTarget = true;
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            RectTransform iconRect = NewRect(row, "Icon", MiddleLeft, new Vector2(8f, 0f), new Vector2(24f, 24f));
            AddImage(iconRect, icon, color);
            AddText(row, "Label", label, "Light", 16f, color, TextAlignmentOptions.MidlineLeft,
                MiddleLeft, new Vector2(40f, 0f), new Vector2(280f, 28f));
            return button;
        }

        private static void AddHelpMark(Transform row)
        {
            RectTransform icon = (RectTransform)row.Find("Icon");
            AddText(icon, "Mark", "?", "Medium", 13f, Color.white, TextAlignmentOptions.Center, Middle, Vector2.zero, new Vector2(24f, 24f));
        }

        private static void AddContactIcon(Transform parent, string name, string spriteFile, float centerY)
        {
            RectTransform tile = NewRect(parent, name, TopLeft, new Vector2(SideMargin, -(centerY - 16f)), new Vector2(32f, 32f));
            AddImage(tile, LoadSprite(UiCanvasPaths.CircleSprite), UiTheme.SurfaceAlt);
            RectTransform glyph = NewRect(tile, "Icon", Middle, Vector2.zero, new Vector2(16f, 16f));
            AddImage(glyph, Icon(spriteFile), UiTheme.Accent);
        }

        private static void AddHairline(Transform parent, string name, float y)
        {
            RectTransform line = NewRect(parent, name, TopLeft, new Vector2(SideMargin, -y), new Vector2(ContentWidth, 1f));
            AddImage(line, null, Hairline);
        }

        private static UiAccordionItem AddAccordionTemplate(RectTransform listContent)
        {
            var root = new GameObject("ItemTemplate", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(listContent, false);
            AddImage(root, Rounded16(), UiTheme.Surface, Image.Type.Sliced);
            var item = root.gameObject.AddComponent<UiAccordionItem>();
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
            header.SetParent(root, false);
            Image hit = AddImage(header, null, Color.clear);
            hit.raycastTarget = true;
            var headerButton = header.gameObject.AddComponent<Button>();
            headerButton.targetGraphic = hit;
            headerButton.transition = Selectable.Transition.None;
            header.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.spacing = 8f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            TextMeshProUGUI question = AddText(header, "Question", "Câu hỏi", "SemiBold", 14f, UiTheme.TextTitle,
                TextAlignmentOptions.MidlineLeft, TopLeft, Vector2.zero, new Vector2(200f, 24f));
            question.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var chevron = new GameObject("Chevron", typeof(RectTransform)).GetComponent<RectTransform>();
            chevron.SetParent(header, false);
            chevron.sizeDelta = new Vector2(24f, 24f);
            AddImage(chevron, Icon(UiCanvasIconGenerator.ChevronRight), Color.white);
            var chevronSize = chevron.gameObject.AddComponent<LayoutElement>();
            chevronSize.preferredWidth = 24f;
            chevronSize.preferredHeight = 24f;

            TextMeshProUGUI answer = AddText(root, "Answer", "Trả lời", "Light", 14f, UiTheme.TextSubtitle,
                TextAlignmentOptions.TopLeft, TopLeft, Vector2.zero, new Vector2(200f, 40f));
            answer.margin = new Vector4(0f, 0f, 32f, 0f);

            SetReference(item, "header", headerButton);
            SetReference(item, "questionLabel", question);
            SetReference(item, "answerLabel", answer);
            SetReference(item, "chevron", chevron);
            root.gameObject.SetActive(false);
            return item;
        }

        private static UiStatusOverlay AddStatusOverlay(Transform page)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasAuthBuilder.StatusOverlayPrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, page);
            instance.transform.SetAsLastSibling();
            return instance.GetComponent<UiStatusOverlay>();
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
