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
    [Header("Dependencies")]
    public UIDocument mainDocument;
    public List<PageRoute> pages;

    public static string CurrentChatTitle = "";

    private Dictionary<PageID, VisualTreeAsset> pageDict;
    private VisualElement rootContainer;
    private VisualElement currentPageElement;

    private readonly List<PageID> tabPages = new List<PageID> { 
        PageID.MainSettings, PageID.HistoryPage, PageID.ARPage 
    };

    void Awake()
    {
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
        Navigate(PageID.MainSettings);
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

        // 3. Inject Logic (Dependency Injection via Method)
        // Đây là điểm mấu chốt: Nhờ Factory lấy logic tương ứng gắn vào UI vừa tạo
        IPageController controller = PageFactory.GetController(pageID);
        controller.Initialize(newPage, this);

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
        // if(buttonName == "BtnLogout")
        // {
        //     btn.clicked += () => LogOutNotification();
        //     return;
        // }
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

    private void SetupPasswordToggle(VisualElement root, string inputName, string btnName)
    {
        // 1. Tìm Input và Nút bằng tên đã đặt trong UXML
        var inputField = root.Q<PlaceHolder>(inputName);
        var toggleBtn = root.Q<Button>(btnName);

        inputField.isPasswordField = true;
    
        toggleBtn.clicked += () => 
        {
            inputField.isPasswordField = !inputField.isPasswordField;
            toggleBtn.ToggleInClassList("eye-open"); 
            inputField.Q("unity-text-input").Focus();
        };
    }

}