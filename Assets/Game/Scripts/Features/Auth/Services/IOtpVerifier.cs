using System;

namespace Game.Features.Auth
{
    /// <summary>
    /// Checks an entered code with the backend before the flow moves on, so a typo is caught on the OTP screen
    /// instead of at the final submit. Flows without such a check simply pass no verifier.
    /// </summary>
    public interface IOtpVerifier
    {
        void VerifyOtp(string email, string code, Action<bool> onResult, Action<string> onError);
    }
}
