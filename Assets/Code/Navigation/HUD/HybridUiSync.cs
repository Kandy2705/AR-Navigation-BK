using System.Collections;
using Project.DestinationUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Đồng bộ UI Outdoor/Indoor (HybridGPSMap). Policy hiện tại:
///   - <b>Outdoor HUD primary</b> (MobileNavigationHUD) — xem <see cref="OutdoorUiPrimaryHud"/>
///   - 1 list đích chung trong dropdown outdoor
///   - Minimap cả 2 mode
///   - Ẩn mode switcher
/// Component này giữ tương thích; logic chi tiết do OutdoorUiPrimaryHud xử lý.
/// </summary>
[DefaultExecutionOrder(220)]
public class HybridUiSync : MonoBehaviour
{
    [SerializeField] private bool enableSync = true;

    [Header("Policy (outdoor primary)")]
    [SerializeField] private bool hideRuntimeModeSwitcher = true;
    [SerializeField] private bool preferIndoorDestinationList = false;
    [SerializeField] private bool hideOutdoorDropdownAndSearch = false;
    [SerializeField] private bool ensureDestinationsFab = false;
    [SerializeField] private bool keepMinimapBothModes = true;

    private Button _destFab;
    private BuildingDestinationListController _destList;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRun()
    {
        if (SceneManager.GetActiveScene().name != "HybridGPSMap") return;
        if (FindFirstObjectByType<HybridUiSync>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("HybridUiSync");
        go.AddComponent<HybridUiSync>();
    }

    private void Awake()
    {
        if (enableSync) StartCoroutine(ApplyDeferred());
    }

    private bool _arActive;

    private void OnEnable()
    {
        NavigationManager.OnAREntered += OnAr;
        NavigationManager.OnARExited += OnExit;
        _arActive = FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) == null;
    }

    private void OnDisable()
    {
        NavigationManager.OnAREntered -= OnAr;
        NavigationManager.OnARExited -= OnExit;
    }

    private void OnAr() { _arActive = true; Apply(); }
    private void OnExit() { _arActive = false; }

    private IEnumerator ApplyDeferred()
    {
        yield return null;
        yield return null;
        if (_arActive) Apply();
        yield return new WaitForSecondsRealtime(0.6f);
        if (_arActive) Apply();
    }

    [ContextMenu("Apply Hybrid UI Sync")]
    public void Apply()
    {
        if (!enableSync) return;
        if (SceneManager.GetActiveScene().name != "HybridGPSMap") return;
        // Chỉ khi tab AR — OutdoorUiPrimaryHud là owner chính.
        if (!_arActive && FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null)
            return;

        if (hideRuntimeModeSwitcher)
            SuppressModeSwitcher();

        if (preferIndoorDestinationList)
            ConfigureDestinationList();

        if (hideOutdoorDropdownAndSearch)
            HideOutdoorPickerChrome();

        if (keepMinimapBothModes)
            ConfigureMinimapBothModes();

        if (ensureDestinationsFab)
            EnsureDestinationsFab();

        Debug.Log("[HybridUiSync] Applied (compat) — outdoor primary owned by OutdoorUiPrimaryHud.");
    }

    private static void SuppressModeSwitcher()
    {
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid != null)
        {
            hybrid.SetRuntimeModeSwitcherVisible(false);
            // Prevent re-show from AR entered handlers.
            var field = typeof(HybridModeController).GetField("createRuntimeModeSwitcher",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(hybrid, false);
        }

        // Hide any already-built switcher canvases (sortingOrder ~5400).
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (c.sortingOrder >= 5300 && c.sortingOrder <= 5600)
            {
                // Heuristic: runtime mode switcher
                string n = c.gameObject.name;
                if (n.IndexOf("Mode", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Runtime", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || c.GetComponentInChildren<Button>(true) != null && c.transform.childCount <= 3)
                {
                    // Only kill if it has Indoor/Outdoor buttons
                    bool isModeBar = false;
                    foreach (var t in c.GetComponentsInChildren<Text>(true))
                    {
                        if (t != null && (t.text == "Indoor" || t.text == "Outdoor" || t.text.Contains("Quay")))
                        {
                            isModeBar = true;
                            break;
                        }
                    }
                    if (isModeBar) c.gameObject.SetActive(false);
                }
            }
        }
    }

    private void ConfigureDestinationList()
    {
        _destList = FindFirstObjectByType<BuildingDestinationListController>(FindObjectsInactive.Include);
        if (_destList == null) return;
        _destList.EnableUnifiedCatalogMode(true);
    }

    private static void HideOutdoorPickerChrome()
    {
        var huds = FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            var hud = huds[i];
            if (hud == null) continue;
            hud.ApplyPassengerCleanMode();
            // Hide dropdown + search — list Destinations thay thế.
            if (hud.targetDropdown != null)
            {
                var p = hud.targetDropdown.transform.parent;
                if (p != null && p.name.IndexOf("Dropdown", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    p.gameObject.SetActive(false);
                else
                    hud.targetDropdown.gameObject.SetActive(false);
            }
            if (hud.destinationSearchField != null)
                hud.destinationSearchField.gameObject.SetActive(false);
        }
    }

    private static void ConfigureMinimapBothModes()
    {
        var root = FindFirstObjectByType<HybridOutdoorNavigationRoot>(FindObjectsInactive.Include);
        if (root != null)
            root.SetKeepMinimapWhenIndoor(true);

        // Force minimap GO active if present.
        string[] names = { "Minimap Canvas", "Minimap", "Minimap Circle Mask", "OutdoorNavigationUI" };
        for (int i = 0; i < names.Length; i++)
        {
            var go = GameObject.Find(names[i]);
            if (go != null && !go.activeSelf) go.SetActive(true);
        }

        // Also search inactive
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.name == "Minimap Canvas" || t.name == "Minimap Circle Mask")
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }

        MinimapHeadingIndicator.TryEnsureForActiveScene();
    }

    private void EnsureDestinationsFab()
    {
        _destList = FindFirstObjectByType<BuildingDestinationListController>(FindObjectsInactive.Include);
        if (_destList == null) return;

        // If Multiset already has ShowDestinationsButton visible, still add outdoor FAB
        // so outdoor mode can open the same list without IndoorEnvironment chrome.
        if (_destFab != null) return;

        Canvas host = null;
        var hud = FindFirstObjectByType<MobileNavigationHUD>(FindObjectsInactive.Include);
        if (hud != null) host = hud.GetComponentInParent<Canvas>();
        if (host == null)
        {
            var outdoorUi = GameObject.Find("OutdoorNavigationUI");
            if (outdoorUi != null) host = outdoorUi.GetComponentInChildren<Canvas>(true);
        }
        if (host == null)
        {
            // Create overlay canvas for FAB
            var canvasGo = new GameObject("DestinationsFabCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            host = canvasGo.GetComponent<Canvas>();
            host.renderMode = RenderMode.ScreenSpaceOverlay;
            host.sortingOrder = 5200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        var go = new GameObject("Destinations FAB", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(host.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 140f);
        rt.sizeDelta = new Vector2(200f, 72f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.45f, 0.85f, 0.94f);

        _destFab = go.GetComponent<Button>();
        _destFab.targetGraphic = img;
        _destFab.onClick.AddListener(OpenUnifiedDestinationList);

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var txt = labelGo.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.text = "Điểm đến";
        txt.fontSize = 30;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
    }

    private void OpenUnifiedDestinationList()
    {
        if (_destList == null)
            _destList = FindFirstObjectByType<BuildingDestinationListController>(FindObjectsInactive.Include);
        if (_destList == null)
        {
            Debug.LogWarning("[HybridUiSync] BuildingDestinationListController not found.");
            return;
        }

        // Ensure destination UI hierarchy can activate even if IndoorEnvironment was off.
        EnsureDestinationUiActivatable(_destList);
        _destList.EnableUnifiedCatalogMode(true);
        _destList.Toggle();
    }

    private static void EnsureDestinationUiActivatable(BuildingDestinationListController list)
    {
        if (list == null) return;
        // Walk up parents and enable until scene root (so Toggle can show panel).
        Transform t = list.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
        if (list.destinationSelectUI != null)
        {
            // Keep closed until Toggle; just ensure parents live.
            var p = list.destinationSelectUI.transform.parent;
            while (p != null)
            {
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                p = p.parent;
            }
        }
    }
}
