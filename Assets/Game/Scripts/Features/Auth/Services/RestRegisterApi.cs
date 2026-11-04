using System;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>
    /// Registration through the shared REST client. Creating the account (users/create-customer, same payload as
    /// the UI Toolkit screen, including its fixed role and cccd defaults from <see cref="RegisterReq"/>) is real.
    /// Sending the OTP is not: the old flow never called a backend for it either, so the OTP screen is UI-only for now.
    /// </summary>
    public sealed class RestRegisterApi : IRegisterApi
    {
        private const string CreateEndpoint = AppConst.BASE_API + "/users/create-customer";

        public void RequestOtp(string email, Action onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[Register] No OTP endpoint wired yet: no code was sent to " + email);
            onSuccess();
        }

        public void Register(RegistrationDraft draft, string password, Action onSuccess, Action<string> onError)
        {
            var body = new RegisterReq
            {
                email = draft.Email,
                password = password,
                name = draft.Name,
                phone = draft.Phone,
                gender = draft.Gender,
                birthday = draft.Birthday.HasValue ? BirthdayFormat.Backend(draft.Birthday.Value) : string.Empty,
            };

            RestClient.Post(CreateEndpoint, body)
                .Then(response => onSuccess())
                .Catch(error => onError(error.Message));
        }
    }
}
