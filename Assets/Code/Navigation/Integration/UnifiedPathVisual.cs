using System.Collections;
using ARNav.Hybrid;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Đồng bộ visual đường dẫn Indoor (Multiset <c>ShowPath</c> LineRenderer)
/// với Outdoor (<see cref="ARPathFinder"/> mesh ribbon chevron).
///
/// Policy HybridGPSMap:
///   1. <b>Một nguồn vẽ path</b>: ARPathFinder + HybridArPathFinderBridge (cả outdoor + indoor dest).
///   2. Tắt visual Multiset ShowPath (LineRenderer) để không còn 2 material khác nhau.
///   3. (Fallback) Nếu Multiset path vẫn bật: copy material/màu/width từ outdoor ribbon.
///
/// Multiset NavigationController / PathEstimation vẫn có thể chạy; chỉ ẩn renderer.
/// </summary>
[DefaultExecutionOrder(100)]
public class UnifiedPathVisual : MonoBehaviour
{
    public enum Mode
    {
        /// <summary>Chỉ ARPathFinder ribbon — khuyến nghị.</summary>
        SingleArPathFinder,
        /// <summary>Giữ Multiset LineRenderer nhưng style giống outdoor.</summary>
        StyleMultisetToMatchOutdoor,
    }

    [Header("Policy")]
    [SerializeField] private Mode mode = Mode.SingleArPathFinder;

    [Tooltip("Tắt HybridPathRenderer (line debug) nếu có — tránh path thứ 3.")]
    [SerializeField] private bool disableHybridDebugLine = true;

    [Header("Refs (auto)")]
    [SerializeField] private ARPathFinder arPathFinder;

    [Header("Multiset style match (fallback mode)")]
    [SerializeField] private float multisetLineWidth = 0.45f;
    [SerializeField] private Color multisetLineColor = new Color(0.05f, 0.45f, 1f, 0.96f);

    private bool _applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRun()
    {
        string n = SceneManager.GetActiveScene().name;
        if (n != "HybridGPSMap" && n != "Hybrid Navigation") return;
        if (FindFirstObjectByType<UnifiedPathVisual>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("UnifiedPathVisual");
        go.AddComponent<UnifiedPathVisual>();
    }

    private void Awake()
    {
        StartCoroutine(ApplyDeferred());
    }

    private void LateUpdate()
    {
        // Multiset may re-enable LineRenderer when SetPOIForNavigation runs — re-suppress.
        if (mode == Mode.SingleArPathFinder)
            SuppressMultisetShowPathVisual();
    }

    private IEnumerator ApplyDeferred()
    {
        yield return null;
        yield return null;
        Apply();
        yield return new WaitForSecondsRealtime(0.75f);
        Apply();
        yield return new WaitForSecondsRealtime(1.5f);
        Apply();
    }

    [ContextMenu("Apply Unified Path Visual")]
    public void Apply()
    {
        ResolveRefs();
        EnsureHybridBridge();

        if (disableHybridDebugLine)
            DisableHybridPathDebugLines();

        switch (mode)
        {
            case Mode.SingleArPathFinder:
                SuppressMultisetShowPathVisual();
                EnsureArPathFinderReady();
                break;
            case Mode.StyleMultisetToMatchOutdoor:
                StyleMultisetLineLikeOutdoor();
                EnsureArPathFinderReady();
                break;
        }

        _applied = true;
        Debug.Log($"[UnifiedPathVisual] Applied mode={mode}. Outdoor ribbon is the visual standard.");
    }

    private void ResolveRefs()
    {
        if (arPathFinder == null)
        {
            var outdoor = GameObject.Find("OutdoorEnvironment");
            if (outdoor != null)
                arPathFinder = outdoor.GetComponentInChildren<ARPathFinder>(true);
            if (arPathFinder == null)
                arPathFinder = FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
        }
    }

    private void EnsureHybridBridge()
    {
        // Bridge pushes HybridRouteCoordinator targets → ARPathFinder (indoor + outdoor).
        var bridge = FindFirstObjectByType<HybridArPathFinderBridge>(FindObjectsInactive.Include);
        if (bridge != null) return;

        if (arPathFinder != null)
        {
            if (arPathFinder.GetComponent<HybridArPathFinderBridge>() == null)
                arPathFinder.gameObject.AddComponent<HybridArPathFinderBridge>();
            Debug.Log("[UnifiedPathVisual] Added HybridArPathFinderBridge on ARPathFinder.");
        }
        else
        {
            var hub = GameObject.Find("Hybrid Hub") ?? gameObject;
            if (hub.GetComponent<HybridArPathFinderBridge>() == null)
                hub.AddComponent<HybridArPathFinderBridge>();
        }
    }

    private void EnsureArPathFinderReady()
    {
        if (arPathFinder == null) return;
        if (!arPathFinder.enabled) arPathFinder.enabled = true;
        if (!arPathFinder.gameObject.activeSelf) arPathFinder.gameObject.SetActive(true);
        // Path mesh root should stay under outdoor stack.
    }

    private void SuppressMultisetShowPathVisual()
    {
        // ShowPath is Multiset global type (no namespace).
        var showPaths = FindObjectsByType<ShowPath>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < showPaths.Length; i++)
        {
            var sp = showPaths[i];
            if (sp == null) continue;

            // Keep component alive for SDK internals, but kill drawing.
            var lr = sp.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.enabled = false;
                lr.positionCount = 0;
                // Zero width so even if re-enabled briefly, nearly invisible until we catch it.
                lr.startWidth = 0f;
                lr.endWidth = 0f;
            }

            // Disable ShowPath.Update drawing loop — path estimation via Multiset may stop;
            // hybrid outdoor HUD has its own distance. Prefer clean single visual.
            if (sp.enabled) sp.enabled = false;
        }

        // Any other LineRenderer under NavigationController GO that looks like a path
        var navCtrl = FindFirstObjectByType<NavigationController>(FindObjectsInactive.Include);
        if (navCtrl != null)
        {
            var lrs = navCtrl.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lrs.Length; i++)
            {
                if (lrs[i] == null) continue;
                // Don't touch UI lines
                if (lrs[i].GetComponentInParent<Canvas>() != null) continue;
                lrs[i].enabled = false;
            }
        }
    }

    private void StyleMultisetLineLikeOutdoor()
    {
        Material shared = null;
        if (arPathFinder != null)
            shared = arPathFinder.GetOrCreateSharedPathMaterial();

        if (shared == null)
        {
            shared = NavigationPathMaterialHelper.CreateDefaultPathMaterial(multisetLineColor);
            var tex = NavigationPathMaterialHelper.CreateChevronStripTexture();
            if (shared != null && tex != null)
                NavigationPathMaterialHelper.SetMaterialMainTexture(shared, tex);
            NavigationPathMaterialHelper.Configure(shared, alwaysOnTop: true);
        }

        var showPaths = FindObjectsByType<ShowPath>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < showPaths.Length; i++)
        {
            var sp = showPaths[i];
            if (sp == null) continue;
            if (!sp.enabled) sp.enabled = true;

            var lr = sp.GetComponent<LineRenderer>();
            if (lr == null) continue;

            lr.enabled = true;
            lr.widthMultiplier = 1f;
            lr.startWidth = multisetLineWidth;
            lr.endWidth = multisetLineWidth * 0.85f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.textureMode = LineTextureMode.Tile;
            lr.alignment = LineAlignment.View;
            if (shared != null)
            {
                lr.sharedMaterial = shared;
                lr.startColor = Color.white;
                lr.endColor = Color.white;
            }
            else
            {
                lr.startColor = multisetLineColor;
                lr.endColor = multisetLineColor;
            }
        }

        // NavigationUIController.material used for _PathLength — sync if present
        if (NavigationUIController.instance != null && shared != null)
        {
            try
            {
                var f = typeof(NavigationUIController).GetField("material");
                if (f != null) f.SetValue(NavigationUIController.instance, shared);
            }
            catch { /* ignore */ }
        }
    }

    private static void DisableHybridPathDebugLines()
    {
        var lines = FindObjectsByType<HybridPathRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null) continue;
            lines[i].enabled = false;
            var lr = lines[i].GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;
        }
    }
}
