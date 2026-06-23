using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System;

// [System.Serializable]
// public struct PageRoute{
//     public PageID id;
//     public VisualTreeAsset asset;
// }

public class Routing : MonoBehaviour
{
    [Header("Dependencies")]
    public UIDocument mainDocument;
    public List<PageRoute> pages;

    public static string CurrentChatTitle = "";

    [Header("Layout Configuration")]
    public VisualTreeAsset mainLayoutAsset; 
    private const string ContentContainerName = "content-viewport";
    private IDisposable _currentController;

    private Dictionary<PageID, VisualTreeAsset> pageDict;

    private readonly List<PageID> tabPages = new List<PageID> { 
        PageID.MainSettings, 
        PageID.HistoryPage, 
        PageID.ARPage
    };

    private VisualElement rootContainer;
    private VisualElement currentPage;
    private VisualElement currentTabContent;
    private PageID currentPageID;

    void Awake()
    {
        pageDict = new Dictionary<PageID, VisualTreeAsset>();

        foreach(var page in pages)
        {
            if (!pageDict.ContainsKey(page.id))
            {
                pageDict.Add(page.id, page.asset);
            }
        }
    }

    void OnEnable()
    {
        if(mainDocument == null) return;

        var root = mainDocument.rootVisualElement;
        rootContainer = root.Q<VisualElement>("RootContainer");

        Navigate(PageID.MainSettings);
        //Navigate(PageID.Onboarding);
    }

    public void Navigate(PageID pageID, bool back = false)
    {
        if (!pageDict.ContainsKey(pageID))
        {
            Debug.LogError($"Chưa thiết lập file UXML cho trang: {pageID}");
            return;
        }

        bool isTargetTab = tabPages.Contains(pageID);
        bool isCurrentTab = tabPages.Contains(currentPageID);
        

        var NavigationDirection = "";
        var RemoveDirection = "";
        if (!back)
        {
            NavigationDirection = "page-left";
            RemoveDirection = "page-right";
        }
        else
        {
            NavigationDirection = "page-right";
            RemoveDirection = "page-left";
        }

        VisualTreeAsset tem = pageDict[pageID];
        VisualElement newPage = tem.Instantiate();

        newPage.AddToClassList("page");
        newPage.AddToClassList(RemoveDirection);

        newPage.style.flexGrow = 1;
        newPage.style.width = Length.Percent(100);
        newPage.style.height = Length.Percent(100);

        rootContainer.Add(newPage);

        newPage.schedule.Execute(() => 
        {
            if (currentPage != null)
            {
                currentPage.RemoveFromClassList("page-center");
                currentPage.AddToClassList(NavigationDirection);
                
                var oldPage = currentPage;
                oldPage.schedule.Execute(() => rootContainer.Remove(oldPage)).ExecuteLater(500);
            }
            newPage.RemoveFromClassList(RemoveDirection);
            newPage.AddToClassList("page-center");

            currentPage = newPage;

        }).ExecuteLater(10);

        _currentController = ControllerFactory.CreateController(pageID, newPage);

        Debug.Log("Navigating Navi");
        Debug.Log($"Page Id hiện tại là {pageID}");
        switch(pageID)
        {
            case PageID.HistoryPage:
                Debug.Log("Navigating to History Page");
                new HistoryManager(newPage, (chatTitle) => {
                
                Routing.CurrentChatTitle = chatTitle;
                
                Navigate(PageID.Chatbox); 
            });
                BindButton(newPage, "BtnChatbox", PageID.Chatbox, false);
                BindButton(newPage, "BtnSettings", PageID.MainSettings, false);
                BindButton(newPage, "BtnBack", PageID.None, true);
                break;
            case PageID.MainSettings:
                BindButton(newPage, "BtnProfile", PageID.Profile, false);
                BindButton(newPage, "BtnSupportCenter", PageID.SupportCenter, false);
                BindButton(newPage, "BtnContact", PageID.Contact, false);
                BindButton(newPage, "BtnHistory", PageID.HistoryPage, true);
                LogoutButton(newPage, "BtnLogout");
                break;
            case PageID.Profile:
                BindButton(newPage, "BtnBack", PageID.MainSettings, true);
                BindButton(newPage, "BtnEmailChange", PageID.EmailChange, false);
                BindButton(newPage, "BtnPasswordChange", PageID.PasswordChange, false);
                break;
            case PageID.EmailChange:
                BindButton(newPage, "BtnBack", PageID.Profile, true);
                break;
            case PageID.PasswordChange:
                BindButton(newPage, "BtnBack", PageID.Profile, true);
                ShowPasswordButton();
                break;
            case PageID.SupportCenter:
                BindButton(newPage, "BtnBack", PageID.MainSettings, true);
                break;
            case PageID.Contact:
                BindButton(newPage, "BtnBack", PageID.MainSettings, true);
                break;
            case PageID.Chatbox:
                BindButton(newPage, "BtnBack", PageID.HistoryPage, true);
                break;
            case PageID.Onboarding:
                BindButton(newPage, "NextOnboardingButton", PageID.Login, true);
                break;
        }

    }

    public void LogOutNotification()
    {
        Debug.Log("Ditmemay");
        var root = mainDocument.rootVisualElement;

        Button _logoutBtnOpen = root.Q<Button>(className: "logout-item"); 
        
        // Tìm các thành phần Modal vừa thêm
        VisualElement _overlay = root.Q<VisualElement>("logout-overlay");
        VisualElement _bottomSheet = root.Q<VisualElement>("bottom-sheet");
        Button _cancelBtn = root.Q<Button>("btn-cancel");
        Button _confirmBtn = root.Q<Button>("btn-confirm");

        if (_logoutBtnOpen != null)
        {
            _logoutBtnOpen.clicked += () => ShowLogoutModal(_overlay, _bottomSheet);
        } 

        if (_cancelBtn != null) _cancelBtn.clicked += () => HideLogoutModal(_overlay, _bottomSheet);
    }

    public void LogoutButton(VisualElement container, string buttonName)
    {
        var btn = container.Q<Button>(buttonName);
        if(btn == null) {Debug.Log("Không có nút Logout"); return;}
        if(buttonName == "BtnLogout")
        {
            btn.clicked += () => LogOutNotification();
            return;
        }
    }

    public void BindButton(VisualElement container, string buttonName, PageID targetPage, bool leftSlide)
    {
        var btn = container.Q<Button>(buttonName);
        if(buttonName == "BtnLogout")
        {
            btn.clicked += () => LogOutNotification();
            return;
        }
        if (btn != null)
        {
            btn.clicked += () => Navigate(targetPage, leftSlide);
        }
    }

    public void ShowPasswordButton()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        SetupPasswordToggle(root, "old-password", "btn-toggle-old");

        SetupPasswordToggle(root, "new-password", "btn-toggle-new");

        SetupPasswordToggle(root, "confirm-password", "btn-toggle-re");
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

    private void SetupPasswordToggle(VisualElement root, string inputName, string btnName)
    {
        // 1. Tìm Input và Nút bằng tên đã đặt trong UXML
        var inputField = root.Q<PlaceHolder>(inputName);
        var toggleBtn = root.Q<Button>(btnName);

        if (inputField == null || toggleBtn == null)
        {
            Debug.LogWarning($"Routing: Không tìm thấy '{inputName}' hoặc '{btnName}' trên page hiện tại — skip.");
            return;
        }

        inputField.isPasswordField = true;
    
        toggleBtn.clicked += () => 
        {
            inputField.isPasswordField = !inputField.isPasswordField;
            toggleBtn.ToggleInClassList("eye-open"); 
            if (inputField.isPasswordField) inputField.Focus();
        };
    }
}