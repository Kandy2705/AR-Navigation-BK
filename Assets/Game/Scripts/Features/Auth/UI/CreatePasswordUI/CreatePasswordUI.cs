using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Auth
{
    /// <summary>
    /// Register, last step ("Đặt mật khẩu nào!"): choose the password, create the account, then land on Login.
    /// The rule printed under the title (8+ characters, letters and digits) is enforced here.
    /// </summary>
    public class CreatePasswordUI : UiPage
    {
        public const string EmptyMessage = "Vui lòng nhập mật khẩu";
        public const string WeakMessage = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ và số";
        public const string MismatchMessage = "Mật khẩu nhập lại không khớp";
        public const string MissingProfileMessage = "Thiếu thông tin đăng ký, vui lòng quay lại";
        public const string FailedMessage = "Đăng ký không thành công, vui lòng thử lại";
        public const string SuccessTitle = "Đăng ký thành công!";
        public const string SuccessMessage = "Vui lòng chờ....\nBạn sẽ được chuyển qua trang đăng nhập.";

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField passwordField;
        [SerializeField] private UiTextField confirmField;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private UiStatusOverlay successOverlay;
        [SerializeField, Min(0f)] private float successSeconds = 2f;

        private bool busy;

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            continueButton.onClick.AddListener(HandleContinue);
        }

        protected override void OnShow()
        {
            busy = false;
            continueButton.interactable = true;
            passwordField.Text = string.Empty;
            confirmField.Text = string.Empty;
            errorLabel.ShowMessage(null);
            successOverlay.Hide();
        }

        private void HandleContinue()
        {
            if (busy)
            {
                return;
            }

            string password = passwordField.Text;
            string error = Validate(password, confirmField.Text);
            if (error != null)
            {
                errorLabel.ShowMessage(error);
                return;
            }

            errorLabel.ShowMessage(null);
            SetBusy(true);
            AuthServices.Register.Register(AuthFlow.Registration, password, HandleSuccess, HandleFailure);
        }

        private static string Validate(string password, string confirmation)
        {
            if (AuthFlow.Registration == null) return MissingProfileMessage;
            if (string.IsNullOrEmpty(password)) return EmptyMessage;
            if (!AuthValidation.IsStrongPassword(password)) return WeakMessage;
            if (!AuthValidation.PasswordsMatch(password, confirmation)) return MismatchMessage;
            return null;
        }

        private void HandleSuccess()
        {
            SetBusy(false);
            successOverlay.ShowThen(SuccessTitle, SuccessMessage, successSeconds, GoToLogin);
        }

        private void HandleFailure(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[CreatePasswordUI] Registration failed: " + reason);
            errorLabel.ShowMessage(FailedMessage);
        }

        private void GoToLogin()
        {
            AuthFlow.Clear();
            if (Navigation != null)
            {
                // Login is reached from Welcome, so Back from it returns there and not into the finished sign-up steps.
                Navigation.NavigateFrom(PageID.WelcomePage, PageID.Login);
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            continueButton.interactable = !value;
        }
    }
}
