using UnityEngine;
using UnityEngine.UIElements;

public class ContactController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "BtnBack", PageID.MainSettings, true);
    }
}