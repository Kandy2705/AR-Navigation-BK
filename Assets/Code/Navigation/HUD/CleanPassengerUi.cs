using System.Collections;
using ARNav.Hybrid;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dọn UI "tùm lum" trên HybridGPSMap: tắt debug overlay chồng panel,
/// gọn status HUD, ẩn calibrate/debug.
///
/// Auto chạy AfterSceneLoad. Dev muốn debug lại: tắt component này
/// hoặc set <see cref="enableCleanUi"/> = false trên instance.
/// </summary>
[DefaultExecutionOrder(200)]
public class CleanPassengerUi : MonoBehaviour
{
    [SerializeField] private bool enableCleanUi = true;

    [Tooltip("Tắt các overlay OnGUI / diagnose (Hybrid state, Multiset pose, OnScreenDebug…).")]
    [SerializeField] private bool disableDebugOverlays = true;

    [Tooltip("Gọn MobileNavigationHUD: bỏ path debug line, status ngắn.")]
    [SerializeField] private bool compactNavigationHud = true;

    [Tooltip("Ẩn Shared AR UI (chat/prompt) nếu đang chồng outdoor HUD.")]
    [SerializeField] private bool hideSharedArUi = true;

    [Tooltip("Ẩn dòng status trên Runtime Mode Switcher (Indoor/Outdoor/Back).")]
    [SerializeField] private bool hideModeSwitcherStatusLine = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRun()
    {
        string n = SceneManager.GetActiveScene().name;
        if (n != "HybridGPSMap" && n != "Hybrid Navigation") return;
        if (FindFirstObjectByType<CleanPassengerUi>(FindObjectsInactive.Include) != null) return;

        var go = new GameObject("CleanPassengerUi");
        go.AddComponent<CleanPassengerUi>();
    }

    private void Awake()
    {
        if (enableCleanUi)
            StartCoroutine(ApplyDeferred());
    }

    private bool _arActive;

    private void OnEnable()
    {
        NavigationManager.OnAREntered += HandleArEntered;
        NavigationManager.OnARExited += HandleArExited;
        _arActive = FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) == null;
    }

    private void OnDisable()
    {
        NavigationManager.OnAREntered -= HandleArEntered;
        NavigationManager.OnARExited -= HandleArExited;
    }

    private void HandleArEntered()
    {
        _arActive = true;
        Apply();
    }

    private void HandleArExited() => _arActive = false;

    private IEnumerator ApplyDeferred()
    {
        // Chờ các RuntimeInitialize + Awake khác spawn HUD/overlay.
        yield return null;
        yield return null;
        // Debug overlays có thể dọn ngay; compact HUD chỉ khi AR.
        if (disableDebugOverlays)
            DisableDebugOverlays();
        if (_arActive) Apply();
        yield return new WaitForSecondsRealtime(0.5f);
        if (disableDebugOverlays)
            DisableDebugOverlays();
        if (_arActive) Apply();
    }

    [ContextMenu("Apply Clean Passenger UI")]
    public void Apply()
    {
        if (!enableCleanUi) return;

        if (disableDebugOverlays)
            DisableDebugOverlays();

        // Không ép HUD outdoor khi đang MainScreen.
        if (!_arActive && FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null)
            return;

        if (compactNavigationHud)
            CompactNavigationHud();

        if (hideSharedArUi)
            HideSharedArUi();

        if (hideModeSwitcherStatusLine)
            HideModeSwitcherExtras();

        Debug.Log("[CleanPassengerUi] Applied — debug overlays off, HUD compact.");
    }

    private static void DisableDebugOverlays()
    {
        // Hybrid OnGUI overlays
        DisableAllOfType<HybridStateDebugOverlay>();
        DisableAllOfType<MultisetPoseProviderDebugOverlay>();
        DisableAllOfType<HybridRuntimeDiagnose>();
        DisableAllOfType<OnScreenDebugOverlay>();
        DisableAllOfType<GPSMapWorldDebugOverlay>();

        // HybridScenarioRunner overlay (if left in scene)
        DisableAllOfType<HybridScenarioRunner>();

        // Hide GO named like debug panels
        string[] hideNames =
        {
            "GPSMapWorldDebugCanvas",
            "HybridStateDebugOverlay",
            "MultisetPoseProviderDebugOverlay",
            "OnScreenDebugOverlay",
            "HybridRuntimeDiagnose",
            "EnvironmentTransformDebugOverlay",
        };
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            for (int h = 0; h < hideNames.Length; h++)
            {
                if (t.name == hideNames[h] && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    private static void DisableAllOfType<T>() where T : Behaviour
    {
        var list = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null) list[i].enabled = false;
        }
    }

    private static void CompactNavigationHud()
    {
        var huds = FindObjectsByType<MobileNavigationHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null) huds[i].ApplyPassengerCleanMode();
        }
    }

    private static void HideSharedArUi()
    {
        var shared = FindObjectsByType<SharedARUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shared.Length; i++)
        {
            if (shared[i] == null) continue;
            shared[i].enabled = false;
            // Root runtime object if any
            var cg = shared[i].GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
            // Common runtime name from SharedARUIController
            var root = GameObject.Find("Shared AR UI");
            if (root != null) root.SetActive(false);
        }
    }

    private static void HideModeSwitcherExtras()
    {
        // Status line under runtime mode switcher canvas (if present)
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid == null) return;

        // Find child Text named Status under runtime switcher canvases
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (c.sortingOrder < 5000 || c.sortingOrder > 5600) continue;
            var texts = c.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            for (int t = 0; t < texts.Length; t++)
            {
                if (texts[t] != null && texts[t].gameObject.name == "Status")
                    texts[t].gameObject.SetActive(false);
            }
        }
    }
}
