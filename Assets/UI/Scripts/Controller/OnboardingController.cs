using UnityEngine;
using UnityEngine.UIElements;

public class OnboardingController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "NextOnboardingButton", PageID.WelcomePage, false);
    }
}