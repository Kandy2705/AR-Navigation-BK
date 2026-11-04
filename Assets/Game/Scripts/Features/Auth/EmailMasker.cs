namespace Game.Features.Auth
{
    /// <summary>Hides most of an email on the OTP screen, same rule as the UI Toolkit screen: first two letters, stars, then the domain.</summary>
    public static class EmailMasker
    {
        public const string Unknown = "***@***.com";

        public static string Mask(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Unknown;
            }

            int at = email.IndexOf('@');
            if (at < 0)
            {
                return Unknown;
            }

            return at <= 2
                ? "*****" + email.Substring(at)
                : email.Substring(0, 2) + "*****" + email.Substring(at);
        }
    }
}
