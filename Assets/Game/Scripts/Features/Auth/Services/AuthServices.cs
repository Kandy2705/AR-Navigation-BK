namespace Game.Features.Auth
{
    /// <summary>Where the auth screens get their backend access. Swap an entry (tests, or a real endpoint later) before the screen opens.</summary>
    public static class AuthServices
    {
        public static ILoginApi Login { get; set; } = new RestLoginApi();

        public static IForgotPasswordApi ForgotPassword { get; set; } = new PendingForgotPasswordApi();

        public static IRegisterApi Register { get; set; } = new RestRegisterApi();
    }
}
