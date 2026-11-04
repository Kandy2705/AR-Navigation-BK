using Game.Features.Auth;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Settings
{
    /// <summary>
    /// "Thay đổi Email": ask for the new address, send a code to it, then (back from the shared OTP screen) confirm
    /// the change. The OTP screen only hands the code back; this page finishes the request and shows the result.
    /// </summary>
    public class ChangeEmailUI : UiPage
    {
        public const string InvalidEmailMessage = "Email không hợp lệ";
        public const string SameEmailMessage = "Email mới trùng với email hiện tại";
        public const string FailedMessage = "Đổi email thất bại, vui lòng thử lại";
        public const string SuccessTitle = "Đổi email thành công!";
        public const string SuccessMessage = "Email của bạn đã được cập nhật.";

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField emailField;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private UiStatusOverlay successOverlay;

        private bool busy;
        private bool returningFromOtp;
        private string newEmail = string.Empty;
        private string otp = string.Empty;

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            continueButton.onClick.AddListener(HandleContinue);
        }

        protected override void OnShow()
        {
            successOverlay.Hide();
            errorLabel.ShowMessage(null);
            if (returningFromOtp)
            {
                returningFromOtp = false;
                Confirm();
                return;
            }

            emailField.Text = string.Empty;
            SetBusy(false);
        }

        private void HandleContinue()
        {
            if (busy)
            {
                return;
            }

            string email = emailField.Text.Trim();
            if (!AuthValidation.IsValidEmail(email))
            {
                errorLabel.ShowMessage(InvalidEmailMessage);
                return;
            }

            if (string.Equals(email, UserProfile.Load().Email, System.StringComparison.OrdinalIgnoreCase))
            {
                errorLabel.ShowMessage(SameEmailMessage);
                return;
            }

            errorLabel.ShowMessage(null);
            SetBusy(true);
            AccountServices.Api.RequestEmailOtp(email, () => OtpSent(email), Fail);
        }

        private void OtpSent(string email)
        {
            SetBusy(false);
            newEmail = email;
            AuthFlow.BeginOtp(email, code =>
            {
                otp = code;
                returningFromOtp = true;
                GoBack();
            });
            OpenPage(PageID.OTPPage);
        }

        private void Confirm()
        {
            SetBusy(true);
            AccountServices.Api.ChangeEmail(newEmail, otp, () =>
            {
                SetBusy(false);
                AccountSession.UpdateCachedEmail(newEmail);
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

        private void Fail(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[ChangeEmailUI] Could not change the email: " + reason);
            errorLabel.ShowMessage(FailedMessage);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            continueButton.interactable = !value;
        }
    }
}
