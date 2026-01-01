using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class UILogin : MonoBehaviour
{
    // Pages
    private VisualElement loginRoot;
    private VisualElement emailRoot;
    private VisualElement otpRoot;
    private VisualElement newPassRoot;

    [SerializeField] private UIRouter router;

    // Back
    private Button loginBackButton;

    // Overlay
    private VisualElement loginLoading;
    private Label loadingTitleLabel;
    private Label loadingMessageLabel;

    // Login fields
    private TextField loginEmailField;
    private TextField loginPasswordField;
    private Toggle rememberToggle;

    // OTP fields
    private IntegerField otpField1;
    private IntegerField otpField2;
    private IntegerField otpField3;
    private IntegerField otpField4;

    // Password toggles
    private VisualElement passwordToggleIcon;
    private bool isPasswordVisible = false;

    [SerializeField] private Texture2D eyeTexture;
    [SerializeField] private Texture2D eyeSlashTexture;

    private VisualElement newPasswordToggleIcon;
    private VisualElement confirmPasswordToggleIcon;
    private bool isNewPasswordVisible = false;
    private bool isConfirmPasswordVisible = false;

    // IconSuccess
    private VisualElement iconSuccess;
    private float iconSuccessAngle = 0f;

    // Buttons
    private Button loginSubmitButton;
    private Button forgotPasswordButton;

    private Button emailBackButton;
    private Button forgotContinueButton;

    private Button otpBackButton;
    private Button otpConfirmButton;

    private Button newPassBackButton;
    private Button newPassChangeButton;

    // Forgot flow fields
    private TextField forgotEmailField;
    private TextField newPasswordField;
    private TextField confirmPasswordField;

    private enum Page { Login, Email, Otp, NewPassword }

    // ---------------------------
    // IMPORTANT: Use OnEnable because you toggle SetActive
    // ---------------------------
    private void OnEnable()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("UIDocument/rootVisualElement not found.");
            return;
        }

        // Pages
        loginRoot = root.Q<VisualElement>("LoginRoot");
        emailRoot = root.Q<VisualElement>("EmailRoot");
        otpRoot = root.Q<VisualElement>("ForgotPasswordRoot");
        newPassRoot = root.Q<VisualElement>("PasswordNewRoot");

        // Back
        loginBackButton = root.Q<Button>("LoginBackButton");

        // Overlay
        loginLoading = root.Q<VisualElement>("LoginLoading");
        loadingTitleLabel = root.Q<Label>("LoadingTitleLabel");
        loadingMessageLabel = root.Q<Label>("LoadingMessageLabel");
        iconSuccess = root.Q<VisualElement>("IconSuccess") ?? root.Q<VisualElement>("IconSucces");

        // Login fields
        loginEmailField = root.Q<TextField>("LoginEmailField");
        loginPasswordField = root.Q<TextField>("LoginPasswordField");
        rememberToggle = root.Q<Toggle>("RememberToggle");

        // OTP fields
        otpField1 = root.Q<IntegerField>("Otp1");
        otpField2 = root.Q<IntegerField>("Otp2");
        otpField3 = root.Q<IntegerField>("Otp3");
        otpField4 = root.Q<IntegerField>("Otp4");

        // Toggles
        passwordToggleIcon = root.Q<VisualElement>("PasswordToggle");
        newPasswordToggleIcon = root.Q<VisualElement>("NewPasswordToggle");
        confirmPasswordToggleIcon = root.Q<VisualElement>("ConfirmPasswordToggle");

        // Buttons
        loginSubmitButton = root.Q<Button>("LoginSubmitButton");
        forgotPasswordButton = root.Q<Button>("ForgotPasswordButton");

        emailBackButton = root.Q<Button>("EmailBackButton");
        forgotContinueButton = root.Q<Button>("ForgotContinueButton");
        forgotEmailField = root.Q<TextField>("ForgotEmailField");

        otpBackButton = root.Q<Button>("OtpBackButton");
        otpConfirmButton = root.Q<Button>("OtpConfirmButton");

        newPassBackButton = root.Q<Button>("NewPassBackButton");
        newPassChangeButton = root.Q<Button>("NewPassChangeButton");
        newPasswordField = root.Q<TextField>("NewPasswordField");
        confirmPasswordField = root.Q<TextField>("ConfirmPasswordField");

        // Ensure buttons re-enabled when coming back from router.ShowWelcome()
        loginSubmitButton?.SetEnabled(true);
        forgotPasswordButton?.SetEnabled(true);
        emailBackButton?.SetEnabled(true);
        forgotContinueButton?.SetEnabled(true);
        otpBackButton?.SetEnabled(true);
        otpConfirmButton?.SetEnabled(true);
        newPassBackButton?.SetEnabled(true);
        newPassChangeButton?.SetEnabled(true);
        loginBackButton?.SetEnabled(true);

        // Overlay init
        if (loginLoading != null)
        {
            loginLoading.style.display = DisplayStyle.None;
            loginLoading.pickingMode = PickingMode.Ignore;
        }
        if (iconSuccess != null) iconSuccess.style.display = DisplayStyle.None;

        // Password fields init
        if (loginPasswordField != null) loginPasswordField.isPasswordField = true;
        if (newPasswordField != null) newPasswordField.isPasswordField = true;
        if (confirmPasswordField != null) confirmPasswordField.isPasswordField = true;

        // Visual fixes
        MakeTextFieldTransparent(loginEmailField);
        MakeTextFieldTransparent(loginPasswordField);
        MakeTextFieldTransparent(forgotEmailField);
        MakeTextFieldTransparent(newPasswordField);
        MakeTextFieldTransparent(confirmPasswordField);

        ConfigureOtpBox(otpField1);
        ConfigureOtpBox(otpField2);
        ConfigureOtpBox(otpField3);
        ConfigureOtpBox(otpField4);

        MakeToggleTransparent(rememberToggle);

        SetPlaceholder(loginEmailField, "Email");
        SetPlaceholder(loginPasswordField, "Mật Khẩu");
        SetPlaceholder(forgotEmailField, "Email");

        // Remember toggle: avoid stacking callbacks across SetActive cycles
        if (rememberToggle != null)
        {
            rememberToggle.UnregisterValueChangedCallback(OnRememberChanged);
            rememberToggle.RegisterValueChangedCallback(OnRememberChanged);
            UpdateToggleVisual(rememberToggle, rememberToggle.value);
        }

        // ---------------------------
        // Rebind events safely: remove then add
        // ---------------------------
        if (loginSubmitButton != null)
        {
            loginSubmitButton.clicked -= OnLoginClicked;
            loginSubmitButton.clicked += OnLoginClicked;
        }
        if (forgotPasswordButton != null)
        {
            forgotPasswordButton.clicked -= OnForgotPasswordClicked;
            forgotPasswordButton.clicked += OnForgotPasswordClicked;
        }
        if (emailBackButton != null)
        {
            emailBackButton.clicked -= OnEmailBackClicked;
            emailBackButton.clicked += OnEmailBackClicked;
        }
        if (forgotContinueButton != null)
        {
            forgotContinueButton.clicked -= OnForgotContinueClicked;
            forgotContinueButton.clicked += OnForgotContinueClicked;
        }
        if (otpBackButton != null)
        {
            otpBackButton.clicked -= OnOtpBackClicked;
            otpBackButton.clicked += OnOtpBackClicked;
        }
        if (otpConfirmButton != null)
        {
            otpConfirmButton.clicked -= OnOtpConfirmClicked;
            otpConfirmButton.clicked += OnOtpConfirmClicked;
        }
        if (newPassBackButton != null)
        {
            newPassBackButton.clicked -= OnNewPassBackClicked;
            newPassBackButton.clicked += OnNewPassBackClicked;
        }
        if (newPassChangeButton != null)
        {
            newPassChangeButton.clicked -= OnNewPassChangeClicked;
            newPassChangeButton.clicked += OnNewPassChangeClicked;
        }
        if (loginBackButton != null)
        {
            loginBackButton.clicked -= OnLoginBackClicked;
            loginBackButton.clicked += OnLoginBackClicked;
        }

        // ClickEvent register/unregister to avoid duplicates
        if (passwordToggleIcon != null)
        {
            passwordToggleIcon.UnregisterCallback<ClickEvent>(OnPasswordToggleClicked);
            passwordToggleIcon.RegisterCallback<ClickEvent>(OnPasswordToggleClicked);
            passwordToggleIcon.style.cursor = new StyleCursor((StyleKeyword)MouseCursor.Link);
            UpdateToggleIcon(passwordToggleIcon, isPasswordVisible);
        }

        if (newPasswordToggleIcon != null)
        {
            newPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnNewPasswordToggleClicked);
            newPasswordToggleIcon.RegisterCallback<ClickEvent>(OnNewPasswordToggleClicked);
            newPasswordToggleIcon.style.cursor = new StyleCursor((StyleKeyword)MouseCursor.Link);
            UpdateToggleIcon(newPasswordToggleIcon, isNewPasswordVisible);
        }

        if (confirmPasswordToggleIcon != null)
        {
            confirmPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnConfirmPasswordToggleClicked);
            confirmPasswordToggleIcon.RegisterCallback<ClickEvent>(OnConfirmPasswordToggleClicked);
            confirmPasswordToggleIcon.style.cursor = new StyleCursor((StyleKeyword)MouseCursor.Link);
            UpdateToggleIcon(confirmPasswordToggleIcon, isConfirmPasswordVisible);
        }

        // Default page when doc is opened
        ShowPage(Page.Login);
    }

    private void OnDisable()
    {
        // Important: clean unbind so when SetActive(true) again you won't stack handlers
        if (loginSubmitButton != null) loginSubmitButton.clicked -= OnLoginClicked;
        if (forgotPasswordButton != null) forgotPasswordButton.clicked -= OnForgotPasswordClicked;

        if (emailBackButton != null) emailBackButton.clicked -= OnEmailBackClicked;
        if (forgotContinueButton != null) forgotContinueButton.clicked -= OnForgotContinueClicked;

        if (otpBackButton != null) otpBackButton.clicked -= OnOtpBackClicked;
        if (otpConfirmButton != null) otpConfirmButton.clicked -= OnOtpConfirmClicked;

        if (newPassBackButton != null) newPassBackButton.clicked -= OnNewPassBackClicked;
        if (newPassChangeButton != null) newPassChangeButton.clicked -= OnNewPassChangeClicked;

        if (loginBackButton != null) loginBackButton.clicked -= OnLoginBackClicked;

        if (rememberToggle != null)
            rememberToggle.UnregisterValueChangedCallback(OnRememberChanged);

        if (passwordToggleIcon != null)
            passwordToggleIcon.UnregisterCallback<ClickEvent>(OnPasswordToggleClicked);

        if (newPasswordToggleIcon != null)
            newPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnNewPasswordToggleClicked);

        if (confirmPasswordToggleIcon != null)
            confirmPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnConfirmPasswordToggleClicked);
    }

    // Back -> Welcome
    private void OnLoginBackClicked()
    {
        router.ShowWelcome();
    }

    // ===== Page switching =====
    private void HideAllScreens()
    {
        if (loginRoot != null) loginRoot.style.display = DisplayStyle.None;
        if (emailRoot != null) emailRoot.style.display = DisplayStyle.None;
        if (otpRoot != null) otpRoot.style.display = DisplayStyle.None;
        if (newPassRoot != null) newPassRoot.style.display = DisplayStyle.None;
    }

    private void ShowPage(Page page)
    {
        HideAllScreens();

        switch (page)
        {
            case Page.Login:
                if (loginRoot != null) loginRoot.style.display = DisplayStyle.Flex;
                break;
            case Page.Email:
                if (emailRoot != null) emailRoot.style.display = DisplayStyle.Flex;
                break;
            case Page.Otp:
                if (otpRoot != null) otpRoot.style.display = DisplayStyle.Flex;
                break;
            case Page.NewPassword:
                if (newPassRoot != null) newPassRoot.style.display = DisplayStyle.Flex;
                break;
        }
    }

    // ===== Overlay helpers =====
    private void ShowLoadingOverlay(string title, string message)
    {
        if (loginLoading == null) return;

        if (loadingTitleLabel != null) loadingTitleLabel.text = title;
        if (loadingMessageLabel != null) loadingMessageLabel.text = message;

        loginLoading.pickingMode = PickingMode.Position;
        loginLoading.style.display = DisplayStyle.Flex;

        if (iconSuccess != null)
        {
            iconSuccess.style.display = DisplayStyle.Flex;
            iconSuccessAngle = 0f;
        }
    }

    private void HideLoadingOverlay()
    {
        if (loginLoading == null) return;

        loginLoading.style.display = DisplayStyle.None;
        loginLoading.pickingMode = PickingMode.Ignore;

        if (iconSuccess != null) iconSuccess.style.display = DisplayStyle.None;
    }

    // ===== Login flow =====
    private void OnLoginClicked()
    {
        loginSubmitButton?.SetEnabled(false);

        ShowLoadingOverlay(
            "Đăng nhập thành công!",
            "Vui lòng chờ...\nBạn sẽ được chuyển qua trang chủ."
        );

        StartCoroutine(LoginProcess());
    }

    private IEnumerator LoginProcess()
    {
        yield return new WaitForSeconds(2.0f);

        HideLoadingOverlay();
        loginSubmitButton?.SetEnabled(true);

        ShowPage(Page.Login);

        if (router != null)
            router.ShowHomePage();
        else
            Debug.LogError("UIRouter is null. Assign it in Inspector.");
    }


    // ===== Forgot password flow =====
    private void OnForgotPasswordClicked() => ShowPage(Page.Email);
    private void OnEmailBackClicked() => ShowPage(Page.Login);

    private void OnForgotContinueClicked()
    {
        var email = forgotEmailField?.value?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            Debug.LogWarning("Email is empty.");
            return;
        }
        ShowPage(Page.Otp);
    }

    private void OnOtpBackClicked() => ShowPage(Page.Email);
    private void OnOtpConfirmClicked() => ShowPage(Page.NewPassword);
    private void OnNewPassBackClicked() => ShowPage(Page.Otp);

    private void OnNewPassChangeClicked()
    {
        var p1 = newPasswordField?.value ?? "";
        var p2 = confirmPasswordField?.value ?? "";

        if (string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(p2))
        {
            Debug.LogWarning("Password empty.");
            return;
        }
        if (p1 != p2)
        {
            Debug.LogWarning("Password mismatch.");
            return;
        }

        ShowLoadingOverlay(
            "Đổi mật khẩu thành công!",
            "Vui lòng chờ...\nBạn sẽ được chuyển về trang đăng nhập."
        );

        StartCoroutine(ForgotPasswordDoneProcess());
    }

    private IEnumerator ForgotPasswordDoneProcess()
    {
        yield return new WaitForSeconds(2.0f);
        HideLoadingOverlay();

        if (forgotEmailField != null) forgotEmailField.value = "";
        if (newPasswordField != null) newPasswordField.value = "";
        if (confirmPasswordField != null) confirmPasswordField.value = "";

        ShowPage(Page.Login);
    }

    // ===== Password toggle handlers =====
    private void OnPasswordToggleClicked(ClickEvent evt)
    {
        isPasswordVisible = !isPasswordVisible;
        if (loginPasswordField != null)
        {
            loginPasswordField.isPasswordField = !isPasswordVisible;
            loginPasswordField.MarkDirtyRepaint();
        }
        UpdateToggleIcon(passwordToggleIcon, isPasswordVisible);
    }

    private void OnNewPasswordToggleClicked(ClickEvent evt)
    {
        isNewPasswordVisible = !isNewPasswordVisible;
        if (newPasswordField != null)
        {
            newPasswordField.isPasswordField = !isNewPasswordVisible;
            newPasswordField.MarkDirtyRepaint();
        }
        UpdateToggleIcon(newPasswordToggleIcon, isNewPasswordVisible);
    }

    private void OnConfirmPasswordToggleClicked(ClickEvent evt)
    {
        isConfirmPasswordVisible = !isConfirmPasswordVisible;
        if (confirmPasswordField != null)
        {
            confirmPasswordField.isPasswordField = !isConfirmPasswordVisible;
            confirmPasswordField.MarkDirtyRepaint();
        }
        UpdateToggleIcon(confirmPasswordToggleIcon, isConfirmPasswordVisible);
    }

    private void UpdateToggleIcon(VisualElement toggleIcon, bool visible)
    {
        if (toggleIcon == null) return;

        var tex = visible ? eyeTexture : eyeSlashTexture;
        if (tex == null)
        {
            toggleIcon.EnableInClassList("eye-visible", visible);
            toggleIcon.EnableInClassList("eye-hidden", !visible);
            return;
        }

        toggleIcon.style.backgroundImage = new StyleBackground(tex);
    }

    private void Update()
    {
        if (iconSuccess != null && iconSuccess.resolvedStyle.display != DisplayStyle.None)
        {
            float speed = 180f;
            iconSuccessAngle += speed * Time.deltaTime;
            if (iconSuccessAngle >= 360f) iconSuccessAngle -= 360f;
            iconSuccess.style.rotate = new Rotate(Angle.Degrees(iconSuccessAngle));
        }
    }

    // ===== Styling helpers (unchanged from your version) =====

    private void ConfigureOtpBox(IntegerField field)
    {
        if (field == null) return;

        ClearVE(field);

        field.style.width = 74;
        field.style.height = 70;
        field.style.backgroundColor = new StyleColor(new Color(20 / 255f, 20 / 255f, 20 / 255f));
        field.style.borderTopLeftRadius = 12;
        field.style.borderTopRightRadius = 12;
        field.style.borderBottomLeftRadius = 12;
        field.style.borderBottomRightRadius = 12;

        var border = new Color(37 / 255f, 37 / 255f, 37 / 255f);
        field.style.borderTopWidth = 1;
        field.style.borderRightWidth = 1;
        field.style.borderBottomWidth = 1;
        field.style.borderLeftWidth = 1;
        field.style.borderTopColor = border;
        field.style.borderRightColor = border;
        field.style.borderBottomColor = border;
        field.style.borderLeftColor = border;

        var baseInput =
            field.Q<VisualElement>(className: "unity-base-field__input")
            ?? field.Q<VisualElement>(className: "unity-base-text-field__input");

        var intInput = field.Q<VisualElement>(className: "unity-integer-field__input");
        var textInput = field.Q<VisualElement>(className: "unity-text-input");

        ClearVE(baseInput);
        ClearVE(intInput);
        ClearVE(textInput);

        if (baseInput != null)
        {
            baseInput.style.paddingLeft = 0;
            baseInput.style.paddingRight = 0;
            baseInput.style.paddingTop = 0;
            baseInput.style.paddingBottom = 0;
            baseInput.style.justifyContent = Justify.Center;
            baseInput.style.alignItems = Align.Center;
        }

        if (textInput != null)
        {
            textInput.style.backgroundColor = Color.clear;
            textInput.style.backgroundImage = new StyleBackground((Texture2D)null);

            textInput.style.borderTopWidth = 0;
            textInput.style.borderRightWidth = 0;
            textInput.style.borderBottomWidth = 0;
            textInput.style.borderLeftWidth = 0;

            textInput.style.paddingLeft = 0;
            textInput.style.paddingRight = 0;
            textInput.style.paddingTop = 0;
            textInput.style.paddingBottom = 0;

            textInput.style.justifyContent = Justify.Center;
            textInput.style.alignItems = Align.Center;
        }

        var textEl = field.Q<TextElement>();
        if (textEl != null)
        {
            textEl.style.color = new StyleColor(new Color(186 / 255f, 186 / 255f, 186 / 255f));
            textEl.style.fontSize = 28;
            textEl.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        field.value = 0;
        field.isDelayed = false;
    }

    private void OnRememberChanged(ChangeEvent<bool> evt)
    {
        UpdateToggleVisual(rememberToggle, evt.newValue);
    }

    private void SetPlaceholder(TextField field, string placeholderText)
    {
        if (field == null) return;

        var p1 = field.Q<Label>(className: "unity-text-field__placeholder");
        var p2 = field.Q<Label>(className: "unity-text-input__placeholder");

        if (p1 != null) p1.text = placeholderText;
        if (p2 != null) p2.text = placeholderText;

        field.RegisterValueChangedCallback(OnTextFieldValueChanged);
        UpdatePlaceholderVisibility(field);
    }

    private void OnTextFieldValueChanged(ChangeEvent<string> evt)
    {
        var field = evt.target as TextField;
        UpdatePlaceholderVisibility(field);
    }

    private void UpdatePlaceholderVisibility(TextField field)
    {
        if (field == null) return;

        bool show = string.IsNullOrEmpty(field.value);
        var p1 = field.Q<Label>(className: "unity-text-field__placeholder");
        var p2 = field.Q<Label>(className: "unity-text-input__placeholder");

        if (p1 != null) p1.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (p2 != null) p2.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void MakeTextFieldTransparent(TextField field)
    {
        if (field == null) return;

        ClearVE(field);
        ClearVE(field.Q<VisualElement>(className: "unity-base-field__input"));
        ClearVE(field.Q<VisualElement>(className: "unity-base-text-field__input"));
        ClearVE(field.Q<VisualElement>(className: "unity-text-field__input"));
        ClearVE(field.Q<VisualElement>(className: "unity-text-input"));
    }

    private void MakeToggleTransparent(Toggle toggle)
    {
        if (toggle == null) return;

        ClearVE(toggle);

        var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
        var checkmark = toggle.Q<VisualElement>(className: "unity-toggle__checkmark");
        var baseInput = toggle.Q<VisualElement>(className: "unity-base-field__input");

        ClearVE(input);
        ClearVE(checkmark);
        ClearVE(baseInput);

        toggle.Query<VisualElement>().ForEach(child => ClearVE(child));

        if (input != null)
        {
            input.style.width = 20;
            input.style.height = 20;

            input.style.borderTopWidth = 1;
            input.style.borderRightWidth = 1;
            input.style.borderBottomWidth = 1;
            input.style.borderLeftWidth = 1;

            var accent = new Color(59 / 255f, 49 / 255f, 137 / 255f);
            input.style.borderTopColor = accent;
            input.style.borderRightColor = accent;
            input.style.borderBottomColor = accent;
            input.style.borderLeftColor = accent;

            input.style.borderTopLeftRadius = 6;
            input.style.borderTopRightRadius = 6;
            input.style.borderBottomLeftRadius = 6;
            input.style.borderBottomRightRadius = 6;

            input.style.backgroundColor = Color.clear;
            input.style.backgroundImage = new StyleBackground((Texture2D)null);
        }

        if (checkmark != null)
        {
            checkmark.style.backgroundColor = Color.clear;
            checkmark.style.backgroundImage = new StyleBackground((Texture2D)null);
        }
    }

    private void UpdateToggleVisual(Toggle toggle, bool isOn)
    {
        if (toggle == null) return;

        var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
        var checkmark = toggle.Q<VisualElement>(className: "unity-toggle__checkmark");
        var accent = new Color(59 / 255f, 49 / 255f, 137 / 255f);

        var tick = EnsureTickElement(toggle);

        if (isOn)
        {
            if (checkmark != null)
            {
                checkmark.style.backgroundColor = accent;
                checkmark.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
            else if (input != null)
            {
                input.style.backgroundColor = accent;
            }

            if (tick != null)
            {
                tick.text = "\u2713";
                tick.style.color = Color.white;
                tick.style.display = DisplayStyle.Flex;
            }
        }
        else
        {
            if (checkmark != null)
            {
                checkmark.style.backgroundColor = Color.clear;
                checkmark.style.backgroundImage = new StyleBackground((Texture2D)null);
            }
            else if (input != null)
            {
                input.style.backgroundColor = Color.clear;
            }

            if (tick != null) tick.style.display = DisplayStyle.None;
        }
    }

    private Label EnsureTickElement(Toggle toggle)
    {
        if (toggle == null) return null;

        var checkmark = toggle.Q<VisualElement>(className: "unity-toggle__checkmark");
        var input = toggle.Q<VisualElement>(className: "unity-toggle__input");
        var parent = (VisualElement)checkmark ?? input;
        if (parent == null) return null;

        parent.style.position = Position.Relative;

        var tick = parent.Q<Label>("rememberTick");
        if (tick != null) return tick;

        tick = new Label("\u2713") { name = "rememberTick" };
        tick.style.position = Position.Absolute;
        tick.style.left = 0;
        tick.style.top = 0;
        tick.style.right = 2;
        tick.style.bottom = 3;

        tick.style.unityTextAlign = TextAnchor.MiddleCenter;
        tick.style.display = DisplayStyle.None;
        tick.style.color = Color.white;
        tick.style.fontSize = 14;
        tick.style.alignSelf = Align.Center;

        parent.Add(tick);
        return tick;
    }

    private void ClearVE(VisualElement ve)
    {
        if (ve == null) return;

        ve.style.backgroundColor = Color.clear;
        ve.style.backgroundImage = new StyleBackground((Texture2D)null);

        ve.style.borderTopWidth = 0;
        ve.style.borderRightWidth = 0;
        ve.style.borderBottomWidth = 0;
        ve.style.borderLeftWidth = 0;

        ve.style.borderTopLeftRadius = 0;
        ve.style.borderTopRightRadius = 0;
        ve.style.borderBottomLeftRadius = 0;
        ve.style.borderBottomRightRadius = 0;
    }
}
