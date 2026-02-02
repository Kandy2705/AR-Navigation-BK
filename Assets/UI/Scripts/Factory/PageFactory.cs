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
        //if (btnBack != null) btnBack.clicked += () => navigator.GoBack();
    }
}

// 3. Factory để quyết định trang nào dùng logic nào
public static class PageFactory
{
    public static IPageController GetController(PageID id)
    {
        switch (id)
        {
            // case PageID.Onboarding: return new OnboardingController();
            // case PageID.Login: return new LoginController();
            // case PageID.Register: return new RegisterController();
            case PageID.HistoryPage: return new HistoryPageController();
            case PageID.MainSettings: return new MainSettingController();
            case PageID.Profile: return new ProfileController();
            case PageID.EmailChange: return new EmailChangeController();
            case PageID.PasswordChange: return new PasswordChangeController();
            case PageID.SupportCenter: return new SupportCenterController();
            case PageID.Contact: return new ContactController();
            case PageID.Chatbox: return new ChatboxController();
            // case PageID.Onboarding: 
            // case PageID.PasswordChange: return new PasswordChangeController();
            // Thêm các trang khác tại đây
            default: return new DefaultPageController();
        }
    }
}