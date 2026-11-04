using System;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Settings
{
    /// <summary>
    /// Password change through users/request-password-change and users/change-password. The backend has no
    /// endpoint for changing the email yet, so those two steps only log a warning and succeed, exactly like the
    /// UI Toolkit flow did; replace them through <see cref="AccountServices.Api"/> once the endpoint exists.
    /// </summary>
    public sealed class RestAccountApi : IAccountApi
    {
        private readonly ChangePasswordService service = new ChangePasswordService();

        public int OtpCooldownSeconds => ChangePasswordService.GetRemainingCooldown();

        public void RequestPasswordOtp(string email, Action onSuccess, Action<string> onError)
        {
            service.RequestOTP(email)
                .Then(_ =>
                {
                    ChangePasswordService.SaveCooldown();
                    onSuccess();
                })
                .Catch(error => onError(Describe(error)));
        }

        public void ChangePassword(string email, string oldPassword, string newPassword, string otp, Action onSuccess, Action<string> onError)
        {
            service.VerifyAndChangePassword(email, oldPassword, newPassword, otp)
                .Then(_ => onSuccess())
                .Catch(error => onError(Describe(error)));
        }

        public void RequestEmailOtp(string newEmail, Action onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[Account] No backend endpoint wired yet: no OTP was sent to " + newEmail);
            onSuccess();
        }

        public void ChangeEmail(string newEmail, string otp, Action onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[Account] No backend endpoint wired yet: the email was not changed on the server.");
            onSuccess();
        }

        /// <summary>The backend's own message when the response carries one, otherwise the exception text.</summary>
        public static string Describe(Exception error)
        {
            if (error is RequestException request && !string.IsNullOrEmpty(request.Response))
            {
                try
                {
                    ProfileResponseWrapper body = JsonUtility.FromJson<ProfileResponseWrapper>(request.Response);
                    if (!string.IsNullOrEmpty(body?.message))
                    {
                        return body.message;
                    }
                }
                catch (Exception)
                {
                    // Not JSON: fall through to the exception text.
                }
            }

            return error.Message;
        }
    }
}
