using Proyecto26;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>
    /// What the app does once a login (or a token refresh) succeeds: remember the tokens (same PlayerPrefs keys
    /// the rest of the app reads), authorize later requests, and cache the user profile. The password is never
    /// stored; staying signed in relies on the refresh token (<see cref="SessionRestorer"/>).
    /// </summary>
    public static class LoginSession
    {
        public const string AccessTokenKey = "ACCESS_TOKEN";
        public const string RefreshTokenKey = "REFRESH_TOKEN";

        /// <summary>Plain-text password written by earlier versions; deleted on every sign-in.</summary>
        public const string LegacyPasswordKey = "PASSWORD";

        /// <param name="password">Ignored; kept so existing callers and tests need no change.</param>
        public static void Start(LoginRes login, string password)
        {
            RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + login.accessToken;
            PlayerPrefs.DeleteKey(LegacyPasswordKey);
            PlayerPrefs.SetString(RefreshTokenKey, login.refreshToken ?? string.Empty);
            PlayerPrefs.SetString(AccessTokenKey, login.accessToken);
            PlayerPrefs.Save();

            var profiles = new ProfileService();
            profiles.GetUserProfile(login.accessToken)
                .Then(profile =>
                {
                    PlayerPrefs.SetString(AppConst.KEY_CACHE, JsonUtility.ToJson(profile));
                    PlayerPrefs.Save();
                })
                .Catch(error =>
                {
                    Debug.LogError("[LoginSession] Could not load the profile: " + error.Message);
                    if (error is RequestException request && request.StatusCode == 401)
                    {
                        profiles.Logout();
                    }
                });
        }
    }
}
