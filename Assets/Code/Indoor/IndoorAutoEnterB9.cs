using UnityEngine;

/// <summary>
/// Observer cho hybrid mode: phát hiện edge Outdoor → Indoor, tự động:
///   1. Gọi <see cref="IndoorMapSwitcher.SwitchTo"/>(B9) trước khi cloud localize
///      (đảm bảo mapOrMapsetCode = MAP_9LME2PB7Y3EN, MapB9 active, MapB10 off).
///   2. Sau localize success:
///      - Trên build APK: disable <c>MapMeshHandler.meshVisualizationOption</c>
///        để mesh tím không phủ camera feed.
///      - Re-tag MainCamera = indoor ARCamera (tránh race với
///        <see cref="HybridModeController.GetActiveARCamera"/>).
///
/// Scope: chỉ chạy code mới khi <c>CurrentMode == Indoor</c>. Outdoor không bị chạm.
/// Pattern reflection cho Multiset SDK — không hard-link DLL.
///
/// Gắn vào 1 GameObject persistent trong scene (vd sibling của "Indoor Bootstrap").
/// </summary>
[DisallowMultipleComponent]
public class IndoorAutoEnterB9 : MonoBehaviour
{
    [Header("References (gán từ scene)")]
    [SerializeField] private HybridModeController hybridModeController;
    [SerializeField] private IndoorMapSwitcher indoorMapSwitcher;

    [Header("Target building")]
    [Tooltip("Building được auto-switch khi vào Indoor. Mặc định B9 (single Map).")]
    [SerializeField] private BuildingId defaultBuilding = BuildingId.B9;

    [Header("Multiset (optional — runtime discovery nếu null)")]
    [Tooltip("MapMeshHandler instance. Để null sẽ tự tìm sau khi SDK instantiate.")]
    [SerializeField] private MonoBehaviour mapMeshHandler;

    [Header("Behavior")]
    [Tooltip("Trên Editor giữ mesh tím (để verify alignment); chỉ disable trên APK build.")]
    [SerializeField] private bool disableMeshVizOnBuildOnly = true;
    [SerializeField] private bool verboseLog = true;

    /// <summary>
    /// Cho phép <see cref="HybridModeController"/> set building map từ xa (khi forceIndoorTestMode).
    /// Gọi TRƯỚC khi update cycle chạy để tránh conflict giữa selectedIndoorMap và defaultBuilding.
    /// </summary>
    public void OverrideDefaultBuilding(BuildingId building)
    {
        defaultBuilding = building;
    }

    private HybridModeController.HybridMode _lastMode = (HybridModeController.HybridMode)(-1);
    private bool _switchToCalled;
    private bool _localizeSuccessHandled;
    private bool _localizeSuccessFired;
    private MonoBehaviour _subscribedLocMgr;

    private void Awake()
    {
        if (hybridModeController == null)
        {
            hybridModeController = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }
        if (indoorMapSwitcher == null)
        {
            indoorMapSwitcher = FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        }
    }

    private void Update()
    {
        if (hybridModeController == null) return;

        var mode = hybridModeController.CurrentMode;

        // Rising edge: Outdoor/Transition → Indoor
        if (mode == HybridModeController.HybridMode.Indoor &&
            _lastMode != HybridModeController.HybridMode.Indoor)
        {
            TriggerSwitchToBuilding();
            _localizeSuccessHandled = false;
        }

        // Reset khi rời Indoor để lần vào sau lại trigger.
        if (mode != HybridModeController.HybridMode.Indoor)
        {
            _switchToCalled = false;
            _localizeSuccessHandled = false;
            _localizeSuccessFired = false;
            UnsubscribeLocalizationSuccess();
        }

        // Khi vào Indoor, đảm bảo đã subscribe UnityEvent LocalizationSuccess.
        if (mode == HybridModeController.HybridMode.Indoor && _subscribedLocMgr == null)
        {
            TrySubscribeLocalizationSuccess();
        }

        // Post-localize actions chuyển toàn bộ vào DeferredReanchor (chạy sau 2 frame)
        // để SDK kịp apply Map Space transform trước khi ta đọc.

        _lastMode = mode;
    }

    private void OnDisable()
    {
        UnsubscribeLocalizationSuccess();
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void TriggerSwitchToBuilding()
    {
        if (_switchToCalled) return;
        if (indoorMapSwitcher == null)
        {
            if (verboseLog) Debug.LogWarning("[IndoorAutoEnterB9] indoorMapSwitcher chưa gán — skip SwitchTo.");
            return;
        }
        if (indoorMapSwitcher.CurrentBuilding == defaultBuilding)
        {
            if (verboseLog) Debug.Log($"[IndoorAutoEnterB9] Đã ở {defaultBuilding}, skip SwitchTo.");
            _switchToCalled = true;
            return;
        }

        bool ok = indoorMapSwitcher.SwitchTo(defaultBuilding);
        _switchToCalled = ok;
        if (verboseLog) Debug.Log($"[IndoorAutoEnterB9] SwitchTo({defaultBuilding}) => {ok}.");
    }

    /// <summary>
    /// Subscribe UnityEvent <c>LocalizationSuccess</c> trên MapLocalizationManager qua reflection.
    /// Khi event fire, set <see cref="_localizeSuccessFired"/> = true để Update chạy
    /// DisableMeshViz + ReinforceCamera ở frame kế tiếp.
    /// </summary>
    private void TrySubscribeLocalizationSuccess()
    {
        // Tìm MapLocalizationManager qua type name (tránh hard-link DLL).
        MonoBehaviour locMgr = null;
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (m != null && m.GetType().Name == "MapLocalizationManager")
            {
                locMgr = m;
                break;
            }
        }
        if (locMgr == null) return;

        var t = locMgr.GetType();
        var field = t.GetField("LocalizationSuccess", System.Reflection.BindingFlags.Instance |
                                                       System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic);
        if (field == null) return;

        var evt = field.GetValue(locMgr) as UnityEngine.Events.UnityEvent;
        if (evt == null) return;

        evt.AddListener(OnLocalizationSuccessFired);
        _subscribedLocMgr = locMgr;
        if (verboseLog) Debug.Log("[IndoorAutoEnterB9] Subscribed LocalizationSuccess on MapLocalizationManager.");
    }

    private void UnsubscribeLocalizationSuccess()
    {
        if (_subscribedLocMgr == null) return;
        var t = _subscribedLocMgr.GetType();
        var field = t.GetField("LocalizationSuccess", System.Reflection.BindingFlags.Instance |
                                                       System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic);
        if (field != null)
        {
            var evt = field.GetValue(_subscribedLocMgr) as UnityEngine.Events.UnityEvent;
            evt?.RemoveListener(OnLocalizationSuccessFired);
        }
        _subscribedLocMgr = null;
    }

    private void OnLocalizationSuccessFired()
    {
        _localizeSuccessFired = true;
        if (verboseLog) Debug.Log("[IndoorAutoEnterB9] LocalizationSuccess fired → schedule post-localize actions.");
        // Defer 2 frames để SDK kịp apply Map Space transform trước khi ta reanchor NavMesh.
        StartCoroutine(DeferredReanchor());
    }

    private System.Collections.IEnumerator DeferredReanchor()
    {
        yield return null;
        yield return null;
        if (hybridModeController == null ||
            hybridModeController.CurrentMode != HybridModeController.HybridMode.Indoor)
        {
            yield break;
        }
        if (_localizeSuccessHandled) yield break;

        DisableMeshVizOnBuild();
        ReinforceMainCameraTag();
        ReanchorNavMeshes();
        _localizeSuccessHandled = true;
    }

    private void DisableMeshVizOnBuild()
    {
        if (disableMeshVizOnBuildOnly && Application.isEditor) return;

        if (mapMeshHandler == null) DiscoverMapMeshHandler();
        if (mapMeshHandler == null) return;

        var t = mapMeshHandler.GetType();
        var vizField = t.GetField("meshVisualizationOption");
        if (vizField == null || !vizField.FieldType.IsEnum) return;

        try
        {
            object disable = System.Enum.Parse(vizField.FieldType, "DisableVisualization");
            vizField.SetValue(mapMeshHandler, disable);
            if (verboseLog) Debug.Log("[IndoorAutoEnterB9] MapMeshHandler.meshVisualizationOption = DisableVisualization.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[IndoorAutoEnterB9] Không set được DisableVisualization: {ex.Message}");
        }
    }

    private void DiscoverMapMeshHandler()
    {
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (m != null && m.GetType().Name == "MapMeshHandler")
            {
                mapMeshHandler = m;
                return;
            }
        }
    }

    private void ReinforceMainCameraTag()
    {
        if (hybridModeController == null) return;
        var indoorCam = hybridModeController.GetActiveARCamera();
        if (indoorCam == null) return;

        int cleared = 0;
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cam == null || cam == indoorCam) continue;
            if (cam.CompareTag("MainCamera"))
            {
                cam.tag = "Untagged";
                cleared++;
            }
        }
        if (!indoorCam.CompareTag("MainCamera")) indoorCam.tag = "MainCamera";
        if (verboseLog) Debug.Log($"[IndoorAutoEnterB9] Reinforced MainCamera = '{indoorCam.name}' (cleared {cleared}).");
    }

    /// <summary>
    /// Sau localize, SDK Multiset dịch Map Space transform để align với thực tế.
    /// POI là child của Map Space → tự follow transform. Nhưng NavMeshData được add
    /// vào runtime navmesh tại tọa độ của <see cref="Unity.AI.Navigation.NavMeshSurface"/>
    /// lúc OnEnable (TRƯỚC localize) → đứng yên ở chỗ cũ trong khi POI đã dịch.
    /// → Path vẽ ra lệch hẳn với thực tế.
    ///
    /// Fix: call <c>RemoveData()</c> + <c>AddData()</c> để re-anchor NavMesh data
    /// theo transform hiện tại của NavMeshSurface (sau khi Map Space đã dịch).
    /// Không phải re-bake — chỉ re-position existing data.
    /// </summary>
    private void ReanchorNavMeshes()
    {
        int count = 0;
        foreach (var surface in FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (surface == null || surface.navMeshData == null) continue;
            try
            {
                surface.RemoveData();
                surface.AddData();
                count++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[IndoorAutoEnterB9] Reanchor fail on '{surface.name}': {ex.Message}");
            }
        }
        if (verboseLog) Debug.Log($"[IndoorAutoEnterB9] Reanchored {count} NavMeshSurface(s) to current transform.");

        // Warp tất cả NavMeshAgent về NavMesh mới (tránh stale agent position).
        foreach (var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (agent == null || !agent.enabled) continue;
            if (UnityEngine.AI.NavMesh.SamplePosition(agent.transform.position,
                    out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    [ContextMenu("Test/Manual Trigger SwitchTo")]
    private void DebugTriggerSwitch() => TriggerSwitchToBuilding();

    [ContextMenu("Test/Manual Reanchor NavMeshes")]
    private void DebugReanchor() => ReanchorNavMeshes();
}
