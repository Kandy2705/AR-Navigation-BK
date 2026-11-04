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
    /// Phase 2: builds the LoginUI, ForgotEmailUI, OtpUI and NewPasswordUI prefabs and the shared status
    /// overlay from the Figma login flow (375x812 frame; positions are measured from the top-left corner).
    /// </summary>
    public static class UiCanvasAuthBuilder
    {
        public const string LoginName = "LoginUI";
        public const string ForgotEmailName = "ForgotEmailUI";
        public const string OtpName = "OtpUI";
        public const string NewPasswordName = "NewPasswordUI";
        public const string StatusOverlayPrefab = UiCanvasPaths.CommonPrefabs + "/StatusOverlay.prefab";

        private const float ButtonWidth = 343f;
        private const float ButtonBottomMargin = 33f;
        private const float CardTop = 277.5f;

        [MenuItem("Tools/UI Canvas/Build Auth Screens (Phase 2)")]
        public static void Build()
        {
            // In Play mode AddComponent runs Awake at once, before the fields are wired.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the auth screens.");
                return;
            }

            BuildStatusOverlay();
            BuildLogin();
            BuildForgotEmail();
            BuildOtp();
            BuildNewPassword();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Auth prefabs built");
        }

        /// <summary>Builds the shared success card unless it already exists (the register screens need it too).</summary>
        public static void EnsureStatusOverlay()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StatusOverlayPrefab) == null)
            {
                BuildStatusOverlay();
            }
        }

        private static void BuildLogin()
        {
            var page = NewPage<LoginUI>(LoginName);
            SetPageId(page, PageID.Login);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Chào mừng trở lại", "Vui lòng nhập email và mật khẩu của bạn để đăng nhập");

            RectTransform card = AddInputCard(content, "Card", CardTop, 2);
            UiTextField email = AddInputRow(card, 0, "EmailField", "Email", LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.MailIconFile)), false);
            UiTextField password = AddInputRow(card, 1, "PasswordField", "Mật khẩu", null, true);

            TextMeshProUGUI error = AddErrorLabel(content, 390f);
            Toggle remember = AddCheckboxRow(content, "RememberToggle", "Ghi nhớ tôi", new Vector2(14f, -406f));

            // Hairline under the remember row; Figma draws it lighter than the card borders.
            RectTransform divider = NewRect(content, "Divider", TopLeft, new Vector2(16f, -467f), new Vector2(ButtonWidth, 1f));
            AddImage(divider, null, UiTheme.Hex("#474747"));

            Button forgot = AddLinkButton(content, "ForgotButton", "Quên mật khẩu ?", "SemiBold", 14f, UiTheme.Primary,
                TopCenter, new Vector2(0f, -489.5f), new Vector2(200f, 24f));
            Button login = AddPillButton(content, "LoginButton", "Đăng nhập", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);

            UiStatusOverlay overlay = AddOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "emailField", email);
            SetReference(page, "passwordField", password);
            SetReference(page, "rememberToggle", remember);
            SetReference(page, "forgotButton", forgot);
            SetReference(page, "loginButton", login);
            SetReference(page, "errorLabel", error);
            SetReference(page, "successOverlay", overlay);
            SavePrefab(page.gameObject, LoginName);
        }

        private static void BuildForgotEmail()
        {
            var page = NewPage<ForgotEmailUI>(ForgotEmailName);
            SetPageId(page, PageID.EmailChangeForm);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Thay đổi mật khẩu",
                "Vui lòng nhập email của bạn và chúng tôi sẽ gửi mã OTP ở bước tiếp theo để đặt lại mật khẩu cho bạn.");

            RectTransform card = AddInputCard(content, "Card", CardTop, 1);
            UiTextField email = AddInputRow(card, 0, "EmailField", "Email", LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.MailIconFile)), false);
            TextMeshProUGUI error = AddErrorLabel(content, CardTop + 53f + 8f);
            Button next = AddPillButton(content, "ContinueButton", "Tiếp tục", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);

            SetReference(page, "backButton", back);
            SetReference(page, "emailField", email);
            SetReference(page, "continueButton", next);
            SetReference(page, "errorLabel", error);
            SavePrefab(page.gameObject, ForgotEmailName);
        }

        private static void BuildOtp()
        {
            var page = NewPage<OtpUI>(OtpName);
            SetPageId(page, PageID.OTPPage);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            // The subtitle holds the masked email, so it is rewritten when the page opens; this is the layout sample.
            Button back = AddAuthHeader(content, "Nhập mã OTP", OtpUI.SubtitleFor("quyen.ley@yourdomain.com"));
            UiOtpInput otp = AddOtpRow(content, 327.5f);
            TextMeshProUGUI error = AddErrorLabel(content, 327.5f + 69f + 8f);
            Button confirm = AddPillButton(content, "ConfirmButton", "Xác nhận", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);

            SetReference(page, "backButton", back);
            SetReference(page, "subtitleLabel", content.Find("Subtitle").GetComponent<TMP_Text>());
            SetReference(page, "otpInput", otp);
            SetReference(page, "confirmButton", confirm);
            SetReference(page, "errorLabel", error);
            SavePrefab(page.gameObject, OtpName);
        }

        private static void BuildNewPassword()
        {
            var page = NewPage<NewPasswordUI>(NewPasswordName);
            SetPageId(page, PageID.PasswordChangeForm);
            AddBackground(page.transform, UiTheme.BackgroundDeep);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            Button back = AddAuthHeader(content, "Tạo mật khẩu mới",
                "Tạo mật khẩu mới. Nếu bạn quên mật khẩu, bạn phải thực hiện các thao tác sau.");

            RectTransform card = AddInputCard(content, "Card", CardTop, 2);
            UiTextField password = AddInputRow(card, 0, "PasswordField", "Mật khẩu mới", null, true);
            UiTextField confirm = AddInputRow(card, 1, "ConfirmField", "Nhập lại mật khẩu mới", null, true);
            TextMeshProUGUI error = AddErrorLabel(content, CardTop + 106f + 8f);
            Button submit = AddPillButton(content, "SubmitButton", "Thay đổi", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);

            UiStatusOverlay overlay = AddOverlay(page.transform);

            SetReference(page, "backButton", back);
            SetReference(page, "passwordField", password);
            SetReference(page, "confirmField", confirm);
            SetReference(page, "submitButton", submit);
            SetReference(page, "errorLabel", error);
            SetReference(page, "successOverlay", overlay);
            SavePrefab(page.gameObject, NewPasswordName);
        }

        /// <summary>
        /// The success card: dim scrim, a 343x431 card with the check mark, title, message and spinner
        /// (Figma positions, measured from the card's top edge). Starts hidden.
        /// </summary>
        private static void BuildStatusOverlay()
        {
            var root = new GameObject("StatusOverlay", typeof(RectTransform));
            Stretch((RectTransform)root.transform);
            var overlay = root.AddComponent<UiStatusOverlay>();

            RectTransform scrim = NewStretch(root.transform, "Scrim");
            Image scrimImage = AddImage(scrim, null, UiTheme.Scrim);
            scrimImage.raycastTarget = true; // swallows taps meant for the page underneath

            RectTransform card = NewRect(root.transform, "Card", Middle, new Vector2(0f, -2.5f), new Vector2(343f, 431f));
            AddImage(card, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR16File)), UiTheme.BackgroundDeep, Image.Type.Sliced);

            RectTransform check = NewRect(card, "Check", TopCenter, new Vector2(0f, -56f), new Vector2(100f, 100f));
            AddImage(check, LoadSprite(UiCanvasPaths.CommonArt(UiCanvasPaths.CheckCircleFile)), Color.white);

            TextMeshProUGUI title = AddText(card, "Title", string.Empty, "SemiBold", 20f, UiTheme.Primary,
                TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -212f), new Vector2(ButtonWidth, 30f));
            TextMeshProUGUI message = AddText(card, "Message", string.Empty, "Light", 16f, UiTheme.TextSubtitle,
                TextAlignmentOptions.Top, TopCenter, new Vector2(0f, -247f), new Vector2(300f, 56f), lineSpacing: -8f);

            RectTransform loader = NewRect(card, "Loader", TopCenter, new Vector2(0f, -337.5f), new Vector2(68f, 68f));
            AddImage(loader, LoadSprite(UiCanvasPaths.CircleSprite), UiTheme.Primary);
            RectTransform arc = NewRect(loader, "Arc", Middle, Vector2.zero, new Vector2(31f, 32f));
            AddImage(arc, LoadSprite(UiCanvasPaths.CommonArt("spinner_arc.png")), Color.white);
            arc.gameObject.AddComponent<UiSpinner>();

            SetReference(overlay, "titleLabel", title);
            SetReference(overlay, "messageLabel", message);

            root.SetActive(false);
            Directory.CreateDirectory(UiCanvasPaths.CommonPrefabs);
            PrefabUtility.SaveAsPrefabAsset(root, StatusOverlayPrefab);
            Object.DestroyImmediate(root);
        }

        /// <summary>Instances the shared overlay on top of a page (outside the safe area so the scrim covers the whole screen).</summary>
        private static UiStatusOverlay AddOverlay(Transform page)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatusOverlayPrefab);
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
