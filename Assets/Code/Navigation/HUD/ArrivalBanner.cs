using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Banner giữa màn hình: "Bạn đã đến nơi" khi user tới điểm đích.
/// Gọi <see cref="Show"/> (idempotent theo destination key cho đến khi hide).
/// Tự tạo Canvas overlay nếu chưa có trong scene.
/// </summary>
[DisallowMultipleComponent]
public class ArrivalBanner : MonoBehaviour
{
    public static ArrivalBanner Instance { get; private set; }

    [Header("Copy")]
    [SerializeField] private string titleText = "Bạn đã đến nơi!";
    [SerializeField] private string subtitleFormat = "{0}";

    [Header("Timing")]
    [Tooltip("Hiện bao lâu (giây) trước khi tự ẩn. 0 = giữ đến khi rời vùng / đổi đích.")]
    [SerializeField] private float autoHideSeconds = 4.5f;

    [SerializeField] private float fadeInSeconds = 0.25f;
    [SerializeField] private float fadeOutSeconds = 0.4f;

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(0.05f, 0.12f, 0.08f, 0.88f);
    [SerializeField] private Color titleColor = new Color(0.55f, 1f, 0.65f, 1f);
    [SerializeField] private Color subtitleColor = Color.white;

    private Canvas _canvas;
    private CanvasGroup _group;
    private Text _title;
    private Text _subtitle;
    private Coroutine _routine;
    private string _lastShownKey;
    private bool _visible;

    public bool IsVisible => _visible;
    public string LastShownKey => _lastShownKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOnHybridScenes()
    {
        string n = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (n != "HybridGPSMap" && n != "Hybrid Navigation" && n != "GPSMapPlane") return;
        if (FindFirstObjectByType<ArrivalBanner>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("ArrivalBanner");
        go.AddComponent<ArrivalBanner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        BuildUiIfNeeded();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static ArrivalBanner EnsureExists()
    {
        if (Instance != null) return Instance;
        var existing = FindFirstObjectByType<ArrivalBanner>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }
        var go = new GameObject("ArrivalBanner");
        return go.AddComponent<ArrivalBanner>();
    }

    /// <summary>
    /// Hiện banner. <paramref name="destinationKey"/> dùng chống spam (cùng đích chỉ show 1 lần).
    /// </summary>
    public void Show(string destinationDisplayName, string destinationKey = null)
    {
        BuildUiIfNeeded();
        string key = string.IsNullOrEmpty(destinationKey) ? destinationDisplayName : destinationKey;
        if (_visible && _lastShownKey == key) return;
        // Nếu đang show đích khác → thay nội dung.
        _lastShownKey = key;

        if (_title != null) _title.text = titleText;
        if (_subtitle != null)
        {
            string sub = string.IsNullOrEmpty(destinationDisplayName)
                ? ""
                : string.Format(subtitleFormat, destinationDisplayName);
            _subtitle.text = sub;
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    /// <summary>Cho phép show lại cùng đích sau khi user rời vùng arrival.</summary>
    public void ResetArrivalLatch(string destinationKey = null)
    {
        if (destinationKey == null || _lastShownKey == destinationKey)
        {
            if (!_visible) _lastShownKey = null;
        }
    }

    public void Hide()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideRoutine());
    }

    private void HideImmediate()
    {
        _visible = false;
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
        if (_canvas != null) _canvas.enabled = false;
    }

    private IEnumerator ShowRoutine()
    {
        _visible = true;
        if (_canvas != null) _canvas.enabled = true;
        if (_group != null)
        {
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        float t = 0f;
        float start = _group != null ? _group.alpha : 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, 1f, fadeInSeconds > 0.01f ? t / fadeInSeconds : 1f);
            if (_group != null) _group.alpha = a;
            yield return null;
        }
        if (_group != null) _group.alpha = 1f;

        if (autoHideSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(autoHideSeconds);
            yield return HideRoutine();
        }
        _routine = null;
    }

    private IEnumerator HideRoutine()
    {
        float t = 0f;
        float start = _group != null ? _group.alpha : 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, 0f, fadeOutSeconds > 0.01f ? t / fadeOutSeconds : 1f);
            if (_group != null) _group.alpha = a;
            yield return null;
        }
        HideImmediate();
        _routine = null;
    }

    private void BuildUiIfNeeded()
    {
        if (_canvas != null) return;

        var canvasGo = new GameObject("ArrivalBannerCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9000; // trên HUD thường
        _group = canvasGo.GetComponent<CanvasGroup>();

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Dim root full screen (tap-through)
        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimGo.transform.SetParent(canvasGo.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        StretchFull(dimRt);
        var dimImg = dimGo.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.35f);
        dimImg.raycastTarget = false;

        // Center card
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(860f, 280f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = panelColor;
        panelImg.raycastTarget = false;

        _title = CreateText(panelGo.transform, "Title", titleText, 52, FontStyle.Bold, titleColor,
            new Vector2(0.5f, 0.62f), new Vector2(800f, 90f));
        _subtitle = CreateText(panelGo.transform, "Subtitle", "", 34, FontStyle.Normal, subtitleColor,
            new Vector2(0.5f, 0.28f), new Vector2(800f, 70f));
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Text CreateText(Transform parent, string name, string content, int size, FontStyle style, Color color,
        Vector2 anchor, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
