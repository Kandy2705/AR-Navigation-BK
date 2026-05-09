using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HybridModeController : MonoBehaviour
{
    public enum HybridMode
    {
        Outdoor,
        Indoor,
        Transition
    }

    [Header("Environment Roots")]
    [SerializeField] private GameObject indoorEnvironment;
    [SerializeField] private GameObject outdoorEnvironment;
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
    [SerializeField] private Vector2 runtimeModeSwitcherOffset = new Vector2(12f, -58f);

    [Header("Android Permissions")]
    [SerializeField] private bool requestAndroidPermissionsBeforeAR = true;
    [SerializeField] private bool requireLocationPermissionForOutdoor = true;
    [SerializeField] private float permissionRequestTimeoutSeconds = 30f;

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
    [Tooltip("Roots that must stay active across modes, such as AR Session, XR Origin, ARCamera, and shared UI.")]
    [SerializeField] private List<GameObject> alwaysActiveRoots = new List<GameObject>();

    [Header("Signal Sources")]
    [SerializeField] private GPSMarker gpsMarker;

    [Header("Initial State")]
    [SerializeField] private HybridMode initialMode = HybridMode.Outdoor;
    [Tooltip("When disabled, AR environments stay inactive until Apply Initial Mode, Force Indoor, or Force Outdoor is called.")]
    [SerializeField] private bool activateInitialModeOnStart = false;

    [Header("Switch Rules")]
    [Tooltip("When disabled, mode only changes through explicit calls such as Force Indoor, Force Outdoor, or Apply Initial Mode.")]
    [SerializeField] private bool autoSwitchEnabled = false;
    [Tooltip("Seconds of continuous localization failure before allowing Indoor -> Outdoor.")]
    [SerializeField] private float indoorLostToOutdoorDelay = 8f;
    [Tooltip("Seconds GPS must stay good before allowing Indoor -> Outdoor.")]
    [SerializeField] private float gpsStableRequiredTime = 3f;
    [Tooltip("Require a good GPS fix before switching Indoor -> Outdoor. Disable this when GPSMarker lives under OutdoorEnvironment.")]
    [SerializeField] private bool requireGpsForIndoorToOutdoor = false;
    [Tooltip("Seconds of stable indoor localization before allowing Outdoor -> Indoor.")]
    [SerializeField] private float indoorSuccessRequiredTime = 2f;
    [Tooltip("Cooldown to prevent mode flapping.")]
    [SerializeField] private float switchCooldown = 5f;
    [Tooltip("If <= 0, inherit from GPSMarker.maxAcceptableAccuracy.")]
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

    private HybridMode currentMode;
    private bool localizationGood;
    private float localizationGoodTimer;
    private float localizationLostTimer;
    private float gpsGoodTimer;
    private float lastSwitchTime = -999f;
    private float lastGpsAccuracy = -1f;
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

    private void Awake()
    {
        if (!activateInitialModeOnStart)
        {
            DeactivateARMode();
            deactivatedARModeInAwake = true;
        }
    }

    private void Start()
    {
        CachePresentationReferences();
        CreateTransitionOverlayIfNeeded();
        CreateSharedOutdoorHudIfNeeded();
        CreateRuntimeModeSwitcherIfNeeded();
        ResetTimers();
        currentMode = HybridMode.Transition;

        if (activateInitialModeOnStart)
        {
            SetRuntimeModeSwitcherVisible(true);
            ApplyMode(initialMode, "Initialize");
        }
        else
        {
            SetRuntimeModeSwitcherVisible(false);
            if (!deactivatedARModeInAwake)
            {
                DeactivateARMode();
            }
        }
    }

    private void Update()
    {
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
            Debug.Log("[HybridMode] LocalizationSuccess");
        }
    }

    public void OnLocalizationFailure()
    {
        localizationGood = false;
        if (verboseLog)
        {
            Debug.Log("[HybridMode] LocalizationFailure");
        }
    }

    [ContextMenu("Hybrid/Force Indoor")]
    public void ForceIndoor()
    {
        SetRuntimeModeSwitcherVisible(true);
        RequestModeWithPermissions(HybridMode.Indoor, "ForceIndoor");
    }

    [ContextMenu("Hybrid/Force Outdoor")]
    public void ForceOutdoor()
    {
        SetRuntimeModeSwitcherVisible(true);
        RequestModeWithPermissions(HybridMode.Outdoor, "ForceOutdoor");
    }

    [ContextMenu("Hybrid/Apply Initial Mode")]
    public void ApplyInitialMode()
    {
        CachePresentationReferences();
        CreateTransitionOverlayIfNeeded();
        currentMode = HybridMode.Transition;
        SetRuntimeModeSwitcherVisible(true);
        RequestModeWithPermissions(initialMode, "ApplyInitialMode");
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

        SetRootActiveDirect(indoorEnvironment, false);
        SetRootActiveDirect(outdoorEnvironment, false);
        SetRootActiveDirect(indoorVisualRoot, false);
        SetRootsActiveDirect(indoorOnlyVisualRoots, false);
        SetRootsActiveDirect(outdoorOnlyVisualRoots, false);
        SetRootsActiveDirect(alwaysActiveRoots, false);

        if (autoManageCanvases)
        {
            SetCanvasesEnabled(indoorEnvironment, false);
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

    [ContextMenu("Hybrid/Mark Localization Failure")]
    public void DebugLocalizationFailure()
    {
        localizationGood = false;
    }

    private bool CanSwitch()
    {
        return Time.time - lastSwitchTime >= switchCooldown;
    }

    private void ApplyMode(HybridMode nextMode, string reason)
    {
        if (currentMode == nextMode)
        {
            return;
        }

        currentMode = HybridMode.Transition;
        if (hasAppliedInitialMode)
        {
            PlayTransition(nextMode);
        }

        if (autoManageMainCameraTag)
        {
            ApplyMainCameraTag(nextMode);
        }

        SetEnvironmentActive(nextMode);
        SetModePresentation(nextMode);

        currentMode = nextMode;

        audioListenersDirty = true;

        lastSwitchTime = Time.time;
        ResetTimers();
        hasAppliedInitialMode = true;

        if (verboseLog)
        {
            Debug.Log($"[HybridMode] -> {currentMode} | reason={reason} | gpsAccuracy={lastGpsAccuracy:F1}m");
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

    private void SetEnvironmentActive(HybridMode mode)
    {
        bool indoorActive = mode == HybridMode.Indoor || keepIndoorActiveWhileOutdoor;
        bool outdoorActive = mode == HybridMode.Outdoor || keepOutdoorActiveWhileIndoor;

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
            SetRootActiveIfNotProtected(indoorVisualRoot, mode == HybridMode.Indoor);
        }

        SetRootsActive(alwaysActiveRoots, true);
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
        }

        if (autoManageAudioListeners)
        {
            AddAudioListeners(indoorEnvironment, indoorAudioListeners);
            AddAudioListeners(outdoorEnvironment, outdoorAudioListeners);
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
        }

        hasCachedPresentationReferences = true;
    }

    private void SetModePresentation(HybridMode mode)
    {
        if (!manageModePresentation)
        {
            return;
        }

        bool indoorVisible = mode == HybridMode.Indoor;
        bool outdoorVisible = mode == HybridMode.Outdoor;

        SetRootsActive(indoorOnlyVisualRoots, indoorVisible);
        SetRootsActive(outdoorOnlyVisualRoots, outdoorVisible);

        if (autoManageMainCameraTag)
        {
            ApplyMainCameraTag(mode);
        }

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
        Camera preferred = mode == HybridMode.Indoor ? indoorMainCamera : outdoorMainCamera;
        if (preferred == null || !preferred.gameObject.activeInHierarchy)
        {
            preferred = mode == HybridMode.Indoor
                ? FindPreferredCamera(indoorEnvironment, "ARCamera")
                : FindPreferredCamera(outdoorEnvironment, "Main Camera");
        }

        ClearMainCameraTag(indoorEnvironment, preferred);
        ClearMainCameraTag(outdoorEnvironment, preferred);

        if (preferred != null)
        {
            preferred.tag = "MainCamera";
        }
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
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = runtimeModeSwitcherOffset;
        panelRect.sizeDelta = new Vector2(210f, 66f);

        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.02f, 0.025f, 0.03f, 0.82f);

        VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(7, 7, 5, 5);
        panelLayout.spacing = 5f;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        runtimeModeStatusText = CreateRuntimeModeText("Status", panelObject.transform, "Transition | GPS Stopped | N/A", 10f);
        LayoutElement statusLayout = runtimeModeStatusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredHeight = 18f;

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

        runtimeIndoorButton = CreateRuntimeModeButton("Indoor Button", "Indoor", buttonRow.transform, ForceIndoor);
        runtimeOutdoorButton = CreateRuntimeModeButton("Outdoor Button", "Outdoor", buttonRow.transform, ForceOutdoor);
        runtimeOffButton = CreateRuntimeModeButton("Off Button", "Off", buttonRow.transform, DeactivateARMode);
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
        
        if (runtimeModeStatusText == null)
        {
            return;
        }

        string gpsStatus = Input.location.status.ToString();
        string accuracyText = "N/A";
        if (Input.location.status == LocationServiceStatus.Running)
        {
            float accuracy = Input.location.lastData.horizontalAccuracy;
            accuracyText = accuracy > 0f ? $"{accuracy:0.#}m" : "N/A";
        }

        // Xây dựng text mới và chỉ set khi nội dung thực sự thay đổi — tránh Canvas rebuild
        string newText = !string.IsNullOrEmpty(runtimePermissionStatus)
            ? runtimePermissionStatus
            : $"{currentMode} | GPS {gpsStatus} | {accuracyText}";

        if (newText != _lastStatusText)
        {
            runtimeModeStatusText.text = newText;
            _lastStatusText = newText;
        }

        // Chỉ cập nhật màu button khi mode thay đổi — tránh Canvas dirty
        if (currentMode != _lastButtonMode)
        {
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
