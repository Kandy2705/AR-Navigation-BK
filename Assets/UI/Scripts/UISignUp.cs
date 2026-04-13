// using System.Collections;
// using UnityEditor;
// using UnityEngine;
// using UnityEngine.UIElements;

// public class UISignUp : MonoBehaviour
// {
//     // Pages
//     private VisualElement signupRoot;          // SignupRoot
//     private VisualElement otpRoot;             // ForgotPasswordRoot (OTP)
//     private VisualElement newPassRoot;         // PasswordNewRoot

//     // Loading overlay
//     private VisualElement signupLoading;       // SignupLoading
//     private Label loadingTitleLabel;           // LoadingTitleLabel
//     private Label loadingMessageLabel;         // LoadingMessageLabel
//     private VisualElement iconSucces;          // IconSucces (loader xoay)
//     private float iconAngle;

//     // Signup fields
//     private TextField userNameField;
//     private TextField phoneNumberField;
//     private TextField emailField;
//     private TextField genderField;
//     private TextField birthField;

//     // OTP fields
//     private IntegerField otp1;
//     private IntegerField otp2;
//     private IntegerField otp3;
//     private IntegerField otp4;

//     // New password fields
//     private TextField newPasswordField;
//     private TextField confirmPasswordField;

//     // Password toggles
//     private VisualElement newPasswordToggleIcon;
//     private VisualElement confirmPasswordToggleIcon;
//     private bool isNewPassVisible;
//     private bool isConfirmPassVisible;

//     [SerializeField] private Texture2D eyeTexture;
//     [SerializeField] private Texture2D eyeSlashTexture;

//     [SerializeField] private UIRouter router;

//     // Buttons
//     private Button signupBackButton;       // SignupRoot
//     private Button signupSubmitButton;     // SignupRoot

//     private Button otpBackButton;          // OtpRoot
//     private Button otpConfirmButton;       // OtpRoot

//     private Button newPassBackButton;      // NewPassRoot
//     private Button newPassChangeButton;    // NewPassRoot

//     private enum Page { Signup, Otp, NewPassword }

//     // ---------------------------
//     // IMPORTANT: Use OnEnable (not Start) because you toggle SetActive
//     // ---------------------------
//     private void OnEnable()
//     {
//         var root = GetComponent<UIDocument>()?.rootVisualElement;
//         if (root == null)
//         {
//             Debug.LogError("UIDocument/rootVisualElement not found.");
//             return;
//         }

//         // Pages
//         signupRoot = root.Q<VisualElement>("SignupRoot");
//         otpRoot = root.Q<VisualElement>("ForgotPasswordRoot");
//         newPassRoot = root.Q<VisualElement>("PasswordNewRoot");

//         // Loading
//         signupLoading = root.Q<VisualElement>("SignupLoading");
//         loadingTitleLabel = root.Q<Label>("LoadingTitleLabel");
//         loadingMessageLabel = root.Q<Label>("LoadingMessageLabel");
//         iconSucces = root.Q<VisualElement>("IconSucces");

//         // Signup fields
//         userNameField = root.Q<TextField>("UserNameField");
//         phoneNumberField = root.Q<TextField>("PhoneNumberField");
//         emailField = root.Q<TextField>("EmailField");
//         genderField = root.Q<TextField>("GenderField");
//         birthField = root.Q<TextField>("InputBirthField");

//         // OTP
//         otp1 = root.Q<IntegerField>("Otp1");
//         otp2 = root.Q<IntegerField>("Otp2");
//         otp3 = root.Q<IntegerField>("Otp3");
//         otp4 = root.Q<IntegerField>("Otp4");

//         // New password
//         newPasswordField = root.Q<TextField>("NewPasswordField");
//         confirmPasswordField = root.Q<TextField>("ConfirmPasswordField");
//         newPasswordToggleIcon = root.Q<VisualElement>("NewPasswordToggle");
//         confirmPasswordToggleIcon = root.Q<VisualElement>("ConfirmPasswordToggle");

//         // Buttons (query inside each page to avoid wrong instance)
//         signupBackButton = signupRoot?.Q<Button>("SignUpBackButton");
//         signupSubmitButton = signupRoot?.Q<Button>("NextSignUpButton");

//         otpBackButton = otpRoot?.Q<Button>("OtpBackButton");
//         otpConfirmButton = otpRoot?.Q<Button>("OtpConfirmButton");

//         newPassBackButton = newPassRoot?.Q<Button>("NewPassBackButton");
//         newPassChangeButton = newPassRoot?.Q<Button>("NewPassChangeButton");

//         // Init overlay state
//         if (signupLoading != null)
//         {
//             signupLoading.style.display = DisplayStyle.None;
//             signupLoading.pickingMode = PickingMode.Ignore;
//         }
//         if (iconSucces != null) iconSucces.style.display = DisplayStyle.None;

//         // Ensure buttons are enabled each time this doc re-opens
//         signupBackButton?.SetEnabled(true);
//         signupSubmitButton?.SetEnabled(true);
//         otpBackButton?.SetEnabled(true);
//         otpConfirmButton?.SetEnabled(true);
//         newPassBackButton?.SetEnabled(true);
//         newPassChangeButton?.SetEnabled(true);

//         // Password fields
//         if (newPasswordField != null) newPasswordField.isPasswordField = true;
//         if (confirmPasswordField != null) confirmPasswordField.isPasswordField = true;

//         // Visual fixes
//         MakeTextFieldTransparent(userNameField);
//         MakeTextFieldTransparent(phoneNumberField);
//         MakeTextFieldTransparent(emailField);
//         MakeTextFieldTransparent(genderField);
//         MakeTextFieldTransparent(birthField);
//         MakeTextFieldTransparent(newPasswordField);
//         MakeTextFieldTransparent(confirmPasswordField);

//         ConfigureOtpBox(otp1);
//         ConfigureOtpBox(otp2);
//         ConfigureOtpBox(otp3);
//         ConfigureOtpBox(otp4);

//         // ---------------------------
//         // Rebind events safely (remove then add) to avoid duplicates / dead refs
//         // ---------------------------
//         if (signupBackButton != null)
//         {
//             signupBackButton.clicked -= OnSignupBackClicked;
//             signupBackButton.clicked += OnSignupBackClicked;
//         }
//         if (signupSubmitButton != null)
//         {
//             signupSubmitButton.clicked -= OnSignupSubmitClicked;
//             signupSubmitButton.clicked += OnSignupSubmitClicked;
//         }

//         if (otpBackButton != null)
//         {
//             otpBackButton.clicked -= OnOtpBackClicked;
//             otpBackButton.clicked += OnOtpBackClicked;
//         }
//         if (otpConfirmButton != null)
//         {
//             otpConfirmButton.clicked -= OnOtpConfirmClicked;
//             otpConfirmButton.clicked += OnOtpConfirmClicked;
//         }

//         if (newPassBackButton != null)
//         {
//             newPassBackButton.clicked -= OnNewPassBackClicked;
//             newPassBackButton.clicked += OnNewPassBackClicked;
//         }
//         if (newPassChangeButton != null)
//         {
//             newPassChangeButton.clicked -= OnNewPassChangeClicked;
//             newPassChangeButton.clicked += OnNewPassChangeClicked;
//         }

//         if (newPasswordToggleIcon != null)
//         {
//             newPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnNewPasswordToggleClicked);
//             newPasswordToggleIcon.RegisterCallback<ClickEvent>(OnNewPasswordToggleClicked);
//             newPasswordToggleIcon.style.cursor = new StyleCursor((StyleKeyword)MouseCursor.Link);
//             UpdateToggleIcon(newPasswordToggleIcon, false);
//         }

//         if (confirmPasswordToggleIcon != null)
//         {
//             confirmPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnConfirmPasswordToggleClicked);
//             confirmPasswordToggleIcon.RegisterCallback<ClickEvent>(OnConfirmPasswordToggleClicked);
//             confirmPasswordToggleIcon.style.cursor = new StyleCursor((StyleKeyword)MouseCursor.Link);
//             UpdateToggleIcon(confirmPasswordToggleIcon, false);
//         }

//         // Reset local state every time Signup shows again
//         isNewPassVisible = false;
//         isConfirmPassVisible = false;

//         // Start page
//         ShowPage(Page.Signup);
//     }

//     private void OnDisable()
//     {
//         // Clean unbind to prevent stacking handlers across SetActive cycles
//         if (signupBackButton != null) signupBackButton.clicked -= OnSignupBackClicked;
//         if (signupSubmitButton != null) signupSubmitButton.clicked -= OnSignupSubmitClicked;

//         if (otpBackButton != null) otpBackButton.clicked -= OnOtpBackClicked;
//         if (otpConfirmButton != null) otpConfirmButton.clicked -= OnOtpConfirmClicked;

//         if (newPassBackButton != null) newPassBackButton.clicked -= OnNewPassBackClicked;
//         if (newPassChangeButton != null) newPassChangeButton.clicked -= OnNewPassChangeClicked;

//         if (newPasswordToggleIcon != null)
//             newPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnNewPasswordToggleClicked);

//         if (confirmPasswordToggleIcon != null)
//             confirmPasswordToggleIcon.UnregisterCallback<ClickEvent>(OnConfirmPasswordToggleClicked);
//     }

//     // ======================
//     // Flow handlers
//     // ======================

//     private void OnSignupBackClicked()
//     {
//         signupBackButton?.SetEnabled(true);
//         router.ShowWelcome();
//     }

//     private void OnSignupSubmitClicked()
//     {
//         // prevent "bấm lần 2 không ăn"
//         signupSubmitButton?.SetEnabled(true);

//         var username = userNameField?.value?.Trim();
//         var phone = phoneNumberField?.value?.Trim();
//         var email = emailField?.value?.Trim();

//         if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(email))
//         {
//             Debug.LogWarning("Signup fields missing.");
//             return;
//         }

//         ShowPage(Page.Otp);
//     }

//     private void OnOtpBackClicked()
//     {
//         ShowPage(Page.Signup);
//     }

//     private void OnOtpConfirmClicked()
//     {
//         ShowPage(Page.NewPassword);
//     }

//     private void OnNewPassBackClicked()
//     {
//         ShowPage(Page.Otp);
//     }

//     private void OnNewPassChangeClicked()
//     {
//         var p1 = newPasswordField?.value ?? "";
//         var p2 = confirmPasswordField?.value ?? "";

//         if (string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(p2))
//         {
//             Debug.LogWarning("Password empty.");
//             return;
//         }
//         if (p1 != p2)
//         {
//             Debug.LogWarning("Password mismatch.");
//             return;
//         }

//         ShowLoadingOverlay(
//             "Đăng ký thành công!",
//             "Vui lòng chờ....\nBạn sẽ được chuyển qua trang đăng nhập."
//         );

//         StartCoroutine(SignupDoneProcess());
//     }

//     private IEnumerator SignupDoneProcess()
//     {
//         yield return new WaitForSeconds(2.0f);
//         HideLoadingOverlay();

//         // Reset + go login
//         ClearOtp();
//         if (newPasswordField != null) newPasswordField.value = "";
//         if (confirmPasswordField != null) confirmPasswordField.value = "";
//         isNewPassVisible = false;
//         isConfirmPassVisible = false;
//         if (newPasswordField != null) newPasswordField.isPasswordField = true;
//         if (confirmPasswordField != null) confirmPasswordField.isPasswordField = true;

//         router.ShowLogin();
//     }

//     // ======================
//     // Page + Loading
//     // ======================

//     private void ShowPage(Page page)
//     {
//         if (signupRoot != null) signupRoot.style.display = (page == Page.Signup) ? DisplayStyle.Flex : DisplayStyle.None;
//         if (otpRoot != null) otpRoot.style.display = (page == Page.Otp) ? DisplayStyle.Flex : DisplayStyle.None;
//         if (newPassRoot != null) newPassRoot.style.display = (page == Page.NewPassword) ? DisplayStyle.Flex : DisplayStyle.None;
//     }

//     private void ShowLoadingOverlay(string title, string message)
//     {
//         if (signupLoading == null) return;

//         if (loadingTitleLabel != null) loadingTitleLabel.text = title;
//         if (loadingMessageLabel != null) loadingMessageLabel.text = message;

//         signupLoading.pickingMode = PickingMode.Position;
//         signupLoading.style.display = DisplayStyle.Flex;

//         if (iconSucces != null)
//         {
//             iconAngle = 0f;
//             iconSucces.style.display = DisplayStyle.Flex;
//         }
//     }

//     private void HideLoadingOverlay()
//     {
//         if (signupLoading == null) return;

//         signupLoading.style.display = DisplayStyle.None;
//         signupLoading.pickingMode = PickingMode.Ignore;

//         if (iconSucces != null) iconSucces.style.display = DisplayStyle.None;
//     }

//     private void Update()
//     {
//         if (iconSucces != null && iconSucces.resolvedStyle.display != DisplayStyle.None)
//         {
//             iconAngle = (iconAngle + 180f * Time.deltaTime) % 360f;
//             iconSucces.style.rotate = new Rotate(Angle.Degrees(iconAngle));
//         }
//     }

//     // ======================
//     // Password toggles
//     // ======================

//     private void OnNewPasswordToggleClicked(ClickEvent evt)
//     {
//         if (newPasswordField == null) return;
//         isNewPassVisible = !isNewPassVisible;
//         newPasswordField.isPasswordField = !isNewPassVisible;
//         newPasswordField.MarkDirtyRepaint();
//         UpdateToggleIcon(newPasswordToggleIcon, isNewPassVisible);
//     }

//     private void OnConfirmPasswordToggleClicked(ClickEvent evt)
//     {
//         if (confirmPasswordField == null) return;
//         isConfirmPassVisible = !isConfirmPassVisible;
//         confirmPasswordField.isPasswordField = !isConfirmPassVisible;
//         confirmPasswordField.MarkDirtyRepaint();
//         UpdateToggleIcon(confirmPasswordToggleIcon, isConfirmPassVisible);
//     }

//     private void UpdateToggleIcon(VisualElement icon, bool visible)
//     {
//         if (icon == null) return;
//         var tex = visible ? eyeTexture : eyeSlashTexture;
//         if (tex == null) return;
//         icon.style.backgroundImage = new StyleBackground(tex);
//     }

//     // ======================
//     // OTP styling helpers
//     // ======================

//     private void ConfigureOtpBox(IntegerField field)
//     {
//         if (field == null) return;

//         ClearVEKeepRadius(field);

//         field.style.width = 74;
//         field.style.height = 70;
//         field.style.backgroundColor = new StyleColor(new Color(20 / 255f, 20 / 255f, 20 / 255f));
//         field.style.borderTopLeftRadius = 12;
//         field.style.borderTopRightRadius = 12;
//         field.style.borderBottomLeftRadius = 12;
//         field.style.borderBottomRightRadius = 12;

//         var border = new Color(37 / 255f, 37 / 255f, 37 / 255f);
//         field.style.borderTopWidth = 1;
//         field.style.borderRightWidth = 1;
//         field.style.borderBottomWidth = 1;
//         field.style.borderLeftWidth = 1;
//         field.style.borderTopColor = border;
//         field.style.borderRightColor = border;
//         field.style.borderBottomColor = border;
//         field.style.borderLeftColor = border;

//         field.style.unityTextAlign = TextAnchor.MiddleCenter;
//         ClearIntegerFieldInternals(field);

//         var textEl = field.Q<TextElement>();
//         if (textEl != null)
//         {
//             textEl.style.color = new StyleColor(new Color(186 / 255f, 186 / 255f, 186 / 255f));
//             textEl.style.fontSize = 28;
//             textEl.style.unityTextAlign = TextAnchor.MiddleCenter;
//         }

//         field.isDelayed = false;
//     }

//     private void ClearIntegerFieldInternals(IntegerField field)
//     {
//         if (field == null) return;

//         string[] classCandidates =
//         {
//             "unity-base-field__input",
//             "unity-base-text-field__input",
//             "unity-base-text-field__input--single-line",
//             "unity-integer-field__input",
//             "unity-text-field__input",
//             "unity-text-input",
//         };

//         foreach (var cls in classCandidates)
//         {
//             var ve = field.Q<VisualElement>(className: cls);
//             ClearVEKeepRadius(ve);
//         }

//         field.Query<VisualElement>().ForEach(child =>
//         {
//             child.style.backgroundColor = Color.clear;
//             child.style.backgroundImage = new StyleBackground((Texture2D)null);
//         });
//     }

//     private void ClearOtp()
//     {
//         if (otp1 != null) otp1.value = 0;
//         if (otp2 != null) otp2.value = 0;
//         if (otp3 != null) otp3.value = 0;
//         if (otp4 != null) otp4.value = 0;
//     }

//     // ======================
//     // TextField transparent helper
//     // ======================

//     private void MakeTextFieldTransparent(TextField field)
//     {
//         if (field == null) return;

//         ClearVE(field);
//         ClearVE(field.Q<VisualElement>(className: "unity-base-field__input"));
//         ClearVE(field.Q<VisualElement>(className: "unity-base-text-field__input"));
//         ClearVE(field.Q<VisualElement>(className: "unity-base-text-field__input--single-line"));
//         ClearVE(field.Q<VisualElement>(className: "unity-text-field__input"));
//         ClearVE(field.Q<VisualElement>(className: "unity-text-input"));
//     }

//     private void ClearVE(VisualElement ve)
//     {
//         if (ve == null) return;

//         ve.style.backgroundColor = Color.clear;
//         ve.style.backgroundImage = new StyleBackground((Texture2D)null);

//         ve.style.borderTopWidth = 0;
//         ve.style.borderRightWidth = 0;
//         ve.style.borderBottomWidth = 0;
//         ve.style.borderLeftWidth = 0;

//         ve.style.borderTopLeftRadius = 0;
//         ve.style.borderTopRightRadius = 0;
//         ve.style.borderBottomLeftRadius = 0;
//         ve.style.borderBottomRightRadius = 0;
//     }

//     private void ClearVEKeepRadius(VisualElement ve)
//     {
//         if (ve == null) return;

//         ve.style.backgroundColor = Color.clear;
//         ve.style.backgroundImage = new StyleBackground((Texture2D)null);

//         ve.style.borderTopWidth = 0;
//         ve.style.borderRightWidth = 0;
//         ve.style.borderBottomWidth = 0;
//         ve.style.borderLeftWidth = 0;
//     }
// }
