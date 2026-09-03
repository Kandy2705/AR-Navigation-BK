using UnityEngine;

namespace ARNavB9V2.Experiment
{
    public enum B9HarmonyVersion
    {
        V1_FixedGeometric = 1,
        V2_ReliableHandover = 2,
        V3_NoDwellTime = 3,
        V4_NoMapIdCheck = 4,
        V5_FullHarmony = 5,
        BQ_QualityThreshold = 6,
        BT_QualityDwell = 7,
    }

    public readonly struct B9HarmonyExperimentProfile
    {
        private B9HarmonyExperimentProfile(
            B9HarmonyVersion version,
            string displayName,
            bool qualityThreshold,
            bool temporalDwell,
            bool mapIdCheck,
            bool recoveryFsm,
            bool adaptiveGuidance,
            bool continuityGate,
            bool minimumModeDuration,
            bool baseline)
        {
            Version = version;
            DisplayName = displayName;
            QualityThreshold = qualityThreshold;
            TemporalDwell = temporalDwell;
            MapIdCheck = mapIdCheck;
            RecoveryFsm = recoveryFsm;
            AdaptiveGuidance = adaptiveGuidance;
            ContinuityGate = continuityGate;
            MinimumModeDuration = minimumModeDuration;
            IsBaseline = baseline;
        }

        public B9HarmonyVersion Version { get; }
        public string VersionCode => Version switch
        {
            B9HarmonyVersion.BQ_QualityThreshold => "BQ",
            B9HarmonyVersion.BT_QualityDwell => "BT",
            _ => "V" + (int)Version,
        };
        public string DisplayName { get; }
        public bool QualityThreshold { get; }
        public bool TemporalDwell { get; }
        public bool MapIdCheck { get; }
        public bool RecoveryFsm { get; }
        public bool AdaptiveGuidance { get; }
        public bool ContinuityGate { get; }
        public bool MinimumModeDuration { get; }
        public bool IsBaseline { get; }

        public float VpsEnterReliability => 0.72f;
        public float GpsExitReliability => 0.70f;
        public float MinimumVpsConfidence => 0.50f;
        public float VpsDwellSeconds => TemporalDwell ? 2f : 0f;
        public float GpsDwellSeconds => TemporalDwell ? 3f : 0f;

        public float GpsWeightAccuracy => 0.30f;
        public float GpsWeightFreshness => 0.20f;
        public float GpsWeightMotion => 0.20f;
        public float GpsWeightTransition => 0.15f;
        public float GpsWeightDwell => IsBaseline || !TemporalDwell ? 0f : 0.15f;
        public float VpsWeightConfidence => 0.30f;
        public float VpsWeightFreshness => 0.20f;
        public float VpsWeightMotion => 0.20f;
        public float VpsWeightMapMatch => MapIdCheck ? 0.15f : 0f;
        public float VpsWeightDwell => IsBaseline || !TemporalDwell ? 0f : 0.15f;

        public float GpsWeightSum => Mathf.Max(
            0.0001f,
            GpsWeightAccuracy + GpsWeightFreshness + GpsWeightMotion
            + GpsWeightTransition + GpsWeightDwell);
        public float VpsWeightSum => Mathf.Max(
            0.0001f,
            VpsWeightConfidence + VpsWeightFreshness + VpsWeightMotion
            + VpsWeightMapMatch + VpsWeightDwell);

        public string FeatureCode =>
            $"Q{Flag(QualityThreshold)}_D{Flag(TemporalDwell)}_M{Flag(MapIdCheck)}_"
            + $"R{Flag(RecoveryFsm)}_A{Flag(AdaptiveGuidance)}";

        public static B9HarmonyExperimentProfile For(B9HarmonyVersion version)
        {
            return version switch
            {
                B9HarmonyVersion.V1_FixedGeometric => new B9HarmonyExperimentProfile(
                    version, "Fixed Geometric", false, false, false, false, false,
                    false, false, false),
                B9HarmonyVersion.V2_ReliableHandover => new B9HarmonyExperimentProfile(
                    version, "Reliable Handover", true, true, true, true, false,
                    true, true, false),
                B9HarmonyVersion.V3_NoDwellTime => new B9HarmonyExperimentProfile(
                    version, "No Dwell Time", true, false, true, true, false,
                    true, true, false),
                B9HarmonyVersion.V4_NoMapIdCheck => new B9HarmonyExperimentProfile(
                    version, "No Map-ID Check", true, true, false, true, false,
                    true, true, false),
                B9HarmonyVersion.BQ_QualityThreshold => new B9HarmonyExperimentProfile(
                    version, "Quality-Threshold Baseline", true, false, false, false,
                    false, false, false, true),
                B9HarmonyVersion.BT_QualityDwell => new B9HarmonyExperimentProfile(
                    version, "Quality + Dwell Baseline", true, true, false, false,
                    false, false, false, true),
                _ => new B9HarmonyExperimentProfile(
                    B9HarmonyVersion.V5_FullHarmony, "Full HARMONY", true, true,
                    true, true, true, true, true, false),
            };
        }

        public static bool IsSelectable(B9HarmonyVersion version)
        {
            int raw = (int)version;
            return raw >= 1 && raw <= 7;
        }

        private static int Flag(bool value) => value ? 1 : 0;
    }
}
