using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class HybridModeController : MonoBehaviour
{
    public enum HybridMode
    {
        Outdoor,
        Indoor,
        Transition
    }

    [Header("Indoor Mode Configuration")]
    [Tooltip("Master toggle: bật indoor mode. Khi false, mọi đường vào Indoor đều bị chặn (Outdoor-only).")]
    [SerializeField] private bool enableIndoorMode = false;

    [Tooltip("Khi true: bỏ qua Outdoor, vào thẳng Indoor localization bằng Multiset SDK khi AR được bật. " +
             "Dùng để test indoor trong tòa nhà mà không qua GPS handover.")]
    [SerializeField] private bool forceIndoorTestMode = false;

    [Tooltip("Chọn map indoor khi forceIndoorTestMode = true.")]
    [SerializeField] private BuildingId selectedIndoorMap = BuildingId.B9;

    /// <summary>Public accessor để NavigationManager check flag mà không cần Inspector ref.</summary>
    public bool ForceIndoorTestModeEnabled => forceIndoorTestMode;

    [Header("Editor Test Options")]
    [Tooltip("Khi true + trong Unity Editor: bỏ qua API call ở màn login/register, navigate thẳng đến trang kế tiếp.")]
    [SerializeField] private bool bypassApiCallsInEditor = true;

    /// <summary>Static check: đang trong Editor AND bypassApiCallsInEditor=true.</summary>
    public static bool ShouldBypassApiInEditor()
    {
#if UNITY_EDITOR
        var instance = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        return instance != null && instance.bypassApiCallsInEditor;
#else
        return false;
#endif
    }

    [Header("Environment Roots")]
    [SerializeField] private GameObject indoorEnvironment;
    [SerializeField] private GameObject outdoorEnvironment;

    /// <summary>
    /// False when <see cref="indoorEnvironment"/> is not assigned (outdoor-only / stripped hybrid).
    /// <see cref="HybridOutdoorNavigationRoot"/> uses this so it does not hide the path HUD in Awake.
    /// </summary>
    public bool HasAssignedIndoorEnvironment => indoorEnvironment != null;
    [Tooltip("Optional child root to show only in Indoor mode while keeping indoor runtime alive.")]
    [SerializeField] private GameObject indoorVisualRoot;

    [Header("Mode Presentation")]
    [SerializeField] private bool manageModePresentation = true;
    [SerializeField] private bool autoManageCanvases = true;
    [SerializeField] private bool autoManageAudioSources = true;
    [SerializeField] private bool autoManageAudioListeners = true;
    [SerializeField] private bool enforceSingleAudioListener = true;
    [SerializeField] private bool autoManageMainCameraTag = true;
    [SerializeField] private Camera indoorMainCamera;
    [SerializeField] private Camera outdoorMainCamera;
    [Tooltip("Objects to hide in Outdoor while keeping indoor localization/runtime alive.")]
    [SerializeField] private List<GameObject> indoorOnlyVisualRoots = new List<GameObject>();
    [Tooltip("Objects to hide in Indoor if outdoor runtime is kept alive.")]
    [SerializeField] private List<GameObject> outdoorOnlyVisualRoots = new List<GameObject>();
    [SerializeField] private List<AudioSource> indoorAudioSources = new List<AudioSource>();
    [SerializeField] private List<AudioSource> outdoorAudioSources = new List<AudioSource>();
    [SerializeField] private List<AudioListener> indoorAudioListeners = new List<AudioListener>();
    [SerializeField] private List<AudioListener> outdoorAudioListeners = new List<AudioListener>();
    [SerializeField] private bool createSharedOutdoorHud = true;

    [Header("Runtime Test Controls")]
    [SerializeField] private bool createRuntimeModeSwitcher = true;
    [SerializeField] private bool showRuntimeModeSwitcherOnlyInAR = true;
    [Tooltip("Indoor | Outdoor | Off bar is anchored bottom-center when true so it does not overlap the navigation HUD.")]
    [SerializeField] private bool anchorRuntimeModeSwitcherAtBottom = true;
    [Tooltip("Offset from anchor: lateral X, Y up from bottom (bottom mode) or from top-left (legacy top mode).")]
    [SerializeField] private Vector2 runtimeModeSwitcherOffset = new Vector2(0f, 20f);
    [Tooltip("GPS / mode status line above the buttons — duplicates Mobile HUD GPS line; hide for a cleaner layout.")]
    [SerializeField] private bool showRuntimeModeSwitcherStatusLine = false;

    [Header("Android Permissions")]
    [SerializeField] private bool requestAndroidPermissionsBeforeAR = true;
    [SerializeField] private bool requireLocationPermissionForOutdoor = true;
    [SerializeField] private float permissionRequestTimeoutSeconds = 30f;

    [Header("iOS Permissions")]
    [SerializeField] private bool requestIOSCameraPermissionBeforeAR = true;

    [Header("Transition Overlay")]
    [SerializeField] private bool createTransitionOverlay = true;
    [SerializeField] private float transitionFadeSeconds = 0.25f;
    [SerializeField] private float transitionHoldSeconds = 0.55f;
    [SerializeField] private string indoorToOutdoorMessage = "Switching to GPS";
    [SerializeField] private string outdoorToIndoorMessage = "Indoor map found";

    [Header("Topology")]
    [Tooltip("Keep indoor runtime alive during Outdoor mode so localization can still run.")]
    [SerializeField] private bool keepIndoorActiveWhileOutdoor = false;
    [Tooltip("Keep outdoor runtime alive during Indoor mode (usually false).")]
    [SerializeField] private bool keepOutdoorActiveWhileIndoor = false;
    [Tooltip("When true: IndoorEnvironment và outdoor environment luôn active đồng thời, không tắt cái nào. Ghi đè keepIndoorActiveWhileOutdoor + keepOutdoorActiveWhileIndoor.")]
    [SerializeField] private bool alwaysKeepBothEnvironmentsActive = true;
    [Tooltip("Roots that must stay active across modes, such as AR Session, XR Origin, ARCamera, and shared UI.")]
    [SerializeField] private List<GameObject> alwaysActiveRoots = new List<GameObject>();

    [Header("Single XR rig (outdoor camera survives Indoor)")]
    [Tooltip("On Awake, reparent the outdoor AR rig to the scene root so turning off OutdoorEnvironment in Indoor mode does not disable XROrigin / AR camera (avoids black screen on device when both stacks were edited active).")]
    [SerializeField] private bool detachOutdoorXrRigFromEnvironment = true;
    [Tooltip("Optional. Root that stays alive in both modes: assign a parent that contains both AR Session and XROrigin if they are not under the same transform. If unset, only the XROrigin node is detached and AR Session may stay under (inactive) OutdoorEnvironment.")]
    [SerializeField] private GameObject outdoorXrRigRootOverride;
    [Tooltip("Disable XROrigin components under IndoorEnvironment so only one rig drives AR.")]
    [SerializeField] private bool disableIndoorXROriginDuplicates = true;
    [Tooltip("While the outdoor environment stack is active, disable AR Session components under IndoorEnvironment to avoid two simultaneous AR sessions. When only indoor is active, indoor sessions stay enabled for a normal camera feed.")]
    [SerializeField] private bool disableIndoorARSessionDuplicates = true;
    [Tooltip("Device-safe topology: chỉ một ARSession được enabled trong toàn scene.")]
    [SerializeField] private bool enforceSingleARSessionAtRuntime = true;

    [Header("Signal sources (optional)")]
    [Tooltip("Optional. Legacy / hybrid scenes only. Outdoor GPSMapPlane flow uses SimpleGPSTracker + MapOrigin — leave empty. When assigned and Max Gps Accuracy Meters <= 0, threshold can inherit maxAcceptableAccuracy from this marker (else default 30 m).")]
    [SerializeField] private GPSMarker gpsMarker;
    [Tooltip("Assign the SimpleGPSTracker on the shared XR rig. Its XR Origin updates will be frozen during Indoor mode so Multiset VPS can drive positioning without GPS interference.")]
    [SerializeField] private SimpleGPSTracker simpleGpsTracker;
    [Tooltip("Assign AlignXROriginToUser if present. Its one-shot alignment flag is reset when returning to Outdoor so it re-anchors the camera to GPS.")]
    [SerializeField] private AlignXROriginToUser alignXROriginToUser;

    [Header("Initial State")]
    [SerializeField] private HybridMode initialMode = HybridMode.Outdoor;
    [Tooltip(
        "When true, applies initial mode on launch. If a NavigationManager is present (app shell), AR is entered automatically "
        + "via EnterARPage — same as tapping AR in UI — so ARPageObject is enabled and hybrid is not left in Transition (avoids black screen).")]
    [SerializeField] private bool activateInitialModeOnStart = false;

    [Header("Switch Rules")]
    [Tooltip("When disabled, mode only changes through explicit calls such as Force Indoor, Force Outdoor, or Apply Initial Mode.")]
    [SerializeField] private bool autoSwitchEnabled = false;
    [Tooltip("Seconds of continuous localization failure before allowing Indoor -> Outdoor.")]
    [SerializeField] private float indoorLostToOutdoorDelay = 8f;
    [Tooltip("Seconds GPS must stay good before allowing Indoor -> Outdoor.")]
    [SerializeField] private float gpsStableRequiredTime = 3f;
    [Tooltip("Require Input.location running with good accuracy before switching Indoor -> Outdoor. Not tied to GPSMarker; use false when auto-switch is off or outdoor-only.")]
    [SerializeField] private bool requireGpsForIndoorToOutdoor = false;
    [Tooltip("Seconds of stable indoor localization before allowing Outdoor -> Indoor.")]
    [SerializeField] private float indoorSuccessRequiredTime = 2f;
    [Tooltip("Cooldown to prevent mode flapping.")]
    [SerializeField] private float switchCooldown = 5f;
    [Tooltip("If > 0, GPS must be at or better than this accuracy (m) for IsGpsGood. If <= 0, uses GPSMarker.maxAcceptableAccuracy when marker assigned, otherwise 30 m.")]
    [SerializeField] private float maxGpsAccuracyMeters = -1f;

    [Header("Debug / Mock")]
    [SerializeField] private bool useMockSignalsInEditor = false;
    [SerializeField] private bool mockLocalizationGood = false;
    [SerializeField] private bool mockGpsGood = true;
    [SerializeField] private bool verboseLog = true;

    private bool audioListenersDirty = true;

    private float lastUITextTime;
    // Cache để tránh TMP/Canvas rebuild không cần thiết mỗi 0.5s
    private string _lastStatusText = null;
    private HybridMode _lastButtonMode = (HybridMode)(-1);

    public HybridMode CurrentMode => currentMode;

    /// <summary>
    /// Read-only accessor cho camera đang được present theo <see cref="CurrentMode"/>.
    /// Giúp observer ngoài (vd <c>IndoorAutoEnterB9</c>, <c>MultisetIndoorBootstrap</c>) tránh
    /// race với <see cref="Camera.main"/> cache khi <c>ApplyMainCameraTag</c> vừa chạy.
    /// Trả về null nếu field tương ứng chưa được gán trong inspector.
    /// </summary>
    public Camera GetActiveARCamera()
    {
        return currentMode == HybridMode.Indoor ? indoorMainCamera : outdoorMainCamera;
    }

    private HybridMode currentMode;
    private bool localizationGood;
    private float localizationGoodTimer;
    private float localizationLostTimer;
    private float gpsGoodTimer;
    private float lastSwitchTime = -999f;
    private float lastGpsAccuracy = -1f;
    private float _relocalizeLogTimer;
    private bool hasAppliedInitialMode;
    private CanvasGroup transitionCanvasGroup;
    private TextMeshProUGUI transitionText;
    private Coroutine transitionRoutine;
    private bool hasCachedPresentationReferences;
    private CanvasGroup runtimeModeSwitcherCanvasGroup;
    private TextMeshProUGUI runtimeModeStatusText;
    private Button runtimeIndoorButton;
    private Button runtimeOutdoorButton;
    private Button runtimeOffButton;
    private bool runtimeModeSwitcherVisible;
    private Coroutine pendingPermissionRoutine;
    private string runtimePermissionStatus;
    private bool deactivatedARModeInAwake;

    /// <summary>Runtime-only: outdoor XROrigin (or override root) reparented off <see cref="outdoorEnvironment"/>.</summary>
    private GameObject _detachedOutdoorXrRigRoot;
    private ARSession _primaryARSession;

    private void Awake()
    {
        // Outdoor-only mode: KHÔNG deactivate AR trong Awake.
        //
        // Bug đã fix: code cũ gọi DeactivateARMode() ngay trong Awake. Nếu scene save
        // với OutdoorEnvironment active, Unity sẽ chạy Awake/OnEnable trên ARSession,
        // ARCameraManager, ARCameraBackground TRƯỚC (vì chúng là children, init trước
        // parent), rồi method này kill chúng → ARCore native session để lại trạng thái
        // hỏng → tap AR sau đó camera đen.
        //
        // Bây giờ AR Session sống từ lúc scene load. MainScreen Canvas (Screen-Space
        // Overlay) tự nhiên che AR view khi user chưa vào AR mode.
        currentMode = HybridMode.Outdoor;
        deactivatedARModeInAwake = false;
        EnforceSingleARSessionTopology();
        DetachOutdoorXrRigForSharedCamera();
    }

    private void Start()
    {
        // forceIndoorTestMode: tự bật enableIndoorMode để vào thẳng Indoor khi AR start.
        // Phải check TRƯỚC CreateRuntimeModeSwitcherIfNeeded() để nút Indoor được tạo.
        if (forceIndoorTestMode)
        {
            enableIndoorMode = true;
            Debug.Log("[HybridMode] [INDOOR_TEST] forceIndoorTestMode=true — sẽ vào thẳng Indoor khi AR bật.");
        }

        // Auto-resolve SimpleGPSTracker nếu chưa gán trong Inspector.
        if (simpleGpsTracker == null)
        {
            simpleGpsTracker = FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
            if (simpleGpsTracker != null && verboseLog)
                Debug.Log("[HybridMode] Auto-resolved SimpleGPSTracker: " + simpleGpsTracker.name);
        }
        if (gpsMarker == null)
        {
            gpsMarker = FindFirstObjectByType<GPSMarker>(FindObjectsInactive.Exclude);
        }

        if (autoSwitchEnabled &&
            FindFirstObjectByType<ARNav.Hybrid.HybridLocalizationManager>(
                FindObjectsInactive.Include) != null)
        {
            autoSwitchEnabled = false;
            Debug.Log(
                "[HybridMode] HybridLocalizationManager là state authority; tắt legacy autoSwitch để tránh hai state machine tranh mode.");
        }

        CachePresentationReferences();
        EnsureARCameraPoseTracking(ResolveOutdoorPresentationCamera());
        CreateTransitionOverlayIfNeeded();
        CreateSharedOutdoorHudIfNeeded();
        CreateRuntimeModeSwitcherIfNeeded();
        ResetTimers();

        // Outdoor-only: AR Session đã alive từ scene load (do Awake không kill nữa).
        // Không gọi DeactivateARMode() — nó vẫn phá AR Session nếu chạy lúc này.
        // MainScreen Canvas che AR view khi user chưa vào AR mode; NavigationManager
        // events (OnAREntered/OnARExited) toggle HUD canvases qua HybridOutdoorNavigationRoot.
        currentMode = HybridMode.Outdoor;
        hasAppliedInitialMode = true;

        // Thanh switcher (Outdoor / Quay về) THUẦN là GUI của AR view:
        //   - Có NavigationManager → ẩn ban đầu (onboarding/login/home), chỉ hiện khi user bật AR.
        //   - Không có NavigationManager (scene standalone test) → AR luôn bật → hiện luôn.
        bool navManagerPresent =
            FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include) != null;
        SetRuntimeModeSwitcherVisible(!navManagerPresent);
    }

    private void OnEnable()
    {
        // Gate thanh switcher theo AR mode: hiện khi vào AR, ẩn khi quay về MainScreen.
        NavigationManager.OnAREntered += HandleNavAREntered;
        NavigationManager.OnARExited  += HandleNavARExited;
    }

    private void OnDisable()
    {
        NavigationManager.OnAREntered -= HandleNavAREntered;
        NavigationManager.OnARExited  -= HandleNavARExited;
    }

    private void HandleNavAREntered()
    {
        SetRuntimeModeSwitcherVisible(true);
        EnsureARCameraPoseTracking(ResolveOutdoorPresentationCamera());
    }
    private void HandleNavARExited()  => SetRuntimeModeSwitcherVisible(false);

    private void Update()
    {
        // RELOCALIZING log — chạy bất kể autoSwitchEnabled
        if (currentMode == HybridMode.Indoor && !localizationGood)
        {
            _relocalizeLogTimer += Time.deltaTime;
            if (_relocalizeLogTimer >= 5f)
            {
                _relocalizeLogTimer = 0f;
                Debug.Log($"[HybridMode] [RELOCALIZING] Chờ VPS localization... hãy giữ camera hướng vào tòa nhà.");
            }
        }
        else
        {
            _relocalizeLogTimer = 0f;
        }

        if (!autoSwitchEnabled)
        {
            return;
        }

        bool gpsGood = IsGpsGood();
        bool indoorLocalized = IsLocalizationGood();

        gpsGoodTimer = gpsGood ? gpsGoodTimer + Time.deltaTime : 0f;
        localizationGoodTimer = indoorLocalized ? localizationGoodTimer + Time.deltaTime : 0f;
        localizationLostTimer = indoorLocalized ? 0f : localizationLostTimer + Time.deltaTime;

        if (currentMode == HybridMode.Indoor)
        {
            bool canLeaveIndoor = !requireGpsForIndoorToOutdoor ||
                gpsGoodTimer >= gpsStableRequiredTime;

            if (localizationLostTimer >= indoorLostToOutdoorDelay &&
                canLeaveIndoor &&
                CanSwitch())
            {
                string reason = requireGpsForIndoorToOutdoor
                    ? "Indoor lost + GPS stable"
                    : "Indoor lost";
                ApplyMode(HybridMode.Outdoor, reason);
            }
        }
        else if (currentMode == HybridMode.Outdoor)
        {
            // Indoor bị khóa — không auto-switch khi enableIndoorMode=false.
            if (!enableIndoorMode) return;

            if (localizationGoodTimer >= indoorSuccessRequiredTime &&
                CanSwitch())
            {
                ApplyMode(HybridMode.Indoor, "Indoor localization stable");
            }
        }
    }

    private void LateUpdate()
    {
        if (autoManageAudioListeners && enforceSingleAudioListener)
        {
            EnforceSingleAudioListener(currentMode);
        }

        UpdateRuntimeModeSwitcher();
    }

    public void OnLocalizationSuccess()
    {
        localizationGood = true;
        if (verboseLog)
        {
            string modeTag = GetModeTag(currentMode);
            Debug.Log($"[HybridMode] [{modeTag}] LocalizationSuccess — pose indoor đã được cập nhật từ Multiset VPS.");
        }
    }

    public void OnLocalizationFailure()
    {
        localizationGood = false;
        if (verboseLog)
        {
            string modeTag = GetModeTag(currentMode);
            Debug.Log($"[HybridMode] [{modeTag}] LOCALIZATION_FAILED — VPS không nhận diện được môi trường. " +
                      "Hãy đảm bảo camera thấy đủ feature points của tòa nhà.");
        }
    }

    [ContextMenu("Hybrid/Force Indoor")]
    public void ForceIndoor()
    {
        enableIndoorMode = true;
        SetRuntimeModeSwitcherVisible(true);
        var manager = FindFirstObjectByType<ARNav.Hybrid.HybridLocalizationManager>(
            FindObjectsInactive.Include);
        if (manager != null &&
            manager.CurrentState != ARNav.Hybrid.HybridNavigationState.TransitionScanning &&
            manager.CurrentState != ARNav.Hybrid.HybridNavigationState.Relocalization &&
            manager.CurrentState != ARNav.Hybrid.HybridNavigationState.Indoor)
        {
            Debug.LogWarning(
                "[HybridMode] Từ chối ForceIndoor mode-only vì HybridLocalizationManager đang giữ state Outdoor. " +
                "Hãy đi qua EntranceAnchor/IndoorMapSwitcher để chọn map và calibration đúng.");
            return;
        }

        ApplyIndoorFromCoordinator("ForceIndoor");
    }

    /// <summary>Mode-only entry used by HybridLocalizationManager through IndoorMapSwitcher.</summary>
    public void ApplyIndoorFromCoordinator(string reason = "HybridLocalizationManager")
    {
        enableIndoorMode = true;
        RequestModeWithPermissions(HybridMode.Indoor, reason);
    }

    [ContextMenu("Hybrid/Force Outdoor")]
    public void ForceOutdoor()
    {
        SetRuntimeModeSwitcherVisible(true);
        var manager = FindFirstObjectByType<ARNav.Hybrid.HybridLocalizationManager>(
            FindObjectsInactive.Include);
        if (manager != null && manager.CurrentState != ARNav.Hybrid.HybridNavigationState.Outdoor)
        {
            manager.RequestImmediateExit("manual Outdoor button");
            return;
        }

        ApplyOutdoorFromCoordinator("ForceOutdoor");
    }

    /// <summary>Mode-only exit used by HybridLocalizationManager through IndoorMapSwitcher.</summary>
    public void ApplyOutdoorFromCoordinator(string reason = "HybridLocalizationManager")
    {
        RequestModeWithPermissions(HybridMode.Outdoor, reason);
    }

    [ContextMenu("Hybrid/Apply Initial Mode")]
    public void ApplyInitialMode()
    {
        CachePresentationReferences();
        CreateTransitionOverlayIfNeeded();
        currentMode = HybridMode.Transition;
        SetRuntimeModeSwitcherVisible(true);

        HybridMode targetMode = initialMode;
        if (forceIndoorTestMode)
        {
            targetMode = HybridMode.Indoor;
            enableIndoorMode = true;
            Debug.Log("[HybridMode] [INDOOR_TEST] Override initial mode → Indoor (forceIndoorTestMode=true).");
        }

        RequestModeWithPermissions(targetMode, "ApplyInitialMode");
    }

    public void SetRuntimeModeSwitcherVisible(bool visible)
    {
        runtimeModeSwitcherVisible = visible;
        ApplyRuntimeModeSwitcherVisibility();
    }

    [ContextMenu("Hybrid/Deactivate AR")]
    public void DeactivateARMode()
    {
        CachePresentationReferences();
        ResetTimers();

        if (pendingPermissionRoutine != null)
        {
            StopCoroutine(pendingPermissionRoutine);
            pendingPermissionRoutine = null;
        }

        runtimePermissionStatus = null;
        SetRuntimeModeButtonsInteractable(true);

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.alpha = 0f;
            transitionCanvasGroup.blocksRaycasts = false;
            transitionCanvasGroup.interactable = false;
        }

        // Outdoor-only: KHÔNG disable outdoorEnvironment GameObject — đó là chỗ ARSession
        // sống. Disable nó = kill ARCore native = camera đen lần sau enter AR.
        // Indoor-related roots vẫn disable (cleanup phòng hờ scene cũ còn refs).
        SetRootActiveDirect(indoorEnvironment, false);
        SetRootActiveDirect(indoorVisualRoot, false);
        SetRootsActiveDirect(indoorOnlyVisualRoots, false);

        // Chỉ ẩn HUD canvases của outdoor, không động vào ARSession/Camera GameObject
        if (autoManageCanvases)
        {
            SetCanvasesEnabled(outdoorEnvironment, false);
        }

        if (autoManageAudioSources)
        {
            SetAudioSourcesEnabled(indoorAudioSources, false);
            SetAudioSourcesEnabled(outdoorAudioSources, false);
        }

        if (autoManageAudioListeners)
        {
            SetAudioListenersEnabled(indoorAudioListeners, false);
            SetAudioListenersEnabled(outdoorAudioListeners, false);
        }

        currentMode = HybridMode.Transition;
        hasAppliedInitialMode = false;

        if (verboseLog)
        {
            Debug.Log("[HybridMode] AR environments deactivated");
        }
    }

    [ContextMenu("Hybrid/Mark Localization Success")]
    public void DebugLocalizationSuccess()
    {
        localizationGood = true;
    }

    /// <summary>
    /// Quay về UI (MainScreen) — dùng ARPageController.SwitchObject() nếu có,
    /// fallback DeactivateARMode nếu không tìm thấy.
    /// </summary>
    public void ReturnToUI()
    {
        var arPageCtrl = FindFirstObjectByType<ARPageController>(FindObjectsInactive.Include);
        if (arPageCtrl != null)
        {
            arPageCtrl.SwitchObject();
        }
        else
        {
            DeactivateARMode();
            if (verboseLog) Debug.LogWarning("[HybridMode] ARPageController not found — fallback DeactivateARMode.");
        }
    }

    [ContextMenu("Hybrid/Mark Localization Failure")]
    public void DebugLocalizationFailure()
    {
        localizationGood = false;
    }

    private string GetModeTag(HybridMode mode)
    {
        if (mode == HybridMode.Indoor && forceIndoorTestMode)
            return "INDOOR_TEST";
        if (mode == HybridMode.Indoor)
            return "INDOOR_VPS";
        if (mode == HybridMode.Outdoor)
            return "OUTDOOR_GPS";
        if (mode == HybridMode.Transition)
            return "TRANSITION";
        return "UNKNOWN";
    }

    private void TriggerIndoorTestMode()
    {
        // 1. Sync IndoorAutoEnterB9.defaultBuilding TRƯỚC để tránh conflict.
        var autoEnter = FindFirstObjectByType<IndoorAutoEnterB9>(FindObjectsInactive.Include);
        if (autoEnter != null)
        {
            autoEnter.OverrideDefaultBuilding(selectedIndoorMap);
            Debug.Log($"[HybridMode] [INDOOR_TEST] IndoorAutoEnterB9.defaultBuilding ← {selectedIndoorMap}.");
        }

        // 2. Kích hoạt building indoor ngay lập tức (load map, set VPS code).
        var switcher = FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        if (switcher == null)
        {
            Debug.LogWarning("[HybridMode] [INDOOR_TEST] Không tìm thấy IndoorMapSwitcher trong scene.");
            return;
        }

        bool ok = switcher.SwitchTo(selectedIndoorMap);
        Debug.Log($"[HybridMode] [INDOOR_TEST] IndoorMapSwitcher.SwitchTo({selectedIndoorMap}) => {(ok ? "OK" : "FAILED")}.");

        if (autoEnter != null)
        {
            Debug.Log("[HybridMode] [INDOOR_TEST] IndoorAutoEnterB9 sẽ subscribe LocalizationSuccess và xử lý re-anchor NavMesh.");
        }
    }

    private bool CanSwitch()
    {
        return Time.time - lastSwitchTime >= switchCooldown;
    }

    private void ApplyMode(HybridMode nextMode, string reason)
    {
        // Gate Indoor: chỉ cho vào khi enableIndoorMode=true.
        if (!enableIndoorMode && nextMode == HybridMode.Indoor)
        {
            if (verboseLog)
                Debug.LogWarning($"[HybridMode] Indoor mode disabled — request '{reason}' bị bỏ qua. " +
                                 $"enableIndoorMode=false. Hiện vẫn ở mode={currentMode}.");
            return;
        }

        if (currentMode == nextMode)
        {
            return;
        }

        currentMode = HybridMode.Transition;
        if (hasAppliedInitialMode)
        {
            PlayTransition(nextMode);
        }

        SetEnvironmentActive(nextMode);
        SyncIndoorCameraToOutdoorMount(nextMode);
        ApplyXROriginFreezeForMode(nextMode);

        // Resolve MainCamera while hierarchy matches mode, before canvases / listeners (avoids wrong Camera.main on presentation step).
        if (autoManageMainCameraTag)
        {
            ApplyMainCameraTag(nextMode);
        }

        RebindOutdoorNavigationCameras(nextMode);

        SetModePresentation(nextMode);
        ManageARCameraComponents(nextMode);

        currentMode = nextMode;

        audioListenersDirty = true;

        lastSwitchTime = Time.time;
        ResetTimers();
        hasAppliedInitialMode = true;

        if (verboseLog)
        {
            string modeTag = GetModeTag(currentMode);
            Debug.Log($"[HybridMode] [{modeTag}] -> {currentMode} | reason={reason} | gpsAccuracy={lastGpsAccuracy:F1}m");
        }

        // Nếu forceIndoorTestMode, tự động trigger IndoorMapSwitcher + load map indoor.
        if (nextMode == HybridMode.Indoor && forceIndoorTestMode)
        {
            TriggerIndoorTestMode();
        }
    }

    private void RequestModeWithPermissions(HybridMode nextMode, string reason)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Application.isPlaying &&
            requestAndroidPermissionsBeforeAR &&
            !HasRequiredAndroidPermissions(nextMode))
        {
            if (pendingPermissionRoutine != null)
            {
                StopCoroutine(pendingPermissionRoutine);
            }

            pendingPermissionRoutine = StartCoroutine(ApplyModeAfterAndroidPermissions(nextMode, reason));
            return;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        if (Application.isPlaying &&
            requestIOSCameraPermissionBeforeAR &&
            !Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            if (pendingPermissionRoutine != null)
            {
                StopCoroutine(pendingPermissionRoutine);
            }

            pendingPermissionRoutine = StartCoroutine(ApplyModeAfterIOSPermissions(nextMode, reason));
            return;
        }
#endif

        runtimePermissionStatus = null;
        SetRuntimeModeButtonsInteractable(true);
        ApplyMode(nextMode, reason);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private bool HasRequiredAndroidPermissions(HybridMode mode)
    {
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            return false;
        }

        if (mode == HybridMode.Outdoor &&
            requireLocationPermissionForOutdoor &&
            !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            return false;
        }

        return true;
    }

    private IEnumerator ApplyModeAfterAndroidPermissions(HybridMode nextMode, string reason)
    {
        SetRuntimeModeSwitcherVisible(true);
        SetRuntimeModeButtonsInteractable(false);

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            runtimePermissionStatus = "Allow Camera";
            yield return RequestAndroidPermission(UnityEngine.Android.Permission.Camera);
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            runtimePermissionStatus = "Camera denied";
            Debug.LogError("[HybridMode] Android camera permission is required before AR can render the device camera.");
            SetRuntimeModeButtonsInteractable(true);
            pendingPermissionRoutine = null;
            yield break;
        }

        if (nextMode == HybridMode.Outdoor &&
            requireLocationPermissionForOutdoor &&
            !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            runtimePermissionStatus = "Allow Location";
            yield return RequestAndroidPermission(UnityEngine.Android.Permission.FineLocation);
        }

        if (nextMode == HybridMode.Outdoor &&
            requireLocationPermissionForOutdoor &&
            !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            runtimePermissionStatus = "Location denied";
            Debug.LogError("[HybridMode] Android fine location permission is required for Outdoor GPS mode.");
            SetRuntimeModeButtonsInteractable(true);
            pendingPermissionRoutine = null;
            yield break;
        }

        runtimePermissionStatus = null;
        SetRuntimeModeButtonsInteractable(true);
        pendingPermissionRoutine = null;
        ApplyMode(nextMode, reason);
    }

    private IEnumerator RequestAndroidPermission(string permission)
    {
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
        {
            yield break;
        }

        bool resolved = false;
        UnityEngine.Android.PermissionCallbacks callbacks = new UnityEngine.Android.PermissionCallbacks();
        callbacks.PermissionGranted += grantedPermission =>
        {
            if (grantedPermission == permission)
            {
                resolved = true;
            }
        };
        callbacks.PermissionDenied += deniedPermission =>
        {
            if (deniedPermission == permission)
            {
                resolved = true;
            }
        };
        callbacks.PermissionDeniedAndDontAskAgain += deniedPermission =>
        {
            if (deniedPermission == permission)
            {
                resolved = true;
            }
        };

        UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);

        float elapsed = 0f;
        while (!resolved && elapsed < permissionRequestTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    private IEnumerator ApplyModeAfterIOSPermissions(HybridMode nextMode, string reason)
    {
        SetRuntimeModeSwitcherVisible(true);
        SetRuntimeModeButtonsInteractable(false);
        runtimePermissionStatus = "Allow Camera";

        IosCameraPermissionBridge.AuthorizationStatus nativeStatus =
            IosCameraPermissionBridge.GetAuthorizationStatus();
        Debug.Log($"[HybridMode] iOS native camera authorization before request: {nativeStatus}.");

        if (nativeStatus == IosCameraPermissionBridge.AuthorizationStatus.NotDetermined &&
            IosCameraPermissionBridge.RequestAuthorization())
        {
            float elapsed = 0f;
            while (nativeStatus == IosCameraPermissionBridge.AuthorizationStatus.NotDetermined &&
                   elapsed < permissionRequestTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
                nativeStatus = IosCameraPermissionBridge.GetAuthorizationStatus();
            }
        }
        else if (nativeStatus == IosCameraPermissionBridge.AuthorizationStatus.Unavailable)
        {
            // Fallback for an old Xcode export that does not contain the native bridge.
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            nativeStatus = Application.HasUserAuthorization(UserAuthorization.WebCam)
                ? IosCameraPermissionBridge.AuthorizationStatus.Authorized
                : IosCameraPermissionBridge.AuthorizationStatus.Denied;
        }

        if (nativeStatus != IosCameraPermissionBridge.AuthorizationStatus.Authorized)
        {
            runtimePermissionStatus = nativeStatus == IosCameraPermissionBridge.AuthorizationStatus.Restricted
                ? "Camera restricted"
                : "Camera denied";
            Debug.LogError(
                $"[HybridMode] iOS camera permission failed with native status {nativeStatus}. " +
                "Reset Location & Privacy or allow Camera in iOS Settings before entering AR.");
            SetRuntimeModeButtonsInteractable(true);
            pendingPermissionRoutine = null;
            yield break;
        }

        runtimePermissionStatus = null;
        SetRuntimeModeButtonsInteractable(true);
        pendingPermissionRoutine = null;
        ApplyMode(nextMode, reason);
    }
#endif

    /// <summary>
    /// Freezes GPS-driven XR Origin updates while in Indoor mode so Multiset VPS can
    /// drive positioning without GPS interference. Resets one-shot alignment on return
    /// to Outdoor so the camera re-anchors to GPS correctly.
    /// </summary>
    private void ApplyXROriginFreezeForMode(HybridMode mode)
    {
        bool freeze = mode == HybridMode.Indoor;
        if (simpleGpsTracker != null)
            simpleGpsTracker.freezeXROriginUpdate = freeze;
        if (gpsMarker != null)
            gpsMarker.freezeXROriginUpdate = freeze;
        // Reset one-shot flag so camera re-aligns to GPS when returning outdoors
        if (!freeze && alignXROriginToUser != null)
            alignXROriginToUser.aligned = false;
    }

    private void SetEnvironmentActive(HybridMode mode)
    {
        bool indoorActive = alwaysKeepBothEnvironmentsActive || mode == HybridMode.Indoor || keepIndoorActiveWhileOutdoor;
        bool outdoorActive = alwaysKeepBothEnvironmentsActive || mode == HybridMode.Outdoor || keepOutdoorActiveWhileIndoor;

        if (indoorEnvironment != null)
        {
            indoorEnvironment.SetActive(indoorActive);
        }

        if (outdoorEnvironment != null)
        {
            outdoorEnvironment.SetActive(outdoorActive);
        }

        if (indoorVisualRoot != null)
        {
            // UI Home Screen / Multiset stack: bật khi Indoor (VPS + map).
            // Outdoor primary HUD không cần Multiset chrome — OutdoorUiPrimaryHud ẩn canvas UI.
            // Khi alwaysKeepBothEnvironmentsActive, environment root vẫn sống; visual root theo mode.
            bool showIndoorUi = mode == HybridMode.Indoor;
            SetRootActiveIfNotProtected(indoorVisualRoot, showIndoorUi);
        }

        SetRootsActive(alwaysActiveRoots, true);

        if (_detachedOutdoorXrRigRoot != null)
            _detachedOutdoorXrRigRoot.SetActive(indoorActive || outdoorActive);

        // Do not disable indoor ARSession in Awake: when Outdoor is off, indoor may be the only session driving
        // the camera. Only suppress indoor sessions while the outdoor hierarchy is active (dual-session guard).
        ApplyIndoorArSessionDuplicatePolicy(outdoorActive);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        WarnIfMultipleActiveArSessions(mode);
#endif
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void WarnIfMultipleActiveArSessions(HybridMode mode)
    {
        if (mode != HybridMode.Indoor && mode != HybridMode.Outdoor)
        {
            return;
        }

        int count = 0;
        foreach (ARSession session in FindObjectsByType<ARSession>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (session != null && session.enabled && session.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        if (count > 1)
        {
            Debug.LogWarning(
                "[HybridMode] " + count +
                " ARSession component(s) are active at once. That often causes black camera or AR instability. " +
                "Search the Hierarchy for extra \"AR Session\" GameObjects (including under UI or duplicate stacks). " +
                "Only one session should be active for the current mode.");
        }
    }
#endif

    /// <summary>
    /// Moves the outdoor AR rig out of <see cref="outdoorEnvironment"/> so Indoor mode can disable the
    /// outdoor hierarchy without losing the device camera. Optionally soft-disables indoor duplicate XROrigins.
    /// </summary>
    private void DetachOutdoorXrRigForSharedCamera()
    {
        if (!detachOutdoorXrRigFromEnvironment || outdoorEnvironment == null)
        {
            return;
        }

        Transform detachTf = null;
        if (outdoorXrRigRootOverride != null)
        {
            detachTf = outdoorXrRigRootOverride.transform;
        }
        else
        {
            XROrigin xr = outdoorEnvironment.GetComponentInChildren<XROrigin>(true);
            if (xr != null)
            {
                detachTf = xr.transform;
            }
        }

        if (detachTf == null)
        {
            if (verboseLog)
            {
                Debug.Log("[HybridMode] Detach XR: no XROrigin / override under OutdoorEnvironment — skip.");
            }

            return;
        }

        if (detachTf.parent != null)
        {
            detachTf.SetParent(null, true);
        }

        _detachedOutdoorXrRigRoot = detachTf.gameObject;

        // Đây là shared rig thật, phải sống xuyên suốt cả hai mode.
        _detachedOutdoorXrRigRoot.SetActive(true);

        if (verboseLog)
        {
            Debug.Log($"[HybridMode] Shared XR rig detached to scene root: {_detachedOutdoorXrRigRoot.name}");
        }

        // Khi both environments luôn on, indoor XR Origin cần sống để VPS tracking.
        if (!alwaysKeepBothEnvironmentsActive && disableIndoorXROriginDuplicates && indoorEnvironment != null)
        {
            foreach (XROrigin indoorXr in indoorEnvironment.GetComponentsInChildren<XROrigin>(true))
            {
                if (indoorXr == null)
                {
                    continue;
                }

                indoorXr.enabled = false;
            }
        }

        hasCachedPresentationReferences = false;
    }

    /// <summary>
    /// When the outdoor environment stack is active, optionally disable duplicate <see cref="ARSession"/> under
    /// indoor. When outdoor is off, re-enable them so Indoor mode can keep a working camera feed (disabling
    /// them once in <c>Awake</c> left components off for the whole session and caused a black screen).
    /// </summary>
    private void ApplyIndoorArSessionDuplicatePolicy(bool outdoorStackActive)
    {
        if (enforceSingleARSessionAtRuntime)
        {
            EnforceSingleARSessionTopology();
            return;
        }

        if (!disableIndoorARSessionDuplicates || indoorEnvironment == null)
        {
            return;
        }

        // Khi alwaysKeepBothEnvironmentsActive: cả 2 environment cùng chạy → không disable indoor session.
        if (alwaysKeepBothEnvironmentsActive) return;

        foreach (ARSession session in indoorEnvironment.GetComponentsInChildren<ARSession>(true))
        {
            if (session == null)
            {
                continue;
            }

            bool enableIndoorSession = !outdoorStackActive;
            if (session.enabled == enableIndoorSession)
            {
                continue;
            }

            session.enabled = enableIndoorSession;
            if (verboseLog)
            {
                Debug.Log(
                    $"[HybridMode] Indoor ARSession on '{session.gameObject.name}' -> enabled={enableIndoorSession} " +
                    $"(outdoor stack active={outdoorStackActive}).");
            }
        }
    }

    private void EnforceSingleARSessionTopology()
    {
        if (!enforceSingleARSessionAtRuntime)
        {
            return;
        }

        ARSession[] sessions = FindObjectsByType<ARSession>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (sessions == null || sessions.Length == 0)
        {
            return;
        }

        if (_primaryARSession == null)
        {
            if (outdoorEnvironment != null)
            {
                ARSession[] outdoorSessions =
                    outdoorEnvironment.GetComponentsInChildren<ARSession>(true);
                if (outdoorSessions.Length > 0)
                {
                    _primaryARSession = outdoorSessions[0];
                }
            }

            _primaryARSession ??= sessions[0];

            // Session dùng chung không được chết khi một environment root bị tắt.
            Transform sessionTransform = _primaryARSession.transform;
            if (sessionTransform.parent != null)
            {
                sessionTransform.SetParent(null, true);
            }
            _primaryARSession.gameObject.SetActive(true);
        }

        foreach (ARSession session in sessions)
        {
            if (session == null) continue;
            bool shouldEnable = ReferenceEquals(session, _primaryARSession);
            if (session.enabled == shouldEnable) continue;

            session.enabled = shouldEnable;
            if (verboseLog)
            {
                Debug.Log(
                    $"[HybridMode] ARSession '{session.gameObject.name}' enabled={shouldEnable} " +
                    "(single-session topology).");
            }
        }
    }

    private void CachePresentationReferences()
    {
        if (hasCachedPresentationReferences)
        {
            return;
        }

        if (indoorVisualRoot != null && !indoorOnlyVisualRoots.Contains(indoorVisualRoot))
        {
            indoorOnlyVisualRoots.Add(indoorVisualRoot);
        }

        if (autoManageAudioSources)
        {
            AddAudioSources(indoorEnvironment, indoorAudioSources);
            AddAudioSources(outdoorEnvironment, outdoorAudioSources);
            if (_detachedOutdoorXrRigRoot != null)
            {
                AddAudioSources(_detachedOutdoorXrRigRoot, outdoorAudioSources);
            }
        }

        if (autoManageAudioListeners)
        {
            AddAudioListeners(indoorEnvironment, indoorAudioListeners);
            AddAudioListeners(outdoorEnvironment, outdoorAudioListeners);
            if (_detachedOutdoorXrRigRoot != null)
            {
                AddAudioListeners(_detachedOutdoorXrRigRoot, outdoorAudioListeners);
            }
        }

        if (autoManageMainCameraTag)
        {
            if (indoorMainCamera == null)
            {
                indoorMainCamera = FindPreferredCamera(indoorEnvironment, "ARCamera");
            }

            if (outdoorMainCamera == null)
            {
                outdoorMainCamera = FindPreferredCamera(outdoorEnvironment, "Main Camera");
            }

            if (outdoorMainCamera == null && _detachedOutdoorXrRigRoot != null)
            {
                outdoorMainCamera = FindPreferredCamera(_detachedOutdoorXrRigRoot, "Main Camera");
            }

            // Tìm thêm trong alwaysActiveRoots (ví dụ SharedARRig khi không dùng detach)
            if (outdoorMainCamera == null && alwaysActiveRoots != null)
            {
                foreach (GameObject root in alwaysActiveRoots)
                {
                    Camera cam = FindPreferredCamera(root, "Main Camera");
                    if (cam != null) { outdoorMainCamera = cam; break; }
                }
            }
        }

        hasCachedPresentationReferences = true;
    }

    private void SetModePresentation(HybridMode mode)
    {
        if (!manageModePresentation)
        {
            return;
        }

        // Khi alwaysKeepBothEnvironmentsActive: cả 2 luôn chạy đồng thời, skip toàn bộ toggle visual/canvas/audio.
        if (alwaysKeepBothEnvironmentsActive)
        {
            return;
        }

        bool indoorVisible  = mode == HybridMode.Indoor;
        bool outdoorVisible = mode == HybridMode.Outdoor;

        SetRootsActive(indoorOnlyVisualRoots, indoorVisible);
        SetRootsActive(outdoorOnlyVisualRoots, outdoorVisible);

        if (autoManageCanvases)
        {
            SetCanvasesEnabled(indoorEnvironment, indoorVisible);
            SetCanvasesEnabled(outdoorEnvironment, outdoorVisible);
        }

        if (autoManageAudioSources)
        {
            SetAudioSourcesEnabled(indoorAudioSources, indoorVisible);
            SetAudioSourcesEnabled(outdoorAudioSources, outdoorVisible);
        }

        if (autoManageAudioListeners)
        {
            SetAudioListenersEnabled(indoorAudioListeners, indoorVisible);
            SetAudioListenersEnabled(outdoorAudioListeners, outdoorVisible);
            EnforceSingleAudioListener(mode);
        }

    }

    /// <summary>
    /// Bật/tắt <c>ARCameraManager</c> và <c>ARCameraBackground</c> trên mỗi camera
    /// theo mode hiện tại. Ngăn tình trạng camera outdoor bị đen sau khi
    /// từ Indoor chuyển về Outdoor do 2 ARCameraManager cùng active tranh nhau AR Session.
    /// Khi indoor + outdoor cùng trỏ một camera (SharedARRig), chỉ set enabled một lần — gọi lần hai
    /// với outdoorActive=false sẽ tắt nhầm feed (màn đen Indoor trên máy thật).
    /// </summary>
    private void ManageARCameraComponents(HybridMode mode)
    {
        bool indoorPhase  = mode == HybridMode.Indoor;
        bool outdoorPhase = mode == HybridMode.Outdoor;

        Camera outdoorCam = ResolveOutdoorPresentationCamera();
        if (outdoorCam == null)
        {
            outdoorCam = LastResortFindPresentationCamera();
        }

        Camera indoorCam = indoorMainCamera;
        if ((indoorCam == null || !indoorCam.gameObject.activeInHierarchy) && indoorEnvironment != null)
        {
            indoorCam = FindPreferredCamera(indoorEnvironment, "ARCamera");
        }

        // HybridGPSMap single rig: duplicate XROrigin disabled, no separate indoor ARCamera.
        if (indoorCam == null)
        {
            indoorCam = outdoorCam;
        }

        bool samePhysicalCamera = indoorCam != null && outdoorCam != null &&
            ReferenceEquals(indoorCam, outdoorCam);

        bool arShouldRun = indoorPhase || outdoorPhase;

        if (samePhysicalCamera)
        {
            SetARCameraComponents(indoorCam, arShouldRun);
            return;
        }

        SetARCameraComponents(indoorCam, indoorPhase);
        SetARCameraComponents(outdoorCam, outdoorPhase);
    }

    /// <summary>Shared / outdoor AR display camera (Main Camera on SharedARRig, detached rig, or outdoor env).</summary>
    private Camera ResolveOutdoorPresentationCamera()
    {
        if (_detachedOutdoorXrRigRoot != null && _detachedOutdoorXrRigRoot.activeInHierarchy)
        {
            Camera fromDetached = FindPreferredCamera(_detachedOutdoorXrRigRoot, "Main Camera");
            if (fromDetached != null)
            {
                return fromDetached;
            }
        }

        if (outdoorMainCamera != null && outdoorMainCamera.gameObject.activeInHierarchy)
        {
            return outdoorMainCamera;
        }

        if (alwaysActiveRoots != null)
        {
            foreach (GameObject root in alwaysActiveRoots)
            {
                if (root == null)
                {
                    continue;
                }

                Camera cam = FindPreferredCamera(root, "Main Camera");
                if (cam != null && cam.gameObject.activeInHierarchy)
                {
                    return cam;
                }
            }
        }

        if (outdoorEnvironment != null)
        {
            Camera fromOutdoorEnv = FindPreferredCamera(outdoorEnvironment, "Main Camera");
            if (fromOutdoorEnv != null && fromOutdoorEnv.gameObject.activeInHierarchy)
            {
                return fromOutdoorEnv;
            }
        }

        return LastResortFindPresentationCamera();
    }

    /// <summary>
    /// Khi chuyển qua Indoor, reparent indoor camera vào outdoor XR Origin mount
    /// để cả 2 camera ở cùng world position → không giật camera, WASD vẫn move được.
    /// </summary>
    private void SyncIndoorCameraToOutdoorMount(HybridMode nextMode)
    {
        if (nextMode != HybridMode.Indoor) return;
        if (!hasCachedPresentationReferences) CachePresentationReferences();

        Camera indoorCam = indoorMainCamera;
        if (indoorCam == null && indoorEnvironment != null)
            indoorCam = FindPreferredCamera(indoorEnvironment, "ARCamera");

        Camera outdoorCam = ResolveOutdoorPresentationCamera();
        if (outdoorCam == null) outdoorCam = LastResortFindPresentationCamera();
        if (outdoorCam == null || outdoorCam.transform.parent == null) return;
        if (indoorCam == null || ReferenceEquals(indoorCam, outdoorCam)) return;

        Transform mount = outdoorCam.transform.parent;
        indoorCam.transform.SetParent(mount, false);
        indoorCam.transform.localPosition = Vector3.zero;
        indoorCam.transform.localRotation = Quaternion.identity;

        if (verboseLog)
            Debug.Log($"[HybridMode] Indoor camera '{indoorCam.name}' reparented to '{mount.name}' @ identity.");
    }

    /// <summary>
    /// Avoid null from ResolveOutdoor when refs are stale — null + ApplyMainCameraTag would strip all MainCamera tags.
    /// Skips minimap / render-texture cameras.
    /// </summary>
    private Camera LastResortFindPresentationCamera()
    {
        if (alwaysActiveRoots != null)
        {
            foreach (GameObject root in alwaysActiveRoots)
            {
                if (root == null || !root.activeInHierarchy)
                {
                    continue;
                }

                Camera found = FindPresentationCameraExcludingMinimap(root);
                if (found != null)
                {
                    return found;
                }
            }
        }

        Camera tagged = Camera.main;
        if (tagged != null && tagged.targetTexture == null && !IsLikelyMinimapOrOffscreenCamera(tagged))
        {
            return tagged;
        }

        foreach (Unity.XR.CoreUtils.XROrigin xr in FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (xr == null || !xr.isActiveAndEnabled)
            {
                continue;
            }

            Camera c = xr.GetComponentInChildren<Camera>(true);
            if (c != null && c.gameObject.activeInHierarchy && c.targetTexture == null &&
                !IsLikelyMinimapOrOffscreenCamera(c))
            {
                return c;
            }
        }

        return null;
    }

    private static Camera FindPresentationCameraExcludingMinimap(GameObject root)
    {
        Camera[] cams = root.GetComponentsInChildren<Camera>(true);
        foreach (Camera c in cams)
        {
            if (c == null || !c.gameObject.activeInHierarchy || c.targetTexture != null)
            {
                continue;
            }

            if (IsLikelyMinimapOrOffscreenCamera(c))
            {
                continue;
            }

            if (c.name.Equals("Main Camera", System.StringComparison.Ordinal))
            {
                return c;
            }
        }

        foreach (Camera c in cams)
        {
            if (c == null || !c.gameObject.activeInHierarchy || c.targetTexture != null)
            {
                continue;
            }

            if (IsLikelyMinimapOrOffscreenCamera(c))
            {
                continue;
            }

            return c;
        }

        return null;
    }

    private static bool IsLikelyMinimapOrOffscreenCamera(Camera c)
    {
        if (c == null)
        {
            return true;
        }

        string n = c.gameObject.name;
        if (n.IndexOf("Minimap", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (n.IndexOf("Map", System.StringComparison.OrdinalIgnoreCase) >= 0 && c.orthographic)
        {
            return true;
        }

        return false;
    }

    private static void SetARCameraComponents(Camera cam, bool enabled)
    {
        if (cam == null) return;

        UnityEngine.XR.ARFoundation.ARCameraManager mgr =
            cam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
        if (mgr != null) mgr.enabled = enabled;

        UnityEngine.XR.ARFoundation.ARCameraBackground bg =
            cam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
        if (bg != null) bg.enabled = enabled;

        ArCameraPoseGuard poseGuard = cam.GetComponent<ArCameraPoseGuard>();
        if (enabled)
        {
            EnsureARCameraPoseTracking(cam);
        }
        else if (poseGuard != null)
        {
            poseGuard.enabled = false;
        }
    }

    private static void EnsureARCameraPoseTracking(Camera cam)
    {
        if (cam == null) return;
        ArCameraPoseGuard.EnsureOn(cam);
    }

    private void SetRootsActive(List<GameObject> roots, bool active)
    {
        if (roots == null)
        {
            return;
        }

        foreach (GameObject root in roots)
        {
            if (root != null)
            {
                SetRootActiveIfNotProtected(root, active);
            }
        }
    }

    private void SetRootsActiveDirect(List<GameObject> roots, bool active)
    {
        if (roots == null)
        {
            return;
        }

        foreach (GameObject root in roots)
        {
            SetRootActiveDirect(root, active);
        }
    }

    private void SetRootActiveDirect(GameObject root, bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
    }

    private void SetRootActiveIfNotProtected(GameObject root, bool active)
    {
        if (root == null)
        {
            return;
        }

        if (IsProtectedRoot(root))
        {
            root.SetActive(true);
            return;
        }

        root.SetActive(active);
    }

    private bool IsProtectedRoot(GameObject root)
    {
        if (alwaysActiveRoots == null || root == null)
        {
            return false;
        }

        foreach (GameObject protectedRoot in alwaysActiveRoots)
        {
            if (protectedRoot == null)
            {
                continue;
            }

            if (root == protectedRoot || root.transform.IsChildOf(protectedRoot.transform))
            {
                return true;
            }
        }

        return false;
    }

    private void SetCanvasesEnabled(GameObject root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = enabled;
        }
    }

    private void AddAudioSources(GameObject root, List<AudioSource> target)
    {
        if (root == null || target == null)
        {
            return;
        }

        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource source in sources)
        {
            if (source != null && !target.Contains(source))
            {
                target.Add(source);
            }
        }
    }

    private void SetAudioSourcesEnabled(List<AudioSource> sources, bool enabled)
    {
        if (sources == null)
        {
            return;
        }

        foreach (AudioSource source in sources)
        {
            if (source == null)
            {
                continue;
            }

            source.mute = !enabled;
            if (!enabled && source.isPlaying)
            {
                source.Pause();
            }
            else if (enabled && source.playOnAwake && !source.isPlaying)
            {
                source.UnPause();
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
        }
    }

    private void AddAudioListeners(GameObject root, List<AudioListener> target)
    {
        if (root == null || target == null)
        {
            return;
        }

        AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
        foreach (AudioListener listener in listeners)
        {
            if (listener != null && !target.Contains(listener))
            {
                target.Add(listener);
            }
        }
    }

    private void SetAudioListenersEnabled(List<AudioListener> listeners, bool enabled)
    {
        if (listeners == null)
        {
            return;
        }

        foreach (AudioListener listener in listeners)
        {
            if (listener != null)
            {
                listener.enabled = enabled;
            }
        }
    }

    private Camera FindPreferredCamera(GameObject root, string preferredName)
    {
        if (root == null)
        {
            return null;
        }

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.name == preferredName)
            {
                return camera;
            }
        }

        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.targetTexture == null)
            {
                return camera;
            }
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    private void ApplyMainCameraTag(HybridMode mode)
    {
        Camera preferred = null;

        if (mode == HybridMode.Indoor)
        {
            preferred = indoorMainCamera;
            if (preferred == null || !preferred.gameObject.activeInHierarchy)
            {
                preferred = indoorEnvironment != null
                    ? FindPreferredCamera(indoorEnvironment, "ARCamera")
                    : null;
            }

            Camera sharedCam = ResolveOutdoorPresentationCamera();
            if ((preferred == null || !preferred.gameObject.activeInHierarchy) && sharedCam != null)
            {
                preferred = sharedCam;
            }
        }
        else
        {
            // After DetachOutdoorXrRigForSharedCamera the live AR camera is usually on the detached root, not under outdoorEnvironment.
            preferred = ResolveOutdoorPresentationCamera();
        }

        if (preferred == null)
        {
            preferred = LastResortFindPresentationCamera();
        }

        if (preferred == null)
        {
            if (verboseLog)
            {
                Debug.LogWarning("[HybridMode] ApplyMainCameraTag: no presentation camera resolved — skipping retag (avoids stripping all MainCamera tags).");
            }

            return;
        }

        if (_detachedOutdoorXrRigRoot != null)
        {
            ClearMainCameraTag(_detachedOutdoorXrRigRoot, preferred);
        }

        ClearMainCameraTag(indoorEnvironment, preferred);
        ClearMainCameraTag(outdoorEnvironment, preferred);

        // SharedARRig is often sibling to OutdoorEnvironment; clear lingering MainCamera here or Camera.main/indoor mismatch.
        if (alwaysActiveRoots != null)
        {
            foreach (GameObject root in alwaysActiveRoots)
            {
                if (root == null)
                {
                    continue;
                }

                ClearMainCameraTag(root, preferred);
            }
        }

        if (preferred != null)
        {
            preferred.tag = "MainCamera";
        }
    }

    /// <summary>
    /// GPSMapPlane wires AR camera in Inspector; hybrid retags MainCamera on the detached rig, so path + GPS must follow the same reference.
    /// </summary>
    private void RebindOutdoorNavigationCameras(HybridMode mode)
    {
        if (mode != HybridMode.Outdoor)
        {
            return;
        }

        Camera main = Camera.main;
        if (main == null)
        {
            return;
        }

        foreach (ARPathFinder finder in FindObjectsByType<ARPathFinder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (finder == null || IsUnderEnvironmentRoot(finder.transform, indoorEnvironment))
            {
                continue;
            }

            finder.RebindToDisplayCamera(main);
        }

        foreach (SimpleGPSTracker tracker in FindObjectsByType<SimpleGPSTracker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (tracker == null || IsUnderEnvironmentRoot(tracker.transform, indoorEnvironment))
            {
                continue;
            }

            tracker.RebindArCamera(main);
        }
    }

    private static bool IsUnderEnvironmentRoot(Transform t, GameObject envRoot)
    {
        return envRoot != null && t != null && t.IsChildOf(envRoot.transform);
    }

    private void ClearMainCameraTag(GameObject root, Camera preferred)
    {
        if (root == null)
        {
            return;
        }

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera == preferred)
            {
                continue;
            }

            if (camera.CompareTag("MainCamera"))
            {
                camera.tag = "Untagged";
            }
        }
    }

    private void EnforceSingleAudioListener(HybridMode mode)
    {
        if (!audioListenersDirty) return;
        audioListenersDirty = false;

        AudioListener[] allListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allListeners == null || allListeners.Length == 0)
        {
            return;
        }

        AudioListener preferred = SelectPreferredAudioListener(mode, allListeners);

        foreach (AudioListener listener in allListeners)
        {
            if (listener != null)
            {
                listener.enabled = listener == preferred;
            }
        }
    }

    private AudioListener SelectPreferredAudioListener(HybridMode mode, AudioListener[] allListeners)
    {
        AudioListener preferred = mode == HybridMode.Indoor
            ? SelectCameraAudioListener(indoorAudioListeners)
            : SelectCameraAudioListener(outdoorAudioListeners);

        if (preferred != null)
        {
            return preferred;
        }

        preferred = mode == HybridMode.Indoor
            ? SelectCameraAudioListener(outdoorAudioListeners)
            : SelectCameraAudioListener(indoorAudioListeners);

        if (preferred != null)
        {
            return preferred;
        }

        foreach (AudioListener listener in allListeners)
        {
            if (listener == null || !listener.gameObject.activeInHierarchy)
            {
                continue;
            }

            Camera camera = listener.GetComponent<Camera>();
            if (camera != null && camera.enabled && camera.targetTexture == null)
            {
                return listener;
            }
        }

        foreach (AudioListener listener in allListeners)
        {
            if (listener != null && listener.gameObject.activeInHierarchy)
            {
                return listener;
            }
        }

        return allListeners[0];
    }

    private AudioListener SelectCameraAudioListener(List<AudioListener> listeners)
    {
        if (listeners == null)
        {
            return null;
        }

        foreach (AudioListener listener in listeners)
        {
            if (listener == null || !listener.gameObject.activeInHierarchy)
            {
                continue;
            }

            Camera camera = listener.GetComponent<Camera>();
            if (camera != null && camera.enabled && camera.targetTexture == null)
            {
                return listener;
            }
        }

        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener.gameObject.activeInHierarchy)
            {
                return listener;
            }
        }

        return null;
    }

    private void CreateTransitionOverlayIfNeeded()
    {
        if (!Application.isPlaying || !createTransitionOverlay || transitionCanvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Hybrid Transition Overlay");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390f, 844f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        transitionCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        transitionCanvasGroup.alpha = 0f;
        transitionCanvasGroup.blocksRaycasts = false;
        transitionCanvasGroup.interactable = false;

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.02f, 0.025f, 0.03f, 0.68f);

        GameObject cardObject = new GameObject("Message");
        cardObject.transform.SetParent(panelObject.transform, false);
        RectTransform cardRect = cardObject.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(280f, 78f);

        Image card = cardObject.AddComponent<Image>();
        card.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(cardObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -10f);

        transitionText = textObject.AddComponent<TextMeshProUGUI>();
        transitionText.alignment = TextAlignmentOptions.Center;
        transitionText.color = Color.white;
        transitionText.fontSize = 18f;
        transitionText.fontStyle = FontStyles.Bold;
        transitionText.text = string.Empty;
    }

    private void CreateSharedOutdoorHudIfNeeded()
    {
        if (!Application.isPlaying || !createSharedOutdoorHud)
        {
            return;
        }

        if (GetComponent<SharedARUIController>() == null)
        {
            gameObject.AddComponent<SharedARUIController>();
        }
    }

    private void CreateRuntimeModeSwitcherIfNeeded()
    {
        if (!Application.isPlaying || !createRuntimeModeSwitcher || runtimeModeSwitcherCanvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Hybrid Runtime Mode Switcher");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5400;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390f, 844f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        runtimeModeSwitcherCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        ApplyRuntimeModeSwitcherVisibility();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        float panelHeight = showRuntimeModeSwitcherStatusLine ? 66f : 44f;

        if (anchorRuntimeModeSwitcherAtBottom)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = runtimeModeSwitcherOffset;
            panelRect.sizeDelta = new Vector2(248f, panelHeight);
        }
        else
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = runtimeModeSwitcherOffset;
            panelRect.sizeDelta = new Vector2(210f, panelHeight);
        }

        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.02f, 0.025f, 0.03f, 0.82f);

        VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(7, 7, showRuntimeModeSwitcherStatusLine ? 5 : 6, showRuntimeModeSwitcherStatusLine ? 5 : 6);
        panelLayout.spacing = 5f;
        panelLayout.childAlignment = TextAnchor.MiddleCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        runtimeModeStatusText = null;
        if (showRuntimeModeSwitcherStatusLine)
        {
            runtimeModeStatusText = CreateRuntimeModeText("Status", panelObject.transform, "Transition | GPS Stopped | N/A", 10f);
            LayoutElement statusLayout = runtimeModeStatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredHeight = 18f;
        }

        GameObject buttonRow = new GameObject("Buttons");
        buttonRow.transform.SetParent(panelObject.transform, false);
        RectTransform rowRect = buttonRow.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 32f);

        HorizontalLayoutGroup rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 4f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        if (enableIndoorMode)
        {
            runtimeIndoorButton = CreateRuntimeModeButton("Indoor Button", "Indoor", buttonRow.transform, ForceIndoor);
        }
        runtimeOutdoorButton = CreateRuntimeModeButton("Outdoor Button", "Outdoor", buttonRow.transform, ForceOutdoor);
        runtimeOffButton = CreateRuntimeModeButton("Back Button", "Quay về", buttonRow.transform, ReturnToUI);
    }

    private TextMeshProUGUI CreateRuntimeModeText(string objectName, Transform parent, string text, float fontSize)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 28f);

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = FontStyles.Bold;
        textComponent.alignment = TextAlignmentOptions.Left;
        textComponent.color = Color.white;
        textComponent.textWrappingMode = TextWrappingModes.NoWrap;
        return textComponent;
    }

    private Button CreateRuntimeModeButton(string objectName, string label, Transform parent, UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);
        buttonObject.AddComponent<RectTransform>();

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.18f, 0.22f, 0.96f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI labelText = CreateRuntimeModeText("Label", buttonObject.transform, label, 10f);
        labelText.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 28f;
        layout.preferredHeight = 28f;
        layout.minWidth = 52f;

        return button;
    }

    private void UpdateRuntimeModeSwitcher()
    {
        if (Time.time - lastUITextTime < 0.5f) return;
        lastUITextTime = Time.time;

        if (runtimeModeStatusText != null)
        {
            string gpsStatus = Input.location.status.ToString();
            string accuracyText = "N/A";
            if (Input.location.status == LocationServiceStatus.Running)
            {
                float accuracy = Input.location.lastData.horizontalAccuracy;
                accuracyText = accuracy > 0f ? $"{accuracy:0.#}m" : "N/A";
            }

            string modeTag = GetModeTag(currentMode);
            string newText = !string.IsNullOrEmpty(runtimePermissionStatus)
                ? runtimePermissionStatus
                : $"[{modeTag}] {currentMode} | GPS {gpsStatus} | {accuracyText}";

            if (newText != _lastStatusText)
            {
                runtimeModeStatusText.text = newText;
                _lastStatusText = newText;
            }
        }

        if (currentMode != _lastButtonMode)
        {
            if (runtimeIndoorButton != null)
                SetRuntimeModeButtonState(runtimeIndoorButton, currentMode == HybridMode.Indoor);
            SetRuntimeModeButtonState(runtimeOutdoorButton, currentMode == HybridMode.Outdoor);
            SetRuntimeModeButtonState(runtimeOffButton, currentMode == HybridMode.Transition);
            _lastButtonMode = currentMode;
        }
    }

    private void SetRuntimeModeButtonState(Button button, bool active)
    {
        if (button == null || button.image == null)
        {
            return;
        }

        button.image.color = active
            ? new Color(0.12f, 0.55f, 0.96f, 0.98f)
            : new Color(0.16f, 0.18f, 0.22f, 0.96f);
    }

    private void ApplyRuntimeModeSwitcherVisibility()
    {
        if (runtimeModeSwitcherCanvasGroup == null)
        {
            return;
        }

        bool visible = !showRuntimeModeSwitcherOnlyInAR || runtimeModeSwitcherVisible;
        runtimeModeSwitcherCanvasGroup.alpha = visible ? 1f : 0f;
        runtimeModeSwitcherCanvasGroup.interactable = visible;
        runtimeModeSwitcherCanvasGroup.blocksRaycasts = visible;
    }

    private void SetRuntimeModeButtonsInteractable(bool interactable)
    {
        if (runtimeIndoorButton != null)
        {
            runtimeIndoorButton.interactable = interactable;
        }

        if (runtimeOutdoorButton != null)
        {
            runtimeOutdoorButton.interactable = interactable;
        }

        if (runtimeOffButton != null)
        {
            runtimeOffButton.interactable = true;
        }
    }

    private void PlayTransition(HybridMode nextMode)
    {
        if (!createTransitionOverlay || transitionCanvasGroup == null)
        {
            return;
        }

        string message = nextMode == HybridMode.Outdoor
            ? indoorToOutdoorMessage
            : outdoorToIndoorMessage;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(TransitionOverlayRoutine(message));
    }

    private IEnumerator TransitionOverlayRoutine(string message)
    {
        if (transitionText != null)
        {
            transitionText.text = message;
        }

        transitionCanvasGroup.blocksRaycasts = true;
        yield return FadeTransitionOverlay(1f);
        yield return new WaitForSeconds(transitionHoldSeconds);
        yield return FadeTransitionOverlay(0f);
        transitionCanvasGroup.blocksRaycasts = false;
        transitionRoutine = null;
    }

    private IEnumerator FadeTransitionOverlay(float targetAlpha)
    {
        float duration = Mathf.Max(0.01f, transitionFadeSeconds);
        float startAlpha = transitionCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        transitionCanvasGroup.alpha = targetAlpha;
    }

    private bool IsLocalizationGood()
    {
#if UNITY_EDITOR
        if (useMockSignalsInEditor)
        {
            return mockLocalizationGood;
        }
#endif
        return localizationGood;
    }

    private bool IsGpsGood()
    {
#if UNITY_EDITOR
        if (useMockSignalsInEditor)
        {
            lastGpsAccuracy = mockGpsGood ? 5f : 999f;
            return mockGpsGood;
        }
#endif
        if (Input.location.status != LocationServiceStatus.Running)
        {
            lastGpsAccuracy = -1f;
            return false;
        }

        float accuracy = Input.location.lastData.horizontalAccuracy;
        lastGpsAccuracy = accuracy;
        if (accuracy <= 0f)
        {
            return false;
        }

        float threshold = maxGpsAccuracyMeters > 0f
            ? maxGpsAccuracyMeters
            : (gpsMarker != null ? gpsMarker.maxAcceptableAccuracy : 30f);

        return accuracy <= threshold;
    }

    private void ResetTimers()
    {
        localizationGoodTimer = 0f;
        localizationLostTimer = 0f;
        gpsGoodTimer = 0f;
    }
}
