namespace Game.Features.Settings
{
    /// <summary>Where the settings screens get their backend; swap <see cref="Api"/> in tests.</summary>
    public static class AccountServices
    {
        public static IAccountApi Api { get; set; } = new RestAccountApi();
    }
}
