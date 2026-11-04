using System;

namespace Game.Features.Auth
{
    /// <summary>What the user entered on the "Hoàn thiện hồ sơ" form, kept while they go through OTP and choose a password.</summary>
    public sealed class RegistrationDraft
    {
        public string Name = string.Empty;
        public string Phone = string.Empty;
        public string Email = string.Empty;
        public string Gender = string.Empty;
        public DateTime? Birthday;
    }
}
