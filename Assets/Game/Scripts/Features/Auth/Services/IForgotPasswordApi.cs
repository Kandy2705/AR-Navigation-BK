using System;

namespace Game.Features.Auth
{
    /// <summary>The two backend steps of "forgot password": send a one-time code, then set the new password with it.</summary>
    public interface IForgotPasswordApi
    {
        void RequestOtp(string email, Action onSuccess, Action<string> onError);

        void ResetPassword(string email, string otp, string newPassword, Action onSuccess, Action<string> onError);
    }
}
