using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Runtime switch giữa các tòa nhà indoor (vd B9 ↔ B10) + chuyển hybrid mode (Outdoor ↔ Indoor).
///
/// Cách dùng:
///   1. Gắn lên 1 GameObject persistent (ví dụ "UI Home Screen").
///   2. Inspector: gán sceneBindings, mapLocalizationManager, hybridModeController.
///   3. Code khác gọi:
///      - EnterIndoor(buildingId): từ Outdoor mode chuyển sang Indoor + load map tòa.
///      - ExitToOutdoor(): tắt indoor, quay về Outdoor GPS.
/// </summary>
[DisallowMultipleComponent]
public class IndoorMapSwitcher : MonoBehaviour
{
    private const BindingFlags ReflectionFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

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
    [Tooltip("Nếu true, sau khi switch map sẽ tự gọi MultiSet LocalizeFrame. Nên để false khi test trong Unity Editor.")]
    [SerializeField] private bool autoTriggerLocalizeAfterSwitch = false;

    [Tooltip("Chỉ bật khi đã gán đủ simulation data cho MultiSet trong Unity Editor.")]
    [SerializeField] private bool allowEditorSimulationLocalize = false;

    [Tooltip("Delay trước khi gọi LocalizeFrame sau khi switch map. Giúp MultiSet có thời gian load map/localizer.")]
    [SerializeField] private float localizeDelayAfterSwitch = 0.5f;

    [SerializeField] private bool verboseLog = true;

    /// <summary>
    /// Tòa đang active. Trả BuildingId.None khi chưa switch lần nào.
    /// </summary>
    public BuildingId CurrentBuilding { get; private set; } = BuildingId.None;

    private Coroutine _localizeCoroutine;

    private void Awake()
    {
        if (hybridModeController == null)
        {
            hybridModeController = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }
    }

    /// <summary>
    /// Bật target, tắt mọi tòa khác, đổi mã localize, ép Indoor mode.
    /// </summary>
    public bool EnterIndoor(BuildingId target)
    {
        return SwitchTo(target);
    }

    /// <summary>
    /// Backward-compat alias cho EnterIndoor.
    /// </summary>
    public bool SwitchTo(BuildingId target)
    {
        if (sceneBindings == null)
        {
            Debug.LogError("[IndoorMapSwitcher] BuildingSceneBindings chưa được gán.");
            return false;
        }

        if (!sceneBindings.TryGet(target, out BuildingRegistry.Entry meta, out BuildingSceneBindings.Binding scene))
        {
            Debug.LogError($"[IndoorMapSwitcher] Không tìm thấy metadata + scene binding cho {target}.");
            return false;
        }

        if (string.IsNullOrEmpty(meta.mapsetCode))
        {
            Debug.LogError($"[IndoorMapSwitcher] Entry {target} chưa có mã mapsetCode trong registry.");
            return false;
        }

        // 1. Tắt mọi building root khác, chỉ bật building target.
        foreach (BuildingSceneBindings.Binding binding in sceneBindings.Bindings)
        {
            if (binding == null || binding.buildingRoot == null)
            {
                continue;
            }

            bool shouldActive = binding.id == target;

            if (binding.buildingRoot.activeSelf != shouldActive)
            {
                binding.buildingRoot.SetActive(shouldActive);
            }
        }

        // 2. Cập nhật MapLocalizationManager: mapOrMapsetCode + localizationType.
        if (mapLocalizationManager != null)
        {
            ApplyMapsetToLocalizer(meta);
        }
        else
        {
            Debug.LogWarning("[IndoorMapSwitcher] mapLocalizationManager chưa gán — không update mã VPS.");
        }

        // 3. Force Indoor mode.
        if (forceIndoorModeOnSwitch && hybridModeController != null)
        {
            if (hybridModeController.CurrentMode != HybridModeController.HybridMode.Indoor)
            {
                hybridModeController.ForceIndoor();

                if (verboseLog)
                {
                    Debug.Log("[IndoorMapSwitcher] HybridModeController.ForceIndoor() invoked.");
                }
            }
        }

        CurrentBuilding = target;

        if (verboseLog)
        {
            Debug.Log($"[IndoorMapSwitcher] Switched to {meta.displayName} ({meta.id}) | code={meta.mapsetCode} | kind={meta.kind}");
        }

        // 4. Trigger localize nếu cần.
        // Mặc định nên để false khi test trong Unity Editor.
        if (autoTriggerLocalizeAfterSwitch && mapLocalizationManager != null)
        {
            StartLocalizeFrameDelayed();
        }

        return true;
    }

    /// <summary>
    /// Quay về Outdoor mode: tắt indoor map, gọi HybridModeController.ForceOutdoor().
    /// </summary>
    public void ExitToOutdoor()
    {
        Clear();

        if (hybridModeController != null)
        {
            if (hybridModeController.CurrentMode != HybridModeController.HybridMode.Outdoor)
            {
                hybridModeController.ForceOutdoor();

                if (verboseLog)
                {
                    Debug.Log("[IndoorMapSwitcher] HybridModeController.ForceOutdoor() invoked.");
                }
            }
        }
    }

    /// <summary>
    /// Tắt mọi building root, reset CurrentBuilding.
    /// </summary>
    public void Clear()
    {
        if (sceneBindings == null)
        {
            return;
        }

        StopPendingLocalizeFrame();

        foreach (BuildingSceneBindings.Binding binding in sceneBindings.Bindings)
        {
            if (binding != null && binding.buildingRoot != null && binding.buildingRoot.activeSelf)
            {
                binding.buildingRoot.SetActive(false);
            }
        }

        CurrentBuilding = BuildingId.None;

        if (verboseLog)
        {
            Debug.Log("[IndoorMapSwitcher] Cleared (building roots off).");
        }
    }

    // ---------------------------------------------------------------------
    // Reflection helpers — không tham chiếu trực tiếp class MultiSet SDK.
    // ---------------------------------------------------------------------

    private void ApplyMapsetToLocalizer(BuildingRegistry.Entry entry)
    {
        if (mapLocalizationManager == null)
        {
            Debug.LogWarning("[IndoorMapSwitcher] MapLocalizationManager is null. Skip ApplyMapsetToLocalizer.");
            return;
        }

        Type managerType = mapLocalizationManager.GetType();

        bool didSetCode = TrySetFieldOrProperty(managerType, "mapOrMapsetCode", entry.mapsetCode);

        if (!didSetCode)
        {
            Debug.LogWarning("[IndoorMapSwitcher] Không tìm thấy field/property 'mapOrMapsetCode' trên MapLocalizationManager.");
        }

        FieldInfo typeField = managerType.GetField("localizationType", ReflectionFlags);

        if (typeField != null && typeField.FieldType.IsEnum)
        {
            string enumName = entry.kind == BuildingRegistry.MultisetLocalizationKind.MapSet ? "MapSet" : "Map";

            try
            {
                object enumValue = Enum.Parse(typeField.FieldType, enumName);
                typeField.SetValue(mapLocalizationManager, enumValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IndoorMapSwitcher] Không set được localizationType='{enumName}': {ex.Message}");
            }

            return;
        }

        PropertyInfo typeProperty = managerType.GetProperty("localizationType", ReflectionFlags);

        if (typeProperty != null && typeProperty.CanWrite && typeProperty.PropertyType.IsEnum)
        {
            string enumName = entry.kind == BuildingRegistry.MultisetLocalizationKind.MapSet ? "MapSet" : "Map";

            try
            {
                object enumValue = Enum.Parse(typeProperty.PropertyType, enumName);
                typeProperty.SetValue(mapLocalizationManager, enumValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IndoorMapSwitcher] Không set được localizationType property='{enumName}': {ex.Message}");
            }

            return;
        }

        Debug.LogWarning("[IndoorMapSwitcher] Không tìm thấy field/property 'localizationType' hoặc nó không phải enum.");
    }

    private bool TrySetFieldOrProperty(Type targetType, string memberName, object value)
    {
        FieldInfo field = targetType.GetField(memberName, ReflectionFlags);

        if (field != null)
        {
            field.SetValue(mapLocalizationManager, value);
            return true;
        }

        PropertyInfo property = targetType.GetProperty(memberName, ReflectionFlags);

        if (property != null && property.CanWrite)
        {
            property.SetValue(mapLocalizationManager, value);
            return true;
        }

        return false;
    }

    private void StartLocalizeFrameDelayed()
    {
        StopPendingLocalizeFrame();
        _localizeCoroutine = StartCoroutine(TriggerLocalizeFrameDelayed());
    }

    private void StopPendingLocalizeFrame()
    {
        if (_localizeCoroutine != null)
        {
            StopCoroutine(_localizeCoroutine);
            _localizeCoroutine = null;
        }
    }

    private IEnumerator TriggerLocalizeFrameDelayed()
    {
        if (localizeDelayAfterSwitch > 0f)
        {
            yield return new WaitForSeconds(localizeDelayAfterSwitch);
        }

        _localizeCoroutine = null;
        TriggerLocalizeFrame();
    }

    private void TriggerLocalizeFrame()
    {
        if (mapLocalizationManager == null)
        {
            Debug.LogWarning("[IndoorMapSwitcher] MapLocalizationManager is null. Skip LocalizeFrame.");
            return;
        }

#if UNITY_EDITOR
        if (!allowEditorSimulationLocalize)
        {
            if (verboseLog)
            {
                Debug.Log("[IndoorMapSwitcher] Unity Editor detected. Skip MultiSet LocalizeFrame. Build lên device hoặc bật allowEditorSimulationLocalize khi đã có simulation data.");
            }

            return;
        }
#endif

        try
        {
            Type managerType = mapLocalizationManager.GetType();

            MethodInfo method = managerType.GetMethod("LocalizeFrame", ReflectionFlags);

            if (method == null)
            {
                Debug.LogWarning("[IndoorMapSwitcher] LocalizeFrame method not found on MapLocalizationManager.");
                return;
            }

            method.Invoke(mapLocalizationManager, null);

            if (verboseLog)
            {
                Debug.Log("[IndoorMapSwitcher] MultiSet LocalizeFrame invoked.");
            }
        }
        catch (TargetInvocationException ex)
        {
            Debug.LogWarning($"[IndoorMapSwitcher] MultiSet LocalizeFrame failed: {ex.InnerException?.GetType().Name} - {ex.InnerException?.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IndoorMapSwitcher] TriggerLocalizeFrame failed: {ex.GetType().Name} - {ex.Message}");
        }
    }

    [ContextMenu("Test/Enter Indoor B9")]
    private void DebugEnterB9()
    {
        EnterIndoor(BuildingId.B9);
    }

    [ContextMenu("Test/Enter Indoor B10")]
    private void DebugEnterB10()
    {
        EnterIndoor(BuildingId.B10);
    }

    [ContextMenu("Test/Exit to Outdoor")]
    private void DebugExitToOutdoor()
    {
        ExitToOutdoor();
    }

    [ContextMenu("Test/Clear")]
    private void DebugClear()
    {
        Clear();
    }
}