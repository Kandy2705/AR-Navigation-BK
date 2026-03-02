using UnityEngine;
using UnityEngine.UIElements;

public class EmailChangeController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("Navigating to Email Change Page");
        new HistoryManager(root, (chatTitle) => {
        
        Routing.CurrentChatTitle = chatTitle;
        
        navigator.Navigate(PageID.Chatbox); 
    });
        navigator.BindButton(root, "Btn-Back", navigator.PreviousPage(), true);
        navigator.BindButton(root, "Btn-Continue", PageID.OTPPage, false);
    }
}