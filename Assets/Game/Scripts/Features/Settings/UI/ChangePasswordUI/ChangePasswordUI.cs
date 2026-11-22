using Game.Features.Auth;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Settings
{
    /// <summary>
    /// "Thay đổi mật khẩu": old, new and repeated password. Checks them, asks the backend to mail a code, hands over
    /// to the shared OTP screen and, once the code comes back, sends the change.
    /// </summary>
    public class ChangePasswordUI : UiPage
    {
        public const string EmptyMessage = "Vui lòng điền đầy đủ thông tin mật khẩu!";
        public const string WrongOldMessage = "Mật khẩu cũ không chính xác, vui lòng nhập lại!";
        public const string MismatchMessage = "Mật khẩu mới và xác nhận mật khẩu không khớp!";
        public const string SameMessage = "Mật khẩu mới không được trùng với mật khẩu cũ!";
        public const string WeakMessage = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ và số";
        public const string NoAccountMessage = "Không tìm thấy tài khoản, vui lòng đăng nhập lại";
        public const string FailedMessage = "Đổi mật khẩu thất bại, vui lòng thử lại";
        public const string SuccessTitle = "Thay đổi thành công!";
        public const string SuccessMessage = "Mật khẩu của bạn đã được cập nhật.";

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField oldField;
        [SerializeField] private UiTextField newField;
        [SerializeField] private UiTextField confirmField;
        [SerializeField] private Button forgotButton;
        [SerializeField] private Button changeButton;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private UiStatusOverlay successOverlay;

        private bool busy;
        private bool returningFromOtp;
        private string otp = string.Empty;

        public static string WaitMessage(int seconds)
        {
            return $"Vui lòng thử lại sau {seconds} giây";
        }

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            forgotButton.onClick.AddListener(OpenForgotPassword);
            changeButton.onClick.AddListener(HandleChange);
        }

        protected override void OnShow()
        {
            successOverlay.Hide();
            errorLabel.ShowMessage(null);
            if (returningFromOtp)
            {
                returningFromOtp = false;
                Submit();
                return;
            }

            oldField.Text = string.Empty;
            newField.Text = string.Empty;
            confirmField.Text = string.Empty;
            SetBusy(false);
        }

        private void HandleChange()
        {
            if (busy)
            {
                return;
            }

            string message = Validate();
            if (message != null)
            {
                errorLabel.ShowMessage(message);
                return;
            }

            string email = UserProfile.Load().Email;
            if (string.IsNullOrEmpty(email))
            {
                errorLabel.ShowMessage(NoAccountMessage);
                return;
            }

            int wait = AccountServices.Api.OtpCooldownSeconds;
            if (wait > 0)
            {
                errorLabel.ShowMessage(WaitMessage(wait));
                return;
            }

            errorLabel.ShowMessage(null);
            SetBusy(true);
            AccountServices.Api.RequestPasswordOtp(email, () => OtpSent(email), Fail);
        }

        /// <summary>The first problem with the three fields, or null when they can be sent.</summary>
        private string Validate()
        {
            string oldPassword = oldField.Text;
            string newPassword = newField.Text;
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmField.Text))
            {
                return EmptyMessage;
            }

            if (!AuthValidation.PasswordsMatch(newPassword, confirmField.Text))
            {
                return MismatchMessage;
            }

            if (newPassword == oldPassword)
            {
                return SameMessage;
            }

            return AuthValidation.IsStrongPassword(newPassword) ? null : WeakMessage;
        }

        private void OtpSent(string email)
        {
            SetBusy(false);
            AuthFlow.BeginOtp(email, code =>
            {
                otp = code;
                returningFromOtp = true;
                GoBack();
            });
            OpenPage(PageID.OTPPage);
        }

        private void Submit()
        {
            SetBusy(true);
            string email = UserProfile.Load().Email;
            string newPassword = newField.Text;
            AccountServices.Api.ChangePassword(email, oldField.Text, newPassword, otp, () =>
            {
                SetBusy(false);
                AuthFlow.Clear();
                successOverlay.ShowThen(SuccessTitle, SuccessMessage, 1.6f, Finish);
            }, Fail);
        }

        private void Finish()
        {
            successOverlay.Hide();
            if (Navigation != null && !Navigation.ReturnTo(PageID.Profile))
            {
                GoBack();
            }
        }

        private void OpenForgotPassword()
        {
            AuthFlow.Clear();
            OpenPage(PageID.EmailChangeForm);
        }

        private void Fail(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[ChangePasswordUI] Could not change the password: " + reason);
            // The backend checks the old password (the app no longer keeps it) and says so in its message.
            bool wrongOld = reason != null && reason.IndexOf("Mật khẩu cũ", System.StringComparison.OrdinalIgnoreCase) >= 0;
            errorLabel.ShowMessage(wrongOld ? WrongOldMessage : FailedMessage);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            changeButton.interactable = !value;
        }
    }
}
