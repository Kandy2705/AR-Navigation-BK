#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine.UIElements;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// 30 Edit-Mode UI Toolkit tests – Table 7.1 (TC_UI_ED01 … TC_UI_ED30).
    /// Mỗi test khởi tạo UXML rồi xác minh sự tồn tại của các phần tử giao diện.
    /// </summary>
    [Category("TestAR")]
    public sealed class UIToolkitEditModeTests
    {
        private const string DocsRoot = UiTestHelpers.DocumentsRoot;

        // ── Row 1 ──────────────────────────────────────────────────────────
        [Test]
        public void TC_UI_ED01_Welcome_HasLoginAndSignupButtons()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Welcome.uxml");
            Assert.NotNull(root.Q<Button>("LoginButton"),  "LoginButton");
            Assert.NotNull(root.Q<Button>("SignUpButton"), "SignUpButton");
        }

        // ── Row 2 ──────────────────────────────────────────────────────────
        [Test]
        public void TC_UI_ED02_MainLayout_HasRootContainer()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Main.uxml");
            Assert.NotNull(root.Q<VisualElement>("RootContainer"), "RootContainer");
        }

        // ── Row 3 ──────────────────────────────────────────────────────────
        [Test]
        public void TC_UI_ED03_Login_HasCoreControls()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Login - New.uxml");
            Assert.NotNull(root.Q<TextField>("EmailInput"),          "EmailInput");
            Assert.NotNull(root.Q<TextField>("PasswordInput"),       "PasswordInput");
            Assert.NotNull(root.Q<Button>("LoginSubmitButton"),      "LoginSubmitButton");
            Assert.NotNull(root.Q<Button>("BtnBack"),                "BtnBack");
            Assert.NotNull(root.Q<Button>("ForgotPasswordButton"),   "ForgotPasswordButton");
            Assert.NotNull(root.Q<VisualElement>("ToggleEyeIcon"),   "ToggleEyeIcon");
            Assert.NotNull(root.Q<Toggle>("RememberToggle"),         "RememberToggle");
            Assert.NotNull(root.Q<VisualElement>("LoginLoading"),    "LoginLoading");
            Assert.NotNull(root.Q<Label>("ErrorLabel"),              "ErrorLabel");
        }

        // ── Row 4 ──────────────────────────────────────────────────────────
        [Test]
        public void TC_UI_ED04_Register_HasFormControls()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Register - New.uxml");
            Assert.NotNull(root.Q<TextField>("UsernameInput"),  "UsernameInput");
            Assert.NotNull(root.Q<TextField>("PhoneInput"),     "PhoneInput");
            Assert.NotNull(root.Q<TextField>("EmailInput"),     "EmailInput");
            Assert.NotNull(root.Q<TextField>("GenderInput"),    "GenderInput");
            Assert.NotNull(root.Q<TextField>("BirthdayInput"),  "BirthdayInput");
            Assert.NotNull(root.Q<Button>("ContinueButton"),    "ContinueButton");
            Assert.NotNull(root.Q<Button>("BtnBack"),           "BtnBack");
        }

        // ── Row 5 (ED05 + ED28) ────────────────────────────────────────────
        [Test]
        public void TC_UI_ED05_MainSetting_HasNavCluster()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Main Setting.uxml");
            Assert.NotNull(root.Q<Button>("BtnProfile"),        "BtnProfile");
            Assert.NotNull(root.Q<Button>("BtnSupportCenter"),  "BtnSupportCenter");
            Assert.NotNull(root.Q<Button>("BtnContact"),        "BtnContact");
            Assert.NotNull(root.Q<Button>("BtnHistory"),        "BtnHistory");
            Assert.NotNull(root.Q<Button>("btn-ar"),            "btn-ar");
            Assert.NotNull(root.Q<Button>("BtnLogout"),         "BtnLogout");
            Assert.NotNull(root.Q<VisualElement>("logout-overlay"), "logout-overlay");
            Assert.NotNull(root.Q<VisualElement>("bottom-sheet"),   "bottom-sheet");
            Assert.NotNull(root.Q<Button>("btn-cancel"),        "btn-cancel");
        }

        [Test]
        public void TC_UI_ED28_MainSetting_HasLogoutConfirmButton()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Main Setting.uxml");
            Assert.NotNull(root.Q<Button>("btn-confirm"), "btn-confirm (logout overlay)");
        }

        // ── Row 6 (ED06 + ED27) ────────────────────────────────────────────
        [Test]
        public void TC_UI_ED06_History_HasCardsNavAndModal()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI History.uxml");
            Assert.IsTrue(root.Query(className: "history-card").ToList().Count > 0, "history-card elements");
            Assert.NotNull(root.Q<Button>("BtnBack"),       "BtnBack");
            Assert.NotNull(root.Q<Button>("btn-ar"),        "btn-ar");
            Assert.NotNull(root.Q<Button>("BtnSettings"),   "BtnSettings");
            Assert.NotNull(root.Q<VisualElement>("DeleteModal"), "DeleteModal");
        }

        [Test]
        public void TC_UI_ED27_History_HasSearchOverlay()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI History.uxml");
            Assert.NotNull(root.Q<VisualElement>("SearchOverlay"), "SearchOverlay");
            Assert.NotNull(root.Q<VisualElement>("InputSearch"),   "InputSearch");
        }

        // ── Row 7 ──────────────────────────────────────────────────────────
        [Test]
        public void TC_UI_ED07_Chat_HasBackButton()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Chat.uxml");
            Assert.NotNull(root.Q<Button>("BtnBack"), "BtnBack");
        }

        // ── Row 8 ──────────────────────────────────────────────────────────
        [Test]
        public void TC_UI_ED08_UserInfo_HasProfileFieldsAndDeepLinks()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI User Info.uxml");
            Assert.NotNull(root.Q<TextField>("input-name"),          "input-name");
            Assert.NotNull(root.Q<TextField>("input-phone"),         "input-phone");
            Assert.NotNull(root.Q<TextField>("input-gender"),        "input-gender");
            Assert.NotNull(root.Q<TextField>("input-birthday"),      "input-birthday");
            Assert.NotNull(root.Q<Button>("BtnBack"),                "BtnBack");
            Assert.NotNull(root.Q<Button>("BtnEmailChange"),         "BtnEmailChange");
            Assert.NotNull(root.Q<Button>("BtnPasswordChange"),      "BtnPasswordChange");
        }

        // ── Row 9 (ED09, ED10, ED11, ED25) ────────────────────────────────
        [Test]
        public void TC_UI_ED09_EmailChanging_HasNavButtons()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Email Changing.uxml");
            Assert.NotNull(root.Q<Button>("Btn-Back"),     "Btn-Back");
            Assert.NotNull(root.Q<Button>("Btn-Continue"), "Btn-Continue");
        }

        [Test]
        public void TC_UI_ED10_EmailChangingForm_HasEmailInput()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Email Changing Form.uxml");
            Assert.NotNull(root.Q<Button>("Btn-Back"),       "Btn-Back");
            Assert.NotNull(root.Q<Button>("Btn-Continue"),   "Btn-Continue");
            Assert.NotNull(root.Q<TextField>("EmailInput"),  "EmailInput");
        }

        [Test]
        public void TC_UI_ED11_EmailChangingOTP_HasFourDigitFields()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Email Changing OTP.uxml");
            Assert.NotNull(root.Q<Button>("Btn-Back"),    "Btn-Back");
            Assert.NotNull(root.Q<Button>("btn-confirm"), "btn-confirm");
            Assert.NotNull(root.Q<TextField>("OTP1"),     "OTP1");
            Assert.NotNull(root.Q<TextField>("OTP2"),     "OTP2");
            Assert.NotNull(root.Q<TextField>("OTP3"),     "OTP3");
            Assert.NotNull(root.Q<TextField>("OTP4"),     "OTP4");
        }

        [Test]
        public void TC_UI_ED25_EmailChangingOTP_OtpFieldCountIsExactlyFour()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Email Changing OTP.uxml");
            var otpFields = root.Query<TextField>(className: "otp-input").ToList();
            Assert.AreEqual(4, otpFields.Count, "Số ô OTP phải là 4");
        }

        // ── Row 10 (ED12, ED13, ED14, ED26) ────────────────────────────────
        [Test]
        public void TC_UI_ED12_PasswordChanging_HasAllFields()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Password Changing.uxml");
            Assert.NotNull(root.Q<Button>("Btn-Back"),                   "Btn-Back");
            Assert.NotNull(root.Q<VisualElement>("OldPasswordInput"),    "OldPasswordInput");
            Assert.NotNull(root.Q<VisualElement>("NewPasswordInput"),    "NewPasswordInput");
            Assert.NotNull(root.Q<VisualElement>("ConfirmNewPasswordInput"), "ConfirmNewPasswordInput");
            Assert.NotNull(root.Q<Button>("OldToggleEyeIcon"),           "OldToggleEyeIcon");
            Assert.NotNull(root.Q<Button>("NewToggleEyeIcon"),           "NewToggleEyeIcon");
            Assert.NotNull(root.Q<Button>("ConfirmToggleEyeIcon"),       "ConfirmToggleEyeIcon");
            Assert.NotNull(root.Q<Label>("ErrorLabel"),                  "ErrorLabel");
            Assert.NotNull(root.Q<Button>("Btn-Confirm"),                "Btn-Confirm");
        }

        [Test]
        public void TC_UI_ED13_PasswordChangingForm_HasFieldsAndSubmit()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Password Changing Form.uxml");
            Assert.NotNull(root.Q<Button>("Btn-Back"),                "Btn-Back");
            Assert.NotNull(root.Q<TextField>("PasswordInput"),        "PasswordInput");
            Assert.NotNull(root.Q<TextField>("ConfirmPasswordInput"), "ConfirmPasswordInput");
            Assert.NotNull(root.Q<Button>("btn-submit"),              "btn-submit");
            Assert.NotNull(root.Q<VisualElement>("ToggleEyeIcon"),    "ToggleEyeIcon");
            Assert.NotNull(root.Q<VisualElement>("ConfirmToggleEyeIcon"), "ConfirmToggleEyeIcon");
        }

        [Test]
        public void TC_UI_ED14_PasswordForm_HasConfirmChain()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Password Form.uxml");
            Assert.NotNull(root.Q<Button>("Btn-Back"),                "Btn-Back");
            Assert.NotNull(root.Q<TextField>("PasswordInput"),        "PasswordInput");
            Assert.NotNull(root.Q<TextField>("ConfirmPasswordInput"), "ConfirmPasswordInput");
            Assert.NotNull(root.Q<VisualElement>("ToggleEyeIcon"),    "ToggleEyeIcon");
            Assert.NotNull(root.Q<VisualElement>("ConfirmToggleEyeIcon"), "ConfirmToggleEyeIcon");
            Assert.NotNull(root.Q<Button>("Btn-Confirm"),             "Btn-Confirm");
            Assert.NotNull(root.Q<Label>("ErrorLabel"),               "ErrorLabel");
        }

        [Test]
        public void TC_UI_ED26_PasswordChangingForm_PasswordInputPresent()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Password Changing Form.uxml");
            Assert.NotNull(root.Q<TextField>("PasswordInput"), "PasswordInput");
        }

        // ── Row 11 (ED15, ED16) ────────────────────────────────────────────
        [Test]
        public void TC_UI_ED15_SupportCenter_HasBackButton()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Support Center.uxml");
            Assert.NotNull(root.Q<Button>("BtnBack"), "BtnBack");
        }

        [Test]
        public void TC_UI_ED16_Contact_HasBackButton()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Contact.uxml");
            Assert.NotNull(root.Q<Button>("BtnBack"), "BtnBack");
        }

        // ── Row 12 (ED17–ED20): Legacy / Onboarding ────────────────────────
        [Test]
        public void TC_UI_ED17_Onboarding_InstantiatesWithoutError()
        {
            Assert.DoesNotThrow(() => UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Onboarding.uxml"));
        }

        [Test]
        public void TC_UI_ED18_LegacyLogin_InstantiatesWithoutError()
        {
            Assert.DoesNotThrow(() => UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Login.uxml"));
        }

        [Test]
        public void TC_UI_ED19_LegacySignup_InstantiatesWithoutError()
        {
            Assert.DoesNotThrow(() => UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Signup.uxml"));
        }

        [Test]
        public void TC_UI_ED20_ChangingOtp_InstantiatesWithoutError()
        {
            Assert.DoesNotThrow(() => UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Changing OTP.uxml"));
        }

        // ── Row 13 (ED21, ED22, ED24) ──────────────────────────────────────
        [Test]
        public void TC_UI_ED21_UIMain_AssetResolvable()
        {
            var tree = UiTestHelpers.LoadTree(DocsRoot + "/UI Main.uxml");
            Assert.NotNull(tree);
        }

        [Test]
        public void TC_UI_ED22_UIWelcome_AssetResolvable()
        {
            var tree = UiTestHelpers.LoadTree(DocsRoot + "/UI Welcome.uxml");
            Assert.NotNull(tree);
        }

        [Test]
        public void TC_UI_ED24_UIMainLayout_AssetResolvableAndInstantiates()
        {
            var tree = UiTestHelpers.LoadTree(DocsRoot + "/UI Main Layout.uxml");
            // Dùng CloneTree(container) – content được thêm trực tiếp vào container,
            // tránh vấn đề TemplateContainer wrapping trong Edit Mode test.
            var container = new VisualElement();
            tree.CloneTree(container);
            Assert.Greater(container.childCount, 0,
                "CloneTree phải thêm ít nhất 1 phần tử vào container.");
            Assert.NotNull(container.Q<VisualElement>("content-viewport"), "content-viewport");
            Assert.NotNull(container.Q<Button>("btn-ar"),      "btn-ar");
            Assert.NotNull(container.Q<Button>("BtnHistory"),  "BtnHistory");
            Assert.NotNull(container.Q<Button>("BtnSettings"), "BtnSettings");
        }

        // ── Row 14 (ED23): Quét toàn bộ UXML ──────────────────────────────
        [Test]
        public void TC_UI_ED23_AllProductionUxml_InstantiateWithoutError()
        {
            string[] files = {
                "UI Welcome.uxml", "UI Main.uxml", "UI Login - New.uxml",
                "UI Register - New.uxml", "UI Main Setting.uxml", "UI History.uxml",
                "UI Chat.uxml", "UI User Info.uxml", "UI Email Changing.uxml",
                "UI Email Changing Form.uxml", "UI Email Changing OTP.uxml",
                "UI Password Changing.uxml", "UI Password Changing Form.uxml",
                "UI Password Form.uxml", "UI Support Center.uxml", "UI Contact.uxml",
                "UI Onboarding.uxml", "UI Login.uxml", "UI Signup.uxml",
                "UI Changing OTP.uxml", "UI Main Layout.uxml"
            };

            foreach (var f in files)
                Assert.DoesNotThrow(
                    () => UiTestHelpers.InstantiateRoot(DocsRoot + "/" + f),
                    $"Exception khi khởi tạo {f}");
        }

        // ── Row 15 (ED29, ED30) ────────────────────────────────────────────
        [Test]
        public void TC_UI_ED29_Register_ContinueButtonPresent()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Register - New.uxml");
            Assert.NotNull(root.Q<Button>("ContinueButton"), "ContinueButton");
        }

        [Test]
        public void TC_UI_ED30_Login_LoginSubmitButtonPresent()
        {
            var root = UiTestHelpers.InstantiateRoot(DocsRoot + "/UI Login - New.uxml");
            Assert.NotNull(root.Q<Button>("LoginSubmitButton"), "LoginSubmitButton");
        }
    }
}
#endif
