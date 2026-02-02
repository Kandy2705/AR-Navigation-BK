using UnityEngine;
using UnityEngine.UIElements;

public class OnboardingController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "BtnProfile", PageID.Profile, false);
        navigator.BindButton(root, "BtnSupportCenter", PageID.SupportCenter, false);
        navigator.BindButton(root, "BtnContact", PageID.Contact, false);
        navigator.BindButton(root, "BtnHistory", PageID.HistoryPage, true);
        navigator.LogoutButton(root, "BtnLogout");
    }
}