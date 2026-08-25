using Unity.XR.CoreUtils;
using UnityEngine;

namespace ARNavB9V2.Outdoor
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class B9OutdoorPoseController : MonoBehaviour
    {
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Camera arCamera;
        [Range(0.01f, 1f)] [SerializeField] private float headingSmoothing = 0.12f;
        [SerializeField] private float positionCorrectionSmoothTimeSeconds = 0.75f;
        [SerializeField] private float maximumCorrectionSpeedMetersPerSecond = 8f;

        private int appliedSampleVersion = -1;
        private bool headingAligned;
        private bool hasOriginTarget;
        private Vector3 originTargetPosition;
        private Vector3 originCorrectionVelocity;

        public void Configure(
            B9OutdoorLocationProvider provider,
            XROrigin origin,
            Camera camera)
        {
            locationProvider = provider;
            xrOrigin = origin;
            arCamera = camera;
        }

        public void ConfigurePositionSmoothing(float smoothTimeSeconds, float maximumSpeedMetersPerSecond)
        {
            positionCorrectionSmoothTimeSeconds = Mathf.Max(0.05f, smoothTimeSeconds);
            maximumCorrectionSpeedMetersPerSecond = Mathf.Max(0.5f, maximumSpeedMetersPerSecond);
        }

        private void LateUpdate()
        {
            if (locationProvider == null || xrOrigin == null || arCamera == null
                || !locationProvider.HasReliableFix)
                return;

            if (locationProvider.HasHeading)
                AlignHeading(locationProvider.HeadingDegrees);

            if (appliedSampleVersion != locationProvider.SampleVersion)
            {
                appliedSampleVersion = locationProvider.SampleVersion;
                Vector3 delta = locationProvider.TargetCampusPosition - arCamera.transform.position;
                delta.y = 0f;
                originTargetPosition = xrOrigin.transform.position + delta;
                originTargetPosition.y = xrOrigin.transform.position.y;
                if (!hasOriginTarget)
                {
                    xrOrigin.transform.position = originTargetPosition;
                    originCorrectionVelocity = Vector3.zero;
                    hasOriginTarget = true;
                }
            }

            if (hasOriginTarget)
            {
                Vector3 position = Vector3.SmoothDamp(
                    xrOrigin.transform.position,
                    originTargetPosition,
                    ref originCorrectionVelocity,
                    positionCorrectionSmoothTimeSeconds,
                    maximumCorrectionSpeedMetersPerSecond,
                    Time.unscaledDeltaTime);
                position.y = xrOrigin.transform.position.y;
                xrOrigin.transform.position = position;
            }
        }

        private void AlignHeading(float targetHeading)
        {
            float currentCameraYaw = arCamera.transform.eulerAngles.y;
            float correction = Mathf.DeltaAngle(currentCameraYaw, targetHeading);
            float blend = headingAligned ? headingSmoothing : 1f;
            float appliedCorrection = correction * blend;
            Vector3 pivot = arCamera.transform.position;
            xrOrigin.transform.RotateAround(
                pivot,
                Vector3.up,
                appliedCorrection);

            // Keep the pending GPS correction in the same rotated coordinate space.
            // Otherwise heading smoothing can pull the origin back toward its old yaw.
            if (hasOriginTarget)
            {
                Vector3 targetOffset = originTargetPosition - pivot;
                targetOffset = Quaternion.AngleAxis(appliedCorrection, Vector3.up) * targetOffset;
                originTargetPosition = pivot + targetOffset;
                originTargetPosition.y = xrOrigin.transform.position.y;
            }
            headingAligned = true;
        }
    }
}
