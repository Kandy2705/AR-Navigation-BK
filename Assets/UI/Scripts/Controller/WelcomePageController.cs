using UnityEngine;
using UnityEngine.UIElements;

public class WelcomePageController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "LoginButton", PageID.Login, false);
        navigator.BindButton(root, "SignUpButton", PageID.Register, false);
    }
}