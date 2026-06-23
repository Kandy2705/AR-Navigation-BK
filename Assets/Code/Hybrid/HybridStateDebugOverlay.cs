using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Overlay state + gate + pose tổng hợp cho acceptance Vòng 2.
    /// Phải thấy: state nhảy đúng theo distance & gate; reason rõ ràng.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridStateDebugOverlay : MonoBehaviour
    {
        [SerializeField] private HybridLocalizationManager manager;

        [Header("Layout")]
        [SerializeField] private int x = 10;
        [SerializeField] private int y = 220;
        [SerializeField] private int width = 460;
        [SerializeField] private int height = 240;
        [SerializeField] private int fontSize = 14;

        private GUIStyle _style;

        private void Awake()
        {
            if (manager == null) manager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
        }

        private void OnGUI()
        {
            if (manager == null)
            {
                GUI.Label(new Rect(x, y, width, 30), "[HybridLocalizationManager] not found");
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = fontSize,
                    richText = true,
                };
                _style.normal.textColor = Color.white;
            }

            var gate = manager.QualityGate;
            var outdoor = manager.OutdoorProvider;
            var indoor = manager.IndoorProvider;

            string stateColor = manager.CurrentState switch
            {
                HybridNavigationState.Outdoor => "#7fff7f",
                HybridNavigationState.ApproachingEntrance => "#ffeb7f",
                HybridNavigationState.TransitionScanning => "#7fdfff",
                HybridNavigationState.Indoor => "#bf7fff",
                HybridNavigationState.Relocalization => "#ffaf7f",
                HybridNavigationState.ExitingIndoor => "#ffd7af",
                HybridNavigationState.Uncertain => "#ff7f7f",
                _ => "white",
            };

            string entrance = manager.ActiveEntrance != null
                ? $"{manager.ActiveEntrance.BuildingId}/{manager.ActiveEntrance.FloorId} @ {manager.DistanceToActiveEntrance:0.0}m"
                : "(none)";

            string outdoorStr = outdoor != null
                ? $"src={outdoor.ActiveSource} acc={outdoor.AccuracyMeters:0.0}m fresh={outdoor.HasFreshFix}"
                : "(no provider)";

            string indoorStr = indoor != null
                ? $"src={indoor.Last.Source} conf={indoor.Last.Confidence:0.00} fresh={indoor.HasFreshPose}"
                : "(no provider)";

            string gateStr = gate != null
                ? $"OutdoorReady={gate.OutdoorReady}  IndoorReady={gate.IndoorReady} hits={gate.IndoorConsecutiveSuccesses} lost={gate.IndoorLost}\nreject={gate.LastIndoorRejectReason}"
                : "(no gate)";

            float age = (float)(Time.timeAsDouble - manager.LastTransitionTime);
            string text =
                $"<b>State:</b> <color={stateColor}>{manager.CurrentState}</color>  age={age:0.0}s\n" +
                $"<b>Reason:</b> {manager.LastTransitionReason}\n" +
                $"<b>Active:</b> bldg={manager.ActiveBuilding}  entrance={entrance}\n" +
                $"<b>User pos:</b> {manager.CurrentUserCampusPosition.ToString("F2")}  fresh={manager.CurrentPoseIsFresh}\n" +
                $"<b>Outdoor:</b> {outdoorStr}\n" +
                $"<b>Indoor:</b> {indoorStr}\n" +
                $"<b>Gate:</b> {gateStr}";

            GUI.Box(new Rect(x, y, width, height), text, _style);
        }
    }
}
