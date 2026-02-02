using UnityEngine;
using UnityEngine.UIElements;

public class SupportCenterController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "BtnBack", PageID.MainSettings, true);
    }
}