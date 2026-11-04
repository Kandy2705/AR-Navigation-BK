using System;

namespace Game.Features.Auth
{
    /// <summary>The two backend steps of registration: send a one-time code to the email, then create the account.</summary>
    public interface IRegisterApi
    {
        void RequestOtp(string email, Action onSuccess, Action<string> onError);

        void Register(RegistrationDraft draft, string password, Action onSuccess, Action<string> onError);
    }
}
