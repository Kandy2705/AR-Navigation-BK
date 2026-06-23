using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Dump tất cả state runtime của hybrid pipeline mỗi 1 giây, kèm overlay góc phải màn hình
    /// và nút "Snapshot Now" trong inspector.
    ///
    /// Drop component này lên BẤT KỲ GameObject persistent (vd 'Hybrid Hub') để chẩn đoán
    /// vì sao state không nhảy / trigger không fire.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridRuntimeDiagnose : MonoBehaviour
    {
        [SerializeField] private float intervalSeconds = 1f;
        [SerializeField] private bool logToConsole = true;
        [SerializeField] private bool showOverlay = true;

        private HybridLocalizationManager _manager;
        private OutdoorPoseProvider _outdoor;
        private MultisetPoseProvider _indoor;
        private LocalizationQualityGate _gate;
        private HybridRouteCoordinator _route;
        private float _nextTime;
        private string _lastReport = "(not sampled yet)";

        private void OnEnable()
        {
            Resolve();
            DumpSnapshot();
        }

        private void Update()
        {
            if (Time.time < _nextTime) return;
            _nextTime = Time.time + intervalSeconds;
            DumpSnapshot();
        }

        private void Resolve()
        {
            _manager ??= FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
            _outdoor ??= FindFirstObjectByType<OutdoorPoseProvider>(FindObjectsInactive.Include);
            _indoor ??= FindFirstObjectByType<MultisetPoseProvider>(FindObjectsInactive.Include);
            _gate ??= FindFirstObjectByType<LocalizationQualityGate>(FindObjectsInactive.Include);
            _route ??= FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
        }

        [ContextMenu("Snapshot Now")]
        public void DumpSnapshot()
        {
            Resolve();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== HybridRuntimeDiagnose ===  t=" + Time.time.ToString("0.00"));

            // 1. Hub presence + active.
            sb.AppendLine($"HybridLocalizationManager : {Describe(_manager)}");
            sb.AppendLine($"OutdoorPoseProvider       : {Describe(_outdoor)}");
            sb.AppendLine($"MultisetPoseProvider      : {Describe(_indoor)}");
            sb.AppendLine($"LocalizationQualityGate   : {Describe(_gate)}");
            sb.AppendLine($"HybridRouteCoordinator    : {Describe(_route)}");

            // 2. Outdoor pose source — quan trọng nhất.
            if (_outdoor != null)
            {
                sb.AppendLine($"Outdoor source={_outdoor.ActiveSource} fresh={_outdoor.HasFreshFix} " +
                              $"acc={_outdoor.AccuracyMeters:0.0}m pos={_outdoor.UserCampusPosition.ToString("F2")}");
            }

            // 3. Manager state + active entrance + distance.
            if (_manager != null)
            {
                sb.AppendLine($"Manager STATE = {_manager.CurrentState}  reason='{_manager.LastTransitionReason}'");
                sb.AppendLine($"  user campus = {_manager.CurrentUserCampusPosition.ToString("F2")}  freshPose={_manager.CurrentPoseIsFresh}");
                sb.AppendLine($"  ActiveBuilding={_manager.ActiveBuilding}  ActiveEntrance={(_manager.ActiveEntrance != null ? _manager.ActiveEntrance.name : "(none)")}  dist={_manager.DistanceToActiveEntrance:0.0}m");
            }

            // 4. ALL entrance anchors in scene.
            sb.AppendLine($"EntranceAnchor.All.Count = {EntranceAnchor.All.Count}");
            foreach (var a in EntranceAnchor.All)
            {
                if (a == null) continue;
                Vector3 user = _manager != null ? _manager.CurrentUserCampusPosition : Vector3.zero;
                float dx = a.CampusWorldPosition.x - user.x;
                float dz = a.CampusWorldPosition.z - user.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float approachR = a.TriggerRadiusMeters * 2.5f; // default multiplier
                string near = dist <= approachR ? "<color=#7fff7f>IN APPROACH</color>"
                              : dist <= approachR * 2f ? "<color=#ffeb7f>NEAR</color>"
                              : $"<color=#ff7f7f>FAR ({dist:0}m)</color>";
                sb.AppendLine($"  • '{a.name}' bldg={a.BuildingId} pos={a.CampusWorldPosition.ToString("F2")} trig={a.TriggerRadiusMeters}m approach={approachR}m → user dist={dist:0.0}m  {near}");
            }

            // 5. Gate readouts.
            if (_gate != null)
            {
                sb.AppendLine($"Gate: OutdoorReady={_gate.OutdoorReady}  IndoorReady={_gate.IndoorReady}  IndoorLost={_gate.IndoorLost}  rejectReason='{_gate.LastIndoorRejectReason}'");
            }

            // 6. Route coordinator.
            if (_route != null)
            {
                sb.AppendLine($"Route phase={_route.CurrentPhase} source={_route.CurrentSource} target={_route.CurrentTarget.ToString("F2")} corners={_route.Corners?.Count ?? 0} straightLineFallback={_route.IsStraightLineFallback}");
                var d = _route.Destination;
                if (d != null) sb.AppendLine($"  destination: name='{d.displayName}' indoor={d.isIndoor} bldg={d.building} pos={d.CampusPosition.ToString("F2")}");
            }

            _lastReport = sb.ToString();
            if (logToConsole) Debug.Log(_lastReport);
        }

        private static string Describe(Component c)
        {
            if (c == null) return "<color=#ff7f7f>MISSING</color>";
            string status = c.gameObject.activeInHierarchy ? "active" : "<color=#ff7f7f>INACTIVE</color>";
            string compEn = c is Behaviour b ? (b.enabled ? "enabled" : "<color=#ff7f7f>DISABLED</color>") : "";
            return $"OK on '{c.gameObject.name}'  {status} {compEn}";
        }

        private GUIStyle _style;
        private void OnGUI()
        {
            if (!showOverlay) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 12 };
                _style.normal.textColor = Color.white;
            }
            GUI.Box(new Rect(Screen.width - 520, 110, 510, 460), _lastReport, _style);
        }
    }
}
