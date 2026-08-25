using ARNavB9V2.Scene;
using UnityEngine;

namespace ARNavB9V2.Indoor
{
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class B9IndoorMinimapController : MonoBehaviour
    {
        [SerializeField] private B9SceneContext foundation;
        [SerializeField] private B9IndoorRouteController routeController;
        [SerializeField] private B9IndoorPoseTracker poseTracker;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private Transform userMarker;
        [SerializeField] private Transform destinationMarker;
        [SerializeField] private float followOrthographicSizeMeters = 11f;
        [SerializeField] private float overviewOrthographicSizeMeters = 32f;
        [SerializeField] private float cameraHeightMeters = 45f;
        [SerializeField] private float cameraSmoothTimeSeconds = 0.2f;
        [SerializeField] private float zoomSmoothTimeSeconds = 0.25f;
        [SerializeField] private float markerLiftAboveModelMeters = 2f;

        private bool overviewMode;
        private bool hasCameraCenter;
        private Vector3 cameraCenter;
        private Vector3 cameraVelocity;
        private float zoomVelocity;
        private Bounds modelBounds;
        private float markerWorldHeight;

        public RenderTexture RenderedTexture => renderTexture;
        public bool IsOverviewMode => overviewMode;

        public void Configure(
            B9SceneContext sceneFoundation,
            B9IndoorRouteController route,
            Camera mapCamera,
            RenderTexture targetTexture,
            Transform user,
            Transform destination)
        {
            foundation = sceneFoundation;
            routeController = route;
            minimapCamera = mapCamera;
            renderTexture = targetTexture;
            userMarker = user;
            destinationMarker = destination;
            RecalculateMapBounds();
            ApplyCameraSettings();
        }

        public void AttachPoseTracker(B9IndoorPoseTracker tracker)
        {
            poseTracker = tracker;
        }

        public void Activate()
        {
            RecalculateMapBounds();
            hasCameraCenter = false;
            cameraVelocity = Vector3.zero;
            zoomVelocity = 0f;
            if (userMarker != null) userMarker.gameObject.SetActive(true);
            if (destinationMarker != null) destinationMarker.gameObject.SetActive(true);
            enabled = true;
            ApplyCameraSettings();
        }

        public void Deactivate()
        {
            if (userMarker != null) userMarker.gameObject.SetActive(false);
            if (destinationMarker != null) destinationMarker.gameObject.SetActive(false);
            enabled = false;
        }

        public void SetOverviewMode(bool enabledOverview)
        {
            overviewMode = enabledOverview;
            zoomVelocity = 0f;
        }

        private void LateUpdate()
        {
            if (minimapCamera == null || routeController == null)
                return;

            Vector3 userPosition = routeController.CurrentUserWorldPosition;
            Vector3 targetCenter = overviewMode ? modelBounds.center : userPosition;
            if (!hasCameraCenter)
            {
                cameraCenter = targetCenter;
                cameraVelocity = Vector3.zero;
                hasCameraCenter = true;
            }
            else
            {
                cameraCenter = Vector3.SmoothDamp(
                    cameraCenter,
                    targetCenter,
                    ref cameraVelocity,
                    cameraSmoothTimeSeconds,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            minimapCamera.transform.position = new Vector3(
                cameraCenter.x,
                markerWorldHeight + cameraHeightMeters,
                cameraCenter.z);
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float targetSize = overviewMode
                ? overviewOrthographicSizeMeters
                : followOrthographicSizeMeters;
            minimapCamera.orthographicSize = Mathf.SmoothDamp(
                minimapCamera.orthographicSize,
                targetSize,
                ref zoomVelocity,
                zoomSmoothTimeSeconds,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (userMarker != null)
            {
                userMarker.position = new Vector3(
                    userPosition.x,
                    markerWorldHeight,
                    userPosition.z);
                float heading = poseTracker != null && poseTracker.IsTracking
                    ? poseTracker.HeadingDegrees
                    : routeController.CurrentHeadingDegrees;
                userMarker.rotation = Quaternion.Euler(0f, heading, 0f);
            }

            if (destinationMarker != null)
            {
                Vector3 destination = routeController.DestinationWorldPosition;
                destinationMarker.position = new Vector3(
                    destination.x,
                    markerWorldHeight,
                    destination.z);
            }
        }

        private void RecalculateMapBounds()
        {
            Transform modelRoot = foundation != null ? foundation.ModelRoot : null;
            Renderer[] renderers = modelRoot != null
                ? modelRoot.GetComponentsInChildren<Renderer>(true)
                : null;
            if (renderers == null || renderers.Length == 0)
            {
                Vector3 center = modelRoot != null ? modelRoot.position : Vector3.zero;
                modelBounds = new Bounds(center, Vector3.one * 20f);
            }
            else
            {
                modelBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    modelBounds.Encapsulate(renderers[i].bounds);
            }

            float horizontalExtent = Mathf.Max(modelBounds.extents.x, modelBounds.extents.z);
            overviewOrthographicSizeMeters = Mathf.Max(18f, horizontalExtent * 1.15f);
            cameraHeightMeters = Mathf.Max(30f, horizontalExtent * 2f);
            markerWorldHeight = modelBounds.max.y + markerLiftAboveModelMeters;
        }

        private void ApplyCameraSettings()
        {
            if (minimapCamera == null)
                return;

            int mapLayer = LayerMask.NameToLayer("MapPlane");
            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            int mask = 0;
            if (mapLayer >= 0) mask |= 1 << mapLayer;
            if (minimapLayer >= 0) mask |= 1 << minimapLayer;

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = overviewMode
                ? overviewOrthographicSizeMeters
                : followOrthographicSizeMeters;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.035f, 0.055f, 0.08f, 1f);
            minimapCamera.nearClipPlane = 0.01f;
            minimapCamera.farClipPlane = cameraHeightMeters + modelBounds.size.y + 30f;
            minimapCamera.cullingMask = mask;
            minimapCamera.targetTexture = renderTexture;
            minimapCamera.enabled = true;
        }
    }
}
