using ARNavB9V2.Data;
using UnityEngine;

namespace ARNavB9V2.Outdoor
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class B9OutdoorMinimapController : MonoBehaviour
    {
        [SerializeField] private B9OutdoorMapDefinition outdoorMap;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private Transform userMarker;
        [SerializeField] private Transform entranceMarker;
        [SerializeField] private float cameraHeightMeters = 90f;
        [SerializeField] private float followOrthographicSizeMeters = 22f;
        [SerializeField] private float overviewOrthographicSizeMeters = 105f;
        [SerializeField] private float cameraFollowSmoothTimeSeconds = 0.22f;
        [SerializeField] private float zoomSmoothTimeSeconds = 0.25f;
        [SerializeField] private float markerHeightMeters = 3.2f;

        private bool overviewMode;
        private bool hasCameraCenter;
        private Vector3 cameraCenter;
        private Vector3 cameraCenterVelocity;
        private float zoomVelocity;
        private bool usePoseOverride;
        private Vector3 poseOverridePosition;
        private float poseOverrideHeading;

        public RenderTexture RenderedTexture => renderTexture;
        public bool IsOverviewMode => overviewMode;

        public void Configure(
            B9OutdoorMapDefinition mapDefinition,
            B9OutdoorLocationProvider provider,
            Camera mapCamera,
            RenderTexture targetTexture,
            Transform user,
            Transform entrance)
        {
            outdoorMap = mapDefinition;
            locationProvider = provider;
            minimapCamera = mapCamera;
            renderTexture = targetTexture;
            userMarker = user;
            entranceMarker = entrance;
            ApplyCameraSettings();
            ApplyMarkerPresentation();
        }

        public void ConfigureInteraction(
            float followSizeMeters,
            float overviewSizeMeters,
            float followSmoothTimeSeconds)
        {
            followOrthographicSizeMeters = Mathf.Max(8f, followSizeMeters);
            overviewOrthographicSizeMeters = Mathf.Max(
                followOrthographicSizeMeters + 5f,
                overviewSizeMeters);
            cameraFollowSmoothTimeSeconds = Mathf.Max(0.05f, followSmoothTimeSeconds);
            ApplyCameraSettings();
            ApplyMarkerPresentation();
        }

        public void SetOverviewMode(bool enabled)
        {
            overviewMode = enabled;
            zoomVelocity = 0f;
        }

        public void SetPoseOverride(Vector3 campusPosition, float headingDegrees)
        {
            usePoseOverride = true;
            poseOverridePosition = campusPosition;
            poseOverrideHeading = Mathf.Repeat(headingDegrees, 360f);
        }

        public void ClearPoseOverride()
        {
            usePoseOverride = false;
        }

        private void Awake()
        {
            ApplyCameraSettings();
            ApplyMarkerPresentation();
        }

        private void LateUpdate()
        {
            if (minimapCamera == null || locationProvider == null)
                return;

            Vector3 targetCenter = usePoseOverride
                ? poseOverridePosition
                : locationProvider.HasReliableFix
                ? locationProvider.CampusPosition
                : outdoorMap != null
                    ? outdoorMap.EntranceCampusPosition
                    : Vector3.zero;

            if (!hasCameraCenter)
            {
                cameraCenter = targetCenter;
                cameraCenterVelocity = Vector3.zero;
                hasCameraCenter = true;
            }
            else
            {
                cameraCenter = Vector3.SmoothDamp(
                    cameraCenter,
                    targetCenter,
                    ref cameraCenterVelocity,
                    cameraFollowSmoothTimeSeconds,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            minimapCamera.transform.position = new Vector3(
                cameraCenter.x,
                cameraCenter.y + cameraHeightMeters,
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
                Vector3 userPosition = usePoseOverride
                    ? poseOverridePosition
                    : locationProvider.HasReliableFix
                    ? locationProvider.CampusPosition
                    : targetCenter;
                userMarker.position = new Vector3(
                    userPosition.x,
                    userPosition.y + markerHeightMeters,
                    userPosition.z);
                if (usePoseOverride || locationProvider.HasHeading)
                {
                    float heading = usePoseOverride
                        ? poseOverrideHeading
                        : locationProvider.HeadingDegrees;
                    userMarker.rotation = Quaternion.Euler(0f, heading, 0f);
                }
            }

            if (entranceMarker != null && outdoorMap != null)
            {
                Vector3 entrance = outdoorMap.EntranceCampusPosition;
                entranceMarker.position = new Vector3(
                    entrance.x,
                    entrance.y + markerHeightMeters,
                    entrance.z);
            }
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
            minimapCamera.farClipPlane = cameraHeightMeters + 50f;
            minimapCamera.cullingMask = mask;
            minimapCamera.targetTexture = renderTexture;
            minimapCamera.enabled = true;
        }

        private void ApplyMarkerPresentation()
        {
            if (userMarker != null)
            {
                Transform dot = userMarker.Find("Position Dot");
                if (dot != null)
                    dot.localScale = new Vector3(2.8f, 0.08f, 2.8f);

                Transform needle = userMarker.Find("Heading Needle");
                if (needle != null)
                {
                    needle.localPosition = new Vector3(0f, 0.12f, 1.7f);
                    needle.localScale = new Vector3(0.65f, 0.1f, 2.2f);
                }
            }

            if (entranceMarker != null)
            {
                Transform dot = entranceMarker.Find("Entrance Dot");
                if (dot != null)
                    dot.localScale = new Vector3(2.5f, 0.08f, 2.5f);
            }
        }
    }
}
