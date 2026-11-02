using ARNavB9V2.Experiment;

namespace ARNavB9V2.Reliability
{
    /// <summary>
    /// Soft parameters of the GPS/VPS acceptance gates. Only these values may be
    /// adapted at runtime; provider validity, map-ID, transition volumes, scan
    /// timeout and legal FSM transitions stay hard-coded in the controllers.
    /// </summary>
    public readonly struct B9HandoverGateParameters
    {
        public B9HandoverGateParameters(
            float gpsThreshold,
            float vpsThreshold,
            float gpsDwellSeconds,
            float vpsDwellSeconds)
        {
            GpsThreshold = gpsThreshold;
            VpsThreshold = vpsThreshold;
            GpsDwellSeconds = gpsDwellSeconds;
            VpsDwellSeconds = vpsDwellSeconds;
        }

        public float GpsThreshold { get; }
        public float VpsThreshold { get; }
        public float GpsDwellSeconds { get; }
        public float VpsDwellSeconds { get; }

        public static B9HandoverGateParameters FromProfile(B9HarmonyExperimentProfile profile)
        {
            return new B9HandoverGateParameters(
                profile.GpsExitReliability,
                profile.VpsEnterReliability,
                profile.GpsDwellSeconds,
                profile.VpsDwellSeconds);
        }
    }

    public interface IB9HandoverGateParameterSource
    {
        /// <summary>
        /// Parameters to use for the given profile right now. Implementations
        /// return <see cref="B9HandoverGateParameters.FromProfile"/> whenever they
        /// cannot provide a valid adaptive value.
        /// </summary>
        B9HandoverGateParameters GetGateParameters(B9HarmonyExperimentProfile profile);
    }
}
