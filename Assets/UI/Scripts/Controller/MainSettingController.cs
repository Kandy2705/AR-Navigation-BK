using UnityEngine;
using UnityEngine.UIElements;

public class MainSettingController : IPageController
{
    private NavigationManager mainNavigator;
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        
        mainNavigator = navigator; 
        navigator.BindButton(root, "BtnProfile", PageID.Profile, false);
        navigator.BindButton(root, "BtnSupportCenter", PageID.SupportCenter, false);
        navigator.BindButton(root, "BtnContact", PageID.Contact, false);
        navigator.BindButton(root, "BtnHistory", PageID.HistoryPage, true);
        LogoutButton(root, "BtnLogout");
    }

    public void LogoutButton(VisualElement container, string buttonName)
    {
        var btn = container.Q<Button>(buttonName);
        if(btn == null) {Debug.Log("Không có nút Logout"); return;}
        if(buttonName == "BtnLogout")
        {
            btn.clicked += () => LogOutNotification(container);
            return;
        }
    }

    public void LogOutNotification(VisualElement root)
    {
        //var root = mainDocument.rootVisualElement;

        Button _logoutBtnOpen = root.Q<Button>(className: "logout-item"); 
        
        VisualElement _overlay = root.Q<VisualElement>("logout-overlay");
        VisualElement _bottomSheet = root.Q<VisualElement>("bottom-sheet");
        Button _cancelBtn = root.Q<Button>("btn-cancel");
        Button _confirmBtn = root.Q<Button>("btn-confirm");

        if (_logoutBtnOpen != null)
        {
            _logoutBtnOpen.clicked += () => ShowLogoutModal(_overlay, _bottomSheet);
        } 

        if (_cancelBtn != null) _cancelBtn.clicked += () => HideLogoutModal(_overlay, _bottomSheet);
        if (_confirmBtn != null) _confirmBtn.clicked += () => Logout();
    }

    private void ShowLogoutModal(VisualElement overlay, VisualElement bottomSheet)
    {
        overlay.AddToClassList("logout-overlay--show");
        bottomSheet.AddToClassList("bottom-sheet--up");
    }

    private void HideLogoutModal(VisualElement overlay, VisualElement bottomSheet)
    {
        overlay.RemoveFromClassList("logout-overlay--show");
        bottomSheet.RemoveFromClassList("bottom-sheet--up");
    }

    private void Logout()
    {
        
        Debug.Log($"Chuẩn bị đăng xuất, dữ liệu KEY_CACHE: {PlayerPrefs.GetString(AppConst.KEY_CACHE, "Không có dữ liệu")}");
        PlayerPrefs.DeleteKey(AppConst.KEY_CACHE);
        // PlayerPrefs.DeleteKey("ACCESS_TOKEN");
        // PlayerPrefs.DeleteKey("REFRESH_TOKEN");
        PlayerPrefs.Save();
        Debug.Log("Đăng xuất thành công");
        Debug.Log("Du lieu cua cache sau khi da dang xuat: " + PlayerPrefs.GetString(AppConst.KEY_CACHE, "Không có dữ liệu"));
        mainNavigator.Navigate(PageID.Login, true);

    }


}