using UnityEngine;
using UnityEngine.UIElements;

public class ChatboxController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigator.BindButton(root, "BtnBack", PageID.HistoryPage, true);
    }
}