using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Màn hình loading GPS — tự động tạo khi scene GPSMapPlane khởi động.
///
/// Luồng hoạt động:
///   1. Hiện overlay tối toàn màn hình với text trạng thái GPS
///   2. Ẩn tất cả TargetAnchor (Des1, Des2) cho đến khi GPS sẵn sàng
///   3. Chờ SimpleGPSTracker.HasFirstFix = true
///   4. Gọi Recalculate() trên từng TargetAnchor → vật thể hiện ra đúng tọa độ
///   5. Fade out overlay rồi tự hủy (standalone GPSMapPlane),
///      hoặc ẩn để tái kích hoạt khi vào Hybrid Outdoor (destroyAfterFade = false).
///
/// Kết quả: người dùng không bao giờ thấy Des1/Des2 ở vị trí sai,
/// và biết rõ app đang chờ GPS thay vì tưởng app bị đơ.
/// </summary>
public class GPSStartupOverlay : MonoBehaviour
{
    // Thời gian fade out sau khi GPS đã sẵn sàng (giây)
    private const float FadeDuration = 0.6f;

    // Trong Editor không có GPS thật → timeout nhanh (3s) để dễ test
    // Trên thiết bị thật → chờ tối đa 30 giây
#if UNITY_EDITOR
    private const float GpsTimeoutSeconds = 3f;
#else
    private const float GpsTimeoutSeconds = 30f;
#endif

    private SimpleGPSTracker _gpsTracker;
    private TargetAnchor[]   _anchors;
    [SerializeField] private Text _statusText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [Tooltip("Standalone GPSMapPlane: destroy object after fade. Hybrid: disable so RestartSessionForHybridReentry can run.")]
    [SerializeField] private bool destroyAfterFade = true;
    private float            _elapsedWait;
    private bool             _revealed;

    // ──────────────────────────────────────────────────────────────────────────
    // Tự động tạo khi scene GPSMapPlane load (+ Editor / Hybrid Hierarchy)
    // ──────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGPSMapPlane()
    {
        if (!GpsOutdoorSceneNames.Includes(SceneManager.GetActiveScene().name)) return;

        // Tránh tạo trùng nếu đã có trong scene
        if (FindFirstObjectByType<GPSStartupOverlay>() != null) return;

        CreateOutdoorOverlayInHierarchy(null, true);
    }

    /// <summary>Places the overlay under optional parent (hybrid OutdoorNavigation stack).</summary>
    public static GPSStartupOverlay CreateOutdoorOverlayInHierarchy(Transform parent, bool destroyAfterFadeWhenDone)
    {
        GameObject canvasGO = new GameObject("GPS Startup Overlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));

        if (parent != null)
        {
            canvasGO.transform.SetParent(parent, false);
        }

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;

        GameObject bgGO = new GameObject("Background",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgGO.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.94f);

        GameObject dotGO = new GameObject("GPS Dot",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dotGO.transform.SetParent(canvasGO.transform, false);
        RectTransform dotRect = dotGO.GetComponent<RectTransform>();
        dotRect.anchorMin        = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax        = new Vector2(0.5f, 0.5f);
        dotRect.pivot            = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = new Vector2(0f, 80f);
        dotRect.sizeDelta        = new Vector2(80f, 80f);
        dotGO.GetComponent<Image>().color = new Color(0.18f, 0.72f, 1f, 1f);

        GameObject textGO = new GameObject("Status Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGO.transform.SetParent(canvasGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin        = new Vector2(0.1f, 0.5f);
        textRect.anchorMax        = new Vector2(0.9f, 0.5f);
        textRect.pivot            = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, -20f);
        textRect.sizeDelta        = new Vector2(0f, 160f);

        Text statusText = textGO.GetComponent<Text>();
        statusText.font               = GetDefaultFont();
        statusText.fontSize           = 44;
        statusText.fontStyle          = FontStyle.Bold;
        statusText.alignment          = TextAnchor.MiddleCenter;
        statusText.color              = Color.white;
        statusText.supportRichText    = true;
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.text               = "Dang ket noi GPS...";

        GameObject hintGO = new GameObject("Hint Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        hintGO.transform.SetParent(canvasGO.transform, false);
        RectTransform hintRect = hintGO.GetComponent<RectTransform>();
        hintRect.anchorMin        = new Vector2(0.1f, 0.5f);
        hintRect.anchorMax        = new Vector2(0.9f, 0.5f);
        hintRect.pivot            = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -100f);
        hintRect.sizeDelta        = new Vector2(0f, 100f);

        Text hintText = hintGO.GetComponent<Text>();
        hintText.font               = GetDefaultFont();
        hintText.fontSize           = 32;
        hintText.alignment          = TextAnchor.MiddleCenter;
        hintText.color              = new Color(0.7f, 0.7f, 0.7f, 1f);
        hintText.supportRichText    = false;
        hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hintText.text               = "Vui long ra ngoai troi\nde GPS hoat dong chinh xac hon";

        GPSStartupOverlay overlay = canvasGO.AddComponent<GPSStartupOverlay>();
        overlay._statusText       = statusText;
        overlay._canvasGroup      = canvasGO.GetComponent<CanvasGroup>();
        overlay.destroyAfterFade  = destroyAfterFadeWhenDone;

        canvasGO.AddComponent<GPSLoadingDotPulse>().dotImage = dotGO.GetComponent<Image>();
        return overlay;
    }

    /// <summary>Call when Hybrid mode re-enters Outdoor (overlay was hidden, not destroyed).</summary>
    public void RestartSessionForHybridReentry()
    {
        if (destroyAfterFade)
        {
            return;
        }

        StopAllCoroutines();

        _revealed      = false;
        _elapsedWait   = 0f;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha          = 1f;
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
        }

        enabled = true;
        gameObject.SetActive(true);

        _gpsTracker = FindFirstObjectByType<SimpleGPSTracker>();
        _anchors    = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None);

        foreach (TargetAnchor anchor in _anchors)
        {
            if (anchor != null)
            {
                anchor.SetVisible(false);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Tìm GPS tracker và tất cả các TargetAnchor trong scene
        _gpsTracker = FindFirstObjectByType<SimpleGPSTracker>();
        _anchors    = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None);

        if (_gpsTracker == null)
        {
            // Không có GPS tracker → ẩn overlay ngay lập tức
            Debug.LogWarning("[GPSStartupOverlay] Không tìm thấy SimpleGPSTracker. Overlay sẽ tự ẩn.");
            Destroy(gameObject);
            return;
        }

        // Đảm bảo các TargetAnchor ẩn từ đầu (TargetAnchor.Awake() đã làm điều này,
        // nhưng gọi lại ở đây để chắc chắn trong trường hợp thứ tự Awake thay đổi)
        foreach (TargetAnchor anchor in _anchors)
            if (anchor != null) anchor.SetVisible(false);
    }

    void Update()
    {
        if (_revealed) return;
        if (_gpsTracker == null) return;

        // Cập nhật text trạng thái GPS mỗi frame để người dùng thấy tiến trình
        UpdateStatusText();

        _elapsedWait += Time.unscaledDeltaTime;

        // Chờ CẢ HAI: GPS fix đầu tiên VÀ compass North alignment hoàn tất.
        // North alignment (~2s) chạy song song, thường xong trước GPS fix (~5-15s).
        bool poseReady    = _gpsTracker.HasFirstFix;
        bool northAligned = _gpsTracker.IsNorthAligned;

        if (poseReady && northAligned)
        {
            StartCoroutine(RevealAndFadeOut());
            return;
        }

        // Timeout: nếu quá GpsTimeoutSeconds vẫn không đủ điều kiện, hiện Des dù thiếu
        if (_elapsedWait >= GpsTimeoutSeconds)
        {
            Debug.LogWarning($"[GPSStartupOverlay] Timeout — poseReady={poseReady} northAligned={northAligned}. Hiện Des với dữ liệu hiện có.");
            StartCoroutine(RevealAndFadeOut(timeout: true));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateStatusText()
    {
        if (_statusText == null || _gpsTracker == null) return;

        bool poseReady    = _gpsTracker.HasFirstFix;
        bool northAligned = _gpsTracker.IsNorthAligned;

        // Hiện trạng thái tổng theo ưu tiên
        if (!northAligned && !poseReady)
        {
            _statusText.text = _elapsedWait < 5f
                ? "Dang khoi dong GPS..."
                : $"Dang ket noi GPS... ({_elapsedWait:F0}s)\nVui long ra ngoai troi";
        }
        else if (!northAligned)
        {
            _statusText.text = "Vi tri san sang — dang can chinh huong Bac...";
        }
        else if (!poseReady)
        {
            if (_gpsTracker.IsCollectingFirstFixAverage)
            {
                _statusText.text = "Dang lay mau GPS (dung yen 10–15 giay)...";
            }
            else
            {
                string compassInfo = _gpsTracker.NorthCorrectionDeg != 0f
                    ? $"\n<size=30>La ban: chinh {_gpsTracker.NorthCorrectionDeg:F0}°</size>"
                    : "";
                _statusText.text = _elapsedWait < 15f
                    ? $"Dang cho dinh vi (can +/- <= 5m)...{compassInfo}"
                    : $"GPS kho ket noi ({_elapsedWait:F0}s)\nThu lai o noi thong thoang hon";
            }
        }

        // Thêm badge accuracy nếu đã có dữ liệu
        if (_gpsTracker.CurrentHorizontalAccuracy > 0f)
        {
            float acc = _gpsTracker.CurrentHorizontalAccuracy;
            string badge = acc <= 5f ? "[TOT]" : acc <= 12f ? "[TB]" : "[YEU]";
            _statusText.text += $"\n<size=34>GPS {badge} +/-{acc:F0}m</size>";
        }
    }

    private IEnumerator RevealAndFadeOut(bool timeout = false)
    {
        // Đánh dấu đã reveal để Update() không chạy lại
        _revealed = true;

        // Cập nhật text một lần cuối trước khi fade
        if (_statusText != null)
        {
            _statusText.text = timeout
                ? "GPS yeu — hien thi vi tri uoc tinh"
                : "GPS san sang!";
        }

        // Gọi Recalculate() trên tất cả TargetAnchor → đặt đúng tọa độ GPS rồi hiện ra
        // #region agent log — DEBUG 40cacb — Log C: overlay reveal timing + GPS state (tests H.E)
        try {
            float dbgElapsed = _elapsedWait;
            bool hasFix = _gpsTracker != null && _gpsTracker.HasFirstFix;
            float acc = _gpsTracker != null ? _gpsTracker.CurrentHorizontalAccuracy : -1f;
            long ts2 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line2 = "{\"sessionId\":\"40cacb\",\"timestamp\":" + ts2 +
                ",\"location\":\"GPSStartupOverlay.cs:RevealAndFadeOut\",\"hypothesisId\":\"E\"" +
                ",\"message\":\"OVERLAY_REVEAL\"" +
                ",\"data\":{" +
                "\"timeout\":" + timeout.ToString().ToLower() +
                ",\"elapsedWaitSec\":" + dbgElapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"hasFirstFix\":" + hasFix.ToString().ToLower() +
                ",\"gpsAccuracy\":" + acc.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"anchorCount\":" + _anchors.Length +
                "}}\n";
            string path2 = System.IO.Path.Combine(Application.persistentDataPath, "debug-40cacb.log");
            System.IO.File.AppendAllText(path2, line2);
            Debug.Log("[DBG40cacb-C] REVEAL timeout=" + timeout + " elapsed=" + dbgElapsed.ToString("F1") +
                " hasFix=" + hasFix + " acc=" + acc.ToString("F1"));
        } catch (System.Exception ex) { Debug.LogWarning("[DBG40cacb] LogC failed: " + ex.Message); }
        // #endregion

        foreach (TargetAnchor anchor in _anchors)
        {
            if (anchor != null)
            {
                anchor.Recalculate(); // Bên trong Recalculate() đã gọi SetVisible(true)
            }
        }

        // Đợi 0.4 giây để người dùng thấy thông báo "GPS san sang!" trước khi fade
        yield return new WaitForSecondsRealtime(0.4f);

        // Fade out overlay
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // Giảm alpha từ 1 → 0
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - (elapsed / FadeDuration);
            yield return null;
        }

        if (destroyAfterFade)
        {
            Destroy(gameObject);
        }
        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable   = false;
            }

            gameObject.SetActive(false);
        }
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}

/// <summary>
/// Component phụ: làm cho chấm tròn GPS nhấp nháy (pulse) trong overlay loading.
/// Tách ra class riêng để GPSStartupOverlay không bị rối.
/// </summary>
public class GPSLoadingDotPulse : MonoBehaviour
{
    public Image dotImage;

    // Tốc độ nhấp nháy (chu kỳ mỗi giây)
    private const float PulseSpeed = 1.8f;

    void Update()
    {
        if (dotImage == null) return;

        // Tính alpha dao động theo sin từ 0.2 → 1.0 để tạo hiệu ứng nhấp nháy mượt
        float alpha = 0.2f + 0.8f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed * Mathf.PI * 2f));
        Color c = dotImage.color;
        c.a = alpha;
        dotImage.color = c;
    }
}
