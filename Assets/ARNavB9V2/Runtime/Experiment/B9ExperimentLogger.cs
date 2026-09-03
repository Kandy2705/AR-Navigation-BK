using System;
using System.Globalization;
using System.IO;
using System.Text;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Reliability;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using UnityEngine;

namespace ARNavB9V2.Experiment
{
    /// <summary>Writes one events, samples, and summary CSV bundle per trial.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class B9ExperimentLogger : MonoBehaviour
    {
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9OutdoorRouteController outdoorRoute;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9IndoorRouteController indoorRoute;
        [SerializeField] private B9IndoorPoseTracker indoorPose;
        [SerializeField] private B9ReliableNavigationController reliabilityController;
        [SerializeField] private B9TransitionPdrTracker transitionPdr;
        [SerializeField] private B9HarmonyVersion harmonyVersion =
            B9HarmonyVersion.V5_FullHarmony;
        [SerializeField] private bool autoStartSession = true;
        [SerializeField, Range(0.1f, 5f)] private float sampleIntervalSeconds = 0.5f;
        [SerializeField, Range(1f, 30f)] private float flushIntervalSeconds = 5f;

        private StreamWriter eventsWriter;
        private StreamWriter samplesWriter;
        private DateTime trialStartedUtc;
        private float sessionStartedAt;
        private float nextSampleAt;
        private float nextFlushAt;
        private float lastIndoorRemaining = float.NaN;
        private int lastObservedStepCount;
        private bool wrongWayActive;
        private float wrongWayBaseline;
        private string lastDestination = string.Empty;
        private string finalReason = string.Empty;
        private B9PoseSource lastLoggedSource = B9PoseSource.Gps;
        private B9HarmonyExperimentProfile harmonyProfile =
            B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony);

        public bool IsRecording => eventsWriter != null && samplesWriter != null;
        public string SessionId { get; private set; } = string.Empty;
        public string CurrentFilePath => EventsFilePath;
        public string LastSavedFilePath { get; private set; } = string.Empty;
        public string EventsFilePath { get; private set; } = string.Empty;
        public string SamplesFilePath { get; private set; } = string.Empty;
        public string SummaryFilePath { get; private set; } = string.Empty;
        public float ElapsedSeconds => IsRecording ? Time.unscaledTime - sessionStartedAt : 0f;
        public int SampleCount { get; private set; }
        public int EventCount { get; private set; }
        public int WrongWayCount { get; private set; }
        public int RecoveryCount { get; private set; }
        public bool HandoverSucceeded { get; private set; }
        public bool DestinationArrived { get; private set; }
        public int SourceToggleCount { get; private set; }
        public B9HarmonyExperimentProfile HarmonyProfile => harmonyProfile;

        public void Configure(
            B9OutdoorSceneContext outdoor,
            B9VpsTransitionController transition,
            B9IndoorSceneContext indoor,
            B9IndoorPoseTracker pose,
            bool startAutomatically = true)
        {
            locationProvider = outdoor != null ? outdoor.LocationProvider : null;
            outdoorRoute = outdoor != null ? outdoor.RouteController : null;
            vpsTransition = transition;
            indoorRoute = indoor != null ? indoor.RouteController : null;
            indoorPose = pose;
            autoStartSession = startAutomatically;
        }

        public void AttachReliability(
            B9ReliableNavigationController controller,
            B9TransitionPdrTracker pdrTracker)
        {
            bool resubscribe = isActiveAndEnabled;
            if (resubscribe)
                UnsubscribeReliability();
            reliabilityController = controller;
            transitionPdr = pdrTracker;
            if (resubscribe)
                SubscribeReliability();
        }

        public void SetExperimentProfile(
            B9HarmonyExperimentProfile profile,
            bool restartActiveTrial = true)
        {
            bool changed = harmonyProfile.Version != profile.Version;
            harmonyProfile = profile;
            harmonyVersion = profile.Version;
            if (!changed || !restartActiveTrial || !IsRecording)
                return;

            EndCurrentTrial("harmony_version_changed_to_" + profile.VersionCode);
            BeginNewTrial();
        }

        private void OnEnable()
        {
            Subscribe();
            Application.lowMemory += HandleLowMemory;
            if (autoStartSession && !IsRecording)
                BeginNewTrial();
        }

        private void OnDisable()
        {
            Application.lowMemory -= HandleLowMemory;
            Unsubscribe();
            EndCurrentTrial("component_disabled");
        }

        private void Update()
        {
            if (!IsRecording)
                return;

            DetectDestinationChange();
            DetectSourceChange();
            DetectWrongWayAndRecovery();
            if (Time.unscaledTime >= nextSampleAt)
            {
                nextSampleAt = Time.unscaledTime + sampleIntervalSeconds;
                WriteSampleRow();
                SampleCount++;
            }

            if (Time.unscaledTime >= nextFlushAt)
            {
                FlushWriters();
                nextFlushAt = Time.unscaledTime + flushIntervalSeconds;
            }
        }

        public void ToggleTrial()
        {
            if (IsRecording)
                EndCurrentTrial("user_finished_trial");
            else
                BeginNewTrial();
        }

        public void BeginNewTrial()
        {
            if (IsRecording)
                EndCurrentTrial("new_trial_requested");

            string directory = Path.Combine(Application.persistentDataPath, "HarmonyLogs");
            Directory.CreateDirectory(directory);
            trialStartedUtc = DateTime.UtcNow;
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
            SessionId = trialStartedUtc.ToString(
                            "yyyyMMdd_HHmmss_fff",
                            CultureInfo.InvariantCulture)
                        + "_"
                        + harmonyProfile.VersionCode
                        + "_GPS_TO_VPS_NORMAL_DEV"
                        + "_"
                        + suffix;
            EventsFilePath = Path.Combine(directory, "events_" + SessionId + ".csv");
            SamplesFilePath = Path.Combine(directory, "samples_" + SessionId + ".csv");
            SummaryFilePath = Path.Combine(directory, "summary_" + SessionId + ".csv");

            eventsWriter = NewWriter(EventsFilePath);
            samplesWriter = NewWriter(SamplesFilePath);
            eventsWriter.WriteLine(
                "utc_iso,session_id,harmony_version,harmony_profile,quality_gate,temporal_dwell,"
                + "map_id_check,recovery_fsm,adaptive_guidance,elapsed_s,event,from_state,to_state,source,destination,"
                + "latitude,longitude,gps_accuracy_m,campus_x,campus_y,campus_z,"
                + "map_x,map_y,map_z,vps_attempt,note");
            samplesWriter.WriteLine(
                "utc_iso,session_id,harmony_version,harmony_profile,quality_gate,temporal_dwell,"
                + "map_id_check,recovery_fsm,adaptive_guidance,elapsed_s,state,source,destination,latitude,longitude,"
                + "gps_accuracy_m,gps_valid,pdr_steps,pdr_confidence,campus_x,campus_y,campus_z,"
                + "map_x,map_y,map_z,outdoor_remaining_m,vps_attempt,vps_state,vps_scan_s,"
                + "indoor_state,indoor_pose_source,indoor_confidence,indoor_steps,heading_deg,"
                + "indoor_remaining_m,route_revision,wrong_way_count,recovery_count,"
                + "active_source,gps_age_s,gps_reliability,gps_stable_s,vps_valid,"
                + "vps_reliability,vps_stable_s,vps_confidence,vps_confidence_available,"
                + "vps_map_id,vps_map_id_available,vps_map_matches,source_toggle_count,"
                + "position_jump_m,heading_jump_deg,candidate_source,gps_threshold_passed,"
                + "vps_threshold_passed,gps_dwell_gate_passed,vps_dwell_gate_passed,decision_reason");

            sessionStartedAt = Time.unscaledTime;
            nextSampleAt = Time.unscaledTime;
            nextFlushAt = Time.unscaledTime + flushIntervalSeconds;
            SampleCount = 0;
            EventCount = 0;
            WrongWayCount = 0;
            RecoveryCount = 0;
            SourceToggleCount = 0;
            HandoverSucceeded = false;
            DestinationArrived = false;
            wrongWayActive = false;
            lastIndoorRemaining = float.NaN;
            lastObservedStepCount = indoorPose != null ? indoorPose.StepCount : 0;
            lastDestination = GetDestination();
            lastLoggedSource = reliabilityController != null
                ? reliabilityController.ActiveSource
                : B9PoseSource.Gps;
            finalReason = string.Empty;
            WriteEventRow("trial_started", string.Empty, string.Empty, "auto=" + autoStartSession);
            WriteSummary(completed: false);
            FlushWriters();
        }

        public void EndCurrentTrial(string reason = "trial_finished")
        {
            if (!IsRecording)
                return;

            finalReason = reason;
            WriteEventRow("trial_finished", string.Empty, string.Empty, reason);
            FlushWriters();
            eventsWriter.Dispose();
            samplesWriter.Dispose();
            eventsWriter = null;
            samplesWriter = null;
            WriteSummary(completed: true);
            LastSavedFilePath = SummaryFilePath;
        }

        public void AddResearchMarker(string note = "manual_marker")
        {
            if (IsRecording)
                WriteEventRow("research_marker", string.Empty, string.Empty, note);
        }

        public void FlushNow()
        {
            if (IsRecording)
            {
                WriteSummary(completed: false);
                FlushWriters();
            }
        }

        private void Subscribe()
        {
            if (outdoorRoute != null)
                outdoorRoute.StateChanged += HandleOutdoorStateChanged;
            if (vpsTransition != null)
                vpsTransition.StateChanged += HandleVpsStateChanged;
            if (indoorRoute != null)
                indoorRoute.StateChanged += HandleIndoorStateChanged;
            if (indoorPose != null)
                indoorPose.StepDetected += HandleIndoorStepDetected;
            if (vpsTransition != null)
                vpsTransition.ExperimentDecision += HandleExperimentDecision;
            SubscribeReliability();
        }

        private void Unsubscribe()
        {
            if (outdoorRoute != null)
                outdoorRoute.StateChanged -= HandleOutdoorStateChanged;
            if (vpsTransition != null)
                vpsTransition.StateChanged -= HandleVpsStateChanged;
            if (indoorRoute != null)
                indoorRoute.StateChanged -= HandleIndoorStateChanged;
            if (indoorPose != null)
                indoorPose.StepDetected -= HandleIndoorStepDetected;
            if (vpsTransition != null)
                vpsTransition.ExperimentDecision -= HandleExperimentDecision;
            UnsubscribeReliability();
        }

        private void SubscribeReliability()
        {
            if (reliabilityController != null)
                reliabilityController.StateChanged += HandleReliabilityStateChanged;
            if (reliabilityController != null)
                reliabilityController.ExperimentDecision += HandleExperimentDecision;
            if (transitionPdr != null)
                transitionPdr.StepDetected += HandleTransitionStepDetected;
        }

        private void UnsubscribeReliability()
        {
            if (reliabilityController != null)
                reliabilityController.StateChanged -= HandleReliabilityStateChanged;
            if (reliabilityController != null)
                reliabilityController.ExperimentDecision -= HandleExperimentDecision;
            if (transitionPdr != null)
                transitionPdr.StepDetected -= HandleTransitionStepDetected;
        }

        private void HandleReliabilityStateChanged(B9ReliabilityTransition transition)
        {
            if (transition.Current == B9NavigationState.IndoorVps)
            {
                if (transition.Source == B9PoseSource.Vps)
                {
                    HandoverSucceeded = true;
                    WriteEventRow(
                        "handover_completed",
                        transition.Previous.ToString(),
                        transition.Current.ToString(),
                        "GPS_TO_VPS success");
                }
                else
                {
                    WriteEventRow(
                        "handover_completed_approximate",
                        transition.Previous.ToString(),
                        transition.Current.ToString(),
                        "Indoor navigation continued from PDR approximate pose; VPS was not accepted");
                }
            }
            WriteEventRow(
                "reliability_state",
                transition.Previous.ToString(),
                transition.Current.ToString(),
                transition.Reason);
        }

        private void HandleExperimentDecision(string eventType, string detail)
        {
            WriteEventRow(eventType, string.Empty, string.Empty, detail);
        }

        private void HandleOutdoorStateChanged(B9OutdoorRouteController.RouteState state)
        {
            WriteEventRow("outdoor_state", string.Empty, state.ToString(), string.Empty);
        }

        private void HandleVpsStateChanged(B9VpsTransitionController.TransitionState state)
        {
            WriteEventRow("vps_state", string.Empty, state.ToString(), vpsTransition?.FailureReason);
        }

        private void HandleIndoorStateChanged(B9IndoorRouteController.IndoorRouteState state)
        {
            WriteEventRow("indoor_state", string.Empty, state.ToString(), string.Empty);
            if (state == B9IndoorRouteController.IndoorRouteState.Arrived)
            {
                if (indoorRoute != null && indoorRoute.LastArrivalWasExit)
                {
                    WriteEventRow("nearest_exit_arrived", string.Empty, state.ToString(), "B9-EXIT");
                    FlushWriters();
                    return;
                }
                DestinationArrived = true;
                WriteEventRow("destination_arrived", string.Empty, state.ToString(), GetDestination());
                FlushWriters();
            }
        }

        private void HandleIndoorStepDetected(int count, float timestamp, Vector3 position)
        {
            if (IsRecording && (count == 1 || count % 5 == 0))
                WriteEventRow("indoor_step_checkpoint", string.Empty, string.Empty, "step=" + count);
        }

        private void HandleTransitionStepDetected(int count, Vector3 position)
        {
            if (IsRecording && (count == 1 || count % 3 == 0))
                WriteEventRow("transition_step_checkpoint", string.Empty, string.Empty, "step=" + count);
        }

        private void DetectDestinationChange()
        {
            string current = GetDestination();
            if (string.Equals(current, lastDestination, StringComparison.OrdinalIgnoreCase))
                return;
            lastDestination = current;
            WriteEventRow("destination_changed", string.Empty, string.Empty, current);
        }

        private void DetectSourceChange()
        {
            if (reliabilityController == null
                || reliabilityController.ActiveSource == lastLoggedSource)
                return;
            B9PoseSource previous = lastLoggedSource;
            lastLoggedSource = reliabilityController.ActiveSource;
            SourceToggleCount++;
            WriteEventRow(
                "source_switched",
                previous.ToString(),
                lastLoggedSource.ToString(),
                previous + "->" + lastLoggedSource);
        }

        private void DetectWrongWayAndRecovery()
        {
            if (indoorRoute == null || indoorPose == null
                || indoorRoute.State != B9IndoorRouteController.IndoorRouteState.Navigating)
            {
                lastIndoorRemaining = float.NaN;
                wrongWayActive = false;
                return;
            }

            float remaining = indoorRoute.RemainingDistanceMeters;
            int steps = indoorPose.StepCount;
            bool walked = steps > lastObservedStepCount;
            if (!float.IsNaN(lastIndoorRemaining) && walked)
            {
                float delta = remaining - lastIndoorRemaining;
                if (!wrongWayActive && delta >= 0.9f)
                {
                    wrongWayActive = true;
                    wrongWayBaseline = remaining;
                    WrongWayCount++;
                    WriteEventRow("wrong_way_detected", string.Empty, string.Empty, "distance_increase=" + Format(delta));
                }
                else if (wrongWayActive && remaining <= wrongWayBaseline - 0.6f)
                {
                    wrongWayActive = false;
                    RecoveryCount++;
                    WriteEventRow("route_recovered", string.Empty, string.Empty, "remaining=" + Format(remaining));
                }
            }

            lastIndoorRemaining = remaining;
            lastObservedStepCount = steps;
        }

        private void WriteEventRow(string eventName, string fromState, string toState, string note)
        {
            if (eventsWriter == null)
                return;
            GetPositions(out Vector3 campus, out Vector3 map);
            string[] columns =
            {
                UtcNow(), SessionId,
                harmonyProfile.VersionCode, harmonyProfile.DisplayName,
                Flag(harmonyProfile.QualityThreshold), Flag(harmonyProfile.TemporalDwell),
                Flag(harmonyProfile.MapIdCheck), Flag(harmonyProfile.RecoveryFsm),
                Flag(harmonyProfile.AdaptiveGuidance),
                Elapsed(), eventName, fromState, toState,
                GetSource(), GetDestination(), Latitude(), Longitude(), GpsAccuracy(),
                Format(campus.x), Format(campus.y), Format(campus.z),
                Format(map.x), Format(map.y), Format(map.z),
                vpsTransition != null ? vpsTransition.LocalizationAttemptCount.ToString(CultureInfo.InvariantCulture) : "0",
                note,
            };
            WriteCsv(eventsWriter, columns);
            EventCount++;
        }

        private void WriteSampleRow()
        {
            if (samplesWriter == null)
                return;
            GetPositions(out Vector3 campus, out Vector3 map);
            string[] columns =
            {
                UtcNow(), SessionId,
                harmonyProfile.VersionCode, harmonyProfile.DisplayName,
                Flag(harmonyProfile.QualityThreshold), Flag(harmonyProfile.TemporalDwell),
                Flag(harmonyProfile.MapIdCheck), Flag(harmonyProfile.RecoveryFsm),
                Flag(harmonyProfile.AdaptiveGuidance),
                Elapsed(), GetState(), GetSource(), GetDestination(),
                Latitude(), Longitude(), GpsAccuracy(),
                locationProvider != null && locationProvider.HasReliableFix ? "1" : "0",
                transitionPdr != null ? transitionPdr.StepCount.ToString(CultureInfo.InvariantCulture) : "0",
                transitionPdr != null ? Format(transitionPdr.Confidence) : string.Empty,
                Format(campus.x), Format(campus.y), Format(campus.z),
                Format(map.x), Format(map.y), Format(map.z),
                outdoorRoute != null ? Format(outdoorRoute.RemainingDistanceMeters) : string.Empty,
                vpsTransition != null ? vpsTransition.LocalizationAttemptCount.ToString(CultureInfo.InvariantCulture) : "0",
                vpsTransition != null ? vpsTransition.State.ToString() : string.Empty,
                vpsTransition != null ? Format(vpsTransition.CurrentScanElapsedSeconds) : string.Empty,
                indoorRoute != null ? indoorRoute.State.ToString() : string.Empty,
                indoorPose != null ? indoorPose.SourceLabel : string.Empty,
                indoorPose != null ? Format(indoorPose.Confidence) : string.Empty,
                indoorPose != null ? indoorPose.StepCount.ToString(CultureInfo.InvariantCulture) : "0",
                GetHeading(),
                indoorRoute != null ? Format(indoorRoute.RemainingDistanceMeters) : string.Empty,
                indoorRoute != null ? indoorRoute.RouteRevision.ToString(CultureInfo.InvariantCulture) : "0",
                WrongWayCount.ToString(CultureInfo.InvariantCulture),
                RecoveryCount.ToString(CultureInfo.InvariantCulture),
                GetSource(),
                locationProvider != null ? Format(locationProvider.SampleAgeSeconds) : string.Empty,
                reliabilityController != null ? Format(reliabilityController.GpsReliability) : string.Empty,
                reliabilityController != null ? Format(reliabilityController.GpsStableSeconds) : string.Empty,
                Flag(vpsTransition != null && vpsTransition.VpsValid),
                vpsTransition != null ? Format(vpsTransition.LastLocalizationQuality) : string.Empty,
                vpsTransition != null ? Format(vpsTransition.VpsStableSeconds) : string.Empty,
                vpsTransition != null ? Format(vpsTransition.LastVpsConfidence) : string.Empty,
                Flag(vpsTransition != null && vpsTransition.LastVpsConfidenceAvailable),
                vpsTransition != null ? vpsTransition.LastLocalizedMapId : string.Empty,
                Flag(vpsTransition != null && vpsTransition.LastMapIdAvailable),
                Flag(vpsTransition != null && vpsTransition.LastMapMatchesBuilding),
                SourceToggleCount.ToString(CultureInfo.InvariantCulture),
                vpsTransition != null ? Format(vpsTransition.LastPositionDeltaMeters) : string.Empty,
                vpsTransition != null ? Format(vpsTransition.LastHeadingDeltaDegrees) : string.Empty,
                GetCandidateSource(),
                Flag(reliabilityController != null && reliabilityController.GpsThresholdPassed),
                Flag(vpsTransition != null && vpsTransition.VpsThresholdPassed),
                Flag(reliabilityController != null && reliabilityController.GpsDwellGatePassed),
                Flag(vpsTransition != null && vpsTransition.VpsDwellGatePassed),
                GetDecisionReason(),
            };
            WriteCsv(samplesWriter, columns);
        }

        private void WriteSummary(bool completed)
        {
            if (string.IsNullOrWhiteSpace(SummaryFilePath))
                return;
            float duration = Mathf.Max(0f, Time.unscaledTime - sessionStartedAt);
            string header =
                "session_id,harmony_version,harmony_profile,quality_gate,temporal_dwell,map_id_check,"
                + "recovery_fsm,adaptive_guidance,start_utc,end_utc,duration_s,direction,completed,handover_success,"
                + "destination_arrived,destination,sample_count,event_count,vps_attempts,"
                + "pdr_steps,indoor_steps,wrong_way_count,recovery_count,final_state,end_reason,"
                + "reliability_gate_enabled,vps_dwell_enabled,map_id_check_enabled,"
                + "uncertainty_guidance_enabled,continuity_gate_enabled,minimum_mode_duration_enabled,"
                + "handover_attempts_total,handover_attempts_evaluable,successful_handovers,"
                + "false_handovers,incomplete_handovers,hsr_percent,fhr_percent,source_toggle_count,"
                + "vpsEnterReliability,gpsExitReliability,minimumVpsConfidence,vpsDwellSeconds,"
                + "gpsDwellSeconds,gps_weight_accuracy,gps_weight_freshness,gps_weight_motion,"
                + "gps_weight_transition,gps_weight_dwell,vps_weight_confidence,vps_weight_freshness,"
                + "vps_weight_motion,vps_weight_map_match,vps_weight_dwell\n";
            int attempts = vpsTransition != null ? vpsTransition.LocalizationAttemptCount : 0;
            int successes = HandoverSucceeded ? 1 : 0;
            int incomplete = completed ? Mathf.Max(0, attempts - successes) : 0;
            string hsr = attempts > 0 ? Format(100f * successes / attempts) : string.Empty;
            string[] row =
            {
                SessionId,
                harmonyProfile.VersionCode,
                harmonyProfile.DisplayName,
                Flag(harmonyProfile.QualityThreshold),
                Flag(harmonyProfile.TemporalDwell),
                Flag(harmonyProfile.MapIdCheck),
                Flag(harmonyProfile.RecoveryFsm),
                Flag(harmonyProfile.AdaptiveGuidance),
                trialStartedUtc.ToString("O", CultureInfo.InvariantCulture),
                completed ? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) : string.Empty,
                Format(duration), "GPS_TO_VPS", completed ? "1" : "0",
                HandoverSucceeded ? "1" : "0", DestinationArrived ? "1" : "0",
                GetDestination(), SampleCount.ToString(CultureInfo.InvariantCulture),
                EventCount.ToString(CultureInfo.InvariantCulture),
                vpsTransition != null ? vpsTransition.LocalizationAttemptCount.ToString(CultureInfo.InvariantCulture) : "0",
                transitionPdr != null ? transitionPdr.StepCount.ToString(CultureInfo.InvariantCulture) : "0",
                indoorPose != null ? indoorPose.StepCount.ToString(CultureInfo.InvariantCulture) : "0",
                WrongWayCount.ToString(CultureInfo.InvariantCulture),
                RecoveryCount.ToString(CultureInfo.InvariantCulture), GetState(), finalReason,
                Bool(harmonyProfile.QualityThreshold),
                Bool(harmonyProfile.TemporalDwell),
                Bool(harmonyProfile.MapIdCheck),
                Bool(harmonyProfile.AdaptiveGuidance),
                Bool(harmonyProfile.ContinuityGate),
                Bool(harmonyProfile.MinimumModeDuration),
                attempts.ToString(CultureInfo.InvariantCulture),
                attempts.ToString(CultureInfo.InvariantCulture),
                successes.ToString(CultureInfo.InvariantCulture),
                "0", incomplete.ToString(CultureInfo.InvariantCulture), hsr, "0",
                SourceToggleCount.ToString(CultureInfo.InvariantCulture),
                Format(harmonyProfile.VpsEnterReliability),
                Format(harmonyProfile.GpsExitReliability),
                Format(harmonyProfile.MinimumVpsConfidence),
                Format(harmonyProfile.VpsDwellSeconds),
                Format(harmonyProfile.GpsDwellSeconds),
                Format(harmonyProfile.GpsWeightAccuracy),
                Format(harmonyProfile.GpsWeightFreshness),
                Format(harmonyProfile.GpsWeightMotion),
                Format(harmonyProfile.GpsWeightTransition),
                Format(harmonyProfile.GpsWeightDwell),
                Format(harmonyProfile.VpsWeightConfidence),
                Format(harmonyProfile.VpsWeightFreshness),
                Format(harmonyProfile.VpsWeightMotion),
                Format(harmonyProfile.VpsWeightMapMatch),
                Format(harmonyProfile.VpsWeightDwell),
            };
            for (int i = 0; i < row.Length; i++)
                row[i] = Escape(row[i]);
            File.WriteAllText(
                SummaryFilePath,
                header + string.Join(",", row) + "\n",
                new UTF8Encoding(true));
        }

        private void GetPositions(out Vector3 campus, out Vector3 map)
        {
            if (reliabilityController != null)
            {
                campus = reliabilityController.CurrentCampusPosition;
                map = reliabilityController.CurrentMapWorldPosition;
                return;
            }
            campus = locationProvider != null ? locationProvider.CampusPosition : Vector3.zero;
            map = indoorPose != null ? indoorPose.CurrentPosition : Vector3.zero;
        }

        private string GetState()
        {
            return reliabilityController != null
                ? reliabilityController.State.ToString()
                : vpsTransition != null ? vpsTransition.State.ToString() : "unknown";
        }

        private string GetSource()
        {
            return reliabilityController != null
                ? reliabilityController.ActiveSource.ToString()
                : "unknown";
        }

        private string GetDestination()
        {
            if (indoorRoute != null && indoorRoute.NavigatingToExit)
                return "B9-EXIT";
            if (outdoorRoute != null
                && outdoorRoute.HasDestination
                && !outdoorRoute.IsIndoorB9Destination)
                return outdoorRoute.SelectedDestinationId;
            if (indoorRoute != null && !string.IsNullOrWhiteSpace(indoorRoute.DestinationRoomId))
                return indoorRoute.DestinationRoomId;
            return outdoorRoute != null ? outdoorRoute.SelectedRoomId : string.Empty;
        }

        private string GetHeading()
        {
            if (indoorPose != null && indoorPose.IsTracking)
                return Format(indoorPose.HeadingDegrees);
            if (transitionPdr != null && transitionPdr.IsTracking)
                return Format(transitionPdr.HeadingDegrees);
            return locationProvider != null ? Format(locationProvider.HeadingDegrees) : string.Empty;
        }

        private string GetCandidateSource()
        {
            if (vpsTransition != null
                && (vpsTransition.State == B9VpsTransitionController.TransitionState.StartingVps
                    || vpsTransition.State == B9VpsTransitionController.TransitionState.Scanning))
                return vpsTransition.CandidateSource;
            return reliabilityController != null
                ? reliabilityController.CandidateSource
                : GetSource();
        }

        private string GetDecisionReason()
        {
            if (vpsTransition != null
                && (vpsTransition.State == B9VpsTransitionController.TransitionState.StartingVps
                    || vpsTransition.State == B9VpsTransitionController.TransitionState.Scanning
                    || vpsTransition.State == B9VpsTransitionController.TransitionState.Failed))
                return vpsTransition.LastGateReason;
            return reliabilityController != null
                ? reliabilityController.DecisionReason
                : string.Empty;
        }

        private string Latitude() => locationProvider != null
            ? locationProvider.Latitude.ToString("F7", CultureInfo.InvariantCulture)
            : string.Empty;
        private string Longitude() => locationProvider != null
            ? locationProvider.Longitude.ToString("F7", CultureInfo.InvariantCulture)
            : string.Empty;
        private string GpsAccuracy() => locationProvider != null
            ? Format(locationProvider.HorizontalAccuracyMeters)
            : string.Empty;
        private string Elapsed() => Format(Time.unscaledTime - sessionStartedAt);
        private static string UtcNow() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        private static StreamWriter NewWriter(string path)
        {
            return new StreamWriter(path, false, new UTF8Encoding(true));
        }

        private static void WriteCsv(TextWriter writer, string[] columns)
        {
            for (int i = 0; i < columns.Length; i++)
                columns[i] = Escape(columns[i]);
            writer.WriteLine(string.Join(",", columns));
        }

        private void FlushWriters()
        {
            eventsWriter?.Flush();
            samplesWriter?.Flush();
        }

        private void HandleLowMemory()
        {
            WriteEventRow("device_low_memory", string.Empty, string.Empty, "Unity low-memory callback");
            FlushWriters();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!IsRecording)
                return;
            WriteEventRow(paused ? "app_paused" : "app_resumed", string.Empty, string.Empty, string.Empty);
            FlushWriters();
        }

        private void OnApplicationQuit()
        {
            EndCurrentTrial("app_quit");
        }

        private static string Format(float value)
        {
            return float.IsFinite(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string Flag(bool value) => value ? "1" : "0";
        private static string Bool(bool value) => value ? "True" : "False";

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (!value.Contains(",") && !value.Contains("\"")
                && !value.Contains("\n") && !value.Contains("\r"))
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
