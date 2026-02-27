using UnityEngine.UIElements;

public interface IPageController
{
    void Initialize(VisualElement root, NavigationManager navigator);
}


public class DefaultPageController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator) 
    {
        var btnBack = root.Q<Button>("BtnBack");
    }
}

// 3. Factory để quyết định trang nào dùng logic nào
public static class PageFactory
{

    public static IPageController GetController(PageID id)
    {
        switch (id)
        {
            case PageID.HistoryPage: return new HistoryPageController();
            case PageID.MainSettings: return new MainSettingController();
            case PageID.Profile: return new ProfileController();
            case PageID.EmailChange: return new EmailChangeController();
            case PageID.PasswordChange: return new PasswordChangeController();
            case PageID.SupportCenter: return new SupportCenterController();
            case PageID.Contact: return new ContactController();
            case PageID.Chatbox: return new ChatboxController();
            case PageID.Login: return new LoginPageController();
            case PageID.Register: return new RegisterPageController();
            case PageID.EmailChangeForm: return new EmailChangeFormController();
            //case PageID.OTPPage: return new OTPPageController();
            //case PageID.Onboarding: return new OnboardingController();
            case PageID.WelcomePage: return new WelcomePageController();
            default: return new DefaultPageController();
        }
    }
}