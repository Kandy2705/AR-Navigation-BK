using System.Text.RegularExpressions;

namespace Game.Features.Auth
{
    /// <summary>Client-side checks for the auth forms. They only catch typos; the backend stays the authority.</summary>
    public static class AuthValidation
    {
        private static readonly Regex EmailPattern =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public static bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailPattern.IsMatch(email.Trim());
        }

        public const int MinPasswordLength = 8;

        /// <summary>The rule printed on the register screen: at least 8 characters, with letters and digits.</summary>
        public static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                return false;
            }

            bool hasLetter = false;
            bool hasDigit = false;
            foreach (char c in password)
            {
                hasLetter |= char.IsLetter(c);
                hasDigit |= char.IsDigit(c);
            }

            return hasLetter && hasDigit;
        }

        public static bool PasswordsMatch(string password, string confirmation)
        {
            return !string.IsNullOrEmpty(password) && password == confirmation;
        }
    }
}
