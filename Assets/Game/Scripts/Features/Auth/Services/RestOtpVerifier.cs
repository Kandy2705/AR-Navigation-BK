using System;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>POST users/verify-otp: checks a "register" or "reset" code without using it up.</summary>
    public static class RestOtpVerifier
    {
        private const string Endpoint = AppConst.BASE_API + "/users/verify-otp";

        public static void Verify(string purpose, string email, string code, Action<bool> onResult, Action<string> onError)
        {
            var body = new VerifyOtpReq { email = email, otpCode = code, purpose = purpose };
            RestClient.Post(Endpoint, body)
                .Then(response => onResult(JsonUtility.FromJson<ApiBoolResponse>(response.Text)?.data == true))
                .Catch(error => onError(ApiError.Describe(error)));
        }
    }
}
