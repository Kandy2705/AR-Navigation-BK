using System.Collections.Generic;
using Game.Foundation.UI;
using UnityEngine;

[System.Serializable]
public struct PageRoute
{
    public PageID id;

    /// <summary>
    /// The screen for this id: a scene instance, or a prefab asset that is instantiated under
    /// <c>canvasPageContainer</c> the first time it is opened.
    /// </summary>
    public UiPage canvasPage;
}

/// <summary>
/// The app's screen router. Pages are Canvas (uGUI) prefabs registered per <see cref="PageID"/>; the router
/// keeps a history stack so Back works, and shows one page at a time with a slide transition.
/// </summary>
public class NavigationManager : MonoBehaviour
{
    public List<PageRoute> pages;
    public PageID firstPage;

    [Header("Canvas UI")]
    [Tooltip("AppCanvas instance. Shown together with this manager.")]
    [SerializeField] private GameObject canvasRoot;
    [Tooltip("Parent for page prefabs that are not scene instances (AppCanvas/Pages).")]
    [SerializeField] private RectTransform canvasPageContainer;

    public static Stack<PageID> pageHistory = new Stack<PageID>();

    private readonly Dictionary<PageID, UiPage> pageSources = new Dictionary<PageID, UiPage>();
    private readonly Dictionary<PageID, UiPage> pageInstances = new Dictionary<PageID, UiPage>();
    private UiPage currentPage;

    private void Awake()
    {
        pageSources.Clear();
        foreach (PageRoute route in pages)
        {
            if (route.canvasPage != null && !pageSources.ContainsKey(route.id))
            {
                pageSources.Add(route.id, route.canvasPage);
            }
        }
    }

    private void OnEnable()
    {
        // Re-enabling the manager (after a scene switch) brings the canvas back and drops stale pages,
        // otherwise the previous page stays on screen under the one Navigate() shows.
        if (canvasRoot != null)
        {
            canvasRoot.SetActive(true);
        }

        HideAllPagesImmediate();
        currentPage = null;

        if (pageSources.Count > 0 && firstPage != PageID.None)
        {
            Navigate(firstPage);
        }
    }

    /// <summary>True when a page is registered for this id.</summary>
    public bool HasRoute(PageID pageID)
    {
        return pageSources.ContainsKey(pageID);
    }

    /// <summary>The page below the current one in the history (the current one when there is nothing below).</summary>
    public PageID PreviousPage()
    {
        if (pageHistory.Count > 1)
        {
            PageID current = pageHistory.Pop();
            PageID previous = pageHistory.Peek();
            pageHistory.Push(current);
            return previous;
        }

        return pageHistory.Count == 1 ? pageHistory.Peek() : PageID.None;
    }

    /// <param name="isBack">True when going back: the history loses its top entry and the page slides in from the left.</param>
    public void Navigate(PageID pageID, bool isBack = false)
    {
        if (!pageSources.ContainsKey(pageID))
        {
            Debug.LogError($"[NavigationManager] No page registered for {pageID}.");
            return;
        }

        UiPage page = ResolvePage(pageID);
        if (page == null)
        {
            return;
        }

        UpdateHistory(pageID, isBack);
        Show(pageID, page, isBack);
    }

    /// <summary>Forgets every page opened so far and opens <paramref name="target"/> as the only one (after login or logout).</summary>
    public void ResetTo(PageID target)
    {
        pageHistory.Clear();
        Navigate(target);
    }

    /// <summary>Goes back one page (what the back arrow does). Returns false when there is no earlier page.</summary>
    public bool GoBack()
    {
        if (pageHistory.Count < 2)
        {
            return false;
        }

        Navigate(PreviousPage(), true);
        return true;
    }

    /// <summary>
    /// Opens <paramref name="target"/> as if it had been reached straight from <paramref name="anchor"/>: everything
    /// opened after the anchor is dropped from the history first. Used when a flow ends somewhere the user never was
    /// (finishing registration lands on Login with Welcome underneath), and by the bottom tabs.
    /// </summary>
    public void NavigateFrom(PageID anchor, PageID target)
    {
        if (pageHistory.Contains(anchor))
        {
            while (pageHistory.Peek() != anchor)
            {
                pageHistory.Pop();
            }
        }

        Navigate(target);
    }

    /// <summary>
    /// Returns to a page that is already in the history, dropping everything opened after it.
    /// Used when a flow ends (e.g. password reset back to Login) so Back does not walk through the finished flow.
    /// </summary>
    public bool ReturnTo(PageID target)
    {
        if (!pageSources.ContainsKey(target) || !pageHistory.Contains(target))
        {
            return false;
        }

        UiPage page = ResolvePage(target);
        if (page == null)
        {
            return false;
        }

        while (pageHistory.Peek() != target)
        {
            pageHistory.Pop();
        }

        Show(target, page, true);
        return true;
    }

    private static void UpdateHistory(PageID pageID, bool isBack)
    {
        if (isBack)
        {
            if (pageHistory.Count > 0)
            {
                pageHistory.Pop();
            }
        }
        else
        {
            pageHistory.Push(pageID);
        }
    }

    private void Show(PageID pageID, UiPage page, bool isBack)
    {
        if (currentPage != null && currentPage != page)
        {
            currentPage.Hide(isBack);
        }

        page.Show(isBack);
        currentPage = page;
        Debug.Log($"[NavigationManager] Opened {pageID}");
    }

    private UiPage ResolvePage(PageID pageID)
    {
        if (pageInstances.TryGetValue(pageID, out UiPage instance) && instance != null)
        {
            return instance;
        }

        if (!pageSources.TryGetValue(pageID, out UiPage source) || source == null)
        {
            return null;
        }

        instance = source;
        if (!source.gameObject.scene.IsValid())
        {
            // Prefab asset: instantiate once under AppCanvas/Pages and keep it for reuse.
            if (canvasPageContainer == null)
            {
                Debug.LogError($"[NavigationManager] canvasPageContainer is not assigned; cannot create page {pageID}.");
                return null;
            }

            instance = Instantiate(source, canvasPageContainer, false);
            instance.gameObject.SetActive(false);
        }

        instance.Attach(this);
        pageInstances[pageID] = instance;
        return instance;
    }

    private void HideAllPagesImmediate()
    {
        // Only touch pages that already exist; prefab pages are instantiated lazily by ResolvePage.
        foreach (KeyValuePair<PageID, UiPage> entry in pageSources)
        {
            UiPage page = entry.Value.gameObject.scene.IsValid()
                ? entry.Value
                : (pageInstances.TryGetValue(entry.Key, out UiPage created) ? created : null);
            if (page != null && page.gameObject.activeSelf)
            {
                page.HideImmediate();
            }
        }
    }
}
