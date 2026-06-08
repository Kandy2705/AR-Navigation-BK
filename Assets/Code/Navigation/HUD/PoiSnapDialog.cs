using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// POI-snap dialog — user tap floating button, chọn POI mình đang đứng,
/// app snap user position = POI's exact lat/lon. Bypass GPS bias hoàn toàn.
///
/// Triết lý:
///   GPS user noise ±4-17m. POI lat/lon (Google Earth/surveyed) sai số ±1-3m.
///   Khi user thật sự đứng tại POI → user position trong app should = POI position.
///   Snap với POI coord (KHÔNG dùng GPS reading) → bias triệt tiêu, accuracy ±1-2m.
///
/// Auto-spawn khi NavigationManager.OnAREntered fire.
/// </summary>
[DisallowMultipleComponent]
public class PoiSnapDialog : MonoBehaviour
{
    private SimpleGPSTracker _tracker;
    private GameObject _floatCanvas;
    private GameObject _popup;
    private RectTransform _listContent;

    // Auto-spawn TẮT — user yêu cầu bỏ floating button "Tôi đang ở" khỏi UI.
    // Để bật lại: uncomment attribute [RuntimeInitializeOnLoadMethod] dưới đây.
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SubscribeToAREntered()
    {
        NavigationManager.OnAREntered -= SpawnIfNeeded;
        NavigationManager.OnAREntered += SpawnIfNeeded;
    }

    private static void SpawnIfNeeded()
    {
        if (FindFirstObjectByType<PoiSnapDialog>() != null) return;
        var go = new GameObject("PoiSnapDialog");
        go.AddComponent<PoiSnapDialog>();
    }

    private void Start()
    {
        _tracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (_tracker == null)
        {
            Debug.LogWarning("[PoiSnapDialog] Không tìm thấy SimpleGPSTracker.");
            Destroy(gameObject);
            return;
        }
        BuildFloatingButton();
    }

    // ─── Floating button (góc dưới-phải) ──────────────────────────────────────

    private void BuildFloatingButton()
    {
        _floatCanvas = new GameObject("PoiSnapFloatCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = _floatCanvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 990;
        var sc = _floatCanvas.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);

        GameObject btn = CreateButton(_floatCanvas.transform, "📍 Tôi đang ở...",
            new Color(0.2f, 0.5f, 0.85f, 0.95f), OpenPopup);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(400, 110);
        rt.anchoredPosition = new Vector2(-30, 280);
    }

    // ─── Popup chọn POI ───────────────────────────────────────────────────────

    private void OpenPopup()
    {
        if (_popup != null) { _popup.SetActive(true); RebuildList(); return; }

        _popup = new GameObject("PoiSnapPopupCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = _popup.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        var sc = _popup.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);

        // Backdrop (tap để đóng)
        GameObject backdrop = CreatePanel(_popup.transform, new Color(0, 0, 0, 0.6f));
        StretchToParent(backdrop);
        Button bdBtn = backdrop.AddComponent<Button>();
        bdBtn.transition = Selectable.Transition.None;
        bdBtn.onClick.AddListener(ClosePopup);

        // Panel chính
        GameObject panel = CreatePanel(backdrop.transform, new Color(0.13f, 0.15f, 0.18f, 0.98f));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(900, 1300);

        // Block click backdrop khi tap trong panel
        panel.AddComponent<Image>().color = new Color(0, 0, 0, 0); // raycast target
        // Title
        Text title = CreateText(panel.transform,
            "Tôi đang đứng tại POI nào?", 52, FontStyle.Bold, TextAnchor.MiddleCenter);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -30);
        trt.sizeDelta = new Vector2(-40, 90);

        // Hint
        Text hint = CreateText(panel.transform,
            "Tap POI bạn đang thực sự đứng tại.\nApp sẽ neo vị trí chính xác ±1m.",
            32, FontStyle.Normal, TextAnchor.MiddleCenter);
        hint.color = new Color(1f, 1f, 1f, 0.7f);
        var hrt = hint.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1);
        hrt.anchoredPosition = new Vector2(0, -140);
        hrt.sizeDelta = new Vector2(-60, 100);

        // Scroll view chứa list POI
        GameObject scrollGo = new GameObject("Scroll",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(ScrollRect), typeof(Mask));
        scrollGo.transform.SetParent(panel.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.1f, 0.5f);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, -270);
        srt.sizeDelta = new Vector2(-60, 850);

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        GameObject contentGo = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(scrollGo.transform, false);
        _listContent = contentGo.GetComponent<RectTransform>();
        _listContent.anchorMin = new Vector2(0, 1);
        _listContent.anchorMax = new Vector2(1, 1);
        _listContent.pivot = new Vector2(0.5f, 1);
        _listContent.anchoredPosition = Vector2.zero;
        _listContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 16;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = _listContent;
        scroll.viewport = scrollGo.GetComponent<RectTransform>();

        // Close button
        GameObject closeBtn = CreateButton(panel.transform, "Đóng",
            new Color(0.4f, 0.4f, 0.45f, 1f), ClosePopup);
        var crt = closeBtn.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0);
        crt.pivot = new Vector2(0.5f, 0);
        crt.anchoredPosition = new Vector2(0, 30);
        crt.sizeDelta = new Vector2(-60, 110);

        RebuildList();
    }

    private void RebuildList()
    {
        if (_listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        TargetAnchor[] targets = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None);
        List<TargetAnchor> sorted = new List<TargetAnchor>(targets);
        sorted.Sort((a, b) => string.Compare(a.TargetName, b.TargetName, System.StringComparison.OrdinalIgnoreCase));

        if (sorted.Count == 0)
        {
            Text emptyMsg = CreateText(_listContent.transform,
                "Không có POI nào", 36, FontStyle.Normal, TextAnchor.MiddleCenter);
            emptyMsg.color = new Color(1f, 1f, 1f, 0.5f);
            return;
        }

        foreach (TargetAnchor t in sorted)
        {
            TargetAnchor captured = t;
            GameObject btn = CreateButton(_listContent.transform, t.TargetName,
                new Color(0.25f, 0.4f, 0.55f, 1f), () => SnapToPoi(captured));
            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 110);
        }
    }

    private void SnapToPoi(TargetAnchor poi)
    {
        if (_tracker == null || poi == null) return;

        bool ok = _tracker.CalibrateAtSurveyedPoint(poi.targetLat, poi.targetLon, snapToSurveyedPoint: true);
        if (ok)
        {
            Debug.Log($"[PoiSnapDialog] Snapped to POI '{poi.TargetName}' " +
                      $"(lat={poi.targetLat:F6}, lon={poi.targetLon:F6}).");
            ShowToast($"✓ Đã neo tại {poi.TargetName}");
            ClosePopup();
        }
        else
        {
            ShowToast("Hiệu chỉnh thất bại (chưa có GPS fix?)");
        }
    }

    private void ClosePopup()
    {
        if (_popup != null) _popup.SetActive(false);
    }

    // ─── Toast 2.5s ───────────────────────────────────────────────────────────

    private void ShowToast(string message)
    {
        GameObject canvasGo = new GameObject("PoiSnapToast",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas c = canvasGo.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1010;
        CanvasScaler sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);

        GameObject bg = CreatePanel(canvasGo.transform, new Color(0.15f, 0.6f, 0.3f, 0.95f));
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.18f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(900, 130);

        Text t = CreateText(bg.transform, message, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        Destroy(canvasGo, 2.5f);
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────

    private static GameObject CreatePanel(Transform parent, Color color)
    {
        GameObject go = new GameObject("Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void StretchToParent(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Text CreateText(Transform parent, string content, int fontSize,
        FontStyle style, TextAnchor anchor)
    {
        GameObject go = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text txt = go.GetComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = anchor;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.raycastTarget = false;
        return txt;
    }

    private static GameObject CreateButton(Transform parent, string label, Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Button",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        Button btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        Text label2 = CreateText(go.transform, label, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
        var lrt = label2.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        return go;
    }
}
