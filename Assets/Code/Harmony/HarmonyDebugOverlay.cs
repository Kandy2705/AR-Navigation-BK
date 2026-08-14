using UnityEngine;

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
        [SerializeField] private Rect panelRect = new Rect(12f, 12f, 430f, 560f);

        private Vector2 _scroll;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            if (manager == null)
                manager = FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include);
            if (logger == null)
                logger = FindFirstObjectByType<HarmonyExperimentLogger>(FindObjectsInactive.Include);
            if (guidance == null)
                guidance = FindFirstObjectByType<UncertaintyGuidanceRenderer>(FindObjectsInactive.Include);

            if (!panelVisible)
            {
                if (GUI.Button(new Rect(12f, 12f, 130f, 42f), "HARMONY Debug"))
                    panelVisible = true;
                return;
            }
            panelRect = GUI.Window(GetInstanceID(), panelRect, DrawWindow, "HARMONY V3");
        }

        private void DrawWindow(int id)
        {
            EnsureStyles();
            if (manager == null)
            {
                GUILayout.Label("HarmonyManager missing", _labelStyle);
                if (GUILayout.Button("Close")) panelVisible = false;
                GUI.DragWindow();
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"Version: {manager.ExperimentVersion}", _labelStyle);
            GUILayout.Label($"State: {manager.State}", _labelStyle);
            GUILayout.Label($"Source: {manager.ActiveSource}", _labelStyle);
            GUILayout.Label(
                $"Reliability GPS/VPS/active: {manager.Reliability.Gps:0.00} / " +
                $"{manager.Reliability.Vps:0.00} / {manager.Reliability.Active:0.00} " +
                $"[{manager.Reliability.Band}]",
                _labelStyle);
            GUILayout.Label(
                $"GPS: valid={manager.GpsSample.IsValid} acc={Format(manager.GpsSample.HorizontalAccuracyMeters)}m " +
                $"age={Format(manager.GpsSample.AgeSeconds)}s",
                _labelStyle);
            string confidence = manager.VpsSample.ConfidenceAvailable
                ? manager.VpsSample.Confidence.ToString("0.00")
                : "UNAVAILABLE";
            GUILayout.Label(
                $"VPS: valid={manager.VpsSample.IsValid} conf={confidence} " +
                $"map={manager.VpsSample.MapId ?? "UNAVAILABLE"} match={manager.VpsSample.MapMatchesBuilding}",
                _labelStyle);
            GUILayout.Label(
                $"Stable GPS/VPS: {manager.Reliability.GpsStableSeconds:0.0}s / " +
                $"{manager.Reliability.VpsStableSeconds:0.0}s",
                _labelStyle);
            GUILayout.Label(
                $"Handover jump: {Format(manager.LastPositionJumpMeters)}m / " +
                $"{Format(manager.LastHeadingJumpDegrees)}°",
                _labelStyle);
            GUILayout.Label($"Reason: {manager.StatusReason}", _labelStyle);
            GUILayout.Label($"VPS gate: {manager.Reliability.VpsReason}", _labelStyle);
            if (guidance != null && !string.IsNullOrEmpty(guidance.GuidanceMessage))
                GUILayout.Label($"Guidance: {guidance.GuidanceMessage}", _labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label("Experiment version", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("V0"))
                manager.SetExperimentVersion(HarmonyExperimentVersion.V0_FixedSwitching);
            if (GUILayout.Button("V1"))
                manager.SetExperimentVersion(HarmonyExperimentVersion.V1_ReliabilityOnly);
            if (GUILayout.Button("V2"))
                manager.SetExperimentVersion(HarmonyExperimentVersion.V2_FullHarmony);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Experiment logger", _labelStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = logger != null && !logger.IsLogging;
            if (GUILayout.Button("Start Test")) logger?.StartLogging();
            GUI.enabled = logger != null && logger.IsLogging;
            if (GUILayout.Button("End Test")) logger?.EndLogging();
            GUI.enabled = logger != null;
            if (GUILayout.Button("Reset Test")) logger?.ResetTest();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Export CSV") && logger != null)
                logger.ExportCsv();
            if (logger != null)
            {
                GUILayout.Label(
                    $"Logging={logger.IsLogging} toggles={logger.SourceToggleCount} " +
                    $"false={logger.FalseSwitchCount} wrong-way={logger.WrongWayCount}",
                    _labelStyle);
                GUILayout.TextArea(
                    string.IsNullOrEmpty(logger.LastExportPath)
                        ? logger.CurrentFilePath
                        : logger.LastExportPath);
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
        }

        private static string Format(float value)
        {
            return float.IsInfinity(value) || float.IsNaN(value)
                ? "N/A"
                : value.ToString("0.0");
        }
    }
}
