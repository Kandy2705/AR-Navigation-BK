using UnityEngine;
using System;
using System.IO;

namespace ARNav.Harmony
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class HarmonyDebugOverlay : MonoBehaviour
    {
        [SerializeField] private HarmonyManager manager;
        [SerializeField] private HarmonyExperimentLogger logger;
        [SerializeField] private UncertaintyGuidanceRenderer guidance;
        [SerializeField] private bool panelVisible = true;
        [SerializeField] private Rect panelRect = new Rect(12f, 12f, 480f, 650f);

        private Vector2 _scroll;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boldStyle;

        private void OnGUI()
        {
            if (manager == null) manager = FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include);
            if (logger == null) logger = FindFirstObjectByType<HarmonyExperimentLogger>(FindObjectsInactive.Include);
            if (guidance == null) guidance = FindFirstObjectByType<UncertaintyGuidanceRenderer>(FindObjectsInactive.Include);

            if (!panelVisible)
            {
                if (GUI.Button(new Rect(12f, 12f, 130f, 42f), "HARMONY Debug")) panelVisible = true;
                return;
            }
            panelRect = GUI.Window(GetInstanceID(), panelRect, DrawWindow, "HARMONY EXPERIMENT CONTROL");
        }

        private void DrawWindow(int id)
        {
            EnsureStyles();
            if (manager == null || logger == null)
            {
                GUILayout.Label("HarmonyManager or Logger missing", _labelStyle);
                if (GUILayout.Button("Close")) panelVisible = false;
                GUI.DragWindow();
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            
            // --- HARMONY Runtime ---
            GUILayout.Label("HARMONY Runtime", _boldStyle);
            string devTag = manager.ExperimentVersion == HarmonyExperimentVersion.Current ? " [DEV]" : "";
            string versionName = manager.ExperimentVersion switch {
                HarmonyExperimentVersion.Current => "Development Full",
                HarmonyExperimentVersion.V1_FixedSwitching => "Fixed Switching",
                HarmonyExperimentVersion.V2_ReliableHandover => "Reliable Handover",
                HarmonyExperimentVersion.V3_FullHarmony => "Full HARMONY",
                HarmonyExperimentVersion.V4_NoDwellTime => "No Dwell Time",
                HarmonyExperimentVersion.V5_NoMapIdCheck => "No Map-ID Check",
                _ => ""
            };
            GUILayout.Label($"Version: {manager.ExperimentVersion}{devTag} - {versionName}", _labelStyle);

            HarmonyConfig cfg = manager.Config;
            if (cfg != null)
            {
                GUILayout.Label($"Flags: Rel={cfg.UseReliabilityGate} Dwell={cfg.RequireVpsDwell} Map={cfg.RequireMapIdMatch} Guid={cfg.UseUncertaintyGuidance} Cont={cfg.UseContinuityGate} MinMode={cfg.EnforceMinimumModeDuration}", _labelStyle);
            }

            // --- Experiment Configuration ---
            GUILayout.Space(8f);
            GUILayout.Label("Experiment Configuration", _boldStyle);
            
            GUI.enabled = !logger.IsLogging;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CUR")) manager.SetExperimentVersion(HarmonyExperimentVersion.Current);
            if (GUILayout.Button("V1")) manager.SetExperimentVersion(HarmonyExperimentVersion.V1_FixedSwitching);
            if (GUILayout.Button("V2")) manager.SetExperimentVersion(HarmonyExperimentVersion.V2_ReliableHandover);
            if (GUILayout.Button("V3")) manager.SetExperimentVersion(HarmonyExperimentVersion.V3_FullHarmony);
            if (GUILayout.Button("V4")) manager.SetExperimentVersion(HarmonyExperimentVersion.V4_NoDwellTime);
            if (GUILayout.Button("V5")) manager.SetExperimentVersion(HarmonyExperimentVersion.V5_NoMapIdCheck);
            GUILayout.EndHorizontal();

            // --- Trial Setup ---
            GUILayout.Space(8f);
            GUILayout.Label("Trial Setup", _boldStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Participant ID:");
            logger.ParticipantId = GUILayout.TextField(logger.ParticipantId, GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Direction:");
            if (GUILayout.Button(logger.TestDirection.ToString()))
            {
                logger.TestDirection = logger.TestDirection == HarmonyTestDirection.GPS_TO_VPS ? HarmonyTestDirection.VPS_TO_GPS : HarmonyTestDirection.GPS_TO_VPS;
            }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Condition:");
            if (GUILayout.Button(logger.TestCondition.ToString()))
            {
                int next = ((int)logger.TestCondition + 1) % Enum.GetValues(typeof(HarmonyTestCondition)).Length;
                logger.TestCondition = (HarmonyTestCondition)next;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            // --- Logger ---
            GUILayout.Space(8f);
            GUILayout.Label("Logger", _boldStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = !logger.IsLogging;
            if (GUILayout.Button("Start Test")) logger.StartLogging();
            GUI.enabled = logger.IsLogging;
            if (GUILayout.Button("End Test")) logger.EndLogging();
            GUI.enabled = true;
            if (GUILayout.Button("Reset Test")) logger.ResetTest();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Print Log Paths"))
            {
                Debug.Log($"Samples: {logger.SamplesFilePath}");
                Debug.Log($"Events: {logger.EventsFilePath}");
                Debug.Log($"Summary: {logger.SummaryFilePath}");
            }

            if (logger.IsLogging)
            {
                GUILayout.Label($"Logging: TRUE", _boldStyle);
            }

            GUILayout.Label($"Attempts: {logger.HandoverAttempts} Success: {logger.SuccessfulHandovers} False: {logger.FalseSwitchCount}", _labelStyle);
            GUILayout.Label($"Toggles: {logger.SourceToggleCount} Wrong-way: {logger.WrongWayCount} Recovery: {logger.RecoveryCount}", _labelStyle);

            // --- State / Source ---
            GUILayout.Space(8f);
            GUILayout.Label("State / Source", _boldStyle);
            string paperState = manager.State switch {
                HarmonyState.Outdoor => "OUT",
                HarmonyState.EnteringTransition => "APP",
                HarmonyState.VpsScanning => "SCAN",
                HarmonyState.Indoor => "IN",
                HarmonyState.Relocalization => "RELOC",
                HarmonyState.ExitingTransition => "EXIT",
                HarmonyState.Uncertain => "UNC",
                _ => "UNK"
            };
            GUILayout.Label($"State: {manager.State} [{paperState}]", _labelStyle);
            GUILayout.Label($"Source: {manager.ActiveSource}", _labelStyle);

            // --- Localization ---
            GUILayout.Space(8f);
            GUILayout.Label("Localization", _boldStyle);
            GUILayout.Label(
                $"GPS: valid={manager.GpsSample.IsValid} acc={Format(manager.GpsSample.HorizontalAccuracyMeters)}m " +
                $"age={Format(manager.GpsSample.AgeSeconds)}s rel={manager.Reliability.Gps:0.00} stable={manager.Reliability.GpsStableSeconds:0.0}s",
                _labelStyle);
            string confidence = manager.VpsSample.ConfidenceAvailable ? manager.VpsSample.Confidence.ToString("0.00") : "UNAVAILABLE";
            GUILayout.Label(
                $"VPS: valid={manager.VpsSample.IsValid} conf={confidence} map={manager.VpsSample.MapId ?? "UNAVAILABLE"} match={manager.VpsSample.MapMatchesBuilding} " +
                $"age={Format(manager.VpsSample.AgeSeconds)}s rel={manager.Reliability.Vps:0.00} stable={manager.Reliability.VpsStableSeconds:0.0}s",
                _labelStyle);
            GUILayout.Label($"System: ActiveRel={manager.Reliability.Active:0.00} Band={manager.Reliability.Band}", _labelStyle);
            GUILayout.Label($"Reason: {manager.StatusReason} | VPS Gate: {manager.Reliability.VpsReason}", _labelStyle);
            
            // --- Handover / Recovery ---
            GUILayout.Space(8f);
            GUILayout.Label("Handover / Recovery", _boldStyle);
            GUILayout.Label($"Handover jump: {Format(manager.LastPositionJumpMeters)}m / {Format(manager.LastHeadingJumpDegrees)}°", _labelStyle);
            GUILayout.Label($"State Age: {manager.StateAgeSeconds:0.0}s", _labelStyle);
            
            if (guidance != null && !string.IsNullOrEmpty(guidance.GuidanceMessage))
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Guidance: {guidance.GuidanceMessage}", _labelStyle);
            }

            GUILayout.EndScrollView();
            if (GUILayout.Button("Hide")) panelVisible = false;
            GUI.DragWindow(new Rect(0f, 0f, panelRect.width, 32f));
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;
            _boxStyle = new GUIStyle(GUI.skin.box);
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = Mathf.Max(14, GUI.skin.label.fontSize),
            };
            _boldStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(14, GUI.skin.label.fontSize),
            };
        }

        private static string Format(float value)
        {
            return float.IsInfinity(value) || float.IsNaN(value) ? "N/A" : value.ToString("0.0");
        }
    }
}
