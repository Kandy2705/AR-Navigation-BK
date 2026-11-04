using System;

namespace Game.Features.Settings
{
    /// <summary>The backend steps of changing the signed-in user's password and email. Both go through a one-time code.</summary>
    public interface IAccountApi
    {
        /// <summary>Seconds before another code may be requested (the backend limits how often a code is sent).</summary>
        int OtpCooldownSeconds { get; }

        void RequestPasswordOtp(string email, Action onSuccess, Action<string> onError);

        void ChangePassword(string email, string oldPassword, string newPassword, string otp, Action onSuccess, Action<string> onError);

        void RequestEmailOtp(string newEmail, Action onSuccess, Action<string> onError);

        void ChangeEmail(string newEmail, string otp, Action onSuccess, Action<string> onError);
    }
}
