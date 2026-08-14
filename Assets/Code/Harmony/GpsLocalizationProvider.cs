using ARNav.Hybrid;
using UnityEngine;

namespace ARNav.Harmony
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-48)]
    public sealed class GpsLocalizationProvider : MonoBehaviour
    {
        [SerializeField] private OutdoorPoseProvider outdoorProvider;

        public OutdoorPoseProvider Source => outdoorProvider;

        private void OnEnable()
        {
            Resolve();
        }

        public HarmonyGpsSample Read()
        {
            Resolve();
            if (outdoorProvider == null)
                return default;

            float heading = outdoorProvider.HeadingDegrees;
            return new HarmonyGpsSample
            {
                IsValid = outdoorProvider.HasFreshFix,
                CampusPosition = outdoorProvider.UserCampusPosition,
                CampusRotation = outdoorProvider.HasHeading
                    ? Quaternion.Euler(0f, heading, 0f)
                    : Quaternion.identity,
                HasHeading = outdoorProvider.HasHeading,
                HeadingDegrees = heading,
                Latitude = outdoorProvider.Latitude,
                Longitude = outdoorProvider.Longitude,
                HorizontalAccuracyMeters = outdoorProvider.AccuracyMeters,
                AgeSeconds = outdoorProvider.FixAgeSeconds,
                JumpRejected = outdoorProvider.LastFixRejectedAsJump,
                RejectedJumpMeters = outdoorProvider.LastRejectedJumpMeters,
                Timestamp = outdoorProvider.LastFixTimestamp,
            };
        }

        private void Resolve()
        {
            if (outdoorProvider == null)
            {
                outdoorProvider = FindFirstObjectByType<OutdoorPoseProvider>(
                    FindObjectsInactive.Include);
            }
        }
    }
}
