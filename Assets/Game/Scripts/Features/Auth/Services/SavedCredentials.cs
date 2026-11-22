using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>
    /// "Ghi nhớ tôi": the email the login form pre-fills. The password is never stored; staying signed in is done
    /// with the refresh token (<see cref="SessionRestorer"/>). Older builds kept the password in plain text under
    /// <see cref="LegacyPasswordKey"/>, so that key is deleted whenever this class writes.
    /// </summary>
    public sealed class SavedCredentials
    {
        private const string EmailKey = "KEY_USER_EMAIL";
        private const string RememberKey = "KEY_IS_REMEMBER";

        /// <summary>Plain-text password written by earlier versions of the app.</summary>
        public const string LegacyPasswordKey = "KEY_USER_PASS";

        private readonly string prefix;

        /// <param name="keyPrefix">Prepended to every key; tests use one so they never touch real saved logins.</param>
        public SavedCredentials(string keyPrefix = "")
        {
            prefix = keyPrefix;
        }

        public bool IsRemembered => PlayerPrefs.GetInt(prefix + RememberKey, 0) == 1;

        public string Email => PlayerPrefs.GetString(prefix + EmailKey, string.Empty);

        public void Remember(string email)
        {
            PlayerPrefs.SetString(prefix + EmailKey, email);
            PlayerPrefs.SetInt(prefix + RememberKey, 1);
            PlayerPrefs.DeleteKey(prefix + LegacyPasswordKey);
            PlayerPrefs.Save();
        }

        public void Forget()
        {
            PlayerPrefs.DeleteKey(prefix + EmailKey);
            PlayerPrefs.DeleteKey(prefix + LegacyPasswordKey);
            PlayerPrefs.SetInt(prefix + RememberKey, 0);
            PlayerPrefs.Save();
        }
    }
}
