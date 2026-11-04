using System;
using Proyecto26;

namespace Game.Features.Auth
{
    /// <summary>POST users/login through the shared REST client (same endpoint and payload as the UI Toolkit screen).</summary>
    public sealed class RestLoginApi : ILoginApi
    {
        private const string Endpoint = AppConst.BASE_API + "/users/login";

        public void Login(string email, string password, Action<LoginRes> onSuccess, Action<string> onError)
        {
            // A stale bearer token from a previous session must not ride along with the login request.
            RestClient.DefaultRequestHeaders.Clear();

            var body = new LoginReq { email = email, password = password };
            RestClient.Post(Endpoint, body)
                .Then(response =>
                {
                    LoginRes result = LoginResponseParser.Parse(response.Text);
                    if (result != null)
                    {
                        onSuccess(result);
                    }
                    else
                    {
                        onError("Login response did not contain a token.");
                    }
                })
                .Catch(error => onError(error.Message));
        }
    }
}
