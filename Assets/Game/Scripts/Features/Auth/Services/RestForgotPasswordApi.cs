using System;
using Proyecto26;

namespace Game.Features.Auth
{
    /// <summary>
    /// Forgot password through users/forgot-password, users/verify-otp and users/reset-password. The backend
    /// answers the first call the same way whether or not the email is registered, so the app cannot be used
    /// to find out which emails have an account.
    /// </summary>
    public sealed class RestForgotPasswordApi : IForgotPasswordApi, IOtpVerifier
    {
        private const string RequestEndpoint = AppConst.BASE_API + "/users/forgot-password";
        private const string ResetEndpoint = AppConst.BASE_API + "/users/reset-password";

        public void RequestOtp(string email, Action onSuccess, Action<string> onError)
        {
            RestClient.Post(RequestEndpoint, new EmailReq { email = email })
                .Then(_ => onSuccess())
                .Catch(error => onError(ApiError.Describe(error)));
        }

        public void VerifyOtp(string email, string code, Action<bool> onResult, Action<string> onError)
        {
            RestOtpVerifier.Verify("reset", email, code, onResult, onError);
        }

        public void ResetPassword(string email, string otp, string newPassword, Action onSuccess, Action<string> onError)
        {
            var body = new ResetPasswordReq { email = email, otpCode = otp, newPassword = newPassword };
            RestClient.Post(ResetEndpoint, body)
                .Then(_ => onSuccess())
                .Catch(error => onError(ApiError.Describe(error)));
        }
    }
}
