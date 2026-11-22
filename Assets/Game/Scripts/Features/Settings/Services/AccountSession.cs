using Game.Features.Auth;
using Game.Features.Chat;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Settings
{
    /// <summary>What is stored about the signed-in user, and how it is dropped on logout.</summary>
    public static class AccountSession
    {
        private const string PasswordKey = LoginSession.LegacyPasswordKey; // only deleted: the app no longer stores it
        private const string AccessTokenKey = "ACCESS_TOKEN";
        private const string RefreshTokenKey = "REFRESH_TOKEN";


        /// <summary>Forgets the user: cached profile, tokens, request header, open chat and any half-finished auth flow. "Remember me" stays.</summary>
        public static void SignOut()
        {
            PlayerPrefs.DeleteKey(AppConst.KEY_CACHE);
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(PasswordKey);
            PlayerPrefs.Save();

            RestClient.DefaultRequestHeaders.Remove("Authorization");
            ChatState.Clear();
            AuthFlow.Clear();
        }

        /// <summary>Writes the new email into the cached profile so the profile screen shows it at once.</summary>
        public static void UpdateCachedEmail(string email)
        {
            string json = PlayerPrefs.GetString(AppConst.KEY_CACHE, string.Empty);
            RegisterRes cached = string.IsNullOrEmpty(json) ? new RegisterRes() : JsonUtility.FromJson<RegisterRes>(json);
            cached.email = email;
            PlayerPrefs.SetString(AppConst.KEY_CACHE, JsonUtility.ToJson(cached));
            PlayerPrefs.Save();
        }
    }
}
