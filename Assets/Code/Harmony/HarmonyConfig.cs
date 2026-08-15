using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNav.Harmony
{
    [CreateAssetMenu(fileName = "HarmonyConfig", menuName = "Hybrid/HARMONY Config")]
    public sealed class HarmonyConfig : ScriptableObject
    {
        [Serializable]
        public sealed class ReliabilityWeights
        {
            [Min(0f)] public float accuracyOrConfidence = 0.3f;
            [Min(0f)] public float freshnessOrValidity = 0.2f;
            [Min(0f)] public float motionStability = 0.2f;
            [Min(0f)] public float transitionOrMapMatch = 0.15f;
            [Min(0f)] public float dwellStability = 0.15f;

            public float Sum =>
                Mathf.Max(0.0001f,
                    accuracyOrConfidence + freshnessOrValidity + motionStability +
                    transitionOrMapMatch + dwellStability);
        }

        [Serializable]
        public sealed class BuildingMapRule
        {
            public BuildingId building = BuildingId.None;
            [Tooltip("Map IDs thật mà LocalizationSuccess trả về, không phải mã MAP_/MSET_ nếu SDK dùng internal ID.")]
            public List<string> acceptedMapIds = new List<string>();
        }

        [Header("Experiment")]
        [SerializeField] private HarmonyExperimentVersion experimentVersion =
            HarmonyExperimentVersion.Current;

        [Header("Transition geometry")]
        [Min(1f)] public float approachRadiusMultiplier = 2.5f;
        [Min(0.5f)] public float enterRadiusMultiplier = 1f;
        [Min(0.5f)] public float exitRadiusMultiplier = 2f;

        [Header("Handover gates")]
        [Range(0f, 1f)] public float vpsEnterReliability = 0.72f;
        [Range(0f, 1f)] public float gpsExitReliability = 0.70f;
        [Range(0f, 1f)] public float minimumVpsConfidence = 0.5f;
        [Min(0f)] public float vpsDwellSeconds = 2f;
        [Min(0f)] public float gpsDwellSeconds = 3f;
        [Min(0f)] public float minimumModeDurationSeconds = 8f;
        [Min(0f)] public float minimumStateDurationSeconds = 0.25f;
        [Min(1f)] public float vpsScanTimeoutSeconds = 25f;
        [Min(0.5f)] public float vpsRetrySeconds = 5f;
        [Min(1f)] public float relocalizationTimeoutSeconds = 15f;
        [Min(0f)] public float sourceLossGraceSeconds = 2.5f;

        [Header("Cross-source continuity")]
        [Min(0.1f)] public float maxHandoverPositionJumpMeters = 8f;
        [Range(1f, 180f)] public float maxHandoverHeadingJumpDegrees = 55f;

        [Header("GPS reliability")]
        [Min(0.1f)] public float gpsExcellentAccuracyMeters = 5f;
        [Min(0.1f)] public float gpsRejectedAccuracyMeters = 30f;
        [Min(0f)] public float gpsFreshAgeSeconds = 0.75f;
        [Min(0.1f)] public float gpsStaleAgeSeconds = 5f;
        [Min(0.1f)] public float gpsMaxPlausibleSpeedMetersPerSecond = 4.5f;
        [Range(0f, 1f)] public float gpsNearTransitionScore = 0.75f;
        public ReliabilityWeights gpsWeights = new ReliabilityWeights();

        [Header("VPS reliability")]
        [Min(0.1f)] public float vpsFreshAgeSeconds = 0.35f;
        [Min(0.1f)] public float vpsStaleAgeSeconds = 2f;
        [Min(0.01f)] public float vpsStablePositionDeltaMeters = 0.35f;
        [Min(0.1f)] public float vpsRejectedPositionDeltaMeters = 3f;
        [Min(0.1f)] public float vpsStableHeadingDeltaDegrees = 5f;
        [Min(1f)] public float vpsRejectedHeadingDeltaDegrees = 45f;
        public ReliabilityWeights vpsWeights = new ReliabilityWeights();

        [Header("Reliability visual bands")]
        [Range(0f, 1f)] public float highReliabilityThreshold = 0.75f;
        [Range(0f, 1f)] public float mediumReliabilityThreshold = 0.4f;
        [Range(0f, 1f)] public float mediumGuidanceAlpha = 0.45f;
        public Color highReliabilityTint = Color.white;
        public Color mediumReliabilityTint = new Color(1f, 0.82f, 0.3f, 1f);
        public Color lowReliabilityTint = new Color(1f, 0.35f, 0.25f, 1f);

        [Header("Map ID validation")]
        [SerializeField] private List<BuildingMapRule> buildingMapRules =
            new List<BuildingMapRule>();

        [Header("Experiment logging")]
        [Min(0.05f)] public float csvSampleIntervalSeconds = 0.25f;
        [Min(1f)] public float falseSwitchWindowSeconds = 8f;
        [Min(0.05f)] public float wrongWayMinimumMovementMeters = 0.35f;
        [Range(91f, 180f)] public float wrongWayAngleDegrees = 120f;
        [Min(0.1f)] public float wrongWayDwellSeconds = 1.25f;

        public HarmonyExperimentVersion ExperimentVersion
        {
            get => experimentVersion;
            set => experimentVersion = value;
        }

        public bool UseReliabilityGate => experimentVersion != HarmonyExperimentVersion.V1_FixedSwitching;
        public bool RequireVpsDwell => experimentVersion != HarmonyExperimentVersion.V1_FixedSwitching && experimentVersion != HarmonyExperimentVersion.V4_NoDwellTime;
        public bool RequireMapIdMatch => experimentVersion != HarmonyExperimentVersion.V1_FixedSwitching && experimentVersion != HarmonyExperimentVersion.V5_NoMapIdCheck;
        public bool UseUncertaintyGuidance => experimentVersion == HarmonyExperimentVersion.Current || experimentVersion == HarmonyExperimentVersion.V3_FullHarmony;
        public bool UseContinuityGate => experimentVersion != HarmonyExperimentVersion.V1_FixedSwitching;
        public bool EnforceMinimumModeDuration => experimentVersion != HarmonyExperimentVersion.V1_FixedSwitching;

        public bool IsAcceptedMapId(BuildingId building, string mapId)
        {
            if (building == BuildingId.None || string.IsNullOrWhiteSpace(mapId))
                return false;

            for (int i = 0; i < buildingMapRules.Count; i++)
            {
                BuildingMapRule rule = buildingMapRules[i];
                if (rule == null || rule.building != building) continue;
                for (int j = 0; j < rule.acceptedMapIds.Count; j++)
                {
                    if (string.Equals(
                            rule.acceptedMapIds[j],
                            mapId,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool HasMapRules(BuildingId building)
        {
            for (int i = 0; i < buildingMapRules.Count; i++)
            {
                BuildingMapRule rule = buildingMapRules[i];
                if (rule != null && rule.building == building &&
                    rule.acceptedMapIds != null && rule.acceptedMapIds.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static HarmonyConfig CreateRuntimeDefaults()
        {
            HarmonyConfig config = CreateInstance<HarmonyConfig>();
            config.name = "HarmonyConfig_RuntimeDefaults";
            return config;
        }

        private void OnValidate()
        {
            gpsRejectedAccuracyMeters =
                Mathf.Max(gpsExcellentAccuracyMeters + 0.01f, gpsRejectedAccuracyMeters);
            gpsStaleAgeSeconds = Mathf.Max(gpsFreshAgeSeconds + 0.01f, gpsStaleAgeSeconds);
            vpsStaleAgeSeconds = Mathf.Max(vpsFreshAgeSeconds + 0.01f, vpsStaleAgeSeconds);
            vpsRejectedPositionDeltaMeters =
                Mathf.Max(vpsStablePositionDeltaMeters + 0.01f, vpsRejectedPositionDeltaMeters);
            vpsRejectedHeadingDeltaDegrees =
                Mathf.Max(vpsStableHeadingDeltaDegrees + 0.01f, vpsRejectedHeadingDeltaDegrees);
            highReliabilityThreshold =
                Mathf.Max(mediumReliabilityThreshold, highReliabilityThreshold);
        }
    }
}
