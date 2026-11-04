using Proyecto26;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>
    /// What the app does once a login succeeds: remember the tokens (same PlayerPrefs keys the rest of the
    /// app reads), authorize later requests, and cache the user profile.
    /// </summary>
    public static class LoginSession
    {
        public static void Start(LoginRes login, string password)
        {
            RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + login.accessToken;
            PlayerPrefs.SetString("PASSWORD", password);
            PlayerPrefs.SetString("REFRESH_TOKEN", login.refreshToken ?? string.Empty);
            PlayerPrefs.SetString("ACCESS_TOKEN", login.accessToken);
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
