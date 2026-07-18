using System.Collections;
using Project.DestinationUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// HybridGPSMap: outdoor HUD chuẩn + chỉ hiện khi tab <b>AR chỉ đường</b> (OnAREntered).
///   - Không auto chọn đích
///   - Sửa input (EventSystem / GraphicRaycaster / bỏ canvas chặn bấm)
///   - Ẩn Multiset chrome; VPS stack giữ khi Indoor
/// </summary>
[DefaultExecutionOrder(260)]
public class OutdoorUiPrimaryHud : MonoBehaviour
{
    [SerializeField] private bool enableOnHybridGpsMap = true;
    [SerializeField] private bool showOutdoorHudInBothModes = true;
    [SerializeField] private bool hideMultisetChrome = true;
    [SerializeField] private bool hideRuntimeModeSwitcher = true;
    [SerializeField] private bool keepMinimap = true;

    private bool _arActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRun()
    {
        if (SceneManager.GetActiveScene().name != "HybridGPSMap") return;
        if (FindFirstObjectByType<OutdoorUiPrimaryHud>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("OutdoorUiPrimaryHud");
        go.AddComponent<OutdoorUiPrimaryHud>();
    }

    private void Awake()
    {
        if (!enableOnHybridGpsMap || SceneManager.GetActiveScene().name != "HybridGPSMap")
        {
            enabled = false;
            return;
        }

        var multi = FindFirstObjectByType<MultisetCanvasPrimaryHud>(FindObjectsInactive.Include);
        if (multi != null)
        {
            multi.enabled = false;
            multi.gameObject.SetActive(false);
        }

        // Có NavigationManager → chỉ hiện HUD khi EnterARPage (tab AR chỉ đường).
        bool hasNav = FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null;
        _arActive = !hasNav;

        NavigationManager.OnAREntered += OnArEntered;
        NavigationManager.OnARExited += OnArExited;

        // Lúc boot: ẩn HUD AR nếu đang MainScreen.
        if (hasNav && !_arActive)
            HideArNavigationUi();

        StartCoroutine(ApplyDeferred());
    }

    private void OnDestroy()
    {
        NavigationManager.OnAREntered -= OnArEntered;
        NavigationManager.OnARExited -= OnArExited;
    }

    private void OnArEntered()
    {
        _arActive = true;
        Apply();
        // Idle path — không auto chỉ đường.
        foreach (var hud in FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hud?.ShowIdleNoDestination();
    }

    private void OnArExited()
    {
        _arActive = false;
        HideArNavigationUi();
    }

    private IEnumerator ApplyDeferred()
    {
        yield return null;
        yield return null;
        if (_arActive) Apply();
        else HideArNavigationUi();
        yield return new WaitForSecondsRealtime(0.4f);
        if (_arActive) Apply();
        else HideArNavigationUi();
    }

    private void LateUpdate()
    {
        if (!_arActive) return;
        EnsureOutdoorHudVisible();
        EnsureUiClickable();
        if (hideMultisetChrome)
            HideMultisetChromeUi();
    }

    [ContextMenu("Apply Outdoor UI Primary")]
    public void Apply()
    {
        if (SceneManager.GetActiveScene().name != "HybridGPSMap") return;
        if (!_arActive)
        {
            HideArNavigationUi();
            return;
        }

        EnsureEventSystem();
        EnsureOutdoorHudVisible();
        ShowOutdoorPickerChrome();
        EnsureUiClickable();
        if (hideMultisetChrome)
            HideMultisetChromeUi();
        if (hideRuntimeModeSwitcher)
            SuppressModeSwitcher();
        if (keepMinimap)
            EnsureMinimap();

        var dest = FindFirstObjectByType<BuildingDestinationListController>(FindObjectsInactive.Include);
        dest?.EnableUnifiedCatalogMode(true);

        var root = FindFirstObjectByType<HybridOutdoorNavigationRoot>(FindObjectsInactive.Include);
        root?.SetKeepMinimapWhenIndoor(true);

        Debug.Log("[OutdoorUiPrimaryHud] AR tab active — outdoor HUD shown, idle until user picks destination.");
    }

    /// <summary>Ẩn toàn bộ UI nav khi không ở tab AR chỉ đường.</summary>
    private void HideArNavigationUi()
    {
        string[] hideNames =
        {
            "Mobile Navigation HUD",
            "OutdoorNavigationUI",
            "Minimap Canvas",
            "GPS Accuracy Circle",
            "Destinations FAB",
            "DestinationsFabCanvas",
            "ArrivalBannerCanvas",
        };
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            for (int i = 0; i < hideNames.Length; i++)
            {
                if (t.name == hideNames[i] && t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
            }
        }

        // Multiset canvas chrome cũng ẩn khi không AR
        HideMultisetChromeUi();
        SuppressModeSwitcher();
    }

    private void EnsureOutdoorHudVisible()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.name == "OutdoorNavigationUI" || t.name == "Mobile Navigation HUD")
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }

        var huds = FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] == null) continue;
            if (!huds[i].gameObject.activeSelf) huds[i].gameObject.SetActive(true);
            huds[i].enabled = true;
            huds[i].ApplyPassengerCleanMode();
        }
    }

    private void ShowOutdoorPickerChrome()
    {
        var huds = FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            var hud = huds[i];
            if (hud == null) continue;
            if (hud.targetDropdown != null)
            {
                hud.targetDropdown.gameObject.SetActive(true);
                var p = hud.targetDropdown.transform.parent;
                if (p != null) p.gameObject.SetActive(true);
                // Dropdown cần interactable + raycast
                hud.targetDropdown.interactable = true;
                var img = hud.targetDropdown.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
            }
            if (hud.destinationSearchField != null)
            {
                hud.destinationSearchField.gameObject.SetActive(true);
                hud.destinationSearchField.interactable = true;
            }
            if (hud.statusText != null)
            {
                hud.statusText.gameObject.SetActive(true);
                var p = hud.statusText.transform.parent;
                if (p != null) p.gameObject.SetActive(true);
            }
            // Rebuild list nhưng GIỮ idle (không auto dest).
            hud.RebuildDestinationList();
            if (!hud.HasActiveDestination)
                hud.ShowIdleNoDestination();
        }
    }

    /// <summary>Sửa UI không bấm được: EventSystem + GraphicRaycaster + bỏ canvas chặn.</summary>
    private void EnsureUiClickable()
    {
        EnsureEventSystem();

        // Outdoor HUD canvases must receive raycasts.
        var huds = FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] == null) continue;
            var canvas = huds[i].GetComponentInParent<Canvas>(true);
            if (canvas == null) canvas = huds[i].GetComponent<Canvas>();
            if (canvas == null) continue;

            canvas.enabled = true;
            if (canvas.renderMode == RenderMode.WorldSpace)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // High enough to be above map, below blocking system dialogs
            if (canvas.sortingOrder < 100) canvas.sortingOrder = 400;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            var cg = canvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = true;
                cg.interactable = true;
                if (cg.alpha < 0.01f) cg.alpha = 1f;
            }
        }

        // Multiset / dim canvases must NOT steal clicks when chrome hidden.
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null || !c.gameObject.scene.IsValid()) continue;
            bool underIndoor = false;
            for (var p = c.transform; p != null; p = p.parent)
            {
                if (p.name == "UI Home Screen" || p.name == "IndoorEnvironment")
                {
                    underIndoor = true;
                    break;
                }
            }
            if (!underIndoor) continue;

            // Disable raycaster on Multiset UI while outdoor primary
            var gr = c.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.enabled = false;
            var cg = c.GetComponent<CanvasGroup>();
            if (cg == null) cg = c.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;
            c.enabled = false;
        }

        // Arrival banner when hidden
        var banners = FindObjectsByType<ArrivalBanner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < banners.Length; i++)
        {
            if (banners[i] != null && !banners[i].IsVisible)
                banners[i].Hide();
        }
    }

    private static void EnsureEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            es = go.GetComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            if (go.GetComponent<InputSystemUIInputModule>() == null
                && go.GetComponent<StandaloneInputModule>() == null)
                go.AddComponent<InputSystemUIInputModule>();
#else
            if (go.GetComponent<StandaloneInputModule>() == null)
                go.AddComponent<StandaloneInputModule>();
#endif
        }
        else
        {
            if (!es.gameObject.activeSelf) es.gameObject.SetActive(true);
            if (!es.enabled) es.enabled = true;
#if ENABLE_INPUT_SYSTEM
            if (es.GetComponent<InputSystemUIInputModule>() == null
                && es.GetComponent<StandaloneInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
#endif
        }
    }

    private void HideMultisetChromeUi()
    {
        string[] hideExact =
        {
            "Header", "TroLyChan", "InputChatBot", "UI ChatBot", "CaptureButton",
            "ShowDestinationsButton", "NavigationUI", "ToastPanel",
            "Destination Select", "DestinationSelectUI", "DestinationList",
        };

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            bool underIndoor = false;
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == "UI Home Screen" || p.name == "IndoorEnvironment")
                {
                    underIndoor = true;
                    break;
                }
            }
            if (!underIndoor) continue;

            if (t.name == "Map Space" || t.name == "MapLocalizationManager" || t.name == "XR Origin"
                || t.name == "AR Session" || t.name.StartsWith("MapB") || t.name.StartsWith("POIs")
                || t.name == "MultiSetSDKManager" || t.name == "NavigationController")
                continue;

            for (int i = 0; i < hideExact.Length; i++)
            {
                if (t.name == hideExact[i] && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    break;
                }
            }

            if (t.name == "Canvas" && t.parent != null && t.parent.name == "UI Home Screen")
            {
                var c = t.GetComponent<Canvas>();
                if (c != null) c.enabled = false;
                var gr = t.GetComponent<GraphicRaycaster>();
                if (gr != null) gr.enabled = false;
                var cg = t.GetComponent<CanvasGroup>();
                if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
        }

        var fab = GameObject.Find("Destinations FAB");
        if (fab != null) Object.Destroy(fab);
        var fabCanvas = GameObject.Find("DestinationsFabCanvas");
        if (fabCanvas != null) Object.Destroy(fabCanvas);
    }

    private static void SuppressModeSwitcher()
    {
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid != null)
        {
            hybrid.SetRuntimeModeSwitcherVisible(false);
            var field = typeof(HybridModeController).GetField("createRuntimeModeSwitcher",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(hybrid, false);
        }
    }

    private static void EnsureMinimap()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.name == "Minimap Canvas" || t.name == "Minimap Circle Mask" || t.name == "Minimap")
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }
        MinimapHeadingIndicator.TryEnsureForActiveScene();
    }
}
