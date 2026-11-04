using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Auth
{
    /// <summary>Forgot password, step 1 ("Thay đổi mật khẩu"): ask for the email the code is sent to.</summary>
    public class ForgotEmailUI : UiPage
    {
        public const string InvalidEmailMessage = "Email không hợp lệ";

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField emailField;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text errorLabel;

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
            errorLabel.ShowMessage(null);
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

            errorLabel.ShowMessage(null);
            SetBusy(true);
            AuthServices.ForgotPassword.RequestOtp(email, () => OtpSent(email), HandleFailure);
        }

        private void OtpSent(string email)
        {
            SetBusy(false);
            AuthFlow.BeginOtp(email, _ => OpenNewPassword());
            Open(PageID.OTPPage);
        }

        private void OpenNewPassword()
        {
            Open(PageID.PasswordChangeForm);
        }

        private void HandleFailure(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[ForgotEmailUI] Could not send the code: " + reason);
            errorLabel.ShowMessage(reason);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            continueButton.interactable = !value;
        }

        private void Open(PageID target)
        {
            if (Navigation != null && Navigation.HasRoute(target))
            {
                Navigation.Navigate(target);
            }
            else
            {
                Debug.Log($"[ForgotEmailUI] {target} screen is not available.");
            }
        }
    }
}
