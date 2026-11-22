using System;
using Proyecto26;

namespace Game.Features.Auth
{
    /// <summary>
    /// Registration through users/register/request-otp, users/verify-otp and users/create-customer. The backend
    /// creates the account only with the code it mailed, which proves the email belongs to the registrant.
    /// </summary>
    public sealed class RestRegisterApi : IRegisterApi, IOtpVerifier
    {
        private const string OtpEndpoint = AppConst.BASE_API + "/users/register/request-otp";
        private const string CreateEndpoint = AppConst.BASE_API + "/users/create-customer";

        public void RequestOtp(string email, Action onSuccess, Action<string> onError)
        {
            RestClient.Post(OtpEndpoint, new EmailReq { email = email })
                .Then(_ => onSuccess())
                .Catch(error => onError(ApiError.Describe(error)));
        }

        public void VerifyOtp(string email, string code, Action<bool> onResult, Action<string> onError)
        {
            RestOtpVerifier.Verify("register", email, code, onResult, onError);
        }

        public void Register(RegistrationDraft draft, string password, Action onSuccess, Action<string> onError)
        {
            RestClient.Post(CreateEndpoint, BuildRequest(draft, password, AuthFlow.Otp))
                .Then(_ => onSuccess())
                .Catch(error => onError(ApiError.Describe(error)));
        }

        /// <summary>The create-customer body; the code is the one the OTP screen confirmed.</summary>
        public static RegisterReq BuildRequest(RegistrationDraft draft, string password, string otpCode)
        {
            return new RegisterReq
            {
                email = draft.Email,
                password = password,
                name = draft.Name,
                phone = draft.Phone,
                gender = draft.Gender,
                birthday = draft.Birthday.HasValue ? BirthdayFormat.Backend(draft.Birthday.Value) : string.Empty,
                otpCode = otpCode ?? string.Empty,
            };
        }
    }
}
