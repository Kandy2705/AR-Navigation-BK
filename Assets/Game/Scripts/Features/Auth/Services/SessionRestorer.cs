using System;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>Exchanges a refresh token for a new session (POST users/refresh-token).</summary>
    public interface ISessionRefreshApi
    {
        /// <param name="onError">Called with true when the token was rejected (401), false for network/server errors.</param>
        void Refresh(string refreshToken, Action<LoginRes> onSuccess, Action<bool> onError);
    }

    public sealed class RestSessionRefreshApi : ISessionRefreshApi
    {
        private const string Endpoint = AppConst.BASE_API + "/users/refresh-token";

        [Serializable]
        private class RefreshReq
        {
            public string refreshToken;
        }

        public void Refresh(string refreshToken, Action<LoginRes> onSuccess, Action<bool> onError)
        {
            RestClient.DefaultRequestHeaders.Remove("Authorization");
            RestClient.Post(Endpoint, new RefreshReq { refreshToken = refreshToken })
                .Then(response =>
                {
                    LoginRes session = LoginResponseParser.Parse(response.Text);
                    if (session != null)
                    {
                        onSuccess(session);
                    }
                    else
                    {
                        onError(false);
                    }
                })
                .Catch(error => onError(error is RequestException request && request.StatusCode == 401));
        }
    }

    /// <summary>
    /// Keeps the user signed in between launches without storing the password: if a refresh token was saved at
    /// the last login, it is exchanged for a new session. A rejected token is forgotten so the user signs in again.
    /// </summary>
    public static class SessionRestorer
    {
        public static ISessionRefreshApi Api { get; set; } = new RestSessionRefreshApi();

        /// <summary>Starts the session it restores; tests replace it.</summary>
        public static Action<LoginRes, string> StartSession { get; set; } = LoginSession.Start;

        public static bool HasSavedSession => !string.IsNullOrEmpty(PlayerPrefs.GetString(LoginSession.RefreshTokenKey, string.Empty));

        /// <param name="done">true when a session was restored and the user can go straight to the home page.</param>
        public static void TryRestore(Action<bool> done)
        {
            string refreshToken = PlayerPrefs.GetString(LoginSession.RefreshTokenKey, string.Empty);
            if (string.IsNullOrEmpty(refreshToken))
            {
                done(false);
                return;
            }

            Api.Refresh(refreshToken, session =>
            {
                StartSession(session, null);
                done(true);
            }, rejected =>
            {
                if (rejected)
                {
                    PlayerPrefs.DeleteKey(LoginSession.RefreshTokenKey);
                    PlayerPrefs.DeleteKey(LoginSession.AccessTokenKey);
                    PlayerPrefs.Save();
                }
                done(false);
            });
        }
    }
}
