using System;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Settings
{
    /// <summary>
    /// Password change through users/request-password-change and users/change-password (the backend checks the
    /// old password); email change through users/request-email-change and users/change-email, which mail the
    /// code to the new address and need the signed-in user's bearer token.
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

        private const string RequestEmailEndpoint = AppConst.BASE_API + "/users/request-email-change";
        private const string ChangeEmailEndpoint = AppConst.BASE_API + "/users/change-email";

        public void RequestEmailOtp(string newEmail, Action onSuccess, Action<string> onError)
        {
            RestClient.Post(RequestEmailEndpoint, new EmailChangeReq { newEmail = newEmail })
                .Then(_ => onSuccess())
                .Catch(error => onError(Describe(error)));
        }

        public void ChangeEmail(string newEmail, string otp, Action onSuccess, Action<string> onError)
        {
            RestClient.Post(ChangeEmailEndpoint, new EmailChangeReq { newEmail = newEmail, otpCode = otp })
                .Then(_ => onSuccess())
                .Catch(error => onError(Describe(error)));
        }

        [Serializable]
        private class EmailChangeReq
        {
            public string newEmail;
            public string otpCode;
        }

        /// <summary>The backend's own message when the response carries one, otherwise a generic text.</summary>
        public static string Describe(Exception error) => Game.Features.Auth.ApiError.Describe(error);
    }
}
