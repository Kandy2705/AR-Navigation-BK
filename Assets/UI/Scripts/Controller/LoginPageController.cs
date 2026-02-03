using UnityEngine;
using UnityEngine.UIElements;

public class LoginPageController : IPageController
{
    private GameObject loginGameObject;
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("hehe khởi tạo được Login Page Controller rồi");
        loginGameObject = navigator.loginPageObject;
        if(loginGameObject != null)
        {
            bool isActive = loginGameObject.activeSelf;
            loginGameObject.SetActive(true);
        }
        navigator.gameObject.SetActive(false);
        //navigator.BindButton(root, "BtnBack", PageID.HistoryPage, true);
    }



}