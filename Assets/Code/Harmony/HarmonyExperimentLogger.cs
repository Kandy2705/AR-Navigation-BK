using System;
using System.Globalization;
using System.IO;
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
        public string CurrentFilePath { get; private set; } = string.Empty;
        public string LastExportPath { get; private set; } = string.Empty;
        public int SourceToggleCount { get; private set; }
        public int FalseSwitchCount { get; private set; }
        public int WrongWayCount { get; private set; }
        public float RouteElapsedSeconds => _routeStartedAt < 0f
            ? 0f
            : Mathf.Max(0f, Time.unscaledTime - _routeStartedAt);
        public float RouteDistanceMeters { get; private set; }

        private StreamWriter _writer;
        private float _nextSampleAt;
        private HarmonyLocalizationSource _lastSource;
        private HarmonyLocalizationSource _sourceBeforeLastSwitch;
        private float _lastSourceSwitchAt = -999f;
        private float _routeStartedAt = -1f;
        private bool _routeWasActive;
        private Vector3 _lastRoutePosition;
        private bool _hasLastRoutePosition;
        private float _wrongWaySince = -1f;
        private bool _wrongWayLatched;
        private string _sessionId;
        private float _handoverStartedAt = -1f;

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
            if (paused) _writer?.Flush();
        }

        private void Update()
        {
            Resolve();
            TrackWrongWay();
            TrackRoute();
            TrackSource();
            if (!IsLogging || Time.unscaledTime < _nextSampleAt || manager == null)
                return;
            _nextSampleAt = Time.unscaledTime +
                            Mathf.Max(0.05f, manager.Config.csvSampleIntervalSeconds);
            WriteRow("sample", string.Empty, string.Empty);
        }

        public void StartLogging()
        {
            if (IsLogging) return;
            Resolve();
            string directory = Path.Combine(
                Application.persistentDataPath,
                "HarmonyLogs");
            Directory.CreateDirectory(directory);
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", Invariant);
            CurrentFilePath = Path.Combine(
                directory,
                $"Harmony_{_sessionId}.csv");
            _writer = new StreamWriter(CurrentFilePath, false, new UTF8Encoding(true));
            _writer.WriteLine(
                "utc_iso,session_id,event,detail,outcome,version,state,source," +
                "gps_lat,gps_lon,gps_accuracy_m,gps_age_s,gps_reliability," +
                "vps_x,vps_y,vps_z,vps_yaw_deg,vps_confidence,vps_confidence_available," +
                "vps_map_id,vps_map_id_available,vps_map_matches,vps_age_s,vps_reliability," +
                "active_reliability,reliability_band,state_age_s,mode_age_s," +
                "handover_latency_s,position_jump_m,heading_jump_deg,source_toggles,false_switches," +
                "route_elapsed_s,route_distance_m,wrong_way_count,status_reason");
            IsLogging = true;
            _nextSampleAt = 0f;
            WriteRow("logging_started", string.Empty, "started");
        }

        public void EndLogging()
        {
            if (!IsLogging) return;
            WriteRow("logging_ended", string.Empty, "ended");
            IsLogging = false;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }

        public void ResetMetrics()
        {
            SourceToggleCount = 0;
            FalseSwitchCount = 0;
            WrongWayCount = 0;
            RouteDistanceMeters = 0f;
            _routeStartedAt = -1f;
            _routeWasActive = false;
            _hasLastRoutePosition = false;
            _wrongWaySince = -1f;
            _wrongWayLatched = false;
            if (IsLogging) WriteRow("metrics_reset", string.Empty, "reset");
        }

        public void ResetTest()
        {
            EndLogging();
            ResetMetrics();
            CurrentFilePath = string.Empty;
            LastExportPath = string.Empty;
            _sessionId = string.Empty;
        }

        public string ExportCsv()
        {
            _writer?.Flush();
            LastExportPath = CurrentFilePath;
            return LastExportPath;
        }

        private void Resolve()
        {
            manager ??= FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include);
            routeCoordinator ??= FindFirstObjectByType<HybridRouteCoordinator>(
                FindObjectsInactive.Include);
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

        private void HandleStateChanged(HarmonyStateTransition transition)
        {
            if (transition.Next == HarmonyState.VpsScanning ||
                transition.Next == HarmonyState.ExitingTransition)
            {
                _handoverStartedAt = Time.unscaledTime;
                if (IsLogging)
                    WriteRow(
                        "transition_started",
                        $"{transition.Previous}->{transition.Next}",
                        "started");
            }
            if ((transition.Previous == HarmonyState.VpsScanning ||
                 transition.Previous == HarmonyState.ExitingTransition) &&
                transition.Next == HarmonyState.Uncertain)
            {
                if (IsLogging)
                    WriteRow(
                        "handover",
                        transition.Previous == HarmonyState.VpsScanning
                            ? "GPS_TO_VPS"
                            : "VPS_TO_GPS",
                        "failure");
                _handoverStartedAt = -1f;
            }
            if (IsLogging)
                WriteRow(
                    "state_transition",
                    $"{transition.Previous}->{transition.Next}",
                    transition.Reason);
        }

        private void HandleHandover(
            string direction,
            bool success,
            float positionJump,
            float headingJump)
        {
            if (IsLogging)
                WriteRow(
                    "mode_switch",
                    direction,
                    success ? "success" : "failure");
            _handoverStartedAt = -1f;
        }

        private void HandleArrival(string destination)
        {
            if (IsLogging) WriteRow("arrival", destination, "success");
            _routeWasActive = false;
        }

        private void TrackSource()
        {
            if (manager == null) return;
            HarmonyLocalizationSource current = manager.ActiveSource;
            if (_lastSource == HarmonyLocalizationSource.None)
            {
                _lastSource = current;
                return;
            }
            if (current == _lastSource ||
                current == HarmonyLocalizationSource.LastTrusted ||
                current == HarmonyLocalizationSource.None)
            {
                return;
            }

            SourceToggleCount++;
            float now = Time.unscaledTime;
            if (current == _sourceBeforeLastSwitch &&
                now - _lastSourceSwitchAt <= manager.Config.falseSwitchWindowSeconds)
            {
                FalseSwitchCount++;
                if (IsLogging) WriteRow("false_switch", $"{_lastSource}->{current}", "detected");
            }
            _sourceBeforeLastSwitch = _lastSource;
            _lastSource = current;
            _lastSourceSwitchAt = now;
        }

        private void TrackRoute()
        {
            bool active = routeCoordinator != null &&
                          routeCoordinator.Destination != null &&
                          routeCoordinator.Destination.IsValid;
            if (active && !_routeWasActive)
            {
                _routeStartedAt = Time.unscaledTime;
                RouteDistanceMeters = 0f;
                _hasLastRoutePosition = false;
                if (IsLogging) WriteRow("route_started", routeCoordinator.Destination.displayName, "started");
            }
            else if (!active && _routeWasActive && IsLogging)
            {
                WriteRow("route_ended", string.Empty, "cleared");
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
            if (!_routeWasActive || manager == null || routeCoordinator == null ||
                !manager.CurrentPoseIsFresh || !_hasLastRoutePosition)
            {
                _wrongWaySince = -1f;
                _wrongWayLatched = false;
                return;
            }

            Vector3 movement = manager.CurrentCampusPosition - _lastRoutePosition;
            movement.y = 0f;
            Vector3 expected = routeCoordinator.NextWaypoint -
                               manager.CurrentCampusPosition;
            expected.y = 0f;
            if (movement.magnitude < manager.Config.wrongWayMinimumMovementMeters ||
                expected.sqrMagnitude < 0.01f)
            {
                return;
            }

            bool wrong = Vector3.Angle(movement, expected) >=
                         manager.Config.wrongWayAngleDegrees;
            if (!wrong)
            {
                _wrongWaySince = -1f;
                _wrongWayLatched = false;
                return;
            }
            if (_wrongWaySince < 0f) _wrongWaySince = Time.unscaledTime;
            if (!_wrongWayLatched &&
                Time.unscaledTime - _wrongWaySince >= manager.Config.wrongWayDwellSeconds)
            {
                _wrongWayLatched = true;
                WrongWayCount++;
                if (IsLogging) WriteRow("wrong_way", string.Empty, "detected");
            }
        }

        private void WriteRow(string eventName, string detail, string outcome)
        {
            if (_writer == null || manager == null) return;
            HarmonyGpsSample gps = manager.GpsSample;
            HarmonyVpsSample vps = manager.VpsSample;
            HarmonyReliabilitySnapshot reliability = manager.Reliability;

            string[] values =
            {
                DateTime.UtcNow.ToString("O", Invariant),
                _sessionId,
                eventName,
                detail,
                outcome,
                manager.ExperimentVersion.ToString(),
                manager.State.ToString(),
                manager.ActiveSource.ToString(),
                F(gps.Latitude),
                F(gps.Longitude),
                F(gps.HorizontalAccuracyMeters),
                F(gps.AgeSeconds),
                F(reliability.Gps),
                F(vps.CampusPosition.x),
                F(vps.CampusPosition.y),
                F(vps.CampusPosition.z),
                F(vps.CampusRotation.eulerAngles.y),
                F(vps.Confidence),
                vps.ConfidenceAvailable.ToString(),
                vps.MapId,
                vps.MapIdAvailable.ToString(),
                vps.MapMatchesBuilding.ToString(),
                F(vps.AgeSeconds),
                F(reliability.Vps),
                F(reliability.Active),
                reliability.Band.ToString(),
                F(StateAge()),
                F(ModeAge()),
                F(_handoverStartedAt < 0f
                    ? 0f
                    : Mathf.Max(0f, Time.unscaledTime - _handoverStartedAt)),
                F(manager.LastPositionJumpMeters),
                F(manager.LastHeadingJumpDegrees),
                SourceToggleCount.ToString(Invariant),
                FalseSwitchCount.ToString(Invariant),
                F(RouteElapsedSeconds),
                F(RouteDistanceMeters),
                WrongWayCount.ToString(Invariant),
                manager.StatusReason,
            };
            _writer.WriteLine(string.Join(",", Array.ConvertAll(values, Escape)));
            _writer.Flush();
        }

        private float StateAge()
        {
            return manager != null ? manager.StateAgeSeconds : 0f;
        }

        private float ModeAge()
        {
            return manager != null ? manager.ModeAgeSeconds : 0f;
        }

        private static string F(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? string.Empty
                : value.ToString("0.###", Invariant);
        }

        private static string F(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? string.Empty
                : value.ToString("0.########", Invariant);
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(",") && !value.Contains("\"") &&
                !value.Contains("\n") && !value.Contains("\r"))
            {
                return value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
