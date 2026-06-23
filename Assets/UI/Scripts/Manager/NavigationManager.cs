using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public struct PageRoute{
    public PageID id;
    public VisualTreeAsset asset;
}

public class NavigationManager : MonoBehaviour
{
    /// <summary>Fired khi người dùng chuyển sang AR world (outdoor nav nên hiện ra).</summary>
    public static event System.Action OnAREntered;
    /// <summary>Fired khi quay lại MainScreen từ AR (outdoor nav nên ẩn đi).
    /// Cũng fire khi OnEnable lúc khởi động — lúc đó chưa có subscriber nào, hoàn toàn an toàn.</summary>
    public static event System.Action OnARExited;

    [Header("Dependencies")]
    public UIDocument mainDocument;
    public List<PageRoute> pages;
    public GameObject ARPageObject;
    [SerializeField] private HybridModeController hybridModeController;
    [SerializeField] private bool keepARPageDisabledOnStart = true;
    private VisualElement rootContainer;
    private SharedNavBar _navBar;

    public static string CurrentChatTitle = "";
    public PageID firstPage;
    private Dictionary<PageID, VisualTreeAsset> pageDict;
    private VisualElement currentPageElement;
    public static Stack<PageID> pageHistory = new Stack<PageID>();

    private readonly List<PageID> tabPages = new List<PageID> { 
        PageID.MainSettings, PageID.HistoryPage, PageID.ARPage 
    };

    private bool _bypassedAuthOnStart;

    [Header("Test / Debug")]
    [Tooltip("Khi true: auto vào ARPage ngay sau khi UI load (bỏ qua login/onboarding). Dùng để test indoor nhanh.")]
    [SerializeField] private bool skipToARForTest = false;

    void Awake()
    {
        if (keepARPageDisabledOnStart && ARPageObject != null)
        {
            ARPageObject.SetActive(false);
        }

        pageDict = new Dictionary<PageID, VisualTreeAsset>();
        foreach(var page in pages)
        {
            if (!pageDict.ContainsKey(page.id)) pageDict.Add(page.id, page.asset);
        }
    }

    void OnEnable()
    {
        if(mainDocument == null) return;
        rootContainer = mainDocument.rootVisualElement.Q<VisualElement>("RootContainer");

        // Reset state khi GameObject được kích hoạt lại (ví dụ quay về từ AR scene).
        // Nếu không clear, currentPageElement sẽ trỏ tới page cũ đã bị detach,
        // làm Navigate() ghi đè lung tung và UI bấm không ăn.
        if (rootContainer != null)
        {
            rootContainer.Clear();
        }
        currentPageElement = null;

        // Apply caret trắng cho mọi page render qua hệ navigation mới.
        CaretStyleApplier.Apply(mainDocument.rootVisualElement);

        // Re-bind shared nav mỗi lần OnEnable vì UIDocument có thể rebuild visual tree
        // khi GameObject bị disable/enable (quay từ AR về).
        if (_navBar == null) _navBar = new SharedNavBar(this);
        _navBar.Reset();
        _navBar.Bind(mainDocument.rootVisualElement);

        // Auto-bypass auth UI khi skipToARForTest hoặc forceIndoorTestMode=true.
        // Tránh load WelcomePage/Login/Register hoàn toàn, vào thẳng AR để test indoor.
        bool shouldBypassAuth = !_bypassedAuthOnStart && (skipToARForTest || IsForceIndoorTestMode());
        if (shouldBypassAuth)
        {
            _bypassedAuthOnStart = true;
            firstPage = PageID.MainSettings; // Return target sau khi AR test — tránh quay về WelcomePage.

            // Tắt legacy UIRouter để nó không tự enable UI Onboarding/Welcome/Login.
            DisableLegacyUIForTest();

            mainDocument.rootVisualElement.schedule.Execute(() =>
            {
                Debug.Log("[NavigationManager] [INDOOR_TEST] Bypass auth UI → entering AR directly.");
                EnterARPage();
            }).ExecuteLater(100);
        }
        else
        {
            Navigate(firstPage);
        }

        // Thông báo outdoor nav nên ẩn khi MainScreen đang active.
        // Lúc khởi động đầu tiên chưa ai subscribe nên hoàn toàn an toàn.
        OnARExited?.Invoke();
    }

    public PageID PreviousPage()
    {
        if (pageHistory.Count > 1)
        {
            PageID currentPage = pageHistory.Peek();
            pageHistory.Pop();
            PageID previousPage = pageHistory.Peek();
            pageHistory.Push(currentPage);
            return previousPage;
        }
        else if(pageHistory.Count == 1) return pageHistory.Peek();
        else return PageID.None;
    }

    public void Navigate(PageID pageID, bool isBack = false)
    {
        if (!pageDict.ContainsKey(pageID))
        {
            Debug.LogError($"Missing UXML for: {pageID}");
            return;
        }

        // Cập nhật UI Document
        VisualTreeAsset asset = pageDict[pageID];
        VisualElement newPage = asset.Instantiate();
        
        SetupPageLayout(newPage, isBack);

        if(isBack){
            if(pageID == PageID.HistoryPage) pageHistory.Push(pageID);
            else pageHistory.Pop();
        }
        else
        {
            pageHistory.Push(pageID);
        }

        IPageController controller = PageFactory.GetController(pageID);
        controller.Initialize(newPage, this);

        // Apply lại caret + clamp scroll cho ScrollView mới render trong page này.
        CaretStyleApplier.Apply(newPage);

        // Cập nhật trạng thái shared nav: hiện ở các tab chính, ẩn ở các page con.
        if (_navBar != null)
        {
            bool isTabPage = _navBar.IsTabPage(pageID);
            _navBar.SetVisible(isTabPage);
            if (isTabPage) _navBar.SetActive(pageID);
        }

        Debug.Log($"Trang trước đó: {PreviousPage()}");
        if (pageHistory.Count >= 1) Debug.Log($"Đang mở trang: {pageHistory.Peek()}");
        else Debug.Log("Không có trang để hiển thị nữa");
        
        rootContainer.Add(newPage);
        HandleTransition(newPage, isBack);
    }

    private void SetupPageLayout(VisualElement page, bool isBack)
    {
        string initialClass = isBack ? "page-left" : "page-right";
        page.AddToClassList("page");
        page.AddToClassList(initialClass);
        page.style.flexGrow = 1;
        page.style.width = Length.Percent(100);
        page.style.height = Length.Percent(100);
    }

    public void SwitchObject()
    {
        if (ARPageObject == null)
        {
            Debug.LogError("[NavigationManager] ARPageObject is not assigned. Cannot switch to AR page.");
            return;
        }

        ARPageObject.SetActive(true);
        ApplyHybridInitialMode();
        OnAREntered?.Invoke();   // Thông báo để outdoor nav có thể hiện ra
        gameObject.SetActive(false);
    }

    private void ApplyHybridInitialMode()
    {
        if (hybridModeController == null)
        {
            hybridModeController = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }

        if (hybridModeController != null)
        {
            hybridModeController.ApplyInitialMode();
        }
    }

    public void EnterARPage()
    {
        // Keep AR as a sentinel entry so return flow can resolve the correct previous page.
        if (pageHistory.Count == 0 || pageHistory.Peek() != PageID.ARPage)
        {
            pageHistory.Push(PageID.ARPage);
        }

        SwitchObject();
    }

    public PageID ConsumeReturnPageFromAR()
    {
        // Remove AR sentinel if present.
        if (pageHistory.Count > 0 && pageHistory.Peek() == PageID.ARPage)
        {
            pageHistory.Pop();
        }

        PageID target;
        if (pageHistory.Count > 0)
        {
            target = pageHistory.Peek();
        }
        else
        {
            target = firstPage != PageID.None ? firstPage : PageID.MainSettings;
        }

        // Pop luôn target ở đỉnh stack — Navigate(target) sẽ push lại,
        // tránh bị duplicate dồn lịch sử mỗi lần ra/vào AR.
        if (pageHistory.Count > 0 && pageHistory.Peek() == target)
        {
            pageHistory.Pop();
        }

        return target;
    }

    private void HandleTransition(VisualElement newPage, bool isBack)
    {
        string enterClass = isBack ? "page-left" : "page-right";
        string exitClass = isBack ? "page-right" : "page-left";

        newPage.schedule.Execute(() => 
        {
            if (currentPageElement != null)
            {
                currentPageElement.RemoveFromClassList("page-center");
                currentPageElement.AddToClassList(exitClass);
                
                var oldPage = currentPageElement;
                oldPage.schedule.Execute(() => rootContainer.Remove(oldPage)).ExecuteLater(500);
            }
            newPage.RemoveFromClassList(enterClass);
            newPage.AddToClassList("page-center");

            currentPageElement = newPage;
        }).ExecuteLater(10); 
    }

    public void BindButton(VisualElement container, string buttonName, PageID targetPage, bool leftSlide)
    {
        var btn = container.Q<Button>(buttonName);
        if (btn != null)
        {
            if (targetPage == PageID.ARPage)
            {
                btn.clicked += () => EnterARPage();
                return;
            }

            btn.clicked += () => Navigate(targetPage, leftSlide);
        }
    }

    public void ShowPasswordButton(VisualElement root)
    {
        SetupPasswordToggle(root, "old-password", "btn-toggle-old");

        SetupPasswordToggle(root, "new-password", "btn-toggle-new");

        SetupPasswordToggle(root, "confirm-password", "btn-toggle-re");
    }

    private void SetupPasswordToggle(VisualElement root, string inputName, string btnName)
    {
        var inputField = root.Q<PlaceHolder>(inputName);
        var toggleBtn = root.Q<Button>(btnName);

        if (inputField == null || toggleBtn == null)
        {
            Debug.LogWarning($"NavigationManager: Không tìm thấy '{inputName}' hoặc '{btnName}' trên page hiện tại — skip.");
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

    public void OnTogglePasswordClick(ClickEvent evt, TextField Input, VisualElement eyeIcon, ref bool checkVisible)
    {
        //Debug.Log($"Check hien tai bien visible: {checkVisible}");
        checkVisible = !checkVisible;
        Input.isPasswordField = !checkVisible;
        UpdateEyeIcon(eyeIcon, checkVisible);
    }

    private void UpdateEyeIcon(VisualElement eyeIcon, bool isPasswordVisible)
    {
        if (isPasswordVisible)
        {
            eyeIcon.RemoveFromClassList("icon-eye-closed");
            eyeIcon.AddToClassList("icon-eye-open");
        }
        else
        {
            eyeIcon.RemoveFromClassList("icon-eye-open");
            eyeIcon.AddToClassList("icon-eye-closed");
        }
    }

    private void DisableLegacyUIForTest()
    {
        // UIRouter (legacy): active trong scene, Awake() gọi ShowOnboarding().
        // Tắt GameObject để nó không mở UI Onboarding/Welcome/Login.
        var uiRouter = FindFirstObjectByType<UIRouter>(FindObjectsInactive.Include);
        if (uiRouter != null && uiRouter.gameObject.activeInHierarchy)
        {
            uiRouter.gameObject.SetActive(false);
            Debug.Log("[NavigationManager] [INDOOR_TEST] Disabled UIRouter (legacy UI).");
        }
    }

    private bool IsForceIndoorTestMode()
    {
        if (hybridModeController != null)
            return hybridModeController.ForceIndoorTestModeEnabled;
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid != null)
        {
            hybridModeController = hybrid;
            return hybrid.ForceIndoorTestModeEnabled;
        }
        return false;
    }
}
