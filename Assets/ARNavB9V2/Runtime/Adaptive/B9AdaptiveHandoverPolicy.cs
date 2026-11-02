using System;
using System.Diagnostics;
using ARNavB9V2.Experiment;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Reliability;
using ARNavB9V2.Vps;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ARNavB9V2.Adaptive
{
    /// <summary>
    /// On-device PPO policy for HARMONY V6. Once per second it reads the same
    /// values the experiment logger records, runs the exported actor network and
    /// publishes smoothed GPS/VPS gate thresholds and dwell times. It only supplies
    /// soft parameters: the reliability controllers keep every hard gate and every
    /// FSM transition. Any failure (missing model, bad output) switches it to the
    /// profile's fixed parameters for the rest of the activation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9AdaptiveHandoverPolicy : MonoBehaviour, IB9HandoverGateParameterSource
    {
        public const string DefaultResourcePath = "ARNavB9V2Policy/b9_handover_policy";

        [SerializeField] private TextAsset policyAsset;
        [SerializeField] private B9ReliableNavigationController reliabilityController;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9TransitionPdrTracker transitionPdr;
        [SerializeField] private B9ExperimentLogger experimentLogger;

        private readonly B9PolicyObservationBuilder observations = new B9PolicyObservationBuilder();
        private B9PolicyModel model;
        private B9PolicyActionDecoder decoder;
        private bool loadAttempted;
        private bool failed;
        private float nextInferenceAt;
        private float activatedAt;
        private string observedSessionId = string.Empty;
        private int ownToggleCount;
        private B9PoseSource ownLastSource;

        public bool IsActive { get; private set; }
        public string Status { get; private set; } = "inactive";
        public string PolicyVersion => model != null ? model.policyVersion : string.Empty;
        public float LastInferenceMilliseconds { get; private set; } = float.NaN;
        public int InferenceCount { get; private set; }
        public float[] LastAction { get; private set; } = Array.Empty<float>();
        private bool IsUsable => IsActive && !failed && decoder != null && decoder.HasValue;

        public void Configure(
            B9ReliableNavigationController reliability,
            B9VpsTransitionController transition,
            B9OutdoorLocationProvider gps,
            B9TransitionPdrTracker pdr,
            B9ExperimentLogger logger)
        {
            reliabilityController = reliability;
            vpsTransition = transition;
            locationProvider = gps;
            transitionPdr = pdr;
            experimentLogger = logger;
        }

        public void Activate()
        {
            IsActive = true;
            failed = false;
            if (!EnsureModel())
                return;
            ResetEpisode();
            Status = "warming_up";
        }

        public void Deactivate()
        {
            IsActive = false;
            Status = "inactive";
            decoder?.Reset();
        }

        public B9HandoverGateParameters GetGateParameters(B9HarmonyExperimentProfile profile)
        {
            return IsUsable && profile.UsesAdaptivePolicy
                ? decoder.Current
                : B9HandoverGateParameters.FromProfile(profile);
        }

        private void Update()
        {
            if (!IsActive || failed || model == null)
                return;
            string sessionId = experimentLogger != null && experimentLogger.IsRecording
                ? experimentLogger.SessionId
                : string.Empty;
            if (sessionId != observedSessionId)
            {
                // A new trial is a new training episode: restart history and EMA.
                ResetEpisode();
                observedSessionId = sessionId;
            }
            TrackOwnToggles();
            if (Time.unscaledTime < nextInferenceAt)
                return;
            nextInferenceAt = Time.unscaledTime + 1f / model.inferenceHz;
            Infer();
        }

        private void Infer()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                float[] observation = observations.Build(ReadSample());
                float[] action = model.Forward(observation);
                stopwatch.Stop();
                foreach (float value in action)
                {
                    if (!float.IsFinite(value))
                    {
                        Fail("policy produced a non-finite action");
                        return;
                    }
                }
                LastAction = action;
                LastInferenceMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
                InferenceCount++;
                B9HandoverGateParameters fallback = B9HandoverGateParameters.FromProfile(
                    B9HarmonyExperimentProfile.For(B9HarmonyVersion.V6_AdaptivePolicy));
                decoder.Decode(action, fallback);
                Status = "running";
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
        }

        private B9PolicySample ReadSample()
        {
            bool logging = experimentLogger != null && experimentLogger.IsRecording;
            return new B9PolicySample
            {
                elapsedSeconds = logging
                    ? experimentLogger.ElapsedSeconds
                    : Time.unscaledTime - activatedAt,
                state = reliabilityController != null
                    ? reliabilityController.State.ToString()
                    : string.Empty,
                activeSource = reliabilityController != null
                    ? reliabilityController.ActiveSource.ToString()
                    : string.Empty,
                gpsAccuracyMeters = locationProvider != null
                    ? locationProvider.HorizontalAccuracyMeters
                    : float.NaN,
                gpsAgeSeconds = locationProvider != null
                    ? locationProvider.SampleAgeSeconds
                    : float.NaN,
                gpsReliability = reliabilityController != null
                    ? reliabilityController.GpsReliability
                    : float.NaN,
                gpsStableSeconds = reliabilityController != null
                    ? reliabilityController.GpsStableSeconds
                    : float.NaN,
                gpsValid = locationProvider != null && locationProvider.HasReliableFix,
                pdrSteps = transitionPdr != null ? transitionPdr.StepCount : 0f,
                pdrConfidence = transitionPdr != null ? transitionPdr.Confidence : float.NaN,
                vpsValid = vpsTransition != null && vpsTransition.VpsValid,
                vpsReliability = vpsTransition != null
                    ? vpsTransition.LastLocalizationQuality
                    : float.NaN,
                vpsConfidence = vpsTransition != null ? vpsTransition.LastVpsConfidence : float.NaN,
                vpsConfidenceAvailable = vpsTransition != null
                                         && vpsTransition.LastVpsConfidenceAvailable,
                vpsMapMatches = vpsTransition != null && vpsTransition.LastMapMatchesBuilding,
                vpsStableSeconds = vpsTransition != null ? vpsTransition.VpsStableSeconds : float.NaN,
                vpsScanSeconds = vpsTransition != null
                    ? vpsTransition.CurrentScanElapsedSeconds
                    : float.NaN,
                sourceToggleCount = logging ? experimentLogger.SourceToggleCount : ownToggleCount,
            };
        }

        private bool EnsureModel()
        {
            if (model != null)
                return true;
            if (loadAttempted)
            {
                Fail("policy model unavailable");
                return false;
            }
            loadAttempted = true;
            try
            {
                TextAsset asset = policyAsset != null
                    ? policyAsset
                    : Resources.Load<TextAsset>(DefaultResourcePath);
                if (asset == null)
                    throw new InvalidOperationException($"Resources/{DefaultResourcePath}.json not found");
                model = B9PolicyModel.FromJson(asset.text);
                decoder = new B9PolicyActionDecoder(model);
                Debug.Log($"[B9AdaptiveHandoverPolicy] Loaded {model.policyVersion} ({model.mode}).");
                return true;
            }
            catch (Exception exception)
            {
                model = null;
                Fail(exception.Message);
                return false;
            }
        }

        private void ResetEpisode()
        {
            observations.Reset();
            decoder?.Reset();
            activatedAt = Time.unscaledTime;
            nextInferenceAt = Time.unscaledTime;
            ownToggleCount = 0;
            ownLastSource = reliabilityController != null
                ? reliabilityController.ActiveSource
                : B9PoseSource.Gps;
        }

        private void TrackOwnToggles()
        {
            if (reliabilityController == null || reliabilityController.ActiveSource == ownLastSource)
                return;
            ownLastSource = reliabilityController.ActiveSource;
            ownToggleCount++;
        }

        private void Fail(string reason)
        {
            if (failed)
                return;
            failed = true;
            Status = "fallback: " + reason;
            Debug.LogWarning($"[B9AdaptiveHandoverPolicy] Using fixed V6 parameters: {reason}");
            experimentLogger?.AddResearchMarker("policy_fallback " + reason);
        }
    }
}
