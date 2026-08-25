using ARNav.Hybrid;
using UnityEngine;

namespace ARNav.Harmony
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class UncertaintyGuidanceRenderer : MonoBehaviour
    {
        [SerializeField] private HarmonyManager manager;

        public string GuidanceMessage { get; private set; } = string.Empty;
        public float AppliedAlpha { get; private set; } = 1f;
        public bool DirectionalGuidanceVisible { get; private set; } = true;

        private ARPathFinder[] _pathFinders;
        private HybridPathRenderer[] _hybridPaths;
        private HybridArrowFollower[] _arrows;
        private float _nextDiscoveryAt;

        private void OnEnable()
        {
            Discover();
        }

        private void LateUpdate()
        {
            if (manager == null)
                manager = FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include);
            if (manager == null) return;
            if (Time.unscaledTime >= _nextDiscoveryAt)
            {
                _nextDiscoveryAt = Time.unscaledTime + 2f;
                Discover();
            }

            HarmonyConfig config = manager.Config;
            bool adaptive = config.UseUncertaintyGuidance;
            bool outdoorGuidance = manager.State == HarmonyState.Outdoor
                                   || manager.State == HarmonyState.EnteringTransition;
            HarmonyReliabilityBand band;
            if (manager.State == HarmonyState.Uncertain)
            {
                band = HarmonyReliabilityBand.Low;
            }
            else if (manager.State == HarmonyState.VpsScanning)
            {
                band = LocalizationReliabilityEvaluator.GetBand(
                    manager.Reliability.Vps,
                    config);
            }
            else
            {
                band = manager.Reliability.Band;
            }

            Color tint = Color.white;
            AppliedAlpha = 1f;
            DirectionalGuidanceVisible = true;
            GuidanceMessage = string.Empty;

            if (adaptive && band == HarmonyReliabilityBand.Medium)
            {
                // GPS ngoài trời thường dao động 10–25 m. Vẫn phải giữ tuyến đường đủ rõ;
                // uncertainty được truyền bằng màu + message, không làm path gần như biến mất.
                AppliedAlpha = outdoorGuidance
                    ? Mathf.Max(0.8f, config.mediumGuidanceAlpha)
                    : config.mediumGuidanceAlpha;
                tint = config.mediumReliabilityTint;
                GuidanceMessage = "Đang kiểm tra vị trí";
            }
            else if (adaptive && band == HarmonyReliabilityBand.Low)
            {
                tint = config.lowReliabilityTint;
                if (outdoorGuidance)
                {
                    AppliedAlpha = 0.6f;
                    DirectionalGuidanceVisible = true;
                    GuidanceMessage = "GPS yếu — tuyến đường chỉ mang tính ước lượng";
                }
                else
                {
                    AppliedAlpha = 0f;
                    DirectionalGuidanceVisible = false;
                    GuidanceMessage =
                        "Vị trí chưa ổn định, vui lòng quét lại khu vực";
                }
            }
            else
            {
                tint = adaptive ? config.highReliabilityTint : Color.white;
            }

            Apply(tint);
        }

        private void Discover()
        {
            if (manager == null)
                manager = FindFirstObjectByType<HarmonyManager>(FindObjectsInactive.Include);
            _pathFinders = FindObjectsByType<ARPathFinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _hybridPaths = FindObjectsByType<HybridPathRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _arrows = FindObjectsByType<HybridArrowFollower>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void Apply(Color tint)
        {
            for (int i = 0; i < _pathFinders.Length; i++)
                _pathFinders[i]?.SetHarmonyGuidance(
                    AppliedAlpha, DirectionalGuidanceVisible, tint);
            for (int i = 0; i < _hybridPaths.Length; i++)
                _hybridPaths[i]?.SetHarmonyGuidance(
                    AppliedAlpha, DirectionalGuidanceVisible, tint);
            for (int i = 0; i < _arrows.Length; i++)
                _arrows[i]?.SetHarmonyGuidance(
                    AppliedAlpha, DirectionalGuidanceVisible, tint);
        }
    }
}
