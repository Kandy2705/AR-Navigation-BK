using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Keeps the AR display camera driven by the physical device pose.
///
/// ARCameraBackground can continue showing a live iOS camera image even when the
/// TrackedPoseDriver actions are disabled or fail to rebind after an AR page/mode
/// transition. In that state world-space navigation content looks glued to the
/// screen and eventually leaves the AR camera frustum while the minimap still sees it.
/// This component repairs the normal Input System actions. TrackedPoseDriver remains
/// the only component allowed to write the camera transform; a second late-frame pose
/// writer can fight ARFoundation and make device rotation appear frozen.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class ArCameraPoseGuard : MonoBehaviour
{
    private const float RepairIntervalSeconds = 1f;
    private const float HealthLogIntervalSeconds = 3f;

    private TrackedPoseDriver poseDriver;
    private ARCameraManager cameraManager;
    private float nextRepairTime;
    private float nextHealthLogTime;
    private float lastCameraFrameTime = -999f;
    private string lastPoseSource = "none";
    private bool subscribed;

    public static ArCameraPoseGuard EnsureOn(Camera camera)
    {
        if (camera == null)
            return null;

        ArCameraPoseGuard guard = camera.GetComponent<ArCameraPoseGuard>();
        if (guard == null)
            guard = camera.gameObject.AddComponent<ArCameraPoseGuard>();

        guard.enabled = true;
        guard.EnsureNow();
        return guard;
    }

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        ResolveComponents();
        Subscribe();
        EnsureNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            nextRepairTime = 0f;
            EnsureNow();
        }
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextRepairTime)
        {
            EnsureNow();
            nextRepairTime = Time.unscaledTime + RepairIntervalSeconds;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        LogHealthPeriodically();
#endif
    }

    public void EnsureNow()
    {
        ResolveComponents();

        if (poseDriver == null)
            poseDriver = gameObject.AddComponent<TrackedPoseDriver>();

        bool rebound = false;
        if (!HasBindings(poseDriver.positionInput.action))
        {
            var position = new InputAction(
                "AR Device Position",
                InputActionType.Value,
                expectedControlType: "Vector3");
            position.AddBinding("<XRHMD>/centerEyePosition");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");
            poseDriver.positionInput = new InputActionProperty(position);
            rebound = true;
        }

        if (!HasBindings(poseDriver.rotationInput.action))
        {
            var rotation = new InputAction(
                "AR Device Rotation",
                InputActionType.Value,
                expectedControlType: "Quaternion");
            rotation.AddBinding("<XRHMD>/centerEyeRotation");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
            poseDriver.rotationInput = new InputActionProperty(rotation);
            rebound = true;
        }

        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

        // HybridGPSMap has no authored tracking-state bindings. Do not let a
        // default/disabled tracking-state action block otherwise valid pose values.
        InputAction trackingState = poseDriver.trackingStateInput.action;
        poseDriver.ignoreTrackingState = !HasBindings(trackingState);

        if (!poseDriver.enabled)
            poseDriver.enabled = true;

        poseDriver.positionInput.action?.Enable();
        poseDriver.rotationInput.action?.Enable();
        if (HasBindings(trackingState))
            trackingState.Enable();

        bool hasPosition = HasResolvedControl(poseDriver.positionInput.action);
        bool hasRotation = HasResolvedControl(poseDriver.rotationInput.action);
        lastPoseSource = hasPosition || hasRotation ? "tracked-pose-driver" : "awaiting-xr-device";

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (rebound)
            Debug.LogWarning($"[ARCameraPose] Rebuilt missing pose actions on '{name}'.");
#endif
    }

    private void ResolveComponents()
    {
        if (poseDriver == null)
            poseDriver = GetComponent<TrackedPoseDriver>();
        if (cameraManager == null)
            cameraManager = GetComponent<ARCameraManager>();
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (cameraManager != null)
            cameraManager.frameReceived += HandleCameraFrame;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (cameraManager != null)
            cameraManager.frameReceived -= HandleCameraFrame;
        subscribed = false;
    }

    private void HandleCameraFrame(ARCameraFrameEventArgs args)
    {
        lastCameraFrameTime = Time.unscaledTime;
    }


    private static bool HasBindings(InputAction action)
    {
        return action != null && action.bindings.Count > 0;
    }

    private static bool HasResolvedControl(InputAction action)
    {
        return action != null && action.enabled && action.controls.Count > 0;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void LogHealthPeriodically()
    {
        if (Time.unscaledTime < nextHealthLogTime)
            return;

        nextHealthLogTime = Time.unscaledTime + HealthLogIntervalSeconds;
        InputAction position = poseDriver != null ? poseDriver.positionInput.action : null;
        InputAction rotation = poseDriver != null ? poseDriver.rotationInput.action : null;
        float frameAge = Mathf.Max(0f, Time.unscaledTime - lastCameraFrameTime);

        Debug.Log(
            $"[ARCameraPose] source={lastPoseSource} AR={ARSession.state} " +
            $"frameAge={frameAge:F1}s driver={(poseDriver != null && poseDriver.enabled)} " +
            $"actions={position?.enabled}/{rotation?.enabled} " +
            $"controls={position?.controls.Count ?? 0}/{rotation?.controls.Count ?? 0} " +
            $"localPos={transform.localPosition} localYaw={transform.localEulerAngles.y:F1}°");
    }
#endif
}
