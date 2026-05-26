using UnityEngine;

/// <summary>
/// Enables or disables the outdoor navigation UI stack when <see cref="HybridModeController"/> enters or leaves Outdoor.
/// Place on an always-active object (e.g. same object as <see cref="HybridModeController"/>).
/// Optionally assign <see cref="outdoorHudVisualSubtree"/> so only the HUD canvases toggle while
/// <see cref="ARPathFinder"/> (placed outside that subtree) keeps updating across all modes.
///
/// Khi scene có <see cref="NavigationManager"/> (MainScreen + AR flow):
///   - Outdoor nav bắt đầu HIDDEN hoàn toàn.
///   - Chỉ hiện khi <see cref="NavigationManager.OnAREntered"/> được fire (user chọn chức năng AR).
///   - Ẩn lại khi <see cref="NavigationManager.OnARExited"/> được fire (quay về MainScreen).
/// </summary>
[DisallowMultipleComponent]
public class HybridOutdoorNavigationRoot : MonoBehaviour
{
    [SerializeField] private HybridModeController hybridModeController;
    [Tooltip("Outdoor nav stack root. Kept active as the path/host parent; only shown/hidden via mode if outdoorHudVisualSubtree is unset.")]
    [SerializeField] private GameObject outdoorNavigationContentRoot;
    [Tooltip("Optional. HUD canvases only — hybrid mode toggles this subtree, not the whole outdoorNavigationContentRoot. Keep ARPathFinder outside this subtree so path updates in all modes.")]
    [SerializeField] private GameObject outdoorHudVisualSubtree;

    private HybridModeController.HybridMode _lastMode = (HybridModeController.HybridMode)(-1);

    // NavigationManager-based AR gating
    private bool _navManagerPresent = false;
    private bool _arActive          = false;

    private GameObject ToggleTarget => outdoorHudVisualSubtree != null ? outdoorHudVisualSubtree : outdoorNavigationContentRoot;
    private bool UsesSplitHudToggle => outdoorHudVisualSubtree != null && outdoorNavigationContentRoot != null;

    private Transform OverlaySearchTransform =>
        outdoorHudVisualSubtree != null ? outdoorHudVisualSubtree.transform
        : outdoorNavigationContentRoot != null ? outdoorNavigationContentRoot.transform : null;

    private void Awake()
    {
        if (hybridModeController == null)
        {
            hybridModeController = GetComponent<HybridModeController>();
            if (hybridModeController == null)
                hybridModeController = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }

        if (ToggleTarget == null)
            return;

        // ──────────────────────────────────────────────────────────────────────
        // NavigationManager present → outdoor nav gated by AR page selection.
        // Bắt đầu HIDDEN hoàn toàn; chỉ hiện khi user bấm vào chức năng AR.
        // ──────────────────────────────────────────────────────────────────────
        if (FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null)
        {
            _navManagerPresent = true;
            HideAll();
            NavigationManager.OnAREntered += HandleAREntered;
            NavigationManager.OnARExited  += HandleARExited;
            return;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Không có NavigationManager → dùng logic cũ dựa vào HybridModeController.
        // ──────────────────────────────────────────────────────────────────────

        // Outdoor-only scenes: activate everything and leave it.
        if (hybridModeController != null && !hybridModeController.HasAssignedIndoorEnvironment)
        {
            if (outdoorNavigationContentRoot != null)
                outdoorNavigationContentRoot.SetActive(true);
            if (outdoorHudVisualSubtree != null)
                outdoorHudVisualSubtree.SetActive(true);
            else
                ToggleTarget.SetActive(true);
            return;
        }

        // Hybrid scene: start with outdoor nav hidden until mode is confirmed Outdoor.
        if (UsesSplitHudToggle)
        {
            outdoorNavigationContentRoot.SetActive(true);
            outdoorHudVisualSubtree.SetActive(false);
        }
        else
        {
            ToggleTarget.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        NavigationManager.OnAREntered -= HandleAREntered;
        NavigationManager.OnARExited  -= HandleARExited;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // NavigationManager event handlers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>User đã chọn chức năng AR → bật outdoor nav.</summary>
    private void HandleAREntered()
    {
        _arActive = true;
        // Reset để LateUpdate re-evaluate mode ngay frame kế tiếp.
        _lastMode = (HybridModeController.HybridMode)(-1);

        // Outdoor-only scene hoặc không có HybridModeController: bật thẳng.
        if (hybridModeController == null || !hybridModeController.HasAssignedIndoorEnvironment)
        {
            if (outdoorNavigationContentRoot != null)
                outdoorNavigationContentRoot.SetActive(true);
            if (outdoorHudVisualSubtree != null)
                outdoorHudVisualSubtree.SetActive(true);
            else if (ToggleTarget != null)
                ToggleTarget.SetActive(true);
        }
        // Hybrid scene: LateUpdate sẽ toggle dựa theo CurrentMode.

        EnsureOutdoorPathFindersActive();
        MinimapHeadingIndicator.TryEnsureForActiveScene();

        if (OverlaySearchTransform != null)
        {
            foreach (GPSStartupOverlay overlay in OverlaySearchTransform.GetComponentsInChildren<GPSStartupOverlay>(true))
                overlay.RestartSessionForHybridReentry();
        }
    }

    /// <summary>Quay về MainScreen → ẩn toàn bộ outdoor nav.</summary>
    private void HandleARExited()
    {
        _arActive = false;
        HideAll();
        _lastMode = (HybridModeController.HybridMode)(-1); // reset cho lần vào AR tiếp theo
    }

    private void HideAll()
    {
        if (outdoorNavigationContentRoot != null)
            outdoorNavigationContentRoot.SetActive(false);
        if (outdoorHudVisualSubtree != null)
            outdoorHudVisualSubtree.SetActive(false);
        else if (ToggleTarget != null)
            ToggleTarget.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LateUpdate — chỉ chạy khi không có NavigationManager, hoặc khi AR đang active
    // ──────────────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (hybridModeController == null || ToggleTarget == null)
            return;

        // Nếu NavigationManager đang quản lý và AR chưa được bật → giữ ẩn.
        if (_navManagerPresent && !_arActive)
            return;

        // Outdoor-only scenes: Awake already activated everything permanently.
        if (!hybridModeController.HasAssignedIndoorEnvironment)
            return;

        HybridModeController.HybridMode mode = hybridModeController.CurrentMode;
        if (mode == _lastMode)
            return;

        bool wasOutdoor = _lastMode == HybridModeController.HybridMode.Outdoor;
        bool nowOutdoor = mode == HybridModeController.HybridMode.Outdoor;
        _lastMode = mode;

        if (UsesSplitHudToggle)
        {
            outdoorNavigationContentRoot.SetActive(true);
            outdoorHudVisualSubtree.SetActive(nowOutdoor);
        }
        else
        {
            ToggleTarget.SetActive(nowOutdoor);
        }

        if (nowOutdoor && !wasOutdoor)
        {
            EnsureOutdoorPathFindersActive();
            MinimapHeadingIndicator.TryEnsureForActiveScene();

            if (OverlaySearchTransform != null)
            {
                foreach (GPSStartupOverlay overlay in OverlaySearchTransform.GetComponentsInChildren<GPSStartupOverlay>(true))
                    overlay.RestartSessionForHybridReentry();
            }
        }
    }

    private void EnsureOutdoorPathFindersActive()
    {
        foreach (ARPathFinder finder in FindObjectsByType<ARPathFinder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (finder == null) continue;
            if (!finder.enabled) finder.enabled = true;
            if (!finder.gameObject.activeSelf) finder.gameObject.SetActive(true);
        }
    }
}
