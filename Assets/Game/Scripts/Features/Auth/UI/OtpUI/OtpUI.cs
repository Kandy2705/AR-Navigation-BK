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

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private UiOtpInput otpInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text errorLabel;

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
            subtitleLabel.text = SubtitleFor(AuthFlow.Email);
            errorLabel.ShowMessage(null);
            otpInput.Clear();
            otpInput.FocusFirst();
        }

        private void HandleConfirm()
        {
            if (!otpInput.IsComplete)
            {
                errorLabel.ShowMessage(IncompleteMessage);
                return;
            }

            AuthFlow.ConfirmOtp(otpInput.Code);
        }
    }
}
