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
    public GameObject ARPageObject;
    private VisualElement rootContainer;

    public static string CurrentChatTitle = "";
    public PageID firstPage;
    private Dictionary<PageID, VisualTreeAsset> pageDict;
    private VisualElement currentPageElement;
    public static Stack<PageID> pageHistory = new Stack<PageID>();

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
        Navigate(firstPage);
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
        if (ARPageObject != null)
        {
            ARPageObject.SetActive(true); 
        }
        gameObject.SetActive(false);
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
            if(targetPage == PageID.ARPage) btn.clicked += () => SwitchObject();
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
        Debug.Log($"Input field: {inputName}, Toggle button: {btnName}");

        inputField.isPasswordField = true;
    
        toggleBtn.clicked += () => 
        {
            inputField.isPasswordField = !inputField.isPasswordField;
            toggleBtn.ToggleInClassList("eye-open"); 
            inputField.Q("unity-text-input").Focus();
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
}