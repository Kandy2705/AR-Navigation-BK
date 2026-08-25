using System;
using System.Collections;
using System.Reflection;
using ARNavB9V2.Data;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Events;

namespace ARNavB9V2.Vps
{
    /// <summary>
    /// Owns the automatic handover from outdoor GPS navigation to B9 VPS localization.
    /// MultiSet is accessed through its public runtime component and UnityEvents, while
    /// the V2 flow remains independent from the legacy hybrid scripts.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class B9VpsTransitionController : MonoBehaviour
    {
        public enum TransitionState
        {
            WaitingForEntrance,
            StartingVps,
            Scanning,
            IndoorLocalized,
            Failed,
        }

        private const BindingFlags ReflectionFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Header("B9 + MultiSet")]
        [SerializeField] private B9BuildingDefinition building;
        [SerializeField] private GameObject sdkManagerRoot;
        [SerializeField] private MonoBehaviour mapLocalizationManager;

        [Header("Outdoor handover")]
        [SerializeField] private B9OutdoorSceneContext outdoorContext;

        [Header("Indoor handover")]
        [SerializeField] private B9SceneContext foundation;
        [SerializeField] private B9MapVisibility mapVisibility;
        [SerializeField] private B9IndoorSceneContext indoorContext;
        [SerializeField] private float localizeDelaySeconds = 0.5f;
        [SerializeField] private float scanTimeoutSeconds = 60f;
        [SerializeField, Range(1, 10)] private int initialLocalizationFrames = 5;
        [SerializeField, Range(1, 10)] private int retryLocalizationFrames = 5;
        [SerializeField, Min(0.1f)] private float localizationFrameIntervalSeconds = 0.6f;
        [SerializeField] private bool localizationBlurCheck;
        [SerializeField, Min(0f)] private float localizationPoseSettleSeconds = 0.35f;
        [SerializeField] private bool allowSdkInvocationInEditor;
        [SerializeField] private bool externalHandoverControl;

        private UnityEvent localizationInitEvent;
        private UnityEvent localizationRequestedEvent;
        private UnityEvent localizationSuccessEvent;
        private UnityEvent localizationFailureEvent;
        private Coroutine requestCoroutine;
        private float scanStartedAt;
        private bool eventsSubscribed;
        private bool indoorHandoverPending;
        private int localizationAttemptCount;

        public TransitionState State { get; private set; } = TransitionState.WaitingForEntrance;
        public string FailureReason { get; private set; } = string.Empty;
        public string ActiveMapCode => building != null ? building.PrimaryMapCode : string.Empty;
        public bool RetryAvailable => State == TransitionState.Failed;
        public int LocalizationAttemptCount => localizationAttemptCount;
        public float CurrentScanElapsedSeconds => scanStartedAt > 0f
            && (State == TransitionState.StartingVps || State == TransitionState.Scanning)
                ? Mathf.Max(0f, Time.unscaledTime - scanStartedAt)
                : 0f;
        public event Action<TransitionState> StateChanged;

        public void SetExternalHandoverControl(bool enabled)
        {
            externalHandoverControl = enabled;
        }

        public void Configure(
            B9BuildingDefinition definition,
            GameObject sdkRoot,
            MonoBehaviour localizer,
            B9OutdoorSceneContext outdoor,
            B9SceneContext sceneFoundation,
            B9MapVisibility visibility,
            bool invokeSdkInEditor = false)
        {
            building = definition;
            sdkManagerRoot = sdkRoot;
            mapLocalizationManager = localizer;
            outdoorContext = outdoor;
            foundation = sceneFoundation;
            mapVisibility = visibility;
            allowSdkInvocationInEditor = invokeSdkInEditor;
        }

        public void AttachIndoorNavigation(B9IndoorSceneContext context)
        {
            indoorContext = context;
        }

        public void ConfigureLocalizationCapture(
            int initialFrames,
            int retryFrames,
            float frameIntervalSeconds,
            bool enableBlurCheck,
            float requestDelaySeconds,
            float timeoutSeconds,
            float poseSettleSeconds = 0.35f)
        {
            initialLocalizationFrames = Mathf.Clamp(initialFrames, 1, 10);
            retryLocalizationFrames = Mathf.Clamp(retryFrames, 1, 10);
            localizationFrameIntervalSeconds = Mathf.Max(0.1f, frameIntervalSeconds);
            localizationBlurCheck = enableBlurCheck;
            localizeDelaySeconds = Mathf.Max(0f, requestDelaySeconds);
            scanTimeoutSeconds = Mathf.Max(5f, timeoutSeconds);
            localizationPoseSettleSeconds = Mathf.Max(0f, poseSettleSeconds);
        }

        private void Awake()
        {
            if (sdkManagerRoot != null)
                sdkManagerRoot.SetActive(true);
            if (foundation != null && foundation.ModelRoot != null)
                foundation.ModelRoot.gameObject.SetActive(false);
            indoorContext?.PrepareForLocalization();
            TrySubscribeSdkEvents();
        }

        private void OnEnable()
        {
            TrySubscribeSdkEvents();
        }

        private void OnDisable()
        {
            UnsubscribeSdkEvents();
            StopPendingRequest();
        }

        private void Update()
        {
            if (!externalHandoverControl
                && State == TransitionState.WaitingForEntrance
                && outdoorContext != null
                && outdoorContext.RouteController != null
                && outdoorContext.RouteController.HasArrivedAtEntrance)
            {
                BeginAutomaticLocalization();
            }

            if (State == TransitionState.Scanning
                && Time.unscaledTime - scanStartedAt >= scanTimeoutSeconds)
            {
                if (mapLocalizationManager != null)
                {
                    TrySetFieldOrProperty(
                        mapLocalizationManager.GetType(),
                        "firstLocalizationUntilSuccess",
                        false);
                }
                Fail("Quét VPS quá thời gian. Hãy hướng camera quanh sảnh rồi quét lại.");
            }
        }

        public void BeginAutomaticLocalization()
        {
            if (State != TransitionState.WaitingForEntrance)
                return;
            StartLocalizationAttempt();
        }

        public void RetryLocalization()
        {
            if (!RetryAvailable)
                return;
            StartLocalizationAttempt();
        }

        public void CancelLocalization()
        {
            StopPendingRequest();
            indoorHandoverPending = false;
            scanStartedAt = 0f;
            FailureReason = string.Empty;
            if (mapLocalizationManager != null)
            {
                TrySetFieldOrProperty(
                    mapLocalizationManager.GetType(),
                    "firstLocalizationUntilSuccess",
                    false);
            }
            SetState(TransitionState.WaitingForEntrance);
        }

        public void ReturnToOutdoor()
        {
            CancelLocalization();
            indoorContext?.PrepareForLocalization();
            if (foundation != null && foundation.ModelRoot != null)
                foundation.ModelRoot.gameObject.SetActive(false);
        }

        private void StartLocalizationAttempt()
        {
            StopPendingRequest();
            FailureReason = string.Empty;
            indoorHandoverPending = false;
            localizationAttemptCount++;
            scanStartedAt = 0f;

            if (!ValidateRuntimeReferences(out string reason))
            {
                Fail(reason);
                return;
            }

            if (!ConfigureMultiSetForB9(out reason))
            {
                Fail(reason);
                return;
            }

            TrySubscribeSdkEvents();
            SetState(TransitionState.StartingVps);
            requestCoroutine = StartCoroutine(RequestLocalizationAfterDelay());
        }

        private IEnumerator RequestLocalizationAfterDelay()
        {
            if (localizeDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(localizeDelaySeconds);

#if UNITY_EDITOR
            if (!allowSdkInvocationInEditor)
            {
                requestCoroutine = null;
                Fail("VPS thật chỉ quét trên iPhone. Build iOS để thử camera hoặc dùng Debug/Simulate VPS Success.");
                yield break;
            }
#endif

            MethodInfo method = mapLocalizationManager.GetType().GetMethod(
                "LocalizeFrame",
                ReflectionFlags,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
            {
                requestCoroutine = null;
                Fail("MultiSet MapLocalizationManager không có LocalizeFrame().");
                yield break;
            }

            scanStartedAt = Time.unscaledTime;
            SetState(TransitionState.Scanning);
            try
            {
                method.Invoke(mapLocalizationManager, null);
            }
            catch (TargetInvocationException exception)
            {
                requestCoroutine = null;
                string detail = exception.InnerException != null
                    ? exception.InnerException.Message
                    : exception.Message;
                Fail("Không thể bắt đầu VPS: " + detail);
                yield break;
            }
            catch (Exception exception)
            {
                requestCoroutine = null;
                Fail("Không thể bắt đầu VPS: " + exception.Message);
                yield break;
            }

            requestCoroutine = null;
        }

        private bool ConfigureMultiSetForB9(out string reason)
        {
            Type type = mapLocalizationManager.GetType();
            if (!TrySetFieldOrProperty(type, "mapOrMapsetCode", building.PrimaryMapCode))
            {
                reason = "Không gán được mã map B9 cho MultiSet.";
                return false;
            }

            FieldInfo localizationType = type.GetField("localizationType", ReflectionFlags);
            if (localizationType == null || !localizationType.FieldType.IsEnum)
            {
                reason = "MultiSet thiếu cấu hình localizationType.";
                return false;
            }

            try
            {
                object mapValue = Enum.Parse(localizationType.FieldType, "Map", true);
                localizationType.SetValue(mapLocalizationManager, mapValue);
            }
            catch (Exception exception)
            {
                reason = "Không chọn được chế độ Single Map B9: " + exception.Message;
                return false;
            }

            TrySetFieldOrProperty(type, "autoLocalize", false);
            TrySetFieldOrProperty(type, "backgroundLocalization", false);
            TrySetFieldOrProperty(type, "relocalization", false);
            TrySetFieldOrProperty(type, "showAlert", false);
            TrySetFieldOrProperty(type, "firstLocalizationUntilSuccess", true);
            int frameCount = localizationAttemptCount <= 1
                ? initialLocalizationFrames
                : retryLocalizationFrames;
            TrySetFieldOrProperty(type, "numberOfFrames", frameCount);
            TrySetFieldOrProperty(
                type,
                "frameCaptureInterval",
                localizationFrameIntervalSeconds);
            TrySetFieldOrProperty(type, "enableBlurCheck", localizationBlurCheck);
            reason = string.Empty;
            return true;
        }

        private bool ValidateRuntimeReferences(out string reason)
        {
            if (building == null)
            {
                reason = "Thiếu cấu hình tòa B9.";
                return false;
            }
            if (building.PrimaryMapCode != "MAP_9LME2PB7Y3EN")
            {
                reason = "Mã VPS B9 không đúng MAP_9LME2PB7Y3EN.";
                return false;
            }
            if (sdkManagerRoot == null || mapLocalizationManager == null)
            {
                reason = "Thiếu runtime MultiSet SDK.";
                return false;
            }
            if (foundation == null || foundation.MapSpace == null || foundation.ModelRoot == null)
            {
                reason = "Thiếu Map Space/model indoor B9.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void TrySubscribeSdkEvents()
        {
            if (eventsSubscribed || mapLocalizationManager == null)
                return;

            localizationInitEvent = GetSdkEvent("LocalizationInit");
            localizationRequestedEvent = GetSdkEvent("LocalizationRequested");
            localizationSuccessEvent = GetSdkEvent("LocalizationSuccess");
            localizationFailureEvent = GetSdkEvent("LocalizationFailure");
            if (localizationSuccessEvent == null || localizationFailureEvent == null)
                return;

            localizationInitEvent?.AddListener(HandleLocalizationInit);
            localizationRequestedEvent?.AddListener(HandleLocalizationRequested);
            localizationSuccessEvent.AddListener(HandleLocalizationSuccess);
            localizationFailureEvent.AddListener(HandleLocalizationFailure);
            eventsSubscribed = true;
        }

        private void UnsubscribeSdkEvents()
        {
            if (!eventsSubscribed)
                return;
            localizationInitEvent?.RemoveListener(HandleLocalizationInit);
            localizationRequestedEvent?.RemoveListener(HandleLocalizationRequested);
            localizationSuccessEvent?.RemoveListener(HandleLocalizationSuccess);
            localizationFailureEvent?.RemoveListener(HandleLocalizationFailure);
            eventsSubscribed = false;
        }

        private UnityEvent GetSdkEvent(string fieldName)
        {
            FieldInfo field = mapLocalizationManager.GetType().GetField(fieldName, ReflectionFlags);
            return field?.GetValue(mapLocalizationManager) as UnityEvent;
        }

        private void HandleLocalizationInit()
        {
            if (State == TransitionState.StartingVps)
                SetState(TransitionState.Scanning);
        }

        private void HandleLocalizationRequested()
        {
            // MultiSet can issue multiple requests while
            // firstLocalizationUntilSuccess is enabled. Keep one absolute timeout
            // for the whole scan session instead of restarting it for every frame batch.
            if (scanStartedAt <= 0f)
                scanStartedAt = Time.unscaledTime;
            SetState(TransitionState.Scanning);
        }

        private void HandleLocalizationSuccess()
        {
            if (State != TransitionState.StartingVps && State != TransitionState.Scanning)
                return;
            if (indoorHandoverPending)
                return;
            indoorHandoverPending = true;
            StopPendingRequest();
            StartCoroutine(CompleteIndoorHandoverAfterSdkPose());
        }

        private IEnumerator CompleteIndoorHandoverAfterSdkPose()
        {
            // MultiSet applies Map Space around the success event. Give the transform
            // enough time to settle before validating and accepting the pose.
            yield return null;
            yield return null;
            if (localizationPoseSettleSeconds > 0f)
                yield return new WaitForSecondsRealtime(localizationPoseSettleSeconds);

            foundation.ModelRoot.gameObject.SetActive(true);
            mapVisibility?.ApplyVisibilityPolicy();
            ReanchorIndoorNavMesh();

            if (outdoorContext != null)
            {
                outdoorContext.RibbonRenderer?.ClearPath();
                if (outdoorContext.RouteController != null)
                    outdoorContext.RouteController.enabled = false;
                if (outdoorContext.PoseController != null)
                    outdoorContext.PoseController.enabled = false;
                if (outdoorContext.LocationProvider != null)
                    outdoorContext.LocationProvider.enabled = false;
                if (outdoorContext.MinimapController != null)
                    outdoorContext.MinimapController.enabled = false;
                if (outdoorContext.SchoolGround != null)
                    outdoorContext.SchoolGround.gameObject.SetActive(false);
                if (outdoorContext.UserMarker != null)
                    outdoorContext.UserMarker.gameObject.SetActive(false);
                if (outdoorContext.EntranceMarker != null)
                    outdoorContext.EntranceMarker.gameObject.SetActive(false);
            }

            ConfigureIndoorMinimap();
            string selectedRoom = outdoorContext != null
                                  && outdoorContext.RouteController != null
                ? outdoorContext.RouteController.SelectedRoomId
                : "B9-104";
            indoorContext?.BeginNavigation(selectedRoom);
            SetState(TransitionState.IndoorLocalized);
        }

        private void HandleLocalizationFailure()
        {
            if (State != TransitionState.StartingVps && State != TransitionState.Scanning)
                return;

            // A failed frame batch is not a failed user flow. MultiSet's
            // firstLocalizationUntilSuccess mode will continue capturing. The Update
            // timeout is the only condition that exposes the manual retry button.
            if (scanStartedAt <= 0f)
                scanStartedAt = Time.unscaledTime;
            indoorHandoverPending = false;
            SetState(TransitionState.Scanning);
        }

        private void ReanchorIndoorNavMesh()
        {
            NavMeshSurface surface = foundation != null ? foundation.NavMeshSurface : null;
            if (surface == null || surface.navMeshData == null || !surface.isActiveAndEnabled)
                return;

            surface.RemoveData();
            surface.AddData();
        }

        private void ConfigureIndoorMinimap()
        {
            if (foundation == null || foundation.MinimapCamera == null || foundation.ModelRoot == null)
                return;

            Renderer[] renderers = foundation.ModelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);
            float height = Mathf.Max(15f, extent * 2f);
            Camera camera = foundation.MinimapCamera;
            camera.transform.position = bounds.center + Vector3.up * height;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(8f, extent * 1.15f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = height + Mathf.Max(30f, extent * 3f);

            int mask = 1 << 0;
            int mapLayer = LayerMask.NameToLayer("MapPlane");
            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            if (mapLayer >= 0) mask |= 1 << mapLayer;
            if (minimapLayer >= 0) mask |= 1 << minimapLayer;
            camera.cullingMask = mask;
            camera.enabled = true;
        }

        private void Fail(string reason)
        {
            indoorHandoverPending = false;
            FailureReason = reason;
            SetState(TransitionState.Failed);
        }

        private void SetState(TransitionState value)
        {
            if (State == value)
                return;
            State = value;
            StateChanged?.Invoke(value);
        }

        private void StopPendingRequest()
        {
            if (requestCoroutine == null)
                return;
            StopCoroutine(requestCoroutine);
            requestCoroutine = null;
        }

        private bool TrySetFieldOrProperty(Type type, string memberName, object value)
        {
            FieldInfo field = type.GetField(memberName, ReflectionFlags);
            if (field != null && value != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(mapLocalizationManager, value);
                return true;
            }
            if (field != null && value == null)
            {
                field.SetValue(mapLocalizationManager, null);
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, ReflectionFlags);
            if (property != null && property.CanWrite
                && value != null && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(mapLocalizationManager, value);
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Simulate Arrival And Start VPS")]
        private void DebugStartVps()
        {
            if (State == TransitionState.WaitingForEntrance)
                BeginAutomaticLocalization();
        }

        [ContextMenu("Debug/Simulate VPS Success")]
        private void DebugLocalizationSuccess()
        {
            if (State == TransitionState.WaitingForEntrance)
                SetState(TransitionState.Scanning);
            HandleLocalizationSuccess();
        }

        [ContextMenu("Debug/Simulate VPS Failure")]
        private void DebugLocalizationFailure()
        {
            if (State == TransitionState.WaitingForEntrance)
                SetState(TransitionState.Scanning);
            HandleLocalizationFailure();
        }
#endif
    }
}
