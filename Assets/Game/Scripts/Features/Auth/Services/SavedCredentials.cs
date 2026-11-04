using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>
    /// "Ghi nhớ tôi": the email and password the login form pre-fills. Uses the PlayerPrefs keys of the UI
    /// Toolkit login so an existing install keeps working. The password is stored as plain text there too.
    /// </summary>
    public sealed class SavedCredentials
    {
        private const string EmailKey = "KEY_USER_EMAIL";
        private const string PasswordKey = "KEY_USER_PASS";
        private const string RememberKey = "KEY_IS_REMEMBER";

        private readonly string prefix;

        /// <param name="keyPrefix">Prepended to every key; tests use one so they never touch real saved logins.</param>
        public SavedCredentials(string keyPrefix = "")
        {
            prefix = keyPrefix;
        }

        public bool IsRemembered => PlayerPrefs.GetInt(prefix + RememberKey, 0) == 1;

        public string Email => PlayerPrefs.GetString(prefix + EmailKey, string.Empty);

        public string Password => PlayerPrefs.GetString(prefix + PasswordKey, string.Empty);

        public void Remember(string email, string password)
        {
            PlayerPrefs.SetString(prefix + EmailKey, email);
            PlayerPrefs.SetString(prefix + PasswordKey, password);
            PlayerPrefs.SetInt(prefix + RememberKey, 1);
            PlayerPrefs.Save();
        }

        public void Forget()
        {
            PlayerPrefs.DeleteKey(prefix + EmailKey);
            PlayerPrefs.DeleteKey(prefix + PasswordKey);
            PlayerPrefs.SetInt(prefix + RememberKey, 0);
            PlayerPrefs.Save();
        }
    }
}
