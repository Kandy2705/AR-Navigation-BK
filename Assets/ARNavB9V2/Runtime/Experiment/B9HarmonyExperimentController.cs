using System;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Reliability;
using ARNavB9V2.Vps;
using UnityEngine;

namespace ARNavB9V2.Experiment
{
    [DefaultExecutionOrder(-25)]
    [DisallowMultipleComponent]
    public sealed class B9HarmonyExperimentController : MonoBehaviour
    {
        private const string VersionPreferenceKey = "ARNavB9V2.HarmonyVersion";

        [SerializeField] private B9HarmonyVersion selectedVersion =
            B9HarmonyVersion.V5_FullHarmony;
        [SerializeField] private B9ReliableNavigationController reliabilityController;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9TransitionPdrTracker transitionPdr;
        [SerializeField] private B9IndoorPoseTracker indoorPose;
        [SerializeField] private B9RouteRibbonRenderer outdoorRibbon;
        [SerializeField] private B9RouteRibbonRenderer indoorRibbon;
        [SerializeField] private B9ExperimentLogger experimentLogger;
        [SerializeField] private bool rememberSelection = true;

        public B9HarmonyVersion SelectedVersion => selectedVersion;
        public B9HarmonyExperimentProfile ActiveProfile { get; private set; }
        public float CurrentGuidanceReliability { get; private set; } = 1f;
        public event Action<B9HarmonyExperimentProfile> VersionChanged;

        public void Configure(
            B9ReliableNavigationController reliability,
            B9VpsTransitionController transition,
            B9OutdoorLocationProvider gps,
            B9TransitionPdrTracker pdr,
            B9IndoorPoseTracker indoorTracker,
            B9RouteRibbonRenderer outdoorRouteRibbon,
            B9RouteRibbonRenderer indoorRouteRibbon,
            B9ExperimentLogger logger)
        {
            reliabilityController = reliability;
            vpsTransition = transition;
            locationProvider = gps;
            transitionPdr = pdr;
            indoorPose = indoorTracker;
            outdoorRibbon = outdoorRouteRibbon;
            indoorRibbon = indoorRouteRibbon;
            experimentLogger = logger;
        }

        private void Awake()
        {
            if (rememberSelection && PlayerPrefs.HasKey(VersionPreferenceKey))
            {
                B9HarmonyVersion stored =
                    (B9HarmonyVersion)PlayerPrefs.GetInt(VersionPreferenceKey);
                if (B9HarmonyExperimentProfile.IsSelectable(stored))
                    selectedVersion = stored;
            }
            ApplySelectedVersion(restartLog: false);
        }

        private void Update()
        {
            CurrentGuidanceReliability = CalculateGuidanceReliability();
            outdoorRibbon?.SetReliabilityPresentation(
                ActiveProfile.AdaptiveGuidance,
                CurrentGuidanceReliability);
            indoorRibbon?.SetReliabilityPresentation(
                ActiveProfile.AdaptiveGuidance,
                CurrentGuidanceReliability);
        }

        public void SelectVersion(B9HarmonyVersion version)
        {
            if (!B9HarmonyExperimentProfile.IsSelectable(version))
                return;
            bool changed = selectedVersion != version;
            selectedVersion = version;
            if (rememberSelection)
            {
                PlayerPrefs.SetInt(VersionPreferenceKey, (int)selectedVersion);
                PlayerPrefs.Save();
            }
            ApplySelectedVersion(restartLog: changed);
        }

        private void ApplySelectedVersion(bool restartLog)
        {
            ActiveProfile = B9HarmonyExperimentProfile.For(selectedVersion);
            reliabilityController?.ApplyExperimentProfile(ActiveProfile);
            vpsTransition?.ApplyExperimentProfile(ActiveProfile);
            outdoorRibbon?.SetReliabilityPresentation(
                ActiveProfile.AdaptiveGuidance,
                1f);
            indoorRibbon?.SetReliabilityPresentation(
                ActiveProfile.AdaptiveGuidance,
                1f);
            experimentLogger?.SetExperimentProfile(ActiveProfile, restartLog);
            VersionChanged?.Invoke(ActiveProfile);
        }

        private float CalculateGuidanceReliability()
        {
            if (reliabilityController == null)
                return 1f;
            return reliabilityController.State switch
            {
                B9NavigationState.OutdoorGps => locationProvider != null
                    && locationProvider.HasReliableFix
                    ? Mathf.Clamp01(1f - locationProvider.HorizontalAccuracyMeters / 30f)
                    : 0.2f,
                B9NavigationState.EnteringWithPdr => transitionPdr != null
                    ? transitionPdr.Confidence
                    : 0.5f,
                B9NavigationState.VpsScanning => vpsTransition != null
                    ? vpsTransition.LastLocalizationQuality
                    : 0.45f,
                B9NavigationState.VpsFailed => 0.15f,
                B9NavigationState.IndoorVps => indoorPose != null
                    ? indoorPose.Confidence
                    : 0.7f,
                B9NavigationState.ExitingWithPdr => transitionPdr != null
                    ? transitionPdr.Confidence
                    : 0.5f,
                _ => 0.5f,
            };
        }
    }
}
