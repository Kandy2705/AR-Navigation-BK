using UnityEngine;
using UnityEngine.UIElements;

public class PasswordConfirmController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "BtnBack", navigator.PreviousPage(), true);
        //navigator.ShowPasswordButton(root);
    }
}