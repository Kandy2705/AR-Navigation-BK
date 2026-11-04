using System;

namespace Game.Features.Auth
{
    /// <summary>
    /// State passed between the screens of one auth flow. The OTP screen is shared by several flows (forgot
    /// password now, register and change-password later), so the screen that sends the user there says what
    /// the code is for and what happens once it is entered.
    /// </summary>
    public static class AuthFlow
    {
        /// <summary>Email the code was sent to.</summary>
        public static string Email { get; private set; } = string.Empty;

        /// <summary>The code the user entered; filled in when the OTP screen confirms.</summary>
        public static string Otp { get; private set; } = string.Empty;

        /// <summary>Called by the OTP screen with the entered code; the flow decides the next page.</summary>
        public static Action<string> OnOtpConfirmed { get; private set; }

        /// <summary>The profile being registered; null when no registration is in progress.</summary>
        public static RegistrationDraft Registration { get; private set; }

        public static void BeginRegistration(RegistrationDraft draft)
        {
            Registration = draft;
        }

        public static void ClearRegistration()
        {
            Registration = null;
        }

        public static void BeginOtp(string email, Action<string> onOtpConfirmed)
        {
            Email = email ?? string.Empty;
            Otp = string.Empty;
            OnOtpConfirmed = onOtpConfirmed;
        }

        public static void ConfirmOtp(string code)
        {
            Otp = code ?? string.Empty;
            OnOtpConfirmed?.Invoke(Otp);
        }

        public static void Clear()
        {
            Email = string.Empty;
            Otp = string.Empty;
            OnOtpConfirmed = null;
            Registration = null;
        }
    }
}
