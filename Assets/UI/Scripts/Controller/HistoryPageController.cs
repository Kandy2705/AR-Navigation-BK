using UnityEngine;
using UnityEngine.UIElements;

public class HistoryPageController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("Navigating to History Page");
        new HistoryManager(root, (chatTitle) => {
        
        Routing.CurrentChatTitle = chatTitle;
        
        navigator.Navigate(PageID.Chatbox); 
    });
        navigator.BindButton(root, "BtnChatbox", PageID.Chatbox, false);
        navigator.BindButton(root, "BtnSettings", PageID.MainSettings, false);
        navigator.BindButton(root, "btn-ar", PageID.ARPage, false);
        navigator.BindButton(root, "BtnBack", PageID.None, true);
    }
}