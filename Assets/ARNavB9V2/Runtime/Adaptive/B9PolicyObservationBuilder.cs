using System;

namespace ARNavB9V2.Adaptive
{
    /// <summary>
    /// One 1 Hz snapshot of the values the experiment logger writes to
    /// samples_*.csv, i.e. what the policy saw during training.
    /// </summary>
    public struct B9PolicySample
    {
        public float elapsedSeconds;
        public string state;
        public string activeSource;
        public float gpsAccuracyMeters;
        public float gpsAgeSeconds;
        public float gpsReliability;
        public float gpsStableSeconds;
        public bool gpsValid;
        public float pdrSteps;
        public float pdrConfidence;
        public bool vpsValid;
        public float vpsReliability;
        public float vpsConfidence;
        public bool vpsConfidenceAvailable;
        public bool vpsMapMatches;
        public float vpsStableSeconds;
        public float vpsScanSeconds;
        public int sourceToggleCount;
    }

    /// <summary>
    /// Builds the 32-value observation exactly as prepare_data_v2.build_episode and
    /// replay_env_v3._observation do. Stateful: the history features depend on the
    /// previous sample, so call <see cref="Reset"/> at the start of every trial.
    /// </summary>
    public sealed class B9PolicyObservationBuilder
    {
        public const int ObservationSize = 32;

        // Training-time orders. "ExitingPdr" is kept as written in prepare_data_v2:
        // the app's state is named ExitingWithPdr, so that state was encoded as all
        // zeros during training and must stay all zeros here.
        private static readonly string[] StateOrder =
        {
            "OutdoorGps", "EnteringWithPdr", "VpsScanning", "IndoorVps", "ExitingPdr", "VpsFailed",
        };
        private static readonly string[] SourceOrder = { "Gps", "Pdr", "Vps" };
        // Features that were a constant 0.5 in every training row.
        private const float UnmodelledFeature = 0.5f;

        private bool hasPrevious;
        private int previousToggleCount;
        private string previousSource;
        private float lastSwitchElapsed;

        public void Reset()
        {
            hasPrevious = false;
        }

        public float[] Build(in B9PolicySample sample)
        {
            float elapsed = Finite(sample.elapsedSeconds);
            if (!hasPrevious)
            {
                previousToggleCount = sample.sourceToggleCount;
                previousSource = sample.activeSource;
                lastSwitchElapsed = elapsed;
                hasPrevious = true;
            }
            float pingPong = sample.sourceToggleCount > previousToggleCount ? 1f : 0f;
            if (!string.Equals(sample.activeSource, previousSource, StringComparison.Ordinal))
                lastSwitchElapsed = elapsed;

            var obs = new float[ObservationSize];
            obs[0] = Clip01(1f - Finite(sample.gpsAccuracyMeters) / 20f);
            obs[1] = Clip01(1f - Finite(sample.gpsAgeSeconds) / 30f);
            obs[2] = UnmodelledFeature;
            obs[3] = Clip01(Finite(sample.gpsReliability));
            obs[4] = Clip01(Finite(sample.gpsStableSeconds) / 10f);
            obs[5] = sample.gpsValid ? 1f : 0f;
            obs[6] = Clip01(Finite(sample.pdrConfidence));
            obs[7] = Clip01(Finite(sample.pdrSteps) / Math.Max(elapsed, 1f) / 2f);
            obs[8] = UnmodelledFeature;
            obs[9] = UnmodelledFeature;
            obs[10] = sample.vpsValid ? 1f : 0f;
            obs[11] = Clip01(Finite(sample.vpsReliability));
            obs[12] = Clip01(Finite(sample.vpsConfidence));
            obs[13] = sample.vpsConfidenceAvailable ? 1f : 0f;
            obs[14] = sample.vpsMapMatches ? 1f : 0f;
            obs[15] = Clip01(Finite(sample.vpsStableSeconds) / 10f);
            obs[16] = Clip01(Finite(sample.vpsScanSeconds) / 10f);
            obs[17] = UnmodelledFeature;
            for (int i = 0; i < StateOrder.Length; i++)
                obs[18 + i] = sample.state == StateOrder[i] ? 1f : 0f;
            for (int i = 0; i < SourceOrder.Length; i++)
                obs[24 + i] = sample.activeSource == SourceOrder[i] ? 1f : 0f;
            // obs[27] is padding of the 10-wide controller block.
            obs[28] = sample.state == "IndoorVps" ? 1f : 0f;
            obs[29] = sample.state == "OutdoorGps" ? 1f : 0f;
            obs[30] = Clip01((elapsed - lastSwitchElapsed) / 10f);
            obs[31] = pingPong;

            previousToggleCount = sample.sourceToggleCount;
            previousSource = sample.activeSource;
            return obs;
        }

        // The logger writes non-finite values as empty cells, which the training
        // pipeline read as 0.
        private static float Finite(float value) => float.IsFinite(value) ? value : 0f;

        private static float Clip01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
