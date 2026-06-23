using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Renderer path đơn giản bằng LineRenderer cho mục đích debug Vòng 3.
    /// Cho phép production code tự gắn renderer xịn (ARPathFinder/NavMeshPathRibbon) song song —
    /// renderer này chỉ cần thiết khi muốn thấy ngay trong test scene.
    ///
    /// Đặc tính:
    ///   - Đổi màu theo <see cref="HybridRouteCoordinator.CurrentSource"/>.
    ///   - Crossfade alpha 0..1 trong <see cref="crossfadeSeconds"/> mỗi lần source đổi —
    ///     tránh teleport "đường nhảy đột ngột" giữa các phase.
    ///   - Tự ẩn khi phase = None / Pause.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public class HybridPathRenderer : MonoBehaviour
    {
        [SerializeField] private HybridRouteCoordinator coordinator;

        [Header("Style")]
        [SerializeField] private float lineWidth = 0.18f;
        [SerializeField] private float yLift = 0.05f;
        [SerializeField] private Color outdoorColor = new Color(0.35f, 0.78f, 1f);
        [SerializeField] private Color indoorColor = new Color(0.78f, 0.55f, 1f);
        [SerializeField] private Color pauseColor = new Color(1f, 0.85f, 0.4f);

        [Header("Smoothing")]
        [Tooltip("Số giây crossfade alpha khi source đổi (chống teleport visual giữa phase).")]
        [SerializeField] private float crossfadeSeconds = 0.7f;

        private LineRenderer _line;
        private HybridRouteCoordinator.RouteSource _displayedSource = HybridRouteCoordinator.RouteSource.None;
        private float _alpha;
        private float _alphaTarget;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.widthMultiplier = lineWidth;
            _line.useWorldSpace = true;
            _line.numCornerVertices = 4;
            _line.numCapVertices = 4;
            if (_line.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                if (shader != null) _line.material = new Material(shader);
            }
            _line.positionCount = 0;
        }

        private void OnEnable()
        {
            if (coordinator == null) coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (coordinator != null) coordinator.OnSourceChanged += HandleSourceChanged;
        }

        private void OnDisable()
        {
            if (coordinator != null) coordinator.OnSourceChanged -= HandleSourceChanged;
        }

        private void HandleSourceChanged(HybridRouteCoordinator.RouteSource prev, HybridRouteCoordinator.RouteSource next)
        {
            // Trigger fade out → swap color → fade in trong Update.
            _alphaTarget = 0f;
        }

        private void Update()
        {
            if (coordinator == null) { _line.positionCount = 0; return; }

            var src = coordinator.CurrentSource;
            var corners = coordinator.Corners;
            bool drawable = src != HybridRouteCoordinator.RouteSource.None
                            && corners != null && corners.Count >= 2;

            // Crossfade alpha target.
            _alphaTarget = drawable ? 1f : 0f;

            // Sau khi alpha tới 0 và source khác → swap visible source rồi fade lên.
            if (src != _displayedSource && _alpha < 0.05f)
            {
                _displayedSource = src;
            }

            float fadeSpeed = crossfadeSeconds > 0.01f ? 1f / crossfadeSeconds : 10f;
            _alpha = Mathf.MoveTowards(_alpha, _alphaTarget, fadeSpeed * Time.deltaTime);

            if (_alpha <= 0.001f || !drawable)
            {
                _line.positionCount = 0;
                return;
            }

            // Apply color theo displayed source (đã swap chỉ khi alpha=0).
            Color c = _displayedSource switch
            {
                HybridRouteCoordinator.RouteSource.Indoor => indoorColor,
                HybridRouteCoordinator.RouteSource.HandoverPause => pauseColor,
                _ => outdoorColor,
            };
            c.a = _alpha;
            _line.startColor = c;
            _line.endColor = c;

            _line.positionCount = corners.Count;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 p = corners[i];
                p.y += yLift;
                _line.SetPosition(i, p);
            }
        }
    }
}
