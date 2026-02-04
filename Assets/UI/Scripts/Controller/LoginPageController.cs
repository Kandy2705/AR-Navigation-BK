using UnityEngine;
using UnityEngine.UIElements;

public class LoginPageController : IPageController
{
    private readonly ProfileService Service = new ProfileService();
    private GameObject loginGameObject;
    private const string cacheKey = AppConst.KEY_CACHE;
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("hehe khởi tạo được Login Page Controller rồi");
        loginGameObject = navigator.loginPageObject;
        if(loginGameObject != null && loginGameObject.activeSelf == false)
        {
            //bool isActive = loginGameObject.activeSelf;
            loginGameObject.SetActive(true);
        }
        navigator.gameObject.SetActive(false);

    }



}