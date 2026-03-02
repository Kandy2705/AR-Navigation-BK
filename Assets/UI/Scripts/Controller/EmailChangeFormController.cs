using UnityEngine;
using UnityEngine.UIElements;

public class EmailChangeFormController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "Btn-Back", navigator.PreviousPage(), true);
        navigator.BindButton(root, "Btn-Continue", PageID.OTPPage, false);
    }
}