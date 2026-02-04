using UnityEngine;
using UnityEngine.UIElements;

public class PasswordChangeController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "BtnBack", PageID.Profile, true);
        navigator.ShowPasswordButton(root);
    }
}