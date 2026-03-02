using UnityEngine;
using UnityEngine.UIElements;

public class EmailChangingOTPController : IPageController
{
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        PageID prevPage = navigator.PreviousPage();
        //PageID nextPage = PageID.None;
        navigator.BindButton(root, "Btn-Back", prevPage, true);
        
        // if(prevPage == PageID.Register) nextPage = PageID.PasswordConfirm;
        // else if(prevPage == PageID.EmailChangeForm) nextPage = PageID.PasswordChangeForm;

        Debug.Log($"Trang trước đó ở hàm này là: {prevPage}");
        navigator.BindButton(root, "btn-confirm", PageID.PasswordConfirm, false);
    }
}