using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Silent GPS auto-snap — không có UI, chạy nền sau khi user vào AR/Outdoor mode.
///
/// Logic:
///   1. Subscribe vào NavigationManager.OnAREntered → tự spawn instance
///   2. Đợi GPS có fix + compass align xong + accuracy ≤ threshold trong N giây
///   3. Gọi SimpleGPSTracker.CalibrateAtSurveyedPoint(lat, lon, snap:true)
///   4. Hiển thị toast ngắn xác nhận "✓ Đã hiệu chỉnh ±Xm" → tự tan
///   5. Self-destruct sau khi xong
///
/// User không bị popup chắn UI. Snap "vô hình" như Google Maps.
/// </summary>
[DisallowMultipleComponent]
public class QuickGpsSnapDialog : MonoBehaviour
{
    [Header("Auto-snap (Google Maps style — không đợi)")]
    [Tooltip("Snap ngay khi có GPS reading đầu tiên, bất kể accuracy. KHUYẾN NGHỊ true (như Google Maps).")]
    [SerializeField] private bool snapOnFirstReading = true;
    [Tooltip("Chỉ dùng nếu snapOnFirstReading=false. Accuracy ≤ giá trị này (mét) được coi là đủ tốt để snap.")]
    [SerializeField] private float autoSnapAccuracyThreshold = 25f;
    [Tooltip("Chỉ dùng nếu snapOnFirstReading=false. Số giây stable trước khi snap.")]
    [SerializeField] private float autoSnapStableSeconds = 2f;
    [Tooltip("Timeout: nếu sau ngần này giây vẫn chưa snap, force snap với accuracy hiện tại.")]
    [SerializeField] private float maxWaitSeconds = 15f;

    private SimpleGPSTracker _tracker;
    private float _accuracyOkSince = -1f;
    private float _spawnTime;
    private bool _snapped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SubscribeToAREntered()
    {
        NavigationManager.OnAREntered -= SpawnAndShow;
        NavigationManager.OnAREntered += SpawnAndShow;
    }

    public static void SpawnAndShow()
    {
        if (FindFirstObjectByType<QuickGpsSnapDialog>() != null) return;
        var go = new GameObject("QuickGpsSnapDialog");
        go.AddComponent<QuickGpsSnapDialog>();
    }

    private void Start()
    {
        _tracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (_tracker == null)
        {
            Debug.LogWarning("[QuickGpsSnap] Không tìm thấy SimpleGPSTracker — không thể snap.");
            Destroy(gameObject);
            return;
        }
        _spawnTime = Time.time;
    }

    private void Update()
    {
        if (_snapped || _tracker == null) return;

        bool hasFix = _tracker.HasLocationFix;
        bool compassReady = _tracker.IsNorthAligned;
        float acc = _tracker.CurrentHorizontalAccuracy;

        // Google Maps style: snap ngay khi có GPS reading đầu, bất kể accuracy.
        // User có vị trí trên minimap NGAY, GPS tự refine sau đó qua rolling average filter.
        if (snapOnFirstReading)
        {
            if (hasFix && acc > 0f)
            {
                TrySnap(isTimeout: false);
            }
            return;
        }

        // Legacy mode (nếu user muốn đợi accuracy tốt):
        if (!hasFix || !compassReady || acc <= 0f)
        {
            _accuracyOkSince = -1f;
            if (Time.time - _spawnTime > maxWaitSeconds && hasFix && acc > 0f)
            {
                TrySnap(isTimeout: true);
            }
            return;
        }

        if (acc <= autoSnapAccuracyThreshold)
        {
            if (_accuracyOkSince < 0f) _accuracyOkSince = Time.time;
            if (Time.time - _accuracyOkSince >= autoSnapStableSeconds)
            {
                TrySnap(isTimeout: false);
            }
        }
        else
        {
            _accuracyOkSince = -1f;
            if (Time.time - _spawnTime > maxWaitSeconds)
            {
                TrySnap(isTimeout: true);
            }
        }
    }

    private void TrySnap(bool isTimeout)
    {
        if (_snapped) return;
        double lat = _tracker.CurrentLatitude;
        double lon = _tracker.CurrentLongitude;
        float acc = _tracker.CurrentHorizontalAccuracy;

        bool ok = _tracker.CalibrateAtSurveyedPoint(lat, lon, snapToSurveyedPoint: true);
        if (!ok) return;

        _snapped = true;
        string tag = isTimeout ? "TIMEOUT" : "AUTO";
        Debug.Log($"[QuickGpsSnap] {tag} snap → ({lat:F6}, {lon:F6}) ±{acc:F1}m sau {Time.time - _spawnTime:F1}s");

        ShowToast($"✓ Đã hiệu chỉnh vị trí (±{acc:F1}m)");
        Destroy(gameObject, 0.1f);
    }

    /// <summary>Toast nhẹ 2.5s ở đáy màn hình. Không chắn UI khác.</summary>
    private void ShowToast(string message)
    {
        var canvasGo = new GameObject("QuickGpsSnapToast",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = canvasGo.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1001;
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);

        var bgGo = new GameObject("Bg",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        bgGo.GetComponent<Image>().color = new Color(0.15f, 0.6f, 0.3f, 0.95f);
        bgGo.GetComponent<Image>().raycastTarget = false;
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.1f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(900, 130);

        var txtGo = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(bgGo.transform, false);
        var txt = txtGo.GetComponent<Text>();
        txt.text = message;
        txt.fontSize = 42;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        Destroy(canvasGo, 2.5f);
    }
}
