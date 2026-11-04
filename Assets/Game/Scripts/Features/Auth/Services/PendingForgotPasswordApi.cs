using System;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>
    /// Stand-in until the backend exposes a forgot-password endpoint. The existing change-password endpoint
    /// (users/change-password) needs the old password, which a user who forgot it does not have, so nothing is
    /// sent and every step succeeds. This is exactly what the UI Toolkit flow did. Replace it through
    /// <see cref="AuthServices.ForgotPassword"/> once the endpoint exists.
    /// </summary>
    public sealed class PendingForgotPasswordApi : IForgotPasswordApi
    {
        public void RequestOtp(string email, Action onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[ForgotPassword] No backend endpoint wired yet: no OTP was sent to " + email);
            onSuccess();
        }

        public void ResetPassword(string email, string otp, string newPassword, Action onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[ForgotPassword] No backend endpoint wired yet: the password was not changed.");
            onSuccess();
        }
    }
}
