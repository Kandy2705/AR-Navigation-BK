using System;
using UnityEngine;

namespace ARNav.Harmony
{
    public enum HarmonyExperimentVersion
    {
        V1_FixedSwitching = 0,    // Migrates old V0_FixedSwitching
        V2_ReliableHandover = 1,  // Migrates old V1_ReliabilityOnly
        Current = 2,              // Migrates old V2_FullHarmony (development default)
        V3_FullHarmony = 3,
        V4_NoDwellTime = 4,
        V5_NoMapIdCheck = 5,
    }

    public enum HarmonyState
    {
        Outdoor,
        EnteringTransition,
        VpsScanning,
        Indoor,
        Relocalization,
        ExitingTransition,
        Uncertain,
    }

    public enum HarmonyTestDirection
    {
        GPS_TO_VPS,
        VPS_TO_GPS
    }

    public enum HarmonyTestCondition
    {
        NORMAL,
        GNSS_DEGRADED,
        VPS_DELAYED_OR_UNAVAILABLE,
        WRONG_OR_AMBIGUOUS_MAP,
        INDOOR_TO_OUTDOOR
    }

    public enum HarmonyLocalizationSource
    {
        None,
        GPS,
        VPS,
        LastTrusted,
    }

    public enum HarmonyReliabilityBand
    {
        Low,
        Medium,
        High,
    }

    [Serializable]
    public struct HarmonyGpsSample
    {
        public bool IsValid;
        public Vector3 CampusPosition;
        public Quaternion CampusRotation;
        public bool HasHeading;
        public float HeadingDegrees;
        public double Latitude;
        public double Longitude;
        public float HorizontalAccuracyMeters;
        public float AgeSeconds;
        public bool JumpRejected;
        public float RejectedJumpMeters;
        public double Timestamp;
    }

    [Serializable]
    public struct HarmonyVpsSample
    {
        public bool IsValid;
        public Vector3 CampusPosition;
        public Quaternion CampusRotation;
        public Vector3 MapLocalPosition;
        public Quaternion MapLocalRotation;
        public float Confidence;
        public bool ConfidenceAvailable;
        public string MapId;
        public bool MapIdAvailable;
        public bool MapMatchesBuilding;
        public float AgeSeconds;
        public double Timestamp;
    }

    [Serializable]
    public struct HarmonyReliabilitySnapshot
    {
        [Range(0f, 1f)] public float Gps;
        [Range(0f, 1f)] public float Vps;
        [Range(0f, 1f)] public float Active;
        public HarmonyReliabilityBand Band;
        public float GpsStableSeconds;
        public float VpsStableSeconds;
        public float VpsPositionDeltaMeters;
        public float VpsHeadingDeltaDegrees;
        public string GpsReason;
        public string VpsReason;
    }

    public readonly struct HarmonyStateTransition
    {
        public HarmonyStateTransition(
            HarmonyState previous,
            HarmonyState next,
            string reason,
            float timestamp)
        {
            Previous = previous;
            Next = next;
            Reason = reason;
            Timestamp = timestamp;
        }

        public HarmonyState Previous { get; }
        public HarmonyState Next { get; }
        public string Reason { get; }
        public float Timestamp { get; }
    }
}
