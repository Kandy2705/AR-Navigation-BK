using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Auth
{
    /// <summary>
    /// One-time code entry ("Nhập mã OTP"). Shared by every flow that sends a code: <see cref="AuthFlow"/> says
    /// which email it went to and what to do with the code.
    /// </summary>
    public class OtpUI : UiPage
    {
        public const string IncompleteMessage = "Vui lòng nhập đủ 4 số";
        public const string WrongCodeMessage = "Mã OTP không đúng hoặc đã hết hạn";

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private UiOtpInput otpInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text errorLabel;

        private bool busy;

        public static string SubtitleFor(string email)
        {
            return $"Chúng tôi đã gửi mã OTP đến email của bạn là {EmailMasker.Mask(email)}. Nhập mã OTP bên dưới để xác minh.";
        }

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            confirmButton.onClick.AddListener(HandleConfirm);
            otpInput.Changed += _ => errorLabel.ShowMessage(null);
        }

        protected override void OnShow()
        {
            SetBusy(false);
            subtitleLabel.text = SubtitleFor(AuthFlow.Email);
            errorLabel.ShowMessage(null);
            otpInput.Clear();
            otpInput.FocusFirst();
        }

        private void HandleConfirm()
        {
            if (busy)
            {
                return;
            }

            if (!otpInput.IsComplete)
            {
                errorLabel.ShowMessage(IncompleteMessage);
                return;
            }

            string code = otpInput.Code;
            IOtpVerifier verifier = AuthFlow.Verifier;
            if (verifier == null)
            {
                AuthFlow.ConfirmOtp(code);
                return;
            }

            // Ask the backend first so a mistyped code is reported here, not after the next form.
            SetBusy(true);
            verifier.VerifyOtp(AuthFlow.Email, code, valid =>
            {
                SetBusy(false);
                if (valid)
                {
                    AuthFlow.ConfirmOtp(code);
                    return;
                }

                otpInput.Clear();
                otpInput.FocusFirst();
                errorLabel.ShowMessage(WrongCodeMessage);
            }, reason =>
            {
                SetBusy(false);
                Debug.LogWarning("[OtpUI] Could not check the code: " + reason);
                errorLabel.ShowMessage(reason);
            });
        }

        private void SetBusy(bool value)
        {
            busy = value;
            confirmButton.interactable = !value;
        }
    }
}
