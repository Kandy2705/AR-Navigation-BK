using System;
using System.Globalization;
using System.IO;
using System.Text;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using UnityEngine;

namespace ARNavB9V2.Experiment
{
    /// <summary>
    /// Records navigation telemetry as a research-ready CSV in persistentDataPath.
    /// The logger owns no navigation state and is safe to remove from production builds.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class B9ExperimentLogger : MonoBehaviour
    {
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9OutdoorRouteController outdoorRoute;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9IndoorRouteController indoorRoute;
        [SerializeField] private B9IndoorPoseTracker indoorPose;
        [SerializeField] private bool autoStartSession = true;
        [SerializeField, Range(0.25f, 5f)] private float sampleIntervalSeconds = 1f;
        [SerializeField, Range(1f, 30f)] private float flushIntervalSeconds = 5f;

        private StreamWriter writer;
        private float sessionStartedAt;
        private float nextSampleAt;
        private float nextFlushAt;
        private float lastIndoorRemaining = float.NaN;
        private int lastObservedStepCount;
        private bool wrongWayActive;
        private float wrongWayBaseline;
        private string lastDestination = string.Empty;

        public bool IsRecording => writer != null;
        public string SessionId { get; private set; } = string.Empty;
        public string CurrentFilePath { get; private set; } = string.Empty;
        public string LastSavedFilePath { get; private set; } = string.Empty;
        public float ElapsedSeconds => IsRecording ? Time.unscaledTime - sessionStartedAt : 0f;
        public int SampleCount { get; private set; }
        public int WrongWayCount { get; private set; }
        public int RecoveryCount { get; private set; }

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
            if (Time.unscaledTime >= nextSampleAt)
            {
                nextSampleAt = Time.unscaledTime + sampleIntervalSeconds;
                DetectWrongWayAndRecovery();
                WriteRow("sample", string.Empty);
                SampleCount++;
            }

            if (Time.unscaledTime >= nextFlushAt)
            {
                writer.Flush();
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

            string directory = Path.Combine(Application.persistentDataPath, "ExperimentLogs");
            Directory.CreateDirectory(directory);
            DateTime now = DateTime.UtcNow;
            SessionId = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                        + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            CurrentFilePath = Path.Combine(directory, "B9_" + SessionId + ".csv");
            writer = new StreamWriter(CurrentFilePath, false, new UTF8Encoding(true));
            writer.WriteLine(
                "utc_iso,session_id,elapsed_s,event,phase,destination,latitude,longitude,"
                + "gps_accuracy_m,outdoor_remaining_m,vps_attempt,vps_state,vps_scan_s,"
                + "indoor_state,pose_source,pose_confidence,step_count,heading_deg,"
                + "position_x,position_y,position_z,indoor_remaining_m,route_revision,"
                + "wrong_way_count,recovery_count,note");

            sessionStartedAt = Time.unscaledTime;
            nextSampleAt = Time.unscaledTime;
            nextFlushAt = Time.unscaledTime + flushIntervalSeconds;
            SampleCount = 0;
            WrongWayCount = 0;
            RecoveryCount = 0;
            wrongWayActive = false;
            lastIndoorRemaining = float.NaN;
            lastObservedStepCount = indoorPose != null ? indoorPose.StepCount : 0;
            lastDestination = GetDestination();
            WriteRow("trial_started", "auto=" + autoStartSession);
            writer.Flush();
        }

        public void EndCurrentTrial(string reason = "trial_finished")
        {
            if (!IsRecording)
                return;

            WriteRow("trial_finished", reason);
            writer.Flush();
            writer.Dispose();
            writer = null;
            LastSavedFilePath = CurrentFilePath;
            CurrentFilePath = string.Empty;
        }

        public void AddResearchMarker(string note = "manual_marker")
        {
            if (IsRecording)
                WriteRow("research_marker", note);
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
                indoorPose.StepDetected += HandleStepDetected;
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
                indoorPose.StepDetected -= HandleStepDetected;
        }

        private void HandleOutdoorStateChanged(B9OutdoorRouteController.RouteState state)
        {
            WriteEvent("outdoor_state", state.ToString());
        }

        private void HandleVpsStateChanged(B9VpsTransitionController.TransitionState state)
        {
            WriteEvent("vps_state", state.ToString());
        }

        private void HandleIndoorStateChanged(B9IndoorRouteController.IndoorRouteState state)
        {
            WriteEvent("indoor_state", state.ToString());
            if (state == B9IndoorRouteController.IndoorRouteState.Arrived)
            {
                WriteEvent("destination_arrived", indoorRoute.DestinationRoomId);
                writer?.Flush();
            }
        }

        private void HandleStepDetected(int count, float timestamp, Vector3 position)
        {
            if (IsRecording && (count == 1 || count % 5 == 0))
                WriteRow("step_checkpoint", "step=" + count);
        }

        private void HandleLowMemory()
        {
            WriteEvent("device_low_memory", "Unity low-memory callback");
            writer?.Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!IsRecording)
                return;
            WriteRow(paused ? "app_paused" : "app_resumed", string.Empty);
            writer.Flush();
        }

        private void OnApplicationQuit()
        {
            EndCurrentTrial("app_quit");
        }

        private void DetectDestinationChange()
        {
            string current = GetDestination();
            if (string.Equals(current, lastDestination, StringComparison.OrdinalIgnoreCase))
                return;
            lastDestination = current;
            WriteRow("destination_changed", current);
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
                    WriteRow("wrong_way_detected", "distance_increase=" + Format(delta));
                }
                else if (wrongWayActive && remaining <= wrongWayBaseline - 0.6f)
                {
                    wrongWayActive = false;
                    RecoveryCount++;
                    WriteRow("route_recovered", "remaining=" + Format(remaining));
                }
            }

            lastIndoorRemaining = remaining;
            lastObservedStepCount = steps;
        }

        private void WriteEvent(string eventName, string note)
        {
            if (IsRecording)
                WriteRow(eventName, note);
        }

        private void WriteRow(string eventName, string note)
        {
            if (writer == null)
                return;

            Vector3 position = indoorPose != null
                ? indoorPose.CurrentPosition
                : Vector3.zero;
            string[] columns =
            {
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                SessionId,
                Format(Time.unscaledTime - sessionStartedAt),
                eventName,
                GetPhase(),
                GetDestination(),
                locationProvider != null ? locationProvider.Latitude.ToString("F7", CultureInfo.InvariantCulture) : string.Empty,
                locationProvider != null ? locationProvider.Longitude.ToString("F7", CultureInfo.InvariantCulture) : string.Empty,
                locationProvider != null ? Format(locationProvider.HorizontalAccuracyMeters) : string.Empty,
                outdoorRoute != null ? Format(outdoorRoute.RemainingDistanceMeters) : string.Empty,
                vpsTransition != null ? vpsTransition.LocalizationAttemptCount.ToString(CultureInfo.InvariantCulture) : "0",
                vpsTransition != null ? vpsTransition.State.ToString() : string.Empty,
                vpsTransition != null ? Format(vpsTransition.CurrentScanElapsedSeconds) : string.Empty,
                indoorRoute != null ? indoorRoute.State.ToString() : string.Empty,
                indoorPose != null ? indoorPose.SourceLabel : string.Empty,
                indoorPose != null ? Format(indoorPose.Confidence) : string.Empty,
                indoorPose != null ? indoorPose.StepCount.ToString(CultureInfo.InvariantCulture) : "0",
                indoorPose != null ? Format(indoorPose.HeadingDegrees) : string.Empty,
                Format(position.x),
                Format(position.y),
                Format(position.z),
                indoorRoute != null ? Format(indoorRoute.RemainingDistanceMeters) : string.Empty,
                indoorRoute != null ? indoorRoute.RouteRevision.ToString(CultureInfo.InvariantCulture) : "0",
                WrongWayCount.ToString(CultureInfo.InvariantCulture),
                RecoveryCount.ToString(CultureInfo.InvariantCulture),
                note,
            };

            for (int i = 0; i < columns.Length; i++)
                columns[i] = Escape(columns[i]);
            writer.WriteLine(string.Join(",", columns));
        }

        private string GetPhase()
        {
            if (vpsTransition == null)
                return "unknown";
            return vpsTransition.State switch
            {
                B9VpsTransitionController.TransitionState.WaitingForEntrance => "outdoor",
                B9VpsTransitionController.TransitionState.StartingVps => "vps_starting",
                B9VpsTransitionController.TransitionState.Scanning => "vps_scanning",
                B9VpsTransitionController.TransitionState.IndoorLocalized => "indoor",
                B9VpsTransitionController.TransitionState.Failed => "vps_failed",
                _ => "unknown",
            };
        }

        private string GetDestination()
        {
            if (indoorRoute != null && !string.IsNullOrWhiteSpace(indoorRoute.DestinationRoomId))
                return indoorRoute.DestinationRoomId;
            return outdoorRoute != null ? outdoorRoute.SelectedRoomId : string.Empty;
        }

        private static string Format(float value)
        {
            return float.IsFinite(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : string.Empty;
        }

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
