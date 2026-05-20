using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Quản lý thanh nav cố định ở UI Main.uxml.
/// - Chỉ bind sự kiện click 1 lần (lúc Awake).
/// - Mỗi lần Navigate, gọi <see cref="SetActive"/> để đổi class màu, không re-render.
/// - Quyết định hướng slide khi chuyển tab dựa theo thứ tự index trong nav.
///   (tab đứng bên phải → slide forward, tab bên trái → slide back)
/// </summary>
public class SharedNavBar
{
    private const string ActiveClass = "nav-active";

    private readonly NavigationManager _navigator;

    /// <summary>Thứ tự tab hiển thị trong nav bar (trái → phải).</summary>
    private readonly List<PageID> _tabOrder = new List<PageID>
    {
        PageID.ARPage,
        PageID.HistoryPage,
        PageID.MainSettings,
    };

    /// <summary>Các page sẽ HIỆN thanh nav (ARPage không thuộc nhóm này — AR là scene riêng).</summary>
    private readonly HashSet<PageID> _navVisiblePages = new HashSet<PageID>
    {
        PageID.HistoryPage,
        PageID.MainSettings,
    };

    private readonly Dictionary<PageID, Button> _tabButtons = new Dictionary<PageID, Button>();

    private VisualElement _root;
    private bool _isBound;
    private PageID _currentTab = PageID.None;

    public SharedNavBar(NavigationManager navigator)
    {
        _navigator = navigator;
    }

    public bool IsTabPage(PageID pageId) => _navVisiblePages.Contains(pageId);

    /// <summary>
    /// Bind các nút nav trong root document. Gọi 1 lần khi Awake.
    /// </summary>
    public void Bind(VisualElement documentRoot)
    {
        if (_isBound || documentRoot == null) return;

        _root = documentRoot.Q<VisualElement>("SharedBottomNav");
        if (_root == null)
        {
            Debug.LogWarning("[SharedNavBar] Không tìm thấy element SharedBottomNav trong UI Main.uxml");
            return;
        }

        var btnAR = _root.Q<Button>("btn-ar");
        var btnHistory = _root.Q<Button>("BtnHistory");
        var btnSettings = _root.Q<Button>("BtnSettings");

        if (btnAR != null)
        {
            // AR là entry sang scene, không tham gia slide direction.
            btnAR.clicked += () => _navigator.EnterARPage();
            _tabButtons[PageID.ARPage] = btnAR;
        }

        if (btnHistory != null)
        {
            btnHistory.clicked += () => GoToTab(PageID.HistoryPage);
            _tabButtons[PageID.HistoryPage] = btnHistory;
        }

        if (btnSettings != null)
        {
            btnSettings.clicked += () => GoToTab(PageID.MainSettings);
            _tabButtons[PageID.MainSettings] = btnSettings;
        }

        _isBound = true;
    }

    private void GoToTab(PageID target)
    {
        if (target == _currentTab) return;
        bool isBack = IsBackDirection(_currentTab, target);
        _navigator.Navigate(target, isBack);
    }

    /// <summary>
    /// Trả về true nếu chuyển từ <paramref name="from"/> sang <paramref name="to"/> là hướng "lùi" (slide trái → phải).
    /// </summary>
    private bool IsBackDirection(PageID from, PageID to)
    {
        int fromIdx = _tabOrder.IndexOf(from);
        int toIdx = _tabOrder.IndexOf(to);
        if (fromIdx < 0 || toIdx < 0) return false;
        return toIdx < fromIdx;
    }

    /// <summary>
    /// Set tab tương ứng với <paramref name="pageId"/> sang trạng thái active (đổi màu).
    /// </summary>
    public void SetActive(PageID pageId)
    {
        if (!_isBound) return;

        _currentTab = pageId;

        foreach (var pair in _tabButtons)
        {
            if (pair.Key == pageId)
                pair.Value.AddToClassList(ActiveClass);
            else
                pair.Value.RemoveFromClassList(ActiveClass);
        }
    }

    /// <summary>Hide/show toàn bộ thanh nav.</summary>
    public void SetVisible(bool visible)
    {
        if (_root == null) return;
        _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
