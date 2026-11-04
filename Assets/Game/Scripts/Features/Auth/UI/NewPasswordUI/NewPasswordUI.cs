using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Auth
{
    /// <summary>Forgot password, last step ("Tạo mật khẩu mới"): choose a new password, then return to login.</summary>
    public class NewPasswordUI : UiPage
    {
        public const string EmptyMessage = "Vui lòng nhập mật khẩu mới";
        public const string MismatchMessage = "Mật khẩu nhập lại không khớp";
        public const string SuccessTitle = "Thay đổi thành công!";
        public const string SuccessMessage = "Vui lòng chờ....\nBạn sẽ được chuyển qua trang đăng nhập.";

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField passwordField;
        [SerializeField] private UiTextField confirmField;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private UiStatusOverlay successOverlay;
        [SerializeField, Min(0f)] private float successSeconds = 2f;

        private bool busy;

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            submitButton.onClick.AddListener(HandleSubmit);
        }

        protected override void OnShow()
        {
            busy = false;
            submitButton.interactable = true;
            passwordField.Text = string.Empty;
            confirmField.Text = string.Empty;
            errorLabel.ShowMessage(null);
            successOverlay.Hide();
        }

        private void HandleSubmit()
        {
            if (busy)
            {
                return;
            }

            string password = passwordField.Text;
            if (string.IsNullOrEmpty(password))
            {
                errorLabel.ShowMessage(EmptyMessage);
                return;
            }

            if (!AuthValidation.PasswordsMatch(password, confirmField.Text))
            {
                errorLabel.ShowMessage(MismatchMessage);
                return;
            }

            errorLabel.ShowMessage(null);
            SetBusy(true);
            AuthServices.ForgotPassword.ResetPassword(AuthFlow.Email, AuthFlow.Otp, password, HandleSuccess, HandleFailure);
        }

        private void HandleSuccess()
        {
            SetBusy(false);
            successOverlay.ShowThen(SuccessTitle, SuccessMessage, successSeconds, ReturnToLogin);
        }

        private void HandleFailure(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[NewPasswordUI] Could not change the password: " + reason);
            errorLabel.ShowMessage(reason);
        }

        private void ReturnToLogin()
        {
            AuthFlow.Clear();
            if (Navigation == null)
            {
                return;
            }

            // Drop the finished flow from the history so Back from Login goes to Welcome, not to the reset steps.
            if (!Navigation.ReturnTo(PageID.Login))
            {
                Navigation.Navigate(PageID.Login);
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            submitButton.interactable = !value;
        }
    }
}
