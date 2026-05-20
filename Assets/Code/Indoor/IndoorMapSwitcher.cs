using UnityEngine;

/// <summary>
/// Runtime switch giữa các tòa nhà indoor (vd B9 ↔ B10) + chuyển hybrid mode (Outdoor ↔ Indoor).
///
/// Cách dùng:
///   1. Gắn lên 1 GameObject persistent (ví dụ "UI Home Screen").
///   2. Inspector: gán <see cref="sceneBindings"/>, <see cref="mapLocalizationManager"/>,
///      <see cref="hybridModeController"/>.
///   3. Code khác gọi:
///      - <see cref="EnterIndoor"/>(buildingId)  : từ Outdoor mode chuyển sang Indoor + load map tòa.
///      - <see cref="ExitToOutdoor"/>            : tắt indoor, quay về Outdoor GPS.
/// </summary>
[DisallowMultipleComponent]
public class IndoorMapSwitcher : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BuildingSceneBindings sceneBindings;

    [Header("Multiset components (assign từ scene)")]
    [Tooltip("MapLocalizationManager (com.multiset.sdk). Dùng MonoBehaviour + reflection để tránh hard-link DLL.")]
    [SerializeField] private MonoBehaviour mapLocalizationManager;

    [Header("Hybrid mode integration")]
    [SerializeField] private HybridModeController hybridModeController;
    [Tooltip("Khi SwitchTo(buildingId): nếu true, tự gọi HybridModeController.ForceIndoor() để bật IndoorEnvironment.")]
    [SerializeField] private bool forceIndoorModeOnSwitch = true;

    [Header("Optional")]
    [SerializeField] private bool autoTriggerLocalizeAfterSwitch = false;
    [SerializeField] private bool verboseLog = true;

    /// <summary>Tòa đang active. Trả <see cref="BuildingId.None"/> khi chưa switch lần nào.</summary>
    public BuildingId CurrentBuilding { get; private set; } = BuildingId.None;

    private void Awake()
    {
        if (hybridModeController == null)
        {
            hybridModeController = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }
    }

    /// <summary>Bật <paramref name="target"/>, tắt mọi tòa khác, đổi mã localize, ép Indoor mode.</summary>
    public bool EnterIndoor(BuildingId target)
    {
        return SwitchTo(target);
    }

    /// <summary>Backward-compat alias cho <see cref="EnterIndoor"/>.</summary>
    public bool SwitchTo(BuildingId target)
    {
        if (sceneBindings == null)
        {
            Debug.LogError("[IndoorMapSwitcher] BuildingSceneBindings chưa được gán.");
            return false;
        }

        if (!sceneBindings.TryGet(target, out var meta, out var scene))
        {
            Debug.LogError($"[IndoorMapSwitcher] Không tìm thấy metadata + scene binding cho {target}.");
            return false;
        }

        if (string.IsNullOrEmpty(meta.mapsetCode))
        {
            Debug.LogError($"[IndoorMapSwitcher] Entry {target} chưa có mã mapsetCode trong registry.");
            return false;
        }

        // 1. Tắt mọi building root khác.
        foreach (var b in sceneBindings.Bindings)
        {
            if (b == null || b.buildingRoot == null) continue;
            bool shouldActive = b.id == target;
            if (b.buildingRoot.activeSelf != shouldActive)
            {
                b.buildingRoot.SetActive(shouldActive);
            }
        }

        // 2. Cập nhật MapLocalizationManager (mã + kind).
        if (mapLocalizationManager != null)
        {
            ApplyMapsetToLocalizer(meta);
        }
        else
        {
            Debug.LogWarning("[IndoorMapSwitcher] mapLocalizationManager chưa gán — không update mã VPS.");
        }

        // 3. Force Indoor mode (tắt Outdoor).
        if (forceIndoorModeOnSwitch && hybridModeController != null)
        {
            if (hybridModeController.CurrentMode != HybridModeController.HybridMode.Indoor)
            {
                hybridModeController.ForceIndoor();
                if (verboseLog) Debug.Log("[IndoorMapSwitcher] HybridModeController.ForceIndoor() invoked.");
            }
        }

        CurrentBuilding = target;

        if (verboseLog)
        {
            Debug.Log($"[IndoorMapSwitcher] Switched to {meta.displayName} ({meta.id}) | code={meta.mapsetCode} | kind={meta.kind}");
        }

        // 4. Trigger localize ngay nếu cần (mặc định off — user tự bấm nút quét).
        if (autoTriggerLocalizeAfterSwitch && mapLocalizationManager != null)
        {
            TriggerLocalizeFrame();
        }

        return true;
    }

    /// <summary>Quay về Outdoor mode: tắt indoor map, gọi HybridModeController.ForceOutdoor().</summary>
    public void ExitToOutdoor()
    {
        Clear();

        if (hybridModeController != null)
        {
            if (hybridModeController.CurrentMode != HybridModeController.HybridMode.Outdoor)
            {
                hybridModeController.ForceOutdoor();
                if (verboseLog) Debug.Log("[IndoorMapSwitcher] HybridModeController.ForceOutdoor() invoked.");
            }
        }
    }

    /// <summary>Tắt mọi building root, reset CurrentBuilding.</summary>
    public void Clear()
    {
        if (sceneBindings == null) return;
        foreach (var b in sceneBindings.Bindings)
        {
            if (b != null && b.buildingRoot != null && b.buildingRoot.activeSelf)
            {
                b.buildingRoot.SetActive(false);
            }
        }
        CurrentBuilding = BuildingId.None;
        if (verboseLog) Debug.Log("[IndoorMapSwitcher] Cleared (building roots off).");
    }

    // ---------------------------------------------------------------------
    // Reflection helpers — không tham chiếu trực tiếp class MultiSet SDK.
    // ---------------------------------------------------------------------

    private void ApplyMapsetToLocalizer(BuildingRegistry.Entry entry)
    {
        var t = mapLocalizationManager.GetType();

        var codeField = t.GetField("mapOrMapsetCode");
        if (codeField != null) codeField.SetValue(mapLocalizationManager, entry.mapsetCode);

        var typeField = t.GetField("localizationType");
        if (typeField != null && typeField.FieldType.IsEnum)
        {
            string enumName = entry.kind == BuildingRegistry.MultisetLocalizationKind.MapSet ? "MapSet" : "Map";
            try
            {
                object enumValue = System.Enum.Parse(typeField.FieldType, enumName);
                typeField.SetValue(mapLocalizationManager, enumValue);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[IndoorMapSwitcher] Không set được localizationType='{enumName}': {ex.Message}");
            }
        }
    }

    private void TriggerLocalizeFrame()
    {
        var t = mapLocalizationManager.GetType();
        var method = t.GetMethod("LocalizeFrame");
        if (method != null) method.Invoke(mapLocalizationManager, null);
    }

    [ContextMenu("Test/Enter Indoor B9")]
    private void DebugEnterB9() => EnterIndoor(BuildingId.B9);

    [ContextMenu("Test/Enter Indoor B10")]
    private void DebugEnterB10() => EnterIndoor(BuildingId.B10);

    [ContextMenu("Test/Exit to Outdoor")]
    private void DebugExitToOutdoor() => ExitToOutdoor();

    [ContextMenu("Test/Clear")]
    private void DebugClear() => Clear();
}
