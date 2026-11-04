using System;

namespace Game.Features.Auth
{
    /// <summary>Signs a user in. Callback based so screens (and tests) do not depend on the REST client.</summary>
    public interface ILoginApi
    {
        void Login(string email, string password, Action<LoginRes> onSuccess, Action<string> onError);
    }
}
