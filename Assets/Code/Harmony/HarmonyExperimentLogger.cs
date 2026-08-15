using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ARNav.Hybrid;
using UnityEngine;

namespace ARNav.Harmony
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class HarmonyExperimentLogger : MonoBehaviour
    {
        [SerializeField] private HarmonyManager manager;
        [SerializeField] private HybridRouteCoordinator routeCoordinator;
        [SerializeField] private bool startLoggingAutomatically;

        public bool IsLogging { get; private set; }
        
        public string ParticipantId = string.Empty;
        public HarmonyTestDirection TestDirection = HarmonyTestDirection.GPS_TO_VPS;
        public HarmonyTestCondition TestCondition = HarmonyTestCondition.NORMAL;

        public string SamplesFilePath { get; private set; } = string.Empty;
        public string EventsFilePath { get; private set; } = string.Empty;
        public string SummaryFilePath { get; private set; } = string.Empty;
        
        public int SourceToggleCount { get; private set; }
        public int FalseSwitchCount { get; private set; }
        public int WrongWayCount { get; private set; }
        
        public int HandoverAttempts { get; private set; }
        public int SuccessfulHandovers { get; private set; }
        public int IncompleteHandovers { get; private set; }
        
        public enum TrialStatus { COMPLETED, ABORTED }
        public TrialStatus Status { get; private set; } = TrialStatus.COMPLETED;
        public int RecoveryCount { get; private set; }

        public float RouteElapsedSeconds => _routeStartedAt < 0f ? 0f : Mathf.Max(0f, Time.unscaledTime - _routeStartedAt);
        public float RouteDistanceMeters { get; private set; }

        private StreamWriter _samplesWriter;
        private StreamWriter _eventsWriter;
        
        private float _nextSampleAt;
        private HarmonyLocalizationSource _lastSource = HarmonyLocalizationSource.None;
        
        private float _routeStartedAt = -1f;
        private bool _routeWasActive;
        private Vector3 _lastRoutePosition;
        private bool _hasLastRoutePosition;
        
        private float _wrongWaySince = -1f;
        private bool _wrongWayLatched;
        
        private string _sessionId;
        private string _trialId;
        private float _trialStartedAtUTC;
        private float _trialStartedAtUnscaled;
        
        private float _handoverStartedAt = -1f;
        
        // Snapshots
        private HarmonyExperimentVersion _snapVersion;
        private string _snapParticipantId;
        private HarmonyTestDirection _snapDirection;
        private HarmonyTestCondition _snapCondition;
        private HarmonyConfig _snapConfig;

        // Metrics lists
        private List<float> _recoveryTimes = new List<float>();
        private List<float> _handoverLatencies = new List<float>();
        private List<float> _positionJumps = new List<float>();
        private List<float> _headingJumps = new List<float>();

        // Episode tracking
        private class HandoverEpisode
        {
            public HarmonyLocalizationSource TargetSource;
            public float CompletionTime;
            public bool IsValidated;
            public bool IsFalse;
        }
        private HandoverEpisode _currentHandoverEpisode;

        private class RecoveryEpisode
        {
            public float StartTime;
            public bool IsActive;
        }
        private RecoveryEpisode _currentRecoveryEpisode = new RecoveryEpisode();

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private void OnEnable()
        {
            Resolve();
            Subscribe();
            if (startLoggingAutomatically) StartLogging();
        }

        private void OnDisable()
        {
            Unsubscribe();
            EndLogging();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _samplesWriter?.Flush();
                _eventsWriter?.Flush();
            }
        }

        private void Update()
        {
            Resolve();
            TrackWrongWay();
            TrackRoute();
            TrackSourceAndValidation();
            TrackRecovery();
            
            if (!IsLogging || Time.unscaledTime < _nextSampleAt || manager == null) return;
            
            _nextSampleAt = Time.unscaledTime + Mathf.Max(0.05f, manager.Config.csvSampleIntervalSeconds);
            WriteSample();
        }

        public void StartLogging()
        {
            if (IsLogging) return;
            Resolve();
            
            if (manager == null || manager.Config == null)
            {
                Debug.LogError("HarmonyExperimentLogger: Missing HarmonyManager or Config.");
                return;
            }

            // 2. Snapshot metadata/config
            _snapVersion = manager.ExperimentVersion;
            _snapParticipantId = ParticipantId;
            _snapDirection = TestDirection;
            _snapCondition = TestCondition;
            _snapConfig = manager.Config;
            
            _trialStartedAtUTC = (float)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            _trialStartedAtUnscaled = Time.unscaledTime;

            // 3. Reset ONLY per-trial counters/transient metric state
            ResetMetrics();
            
            // 4. Generate collision-safe TrialId
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", Invariant);
            string guidSuffix = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
            string safeParticipant = string.IsNullOrEmpty(ParticipantId) ? "DEV" : string.Concat(ParticipantId.Split(Path.GetInvalidFileNameChars()));
            _trialId = $"{_sessionId}_{manager.ExperimentVersion}_{TestDirection}_{safeParticipant}_{guidSuffix}";

            // 5. Construct log directory
            string directory = Path.Combine(Application.persistentDataPath, "HarmonyLogs");
            
            // 6. Create directory
            Directory.CreateDirectory(directory);
            
            // 7. Construct ALL three complete file paths
            SamplesFilePath = Path.Combine(directory, $"samples_{_trialId}.csv");
            EventsFilePath = Path.Combine(directory, $"events_{_trialId}.csv");
            SummaryFilePath = Path.Combine(directory, $"summary_{_trialId}.csv");
            
            // 8. Verify none of those paths are null/empty
            if (string.IsNullOrWhiteSpace(SamplesFilePath) || string.IsNullOrWhiteSpace(EventsFilePath) || string.IsNullOrWhiteSpace(SummaryFilePath))
            {
                Debug.LogError("HarmonyExperimentLogger: One or more generated file paths are empty. Cannot start logging.");
                return;
            }
            
            // 9. Perform collision checks
            if (File.Exists(SummaryFilePath) || File.Exists(SamplesFilePath) || File.Exists(EventsFilePath))
            {
                Debug.LogError($"HarmonyExperimentLogger: Trial ID collision for {_trialId}");
                return;
            }

            // 10. Only AFTER all paths are valid, create StreamWriter objects
            try
            {
                _samplesWriter = new StreamWriter(SamplesFilePath, false, new UTF8Encoding(true));
                _eventsWriter = new StreamWriter(EventsFilePath, false, new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"HarmonyExperimentLogger: Failed to create StreamWriters. {ex.Message}");
                _samplesWriter?.Dispose();
                _samplesWriter = null;
                _eventsWriter?.Dispose();
                _eventsWriter = null;
                return;
            }

            // 11. Write CSV headers
            _samplesWriter.WriteLine(
                "utc_iso,trial_id,participant_id,version,direction,test_condition,elapsed_s," +
                "state,paper_state,active_source,last_trusted_source," +
                "gps_valid,gps_lat,gps_lon,gps_accuracy_m,gps_age_s,gps_reliability,gps_stable_s," +
                "vps_valid,vps_x,vps_y,vps_z,vps_yaw_deg,vps_age_s,vps_confidence,vps_confidence_available," +
                "vps_map_id,vps_map_id_available,vps_map_matches,vps_reliability,vps_stable_s," +
                "active_reliability,reliability_band,state_age_s,mode_age_s," +
                "position_jump_m,heading_jump_deg,source_toggle_count,false_handover_count," +
                "route_elapsed_s,route_distance_m,wrong_way_count,status_reason,vps_gate_reason");

            _eventsWriter.WriteLine(
                "utc_iso,trial_id,participant_id,version,direction,test_condition,elapsed_s," +
                "event_type,from_state,to_state,from_source,to_source,state,source," +
                "detail,outcome,gps_reliability,vps_reliability,handover_latency_s," +
                "position_jump_m,heading_jump_deg,recovery_time_s,status_reason");

            IsLogging = true;
            _nextSampleAt = 0f;
            _lastSource = manager.ActiveSource;
            
            WriteEvent("trial_started", string.Empty, "started");
        }

        public void EndLogging(TrialStatus endStatus = TrialStatus.COMPLETED)
        {
            if (!IsLogging) return;
            
            if (_currentHandoverEpisode != null && !_currentHandoverEpisode.IsValidated)
            {
                _currentHandoverEpisode.IsValidated = true;
                IncompleteHandovers++;
                WriteEvent("handover_incomplete", $"target={_currentHandoverEpisode.TargetSource}", "incomplete");
            }
            
            Status = endStatus;
            WriteEvent(endStatus == TrialStatus.COMPLETED ? "trial_ended" : "trial_aborted", string.Empty, endStatus.ToString().ToLower());
            IsLogging = false;
            
            WriteSummary();
            
            _samplesWriter?.Flush();
            _samplesWriter?.Dispose();
            _samplesWriter = null;
            
            _eventsWriter?.Flush();
            _eventsWriter?.Dispose();
            _eventsWriter = null;
        }

        public void ResetTest()
        {
            if (IsLogging)
            {
                EndLogging(TrialStatus.ABORTED);
            }
            ResetMetrics();
            SamplesFilePath = string.Empty;
            EventsFilePath = string.Empty;
            SummaryFilePath = string.Empty;
            _sessionId = string.Empty;
            _trialId = string.Empty;
        }

        private void ResetMetrics()
        {
            SourceToggleCount = 0;
            FalseSwitchCount = 0;
            WrongWayCount = 0;
            HandoverAttempts = 0;
            SuccessfulHandovers = 0;
            IncompleteHandovers = 0;
            RecoveryCount = 0;
            
            RouteDistanceMeters = 0f;
            _routeStartedAt = -1f;
            _routeWasActive = false;
            _hasLastRoutePosition = false;
            _wrongWaySince = -1f;
            _wrongWayLatched = false;
            
            _handoverStartedAt = -1f;
            _currentHandoverEpisode = null;
            _currentRecoveryEpisode.IsActive = false;
            
            _recoveryTimes.Clear();
            _handoverLatencies.Clear();
            _positionJumps.Clear();
            _headingJumps.Clear();
        }

        private void Resolve()
        {
            manager ??= FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include);
            routeCoordinator ??= FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
        }

        private void Subscribe()
        {
            if (manager != null)
            {
                manager.StateChanged -= HandleStateChanged;
                manager.StateChanged += HandleStateChanged;
                manager.HandoverCompleted -= HandleHandover;
                manager.HandoverCompleted += HandleHandover;
            }
            ArrivalWatcher.Arrived -= HandleArrival;
            ArrivalWatcher.Arrived += HandleArrival;
        }

        private void Unsubscribe()
        {
            if (manager != null)
            {
                manager.StateChanged -= HandleStateChanged;
                manager.HandoverCompleted -= HandleHandover;
            }
            ArrivalWatcher.Arrived -= HandleArrival;
        }

        private string GetPaperState(HarmonyState state)
        {
            return state switch
            {
                HarmonyState.Outdoor => "OUT",
                HarmonyState.EnteringTransition => "APP",
                HarmonyState.VpsScanning => "SCAN",
                HarmonyState.Indoor => "IN",
                HarmonyState.Relocalization => "RELOC",
                HarmonyState.ExitingTransition => "EXIT",
                HarmonyState.Uncertain => "UNC",
                _ => "UNK"
            };
        }

        private void HandleStateChanged(HarmonyStateTransition transition)
        {
            if (transition.Next == HarmonyState.VpsScanning || transition.Next == HarmonyState.ExitingTransition)
            {
                _handoverStartedAt = Time.unscaledTime;
                WriteEvent("handover_attempt_started", $"{transition.Previous}->{transition.Next}", "started", fromState: transition.Previous, toState: transition.Next);
            }
            if (transition.Previous == HarmonyState.VpsScanning || transition.Previous == HarmonyState.ExitingTransition)
            {
                if (transition.Next == HarmonyState.Uncertain || transition.Next == HarmonyState.Outdoor || transition.Next == HarmonyState.Indoor)
                {
                    bool success = (transition.Previous == HarmonyState.VpsScanning && transition.Next == HarmonyState.Indoor) || 
                                   (transition.Previous == HarmonyState.ExitingTransition && transition.Next == HarmonyState.Outdoor);
                    if (!success)
                    {
                        WriteEvent("handover_failed", transition.Previous == HarmonyState.VpsScanning ? "GPS_TO_VPS" : "VPS_TO_GPS", "failure", fromState: transition.Previous, toState: transition.Next);
                        _handoverStartedAt = -1f;
                    }
                }
            }
            if (transition.Previous == HarmonyState.Relocalization && transition.Next == HarmonyState.Outdoor)
            {
                WriteEvent("relocalization_cancelled", transition.Reason, "cancelled", fromState: transition.Previous, toState: transition.Next);
                if (_handoverStartedAt > 0f)
                {
                     WriteEvent("handover_cancelled", "GPS_TO_VPS", "cancelled", fromState: transition.Previous, toState: transition.Next);
                     _handoverStartedAt = -1f;
                }
            }
            if (transition.Next == HarmonyState.Relocalization)
            {
                WriteEvent("relocalization_started", transition.Reason, "started", fromState: transition.Previous, toState: transition.Next);
            }
            if (transition.Previous == HarmonyState.Relocalization)
            {
                if (transition.Next == HarmonyState.Indoor) WriteEvent("relocalization_recovered", transition.Reason, "success", fromState: transition.Previous, toState: transition.Next);
                else WriteEvent("relocalization_failed", transition.Reason, "failure", fromState: transition.Previous, toState: transition.Next);
            }
            
            WriteEvent("state_transition", transition.Reason, $"{transition.Previous}->{transition.Next}", fromState: transition.Previous, toState: transition.Next);
        }

        private void HandleHandover(string direction, bool success, float positionJump, float headingJump)
        {
            float latency = _handoverStartedAt > 0f ? Time.unscaledTime - _handoverStartedAt : 0f;
            _handoverStartedAt = -1f;

            if (success)
            {
                _positionJumps.Add(positionJump);
                _headingJumps.Add(headingJump);
                _handoverLatencies.Add(latency);
                
                bool isDirectionalMatch = (direction == "GPS_TO_VPS" && _snapDirection == HarmonyTestDirection.GPS_TO_VPS) ||
                                          (direction == "VPS_TO_GPS" && _snapDirection == HarmonyTestDirection.VPS_TO_GPS);
                if (isDirectionalMatch)
                {
                    HandoverAttempts++;
                }

                _currentHandoverEpisode = new HandoverEpisode
                {
                    TargetSource = direction == "GPS_TO_VPS" ? HarmonyLocalizationSource.VPS : HarmonyLocalizationSource.GPS,
                    CompletionTime = Time.unscaledTime,
                    IsValidated = false,
                    IsFalse = false
                };
            }

            WriteEvent("handover_completed", direction, success ? "success" : "failure", latency: latency, posJump: positionJump, headJump: headingJump);
        }

        private void HandleArrival(string destination)
        {
            WriteEvent("navigation_arrived", destination, "success");
            _routeWasActive = false;
        }

        private void TrackSourceAndValidation()
        {
            if (manager == null) return;
            HarmonyLocalizationSource current = manager.ActiveSource;
            
            // False Handover validation
            if (_currentHandoverEpisode != null && !_currentHandoverEpisode.IsValidated)
            {
                float elapsed = Time.unscaledTime - _currentHandoverEpisode.CompletionTime;
                
                bool lostConditions = false;
                if (_currentHandoverEpisode.TargetSource == HarmonyLocalizationSource.VPS)
                {
                    if (!manager.VpsSample.IsValid) lostConditions = true;
                    else if (_snapConfig.RequireVpsDwell && manager.Reliability.VpsStableSeconds < _snapConfig.vpsDwellSeconds) lostConditions = true;
                    else if (_snapConfig.RequireMapIdMatch && !manager.VpsSample.MapMatchesBuilding) lostConditions = true;
                    else if (manager.Reliability.Vps < _snapConfig.vpsEnterReliability) lostConditions = true;
                }
                else if (_currentHandoverEpisode.TargetSource == HarmonyLocalizationSource.GPS)
                {
                    if (!manager.GpsSample.IsValid || manager.Reliability.Gps < _snapConfig.gpsExitReliability) lostConditions = true;
                }

                bool sourceReverted = (current != _currentHandoverEpisode.TargetSource && current != HarmonyLocalizationSource.LastTrusted && current != HarmonyLocalizationSource.None);

                if ((lostConditions || sourceReverted) && !_currentHandoverEpisode.IsFalse)
                {
                    _currentHandoverEpisode.IsFalse = true;
                    _currentHandoverEpisode.IsValidated = true; // Ends episode
                    FalseSwitchCount++;
                    WriteEvent("false_handover", $"target={_currentHandoverEpisode.TargetSource}", "detected");
                }
                else if (elapsed >= _snapConfig.falseSwitchWindowSeconds)
                {
                    _currentHandoverEpisode.IsValidated = true; // Ends episode successfully
                    SuccessfulHandovers++;
                    WriteEvent("handover_validated", $"target={_currentHandoverEpisode.TargetSource}", "validated");
                }
            }

            if (_lastSource == HarmonyLocalizationSource.None)
            {
                _lastSource = current;
                return;
            }
            if (current == _lastSource || current == HarmonyLocalizationSource.LastTrusted || current == HarmonyLocalizationSource.None)
            {
                return;
            }

            SourceToggleCount++;
            WriteEvent("source_changed", $"{_lastSource}->{current}", "changed", fromSource: _lastSource, toSource: current);
            _lastSource = current;
        }

        private void TrackRecovery()
        {
            if (manager == null) return;
            
            bool needsRecovery = manager.State == HarmonyState.Uncertain || manager.State == HarmonyState.Relocalization;
            
            if (needsRecovery && !_currentRecoveryEpisode.IsActive)
            {
                _currentRecoveryEpisode.IsActive = true;
                _currentRecoveryEpisode.StartTime = Time.unscaledTime;
            }
            else if (!needsRecovery && _currentRecoveryEpisode.IsActive)
            {
                _currentRecoveryEpisode.IsActive = false;
                float duration = Time.unscaledTime - _currentRecoveryEpisode.StartTime;
                RecoveryCount++;
                _recoveryTimes.Add(duration);
            }
        }

        private void TrackRoute()
        {
            bool active = routeCoordinator != null && routeCoordinator.Destination != null && routeCoordinator.Destination.IsValid;
            if (active && !_routeWasActive)
            {
                _routeStartedAt = Time.unscaledTime;
                RouteDistanceMeters = 0f;
                _hasLastRoutePosition = false;
                WriteEvent("route_started", routeCoordinator.Destination.displayName, "started");
            }
            else if (!active && _routeWasActive)
            {
                WriteEvent("route_ended", string.Empty, "cleared");
            }
            _routeWasActive = active;

            if (!active || manager == null || !manager.CurrentPoseIsFresh) return;
            Vector3 current = manager.CurrentCampusPosition;
            if (_hasLastRoutePosition)
            {
                float delta = HorizontalDistance(current, _lastRoutePosition);
                if (delta <= 10f) RouteDistanceMeters += delta;
            }
            _lastRoutePosition = current;
            _hasLastRoutePosition = true;
        }

        private void TrackWrongWay()
        {
            if (!_routeWasActive || manager == null || routeCoordinator == null || !manager.CurrentPoseIsFresh || !_hasLastRoutePosition)
            {
                _wrongWaySince = -1f;
                _wrongWayLatched = false;
                return;
            }

            Vector3 movement = manager.CurrentCampusPosition - _lastRoutePosition;
            movement.y = 0f;
            Vector3 expected = routeCoordinator.NextWaypoint - manager.CurrentCampusPosition;
            expected.y = 0f;
            if (movement.magnitude < manager.Config.wrongWayMinimumMovementMeters || expected.sqrMagnitude < 0.01f) return;

            bool wrong = Vector3.Angle(movement, expected) >= manager.Config.wrongWayAngleDegrees;
            if (!wrong)
            {
                _wrongWaySince = -1f;
                _wrongWayLatched = false;
                return;
            }
            if (_wrongWaySince < 0f) _wrongWaySince = Time.unscaledTime;
            if (!_wrongWayLatched && Time.unscaledTime - _wrongWaySince >= manager.Config.wrongWayDwellSeconds)
            {
                _wrongWayLatched = true;
                WrongWayCount++;
                WriteEvent("wrong_way", string.Empty, "detected");
            }
        }

        private void WriteSample()
        {
            if (_samplesWriter == null || manager == null) return;
            HarmonyGpsSample gps = manager.GpsSample;
            HarmonyVpsSample vps = manager.VpsSample;
            HarmonyReliabilitySnapshot reliability = manager.Reliability;
            float elapsed = Time.unscaledTime - _trialStartedAtUnscaled;

            string[] values =
            {
                DateTime.UtcNow.ToString("O", Invariant),
                _trialId, _snapParticipantId, _snapVersion.ToString(), _snapDirection.ToString(), _snapCondition.ToString(),
                F(elapsed),
                manager.State.ToString(), GetPaperState(manager.State), manager.ActiveSource.ToString(), "", // last_trusted_source is internal in manager, we leave it blank unless exposed
                gps.IsValid.ToString(), F(gps.Latitude), F(gps.Longitude), F(gps.HorizontalAccuracyMeters), F(gps.AgeSeconds), F(reliability.Gps), F(reliability.GpsStableSeconds),
                vps.IsValid.ToString(), F(vps.CampusPosition.x), F(vps.CampusPosition.y), F(vps.CampusPosition.z), F(vps.CampusRotation.eulerAngles.y), F(vps.AgeSeconds), F(vps.Confidence), vps.ConfidenceAvailable.ToString(),
                vps.MapId ?? "", vps.MapIdAvailable.ToString(), vps.MapMatchesBuilding.ToString(), F(reliability.Vps), F(reliability.VpsStableSeconds),
                F(reliability.Active), reliability.Band.ToString(), F(manager.StateAgeSeconds), F(manager.ModeAgeSeconds),
                F(manager.LastPositionJumpMeters), F(manager.LastHeadingJumpDegrees), SourceToggleCount.ToString(Invariant), FalseSwitchCount.ToString(Invariant),
                F(RouteElapsedSeconds), F(RouteDistanceMeters), WrongWayCount.ToString(Invariant), manager.StatusReason, reliability.VpsReason
            };
            _samplesWriter.WriteLine(string.Join(",", Array.ConvertAll(values, Escape)));
        }

        private void WriteEvent(string eventType, string detail, string outcome, HarmonyState? fromState = null, HarmonyState? toState = null, HarmonyLocalizationSource? fromSource = null, HarmonyLocalizationSource? toSource = null, float latency = 0f, float posJump = 0f, float headJump = 0f, float recovery = 0f)
        {
            if (_eventsWriter == null || manager == null) return;
            float elapsed = Time.unscaledTime - _trialStartedAtUnscaled;

            string[] values =
            {
                DateTime.UtcNow.ToString("O", Invariant),
                _trialId, _snapParticipantId, _snapVersion.ToString(), _snapDirection.ToString(), _snapCondition.ToString(),
                F(elapsed),
                eventType,
                fromState?.ToString() ?? "", toState?.ToString() ?? "",
                fromSource?.ToString() ?? "", toSource?.ToString() ?? "",
                manager.State.ToString(), manager.ActiveSource.ToString(),
                detail, outcome,
                F(manager.Reliability.Gps), F(manager.Reliability.Vps),
                latency > 0f ? F(latency) : "",
                posJump > 0f ? F(posJump) : "",
                headJump > 0f ? F(headJump) : "",
                recovery > 0f ? F(recovery) : "",
                manager.StatusReason
            };
            _eventsWriter.WriteLine(string.Join(",", Array.ConvertAll(values, Escape)));
        }

        private void WriteSummary()
        {
            if (string.IsNullOrEmpty(SummaryFilePath) || _snapConfig == null) return;
            
            float duration = Time.unscaledTime - _trialStartedAtUnscaled;
            int evaluableAttempts = HandoverAttempts - IncompleteHandovers;
            float hsr = evaluableAttempts > 0 ? (float)SuccessfulHandovers / evaluableAttempts * 100f : -1f;
            float fhr = evaluableAttempts > 0 ? (float)FalseSwitchCount / evaluableAttempts * 100f : -1f;
            int excessFlapping = Mathf.Max(0, SourceToggleCount - SuccessfulHandovers);

            using (StreamWriter w = new StreamWriter(SummaryFilePath, false, new UTF8Encoding(true)))
            {
                string header = "trial_id,trial_status,participant_id,version,direction,test_condition,start_utc,end_utc,duration_s," +
                                "reliability_gate_enabled,vps_dwell_enabled,map_id_check_enabled,uncertainty_guidance_enabled,continuity_gate_enabled,minimum_mode_duration_enabled," +
                                "handover_attempts_total,handover_attempts_evaluable,successful_handovers,false_handovers,incomplete_handovers,hsr_percent,fhr_percent,source_toggle_count,excess_mode_flapping," +
                                "handover_latency_mean_s,handover_latency_median_s,handover_latency_max_s," +
                                "position_jump_mean_m,position_jump_median_m,position_jump_max_m," +
                                "heading_jump_mean_deg,heading_jump_median_deg,heading_jump_max_deg," +
                                "recovery_count,recovery_time_mean_s,recovery_time_median_s,recovery_time_max_s," +
                                "wrong_way_count,task_completion_time_s,navigation_success," +
                                "approachRadiusMultiplier,enterRadiusMultiplier,exitRadiusMultiplier,vpsEnterReliability,gpsExitReliability," +
                                "minimumVpsConfidence,vpsDwellSeconds,gpsDwellSeconds,minimumModeDurationSeconds,minimumStateDurationSeconds," +
                                "vpsScanTimeoutSeconds,relocalizationTimeoutSeconds,sourceLossGraceSeconds,maxHandoverPositionJumpMeters,maxHandoverHeadingJumpDegrees," +
                                "gpsExcellentAccuracyMeters,gpsRejectedAccuracyMeters,gpsFreshAgeSeconds,gpsStaleAgeSeconds,gpsMaxPlausibleSpeedMetersPerSecond,gpsNearTransitionScore," +
                                "vpsFreshAgeSeconds,vpsStaleAgeSeconds,vpsStablePositionDeltaMeters,vpsRejectedPositionDeltaMeters,vpsStableHeadingDeltaDegrees,vpsRejectedHeadingDeltaDegrees," +
                                "highReliabilityThreshold,mediumReliabilityThreshold,falseSwitchWindowSeconds,csvSampleIntervalSeconds," +
                                "gps_weight_accuracy,gps_weight_freshness,gps_weight_motion,gps_weight_transition,gps_weight_dwell," +
                                "vps_weight_confidence,vps_weight_freshness,vps_weight_motion,vps_weight_map_match,vps_weight_dwell";
                w.WriteLine(header);
                
                string[] values = {
                    _trialId, Status.ToString(), _snapParticipantId, _snapVersion.ToString(), _snapDirection.ToString(), _snapCondition.ToString(),
                    _trialStartedAtUTC.ToString(Invariant), ((float)DateTime.UtcNow.Subtract(new DateTime(1970,1,1)).TotalSeconds).ToString(Invariant), F(duration),
                    _snapConfig.UseReliabilityGate.ToString(), _snapConfig.RequireVpsDwell.ToString(), _snapConfig.RequireMapIdMatch.ToString(), _snapConfig.UseUncertaintyGuidance.ToString(), _snapConfig.UseContinuityGate.ToString(), _snapConfig.EnforceMinimumModeDuration.ToString(),
                    HandoverAttempts.ToString(Invariant), evaluableAttempts.ToString(Invariant), SuccessfulHandovers.ToString(Invariant), FalseSwitchCount.ToString(Invariant), IncompleteHandovers.ToString(Invariant),
                    hsr >= 0f ? F(hsr) : "", fhr >= 0f ? F(fhr) : "", SourceToggleCount.ToString(Invariant), excessFlapping.ToString(Invariant),
                    F(Mean(_handoverLatencies)), F(Median(_handoverLatencies)), F(Max(_handoverLatencies)),
                    F(Mean(_positionJumps)), F(Median(_positionJumps)), F(Max(_positionJumps)),
                    F(Mean(_headingJumps)), F(Median(_headingJumps)), F(Max(_headingJumps)),
                    RecoveryCount.ToString(Invariant), F(Mean(_recoveryTimes)), F(Median(_recoveryTimes)), F(Max(_recoveryTimes)),
                    WrongWayCount.ToString(Invariant), F(RouteElapsedSeconds), "", // navigation_success unknown
                    F(_snapConfig.approachRadiusMultiplier), F(_snapConfig.enterRadiusMultiplier), F(_snapConfig.exitRadiusMultiplier),
                    F(_snapConfig.vpsEnterReliability), F(_snapConfig.gpsExitReliability), F(_snapConfig.minimumVpsConfidence),
                    F(_snapConfig.vpsDwellSeconds), F(_snapConfig.gpsDwellSeconds), F(_snapConfig.minimumModeDurationSeconds), F(_snapConfig.minimumStateDurationSeconds),
                    F(_snapConfig.vpsScanTimeoutSeconds), F(_snapConfig.relocalizationTimeoutSeconds), F(_snapConfig.sourceLossGraceSeconds),
                    F(_snapConfig.maxHandoverPositionJumpMeters), F(_snapConfig.maxHandoverHeadingJumpDegrees),
                    F(_snapConfig.gpsExcellentAccuracyMeters), F(_snapConfig.gpsRejectedAccuracyMeters),
                    F(_snapConfig.gpsFreshAgeSeconds), F(_snapConfig.gpsStaleAgeSeconds), F(_snapConfig.gpsMaxPlausibleSpeedMetersPerSecond), F(_snapConfig.gpsNearTransitionScore),
                    F(_snapConfig.vpsFreshAgeSeconds), F(_snapConfig.vpsStaleAgeSeconds), F(_snapConfig.vpsStablePositionDeltaMeters), F(_snapConfig.vpsRejectedPositionDeltaMeters), F(_snapConfig.vpsStableHeadingDeltaDegrees), F(_snapConfig.vpsRejectedHeadingDeltaDegrees),
                    F(_snapConfig.highReliabilityThreshold), F(_snapConfig.mediumReliabilityThreshold), F(_snapConfig.falseSwitchWindowSeconds), F(_snapConfig.csvSampleIntervalSeconds),
                    F(_snapConfig.gpsWeights.accuracyOrConfidence), F(_snapConfig.gpsWeights.freshnessOrValidity), F(_snapConfig.gpsWeights.motionStability), F(_snapConfig.gpsWeights.transitionOrMapMatch), F(_snapConfig.gpsWeights.dwellStability),
                    F(_snapConfig.vpsWeights.accuracyOrConfidence), F(_snapConfig.vpsWeights.freshnessOrValidity), F(_snapConfig.vpsWeights.motionStability), F(_snapConfig.vpsWeights.transitionOrMapMatch), F(_snapConfig.vpsWeights.dwellStability)
                };
                w.WriteLine(string.Join(",", Array.ConvertAll(values, Escape)));
            }
        }

        private static float Mean(List<float> list) => list.Count == 0 ? float.NaN : list.Average();
        private static float Max(List<float> list) => list.Count == 0 ? float.NaN : list.Max();
        private static float Median(List<float> list)
        {
            if (list.Count == 0) return float.NaN;
            var sorted = list.OrderBy(n => n).ToList();
            int mid = sorted.Count / 2;
            return (sorted.Count % 2 != 0) ? sorted[mid] : (sorted[mid] + sorted[mid - 1]) / 2f;
        }

        private static string F(float value) => (float.IsNaN(value) || float.IsInfinity(value)) ? string.Empty : value.ToString("0.###", Invariant);
        private static string F(double value) => (double.IsNaN(value) || double.IsInfinity(value)) ? string.Empty : value.ToString("0.########", Invariant);
        private static string Escape(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n") && !value.Contains("\r")) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        private static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
