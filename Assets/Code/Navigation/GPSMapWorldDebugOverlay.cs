using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GPSMapPlane-only overlay: camera / XR Origin / smoothed GPS / selected destination / distance to map origin on XZ.
/// Field accuracy order for anchors: surveyed MapOrigin at texture "zero" point; uniform Ground scale vs real meters;
/// measure D_actual vs D_Unity between landmarks; run <see cref="SimpleGPSTracker.CalibrateAtOrigin"/> last for device bias.
/// </summary>
public class GPSMapWorldDebugOverlay : MonoBehaviour
{
    // Anchor accuracy (ngoài trời): (1) MapOrigin = điểm khảo sát khớp "0" trên texture;
    // (2) Ground scale đồng đều theo mét thật; (3) đo D_thực vs D_Unity giữa 2 neo; (4) CalibrateAtOrigin cuối cùng (bias máy).

    [SerializeField] private float refreshIntervalSeconds = 0.18f;
    [Tooltip("If |XR XZ| to world origin is at or below this (meters), show OK (plus GPS accuracy caveats).")]
    [SerializeField] private float originOkRadiusMeters = 5f;
    [SerializeField] private bool startPanelHidden = true;

    private Text _label;
    private RectTransform _panelRoot;
    private CanvasGroup _canvasGroup;
    private float _nextRefreshTime;
    private SimpleGPSTracker _tracker;
    private ARPathFinder _pathFinder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGPSMapPlane()
    {
        if (!GpsOutdoorSceneNames.Includes(SceneManager.GetActiveScene().name))
            return;
        if (FindFirstObjectByType<GPSMapWorldDebugOverlay>() != null)
            return;

        GameObject host = new GameObject(nameof(GPSMapWorldDebugOverlay));
        host.AddComponent<GPSMapWorldDebugOverlay>();
    }

    private void Start()
    {
        StartCoroutine(SetupDelayed());
    }

    private IEnumerator SetupDelayed()
    {
        // HUD is built via another RuntimeInitialize hook; wait a couple frames.
        yield return null;
        yield return null;

        _tracker = FindFirstObjectByType<SimpleGPSTracker>();
        _pathFinder = FindFirstObjectByType<ARPathFinder>();

        // Must be above Mobile Navigation HUD (100) and Minimap Canvas in GPSMapPlane (200),
        // otherwise the DBG toggle (was top-right) sits under the minimap and disappears.
        Transform parent = EnsureTopMostDebugCanvas().transform;

        BuildUi(parent);

        _nextRefreshTime = Time.unscaledTime;
        RefreshText();

        Debug.Log("[GPSMapWorldDebugOverlay] UI ready — cham nut DBG (goc tren TRAI, duoi status) de mo world debug.");
    }

    private static Canvas EnsureTopMostDebugCanvas()
    {
        GameObject existing = GameObject.Find("GPSMapWorldDebugCanvas");
        if (existing != null)
            return existing.GetComponent<Canvas>();

        GameObject go = new GameObject("GPSMapWorldDebugCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform crt = go.GetComponent<RectTransform>();
        crt.localScale = Vector3.one;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 320;
        CanvasScaler s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1080f, 1920f);
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        s.matchWidthOrHeight = 0.5f;
        return c;
    }

    private void BuildUi(Transform parent)
    {
        RectTransform panel = CreatePanel(parent, "World Debug Panel", new Color(0.05f, 0.08f, 0.12f, 0.88f));
        // Top-left column: avoids bottom dropdown; stays on left half — minimap remains on the right.
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(12f, -324f);
        panel.sizeDelta = new Vector2(560f, 520f);

        _canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = startPanelHidden ? 0f : 1f;
        _canvasGroup.interactable = !startPanelHidden;
        _canvasGroup.blocksRaycasts = !startPanelHidden;

        _panelRoot = panel;

        GameObject textGo = new GameObject("World Debug Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(panel, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 14f);
        tr.offsetMax = new Vector2(-12f, -12f);

        _label = textGo.GetComponent<Text>();
        _label.font = GetDefaultFont();
        _label.fontSize = 24;
        _label.alignment = TextAnchor.UpperLeft;
        _label.horizontalOverflow = HorizontalWrapMode.Wrap;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        _label.color = Color.white;
        _label.supportRichText = true;
        _label.lineSpacing = 1.05f;
        _label.raycastTarget = false;
        _label.text = "World debug…";

        GameObject dbgBtnRoot = CreateToggleButton(parent);
        if (dbgBtnRoot != null)
            dbgBtnRoot.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Left column: anchors under main HUD strip. Child Text must not raycast or it steals clicks from Button.
    /// </summary>
    /// <returns>Root GameObject for z-order bookkeeping.</returns>
    private GameObject CreateToggleButton(Transform canvasRoot)
    {
        GameObject btnGo = new GameObject("World Debug Toggle",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(canvasRoot, false);

        RectTransform br = btnGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 1f);
        br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 1f);
        // Sit just under the HUD status strip (MobileNavigationHUD ~248px tall from top edge @ 1080x1920 ref).
        br.anchoredPosition = new Vector2(14f, -262f);
        br.sizeDelta = new Vector2(132f, 52f);

        Image img = btnGo.GetComponent<Image>();
        img.color = new Color(0.12f, 0.18f, 0.28f, 0.92f);
        img.raycastTarget = true;

        GameObject cap = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        cap.transform.SetParent(btnGo.transform, false);
        RectTransform ctr = cap.GetComponent<RectTransform>();
        ctr.anchorMin = Vector2.zero;
        ctr.anchorMax = Vector2.one;
        ctr.offsetMin = Vector2.zero;
        ctr.offsetMax = Vector2.zero;
        Text t = cap.GetComponent<Text>();
        t.font = GetDefaultFont();
        t.fontSize = 28;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = "DBG";
        t.raycastTarget = false;

        Button b = btnGo.GetComponent<Button>();
        b.onClick.AddListener(TogglePanelVisible);
        return btnGo;
    }

    private void TogglePanelVisible()
    {
        if (_canvasGroup == null)
        {
            Debug.LogWarning("[GPSMapWorldDebugOverlay] Toggle: panel CanvasGroup missing (UI still building?).");
            return;
        }

        bool show = _canvasGroup.alpha < 0.5f;
        _canvasGroup.alpha = show ? 1f : 0f;
        _canvasGroup.interactable = show;
        _canvasGroup.blocksRaycasts = show;
    }

    private void Update()
    {
        if (_label == null)
            return;
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        RefreshText();
    }

    private void RefreshText()
    {
        if (_label == null)
            return;

        if (_tracker == null)
            _tracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (_pathFinder == null)
            _pathFinder = FindFirstObjectByType<ARPathFinder>();

        CultureInfo iv = CultureInfo.InvariantCulture;

        Camera cam = _tracker != null && _tracker.ArCamera != null
            ? _tracker.ArCamera
            : Camera.main;
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

        Transform xr = _tracker != null ? _tracker.xrOrigin : null;
        Vector3 xrPos = xr != null ? xr.position : Vector3.zero;

        float xrDistOriginXz =
            Mathf.Sqrt(xrPos.x * xrPos.x + xrPos.z * xrPos.z);

        Vector3 smoothed = _tracker != null ? _tracker.SmoothedWorldPosition : xrPos;
        float trackerDistOrigin = _tracker != null ? _tracker.DistanceFromMapOriginXZ : xrDistOriginXz;

        bool hasGps = _tracker != null && _tracker.HasFirstFix;
        string poseSourceLine = BuildPoseSourceLine();

        string accText = "---";
        if (_tracker != null)
        {
            if (_tracker.HasLocationFix && _tracker.CurrentHorizontalAccuracy >= 0f)
                accText = string.Format(iv, "{0:F1} m", _tracker.CurrentHorizontalAccuracy);
            else if (_tracker.HasLocationFix)
                accText = "? m";
            else
                accText = "N/A";
        }

        string originJudge;
        float combinedTolerance = originOkRadiusMeters;
        if (_tracker != null && _tracker.HasLocationFix && _tracker.CurrentHorizontalAccuracy > 0f)
            combinedTolerance = originOkRadiusMeters + _tracker.CurrentHorizontalAccuracy * 0.5f;

        if (xr == null || _tracker == null)
            originJudge = "<color=#ffaa77>XR / tracker chua ro</color>";
        else if (!hasGps)
        {
#if UNITY_EDITOR
            originJudge = "<color=#aaccff>EDITOR khong GPS: XR la vi tri scene/simulator -> bo qua canh bao \"goc vat ly\". Test APK ngoai troi moi xac minh (~0).</color>";
#else
            originJudge = "<color=#ffaa77>Chua co GPS fix — mo app ngoai troi roi xem lai distance.</color>";
#endif
        }
        else if (xrDistOriginXz <= combinedTolerance)
            originJudge = $"<color=#88ffaa>OK (-|XZ| XR <= {combinedTolerance:F0} m goc tinh)</color>";
        else
            originJudge = $"<color=#ff8866>CAN XEM LAI (|XZ| XR = {xrDistOriginXz:F1} m >> ~0 tai goc vat ly)</color>";

        Transform dest = _pathFinder != null ? _pathFinder.TargetNode : null;
        string destLine = FormatDestination(dest, iv);

        string gpsDynamicsLine = BuildGpsDynamicsLine(iv, smoothed, trackerDistOrigin, accText, hasGps, xrPos);

        var sb = new StringBuilder(512);
        sb.AppendLine("<b>World debug (GPS outdoor)</b>");
        sb.AppendLine(poseSourceLine);
        sb.AppendLine("<size=22>Camera world: ").Append(FormatVec(camPos, iv)).Append("</size>");
        sb.AppendLine("<size=22>XR Origin: ").Append(FormatVec(xrPos, iv)).Append("</size>");
        sb.AppendLine(gpsDynamicsLine);
        sb.AppendLine("<size=22>Dich (path): ").Append(destLine).Append("</size>");
        sb.AppendLine("<size=22>|XR| XZ ve (0,0): ").AppendFormat(iv, "{0:F2}", xrDistOriginXz)
            .Append(" m — ").Append(originJudge).Append("</size>");
        sb.AppendLine("<size=20><i>Goc vat ly: XR XZ ~ (0,0); camera co the lech nhieu do offset rig AR (khong phai loi GPS).</i></size>");

        _label.text = sb.ToString();
    }

    private static string BuildPoseSourceLine()
    {
        SimpleGPSTracker tracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (tracker == null)
            return "<size=22>Pose: (no tracker)</size>";

        string fix = tracker.HasFirstFix ? "fix OK" : tracker.IsCollectingFirstFixAverage ? "averaging" : "waiting fix";
        string north = tracker.IsNorthAligned ? "north OK" : "aligning north";
        NavigationProximityRefinement refine = FindFirstObjectByType<NavigationProximityRefinement>();
        string snap = refine != null && refine.IsRefinementActive
            ? $" | snap {refine.ActiveRefinementMeters:F1}m"
            : "";
        return "<size=22>Pose: <b>Device GPS</b> | " + fix + " | " + north + snap + "</size>";
    }

    private static string BuildGpsDynamicsLine(
        CultureInfo iv,
        Vector3 smoothedWorld,
        float trackerDistOrigin,
        string accText,
        bool hasGps,
        Vector3 xrScenePos)
    {
        if (!hasGps)
        {
            string xrStr = FormatVec(xrScenePos, iv);
            return "<size=22><color=#ccccaa>GPS (smoothed / dist map): CHUA dong bo — XR tai scene/simulator " +
                   xrStr + "; bien noi bo SimpleGPSTracker van (0,0,0)</color></size>";
        }

        return "<size=22>GPS smoothed XZ y: " + FormatVec(smoothedWorld, iv) +
               " | dist goc(map): " + trackerDistOrigin.ToString("F1", iv) +
               " m | acc " + accText + "</size>";
    }

    private static string FormatDestination(Transform dest, CultureInfo iv)
    {
        if (dest != null)
            return dest.name + "  " + FormatVec(dest.position, iv);

        TargetAnchor[] anchors = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None);
        if (anchors == null || anchors.Length == 0)
            return "(khong tim thay TargetAnchor / pathFinder)";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < anchors.Length; i++)
        {
            TargetAnchor a = anchors[i];
            if (a == null) continue;
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(a.TargetName).Append(": ").Append(FormatVec(a.transform.position, iv));
        }

        return sb.Length > 0 ? sb.ToString() : "(khong co neo)";
    }

    private static string FormatVec(Vector3 v, CultureInfo iv)
    {
        return string.Format(iv, "({0:F2}, {1:F2}, {2:F2})", v.x, v.y, v.z);
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
