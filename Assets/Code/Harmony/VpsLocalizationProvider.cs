using ARNav.Hybrid;
using UnityEngine;

namespace ARNav.Harmony
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-47)]
    public sealed class VpsLocalizationProvider : MonoBehaviour
    {
        [SerializeField] private MultisetPoseProvider multisetProvider;
        [SerializeField] private HarmonyConfig config;
        [SerializeField] private BuildingLocalizationProfile activeProfile;
        [SerializeField] private BuildingId activeBuilding = BuildingId.None;

        public MultisetPoseProvider Source => multisetProvider;
        public BuildingLocalizationProfile ActiveProfile => activeProfile;

        private void OnEnable()
        {
            Resolve();
        }

        public void Configure(
            BuildingId building,
            BuildingLocalizationProfile profile,
            HarmonyConfig harmonyConfig)
        {
            activeBuilding = building;
            activeProfile = profile;
            if (harmonyConfig != null) config = harmonyConfig;
        }

        public HarmonyVpsSample Read()
        {
            Resolve();
            if (multisetProvider == null)
                return default;

            MultisetPoseProvider.PoseReading pose = multisetProvider.Last;
            bool mapMatches = pose.MapIdAvailable &&
                              ((activeProfile != null && activeProfile.IsAcceptedMapId(pose.MapId)) ||
                               (config != null && config.IsAcceptedMapId(activeBuilding, pose.MapId)));

            return new HarmonyVpsSample
            {
                IsValid = multisetProvider.HasFreshPose,
                CampusPosition = pose.CampusPosition,
                CampusRotation = pose.CampusRotation,
                MapLocalPosition = pose.MapLocalPosition,
                MapLocalRotation = pose.MapLocalRotation,
                Confidence = pose.Confidence,
                ConfidenceAvailable = pose.ConfidenceAvailable,
                MapId = pose.MapId,
                MapIdAvailable = pose.MapIdAvailable,
                MapMatchesBuilding = mapMatches,
                AgeSeconds = multisetProvider.LastPoseAgeSeconds,
                Timestamp = pose.Timestamp,
            };
        }

        private void Resolve()
        {
            if (multisetProvider == null || !multisetProvider.isActiveAndEnabled)
                multisetProvider = MultisetPoseProvider.FindActiveProvider();
        }
    }
}
