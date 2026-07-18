using System.Collections;
using ARNav.Hybrid;
using Project.DestinationUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HybridGPSMap: dùng <b>Canvas Multiset (UI Home Screen)</b> làm HUD chính
/// cho cả Outdoor + Indoor — đúng style ảnh (Header xóa đường / Lịch sử,
/// minimap, TroLy, FAB search/capture, Điểm đến).
///
/// Việc làm:
///   1. Giữ UI Home Screen + Canvas Multiset bật khi đang AR (không chỉ Indoor)
///   2. Ẩn outdoor Mobile Navigation HUD chrome (status/dropdown) — tránh 2 UI đè
///   3. Wire nút "xóa chỉ đường" → clear hybrid path
///   4. Wire "Điểm đến" / ShowDestinations → BuildingDestinationListController unified list
///   5. Wire "Quay về" → thoát AR / MainScreen
///   6. Không tạo FAB lạ nếu Multiset đã có nút Điểm đến
/// </summary>
[DefaultExecutionOrder(250)]
public class MultisetCanvasPrimaryHud : MonoBehaviour
{
    [Tooltip("Mặc định TẮT — policy hiện tại dùng OutdoorUiPrimaryHud.")]
    [SerializeField] private bool enableOnHybridGpsMap = false;
    [SerializeField] private bool hideOutdoorHudChrome = true;
    [SerializeField] private bool forceMultisetCanvasInAr = true;
    [SerializeField] private bool wireHeaderButtons = true;
    [SerializeField] private bool preferMultisetMinimap = false;

    private GameObject _uiHomeScreen;
    private GameObject _multisetCanvas;
    private BuildingDestinationListController _destList;
    private bool _arActive;
    private bool _wired;

    // Auto-spawn disabled — OutdoorUiPrimaryHud owns HybridGPSMap UI policy.
    // [RuntimeInitializeOnLoadMethod] removed intentionally.

    private void Awake()
    {
        if (!enableOnHybridGpsMap || SceneManager.GetActiveScene().name != "HybridGPSMap")
        {
            enabled = false;
            return;
        }

        NavigationManager.OnAREntered += HandleArEntered;
        NavigationManager.OnARExited += HandleArExited;
        // Nếu đã trong AR (script load trễ)
        if (FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) == null)
            _arActive = true; // no MainScreen gate

        StartCoroutine(Bootstrap());
    }

    private void OnDestroy()
    {
        NavigationManager.OnAREntered -= HandleArEntered;
        NavigationManager.OnARExited -= HandleArExited;
    }

    private void HandleArEntered()
    {
        _arActive = true;
        Apply();
    }

    private void HandleArExited()
    {
        _arActive = false;
    }

    private IEnumerator Bootstrap()
    {
        yield return null;
        yield return null;
        ResolveRefs();
        Apply();
        yield return new WaitForSecondsRealtime(0.5f);
        ResolveRefs();
        Apply();
        yield return new WaitForSecondsRealtime(1f);
        Apply();
    }

    private void LateUpdate()
    {
        if (!_arActive || !forceMultisetCanvasInAr) return;
        // HybridModeController may re-hide indoorVisualRoot every mode change — re-assert Multiset HUD.
        KeepMultisetCanvasAlive();
    }

    [ContextMenu("Apply Multiset Primary HUD")]
    public void Apply()
    {
        ResolveRefs();
        if (!_arActive && FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null)
        {
            // Chưa vào AR — không ép canvas (MainScreen ưu tiên).
            return;
        }

        KeepMultisetCanvasAlive();

        if (hideOutdoorHudChrome)
            HideOutdoorChrome();

        if (wireHeaderButtons && !_wired)
            WireButtons();

        // Unified list + full Multiset UI khi indoor (không cắt).
        if (_destList != null)
            _destList.EnableUnifiedCatalogMode(true);

        // Mode switcher ẩn — auto hybrid.
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid != null)
        {
            hybrid.SetRuntimeModeSwitcherVisible(false);
            var f = typeof(HybridModeController).GetField("createRuntimeModeSwitcher",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null) f.SetValue(hybrid, false);
        }

        // Không cần FAB riêng nếu đã có Điểm đến Multiset / đã wire.
        DestroyExtraDestFabIfMultisetHasDestinations();

        Debug.Log("[MultisetCanvasPrimaryHud] Multiset Canvas is primary HUD for Outdoor+Indoor.");
    }

    private void ResolveRefs()
    {
        if (_uiHomeScreen == null)
            _uiHomeScreen = FindByName("UI Home Screen");
        if (_multisetCanvas == null && _uiHomeScreen != null)
        {
            var t = _uiHomeScreen.transform.Find("Canvas");
            if (t != null) _multisetCanvas = t.gameObject;
            else
            {
                var c = _uiHomeScreen.GetComponentInChildren<Canvas>(true);
                if (c != null) _multisetCanvas = c.gameObject;
            }
        }
        if (_destList == null)
            _destList = FindFirstObjectByType<BuildingDestinationListController>(FindObjectsInactive.Include);
    }

    private void KeepMultisetCanvasAlive()
    {
        if (_uiHomeScreen == null) ResolveRefs();
        if (_uiHomeScreen == null) return;

        // Parents (IndoorEnvironment…) must be active for canvas to show.
        Transform p = _uiHomeScreen.transform;
        while (p != null)
        {
            if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
            p = p.parent;
        }

        if (!_uiHomeScreen.activeSelf) _uiHomeScreen.SetActive(true);
        if (_multisetCanvas != null && !_multisetCanvas.activeSelf)
            _multisetCanvas.SetActive(true);

        // HybridModeController.SetCanvasesEnabled may set canvas.enabled=false in Outdoor.
        if (_multisetCanvas != null)
        {
            foreach (var c in _multisetCanvas.GetComponentsInChildren<Canvas>(true))
            {
                if (c != null && !c.enabled) c.enabled = true;
            }
        }
        else if (_uiHomeScreen != null)
        {
            foreach (var c in _uiHomeScreen.GetComponentsInChildren<Canvas>(true))
            {
                if (c != null && !c.enabled) c.enabled = true;
            }
        }
    }

    private void HideOutdoorChrome()
    {
        // Ẩn status + dropdown outdoor; giữ minimap nếu Multiset chưa có.
        var huds = FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            var hud = huds[i];
            if (hud == null) continue;
            if (hud.statusText != null)
            {
                var panel = hud.statusText.transform.parent;
                if (panel != null) panel.gameObject.SetActive(false);
                else hud.statusText.gameObject.SetActive(false);
            }
            if (hud.targetDropdown != null)
            {
                var p = hud.targetDropdown.transform.parent;
                if (p != null) p.gameObject.SetActive(false);
                else hud.targetDropdown.gameObject.SetActive(false);
            }
            if (hud.destinationSearchField != null)
                hud.destinationSearchField.gameObject.SetActive(false);
            if (hud.toastText != null)
                hud.toastText.gameObject.SetActive(true); // toast vẫn dùng được
        }

        // GPS accuracy circle optional hide for clean Multiset look
        var acc = GameObject.Find("GPS Accuracy Circle");
        if (acc != null) acc.SetActive(false);
    }

    private void WireButtons()
    {
        // ── Xóa chỉ đường / stop ─────────────────────────────────────────────
        WireButtonByNameContains(new[] { "xóa chỉ đường", "xoa chi duong", "stop", "Stop", "Clear Path", "Xóa chỉ đường" },
            OnClearPathClicked);

        // ── Điểm đến / Destinations ──────────────────────────────────────────
        WireButtonByNameContains(new[] { "Điểm đến", "Diem den", "ShowDestinations", "Destinations", "Destination" },
            OnDestinationsClicked);

        // Also Multiset ShowDestinationsButton
        var showDest = FindByName("ShowDestinationsButton");
        if (showDest != null)
        {
            var b = showDest.GetComponent<Button>();
            if (b != null)
            {
                b.onClick.RemoveListener(OnDestinationsClicked);
                b.onClick.AddListener(OnDestinationsClicked);
            }
        }

        // ── Quay về ──────────────────────────────────────────────────────────
        WireButtonByNameContains(new[] { "Quay về", "Quay ve", "Back", "Return" },
            OnBackClicked);

        // ── Lịch sử / Cài đặt — leave Multiset/default navigation if wired in scene

        _wired = true;
    }

    private void WireButtonByNameContains(string[] nameHints, UnityEngine.Events.UnityAction action)
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null || !btn.gameObject.scene.IsValid()) continue;
            string n = btn.gameObject.name;
            string label = "";
            var t = btn.GetComponentInChildren<Text>(true);
            if (t != null) label = t.text ?? "";
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null && string.IsNullOrEmpty(label)) label = tmp.text ?? "";

            bool match = false;
            for (int h = 0; h < nameHints.Length; h++)
            {
                string hint = nameHints[h];
                if (n.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = true;
                    break;
                }
            }
            if (!match) continue;

            // Avoid wiring mode switcher "Outdoor" etc. if any left
            if (label == "Indoor" || label == "Outdoor") continue;

            btn.onClick.RemoveListener(action);
            btn.onClick.AddListener(action);
            Debug.Log($"[MultisetCanvasPrimaryHud] Wired '{n}' / '{label}' → {action.Method.Name}");
        }
    }

    private void OnClearPathClicked()
    {
        var svc = HybridDestinationService.Instance ?? HybridDestinationService.EnsureExists();
        svc?.Clear();

        // Multiset stop if available
        try
        {
            if (NavigationUIController.instance != null)
                NavigationUIController.instance.ClickedStopButton();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MultisetCanvasPrimaryHud] Multiset stop: {ex.Message}");
        }

        var finders = FindObjectsByType<ARPathFinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < finders.Length; i++)
            finders[i]?.ClearNavigationVisuals();

        Debug.Log("[MultisetCanvasPrimaryHud] Cleared path (Xóa chỉ đường).");
    }

    private void OnDestinationsClicked()
    {
        if (_destList == null)
            _destList = FindFirstObjectByType<BuildingDestinationListController>(FindObjectsInactive.Include);
        if (_destList == null)
        {
            // Fallback Multiset ToggleDestinationSelectUI
            try { NavigationUIController.instance?.ToggleDestinationSelectUI(); }
            catch { /* ignore */ }
            return;
        }

        KeepMultisetCanvasAlive();
        // Ensure destination panel parents active
        if (_destList.destinationSelectUI != null)
        {
            Transform p = _destList.destinationSelectUI.transform;
            while (p != null)
            {
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                p = p.parent;
            }
        }

        _destList.EnableUnifiedCatalogMode(true);
        _destList.Toggle();
    }

    private void OnBackClicked()
    {
        // HybridModeController.ReturnToUI → ARPageController.SwitchObject → MainScreen + OnARExited
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid != null)
        {
            hybrid.ReturnToUI();
            return;
        }

        var arPage = FindFirstObjectByType<ARPageController>(FindObjectsInactive.Include);
        if (arPage != null)
        {
            arPage.SwitchObject();
            return;
        }

        var nav = FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include);
        if (nav != null && nav.ARPageObject != null)
        {
            nav.ARPageObject.SetActive(false);
            nav.gameObject.SetActive(true);
        }
    }

    private void DestroyExtraDestFabIfMultisetHasDestinations()
    {
        // HybridUiSync may have created "Destinations FAB" — keep one entry point only if Multiset has Destinations.
        var showDest = FindByName("ShowDestinationsButton");
        var fab = GameObject.Find("Destinations FAB");
        if (showDest != null && fab != null)
        {
            // Keep Multiset button; remove our FAB to avoid duplicate "Điểm đến"
            Object.Destroy(fab);
        }
    }

    private static GameObject FindByName(string name)
    {
        // Include inactive
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == name && t.gameObject.scene.IsValid())
                return t.gameObject;
        }
        return GameObject.Find(name);
    }
}
