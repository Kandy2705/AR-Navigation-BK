using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// On-screen overlay verify <see cref="MultisetPoseProvider"/> đang trả pose gì.
    /// Dùng để chấm acceptance Vòng 1:
    ///   - HasFreshPose = true sau khi VPS localized (hoặc fallback đọc được MapSpace).
    ///   - MapLocalPos/Rot khác zero khi đứng trong tòa.
    ///   - CampusPos hợp lý vs vị trí thật của cửa (đã calibrate).
    /// </summary>
    [DisallowMultipleComponent]
    public class MultisetPoseProviderDebugOverlay : MonoBehaviour
    {
        [SerializeField] private MultisetPoseProvider provider;

        [Header("Layout")]
        [SerializeField] private int x = 10;
        [SerializeField] private int y = 10;
        [SerializeField] private int width = 460;
        [SerializeField] private int height = 200;
        [SerializeField] private int fontSize = 14;

        private GUIStyle _style;

        private void Awake()
        {
            if (provider == null || !provider.isActiveAndEnabled)
                provider = MultisetPoseProvider.FindActiveProvider();
        }

        private void OnGUI()
        {
            if (provider == null)
            {
                GUI.Label(new Rect(x, y, width, 30), "[PoseProvider] not assigned");
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

            var p = provider.Last;
            string fresh = provider.HasFreshPose
                ? "<color=#7fff7f>FRESH</color>"
                : "<color=#ff7f7f>STALE</color>";
            string src = p.Source.ToString();
            string mapId = string.IsNullOrEmpty(p.MapId) ? "(none)" : p.MapId;

            string cam = provider.ArCamera != null ? provider.ArCamera.name : "<color=#ff7f7f>null</color>";
            string ms = provider.MapSpace != null ? provider.MapSpace.name : "<color=#ff7f7f>null</color>";
            string cal = provider.ActiveCalibration != null
                ? $"{provider.ActiveCalibration.TargetBuilding}/{provider.ActiveCalibration.FloorId}"
                : "<color=#ff7f7f>none</color>";

            string text =
                $"<b>MultisetPoseProvider</b>  {fresh}\n" +
                $"src={src}  conf={p.Confidence:0.00}  mapId={mapId}\n" +
                $"camera={cam}  mapSpace={ms}  calib={cal}\n" +
                $"mapLocal pos={p.MapLocalPosition.ToString("F2")}\n" +
                $"mapLocal rot={p.MapLocalRotation.eulerAngles.ToString("F1")}\n" +
                $"campus   pos={p.CampusPosition.ToString("F2")}\n" +
                $"campus   rot={p.CampusRotation.eulerAngles.ToString("F1")}\n" +
                $"age={(Time.timeAsDouble - p.Timestamp):0.00}s";

            GUI.Box(new Rect(x, y, width, height), text, _style);
        }
    }
}
