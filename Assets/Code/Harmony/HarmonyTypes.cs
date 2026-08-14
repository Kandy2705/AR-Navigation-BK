using System;
using UnityEngine;

namespace ARNav.Harmony
{
    public enum HarmonyExperimentVersion
    {
        V0_FixedSwitching,
        V1_ReliabilityOnly,
        V2_FullHarmony,
    }

    public enum HarmonyState
    {
        Outdoor,
        EnteringTransition,
        VpsScanning,
        Indoor,
        ExitingTransition,
        Uncertain,
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
