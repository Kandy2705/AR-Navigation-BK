#if UNITY_EDITOR
namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Every Canvas screen the AppShell scene routes to, in navigation order. The auth screens reuse the
    /// PageIDs of the UI Toolkit pages they replace (Register, PasswordConfirm = choose the password when signing up,
    /// EmailChangeForm = forgot-password email, OTPPage shared by both flows, PasswordChangeForm), so links from
    /// screens that are not migrated yet keep working.
    /// </summary>
    public static class UiCanvasShellRoutes
    {
        public struct Route
        {
            public PageID Id;
            public string Screen;
        }

        public static readonly Route[] All =
        {
            new Route { Id = PageID.SplashPage, Screen = UiCanvasOnboardingBuilder.SplashName },
            new Route { Id = PageID.Onboarding, Screen = UiCanvasOnboardingBuilder.IntroName },
            new Route { Id = PageID.WelcomePage, Screen = UiCanvasOnboardingBuilder.WelcomeName },
            new Route { Id = PageID.Login, Screen = UiCanvasAuthBuilder.LoginName },
            new Route { Id = PageID.Register, Screen = UiCanvasRegisterBuilder.RegisterName },
            new Route { Id = PageID.PasswordConfirm, Screen = UiCanvasRegisterBuilder.CreatePasswordName },
            new Route { Id = PageID.EmailChangeForm, Screen = UiCanvasAuthBuilder.ForgotEmailName },
            new Route { Id = PageID.OTPPage, Screen = UiCanvasAuthBuilder.OtpName },
            new Route { Id = PageID.PasswordChangeForm, Screen = UiCanvasAuthBuilder.NewPasswordName },
        };
    }
}
#endif
