using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Heads-Up Display for the GPSMapPlane scene.
///
/// End-user experience: shows target, distance, bearing, GPS badge. No manual action needed.
///
/// Hidden calibration (GPSMapPlane):
///   • Hold top status panel 2s — calibrate at MapOrigin (stand on surveyed origin, GPS +/- &lt;= 5 m).
///   • Hold top status panel 5s — reset GPS calibration.
///   • Hold destination dropdown 2s — calibrate at selected Des (stand on that point).
/// Near Des, NavigationProximityRefinement snaps display position for sub-meter arrival feel.
///
/// Created automatically at runtime for <see cref="GpsOutdoorSceneNames.StandaloneGpsSceneName"/>,
/// or placed under <c>OutdoorNavigationUI</c> for <see cref="GpsOutdoorSceneNames.HybridGpsMapSceneName"/>.
/// </summary>
public class MobileNavigationHUD : MonoBehaviour
{
    public const string HudObjectName   = "Mobile Navigation HUD";
    private const float  AccuracyGood    = 5f;
    private const float  AccuracyPoor    = 12f;
    private const float  ArrivalMeters   = 3f;
    private const float  ToastSeconds    = 3f;
    private const float  CalibratePressSecs = 2f;
    private const float  ResetCalibratePressSecs = 5f;

    [Header("Scene References")]
    public ARPathFinder    pathFinder;
    public SimpleGPSTracker gpsTracker;
    public Transform       userTransform;
    public TargetAnchor[]  targets;

    [Header("UI References")]
    public Text     statusText;
    public Dropdown targetDropdown;
    public Text     toastText;

    [Header("Display")]
    [SerializeField] private int   selectedIndex;
    [SerializeField] private float refreshIntervalSeconds = 0.2f;
    [Tooltip("Extra line Gan Des snap—useful when tuning proximity refinement; hides for cleaner passenger UI.")]
    [SerializeField] private bool  showProximityRefinementHint;
    [Tooltip("Append ARPathFinder.PathHudDebugLine under the status panel (path built / NavMesh / GPS gate).")]
    [SerializeField] private bool  showPathBuildDebugLine = true;

    // --- State ---
    private float nextRefreshTime;
    private bool  wasArrived;
    private float toastEndTime    = -1f;

    // Hidden long-press state (status panel = origin calibrate / reset)
    private bool  _pressingPanel;
    private float _pressStartTime = -1f;
    private bool  _originCalibrateFired;

    // Dropdown panel = calibrate at selected Des
    private bool  _pressingDropdown;
    private float _dropdownPressStartTime = -1f;
    private bool  _desCalibrateFired;

    // ──────────────────────────────────────────────────────────────────────────
    // Runtime auto-creation
    // ──────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGPSMapPlane()
    {
        if (!GpsOutdoorSceneNames.Includes(SceneManager.GetActiveScene().name)) return;
        if (FindFirstObjectByType<MobileNavigationHUD>() != null) return;
        InstantiateOutdoorHudInHierarchy(hudCanvasParent: null, accuracyCircleParent: null);
    }

    /// <summary>
    /// Builds the HUD under an optional parent (hybrid Hierarchy). Accuracy circle optionally parented.
    /// </summary>
    public static MobileNavigationHUD InstantiateOutdoorHudInHierarchy(Transform hudCanvasParent, Transform accuracyCircleParent)
    {
        EnsureRuntimeEventSystem();

        Canvas canvas = CreateRuntimeCanvas(hudCanvasParent);

        RectTransform statusBg;
        Text          statusTxt = CreateRuntimeStatusPanel(canvas.transform, out statusBg);
        Dropdown      dropdown  = CreateRuntimeDropdownPanel(canvas.transform);
        Text          toast     = CreateToastText(canvas.transform);

        MobileNavigationHUD hud = canvas.gameObject.AddComponent<MobileNavigationHUD>();
        hud.statusText     = statusTxt;
        hud.targetDropdown = dropdown;
        hud.toastText      = toast;

        hud.ResolveReferences();
        hud.BuildDropdownOptions();
        dropdown.onValueChanged.AddListener(hud.SelectTarget);
        hud.SelectTarget(0);

        SetupStatusPanelLongPress(statusBg, hud);
        SetupDropdownLongPress(dropdown, hud);

        CreateAccuracyCircle(accuracyCircleParent);
        return hud;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MonoBehaviour lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        ResolveReferences();
        BuildDropdownOptions();
    }

    void OnEnable()
    {
        // HybridGPSMap: HUD often starts inactive under OutdoorNavigationUI — when Outdoor turns on, rebind
        // pathFinder/gpsTracker so we do not keep a stale inactive ARPathFinder (PathHudDebugLine never updates).
        ResolveReferences();

        if (targetDropdown != null)
        {
            EnsureDropdownAlphaFadeSafe(targetDropdown);
            TryInitDropdownTweenRunnerEarly(targetDropdown);
            targetDropdown.onValueChanged.AddListener(SelectTarget);
        }

        if (targets != null && targets.Length > 0 && pathFinder != null)
            SelectTarget(Mathf.Clamp(selectedIndex, 0, targets.Length - 1));
    }

    void Start()
    {
        SelectTarget(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, targets.Length - 1)));
        UpdateStatusText(true);
        if (toastText != null) toastText.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (targetDropdown != null)
            targetDropdown.onValueChanged.RemoveListener(SelectTarget);
    }

    void Update()
    {
        UpdateStatusText(false);
        UpdateToast();
        UpdateStatusPanelLongPress();
        UpdateDropdownLongPress();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    public void SelectTarget(int index)
    {
        if (targets == null || targets.Length == 0) return;

        selectedIndex = Mathf.Clamp(index, 0, targets.Length - 1);
        TargetAnchor sel = targets[selectedIndex];

        if (pathFinder != null && sel != null)
            pathFinder.SetTarget(sel.transform);

        if (targetDropdown != null && targetDropdown.value != selectedIndex)
            targetDropdown.SetValueWithoutNotify(selectedIndex);

        wasArrived = false;
        UpdateStatusText(true);
    }

    // Called from EventTrigger PointerDown on status panel background
    public void OnStatusPanelPressStart()
    {
        _pressingPanel = true;
        _pressStartTime = Time.unscaledTime;
        _originCalibrateFired = false;
    }

    public void OnStatusPanelPressEnd()
    {
        _pressingPanel = false;
        _pressStartTime = -1f;
    }

    public void OnDropdownPanelPressStart()
    {
        _pressingDropdown = true;
        _dropdownPressStartTime = Time.unscaledTime;
        _desCalibrateFired = false;
    }

    public void OnDropdownPanelPressEnd()
    {
        _pressingDropdown = false;
        _dropdownPressStartTime = -1f;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdateStatusPanelLongPress()
    {
        if (!_pressingPanel || _pressStartTime < 0f) return;

        float held = Time.unscaledTime - _pressStartTime;
        if (held >= ResetCalibratePressSecs)
        {
            _pressingPanel = false;
            _pressStartTime = -1f;
            TriggerResetCalibration();
            return;
        }

        if (held >= CalibratePressSecs && !_originCalibrateFired)
        {
            _originCalibrateFired = true;
            TriggerCalibrationAtOrigin();
        }
    }

    private void UpdateDropdownLongPress()
    {
        if (!_pressingDropdown || _dropdownPressStartTime < 0f) return;

        float held = Time.unscaledTime - _dropdownPressStartTime;
        if (held >= CalibratePressSecs && !_desCalibrateFired)
        {
            _desCalibrateFired = true;
            TriggerCalibrationAtSelectedDestination();
        }
    }

    private void TriggerCalibrationAtOrigin()
    {
        if (gpsTracker == null) { ShowToast("GPS tracker not found!"); return; }
        if (!gpsTracker.HasLocationFix) { ShowToast("GPS: no fix yet."); return; }

        float acc = gpsTracker.CurrentHorizontalAccuracy;
        float maxCal = gpsTracker.CalibrateMaxAccuracyMeters;
        if (acc > maxCal)
        {
            ShowToast($"GPS yeu (+/-{acc:F0}m). Can <= {maxCal:F0}m. Dung yen ngoai troi.");
            return;
        }

        if (!gpsTracker.CalibrateAtOrigin())
        {
            ShowToast("Calibrate goc that bai.");
            return;
        }

        RecalculateAllAnchors();
        ShowToast($"[CAL GOC] OK (+/-{acc:F0}m). Doi GPS on dinh...");
        UpdateStatusText(true);
    }

    private void TriggerCalibrationAtSelectedDestination()
    {
        if (gpsTracker == null) { ShowToast("GPS tracker not found!"); return; }
        TargetAnchor sel = GetSelectedTarget();
        if (sel == null) { ShowToast("Chua chon diem den."); return; }
        if (!gpsTracker.HasLocationFix) { ShowToast("GPS: no fix yet."); return; }

        float acc = gpsTracker.CurrentHorizontalAccuracy;
        float maxCal = gpsTracker.CalibrateMaxAccuracyMeters;
        if (acc > maxCal)
        {
            ShowToast($"GPS yeu (+/-{acc:F0}m). Dung tai {sel.TargetName}, cho +/- tot hon.");
            return;
        }

        if (!gpsTracker.CalibrateAtSurveyedPoint(sel.targetLat, sel.targetLon))
        {
            ShowToast("Calibrate tai Des that bai.");
            return;
        }

        RecalculateAllAnchors();
        ShowToast($"[CAL {sel.TargetName}] OK (+/-{acc:F0}m)");
        UpdateStatusText(true);
    }

    private void TriggerResetCalibration()
    {
        if (gpsTracker == null) { ShowToast("GPS tracker not found!"); return; }
        gpsTracker.ResetCalibration();
        RecalculateAllAnchors();
        ShowToast("[CAL] Da xoa hieu chinh GPS.");
        UpdateStatusText(true);
    }

    private void RecalculateAllAnchors()
    {
        if (targets == null) return;
        foreach (TargetAnchor anchor in targets)
        {
            if (anchor != null)
                anchor.Recalculate();
        }
    }

    private void ResolveReferences()
    {
        GameObject outdoorRoot = GameObject.Find("OutdoorEnvironment");
        ARPathFinder outdoorPath = null;
        if (outdoorRoot != null)
            outdoorPath = outdoorRoot.GetComponentInChildren<ARPathFinder>(false);

        if (outdoorPath != null)
        {
            pathFinder = outdoorPath;
        }
        else if (pathFinder == null ||
                 (outdoorRoot != null && pathFinder != null &&
                  !pathFinder.transform.IsChildOf(outdoorRoot.transform)))
        {
            // Hybrid scene mis-wired or GPSMapPlane / no outdoor: take any active ARPathFinder
            pathFinder = FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Exclude);
        }

        if (gpsTracker == null || !gpsTracker.isActiveAndEnabled)
            gpsTracker = FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Exclude);
        if (userTransform == null && gpsTracker != null) userTransform = gpsTracker.xrOrigin;

        if (targets == null || targets.Length == 0)
        {
            targets = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None)
                .OrderBy(t => t.gameObject.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    private void BuildDropdownOptions()
    {
        if (targetDropdown == null || targets == null) return;
        targetDropdown.ClearOptions();
        targetDropdown.AddOptions(
            targets.Select((t, i) => t != null ? $"{i + 1}. {t.TargetName}" : "Missing Target").ToList());
        targetDropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, targets.Length - 1)));
        targetDropdown.RefreshShownValue();
    }

    private void ShowToast(string msg)
    {
        toastEndTime = Time.unscaledTime + ToastSeconds;
        if (toastText != null)
        {
            toastText.text = msg;
            toastText.gameObject.SetActive(true);
        }
    }

    private void UpdateToast()
    {
        if (toastText == null || toastEndTime < 0f) return;
        if (Time.unscaledTime >= toastEndTime)
        {
            toastText.gameObject.SetActive(false);
            toastEndTime = -1f;
        }
    }

    private void UpdateStatusText(bool force)
    {
        if (statusText == null) return;
        if (!force && Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;

        TargetAnchor sel     = GetSelectedTarget();
        string targetName    = sel != null ? sel.TargetName : "None";
        float  dist          = GetDistanceMeters(sel);
        string distText      = dist >= 0f ? $"{dist:F0} m" : "--";
        string bearingText   = BuildBearingText(sel);
        bool   arrived       = sel != null && userTransform != null && dist >= 0f && dist < ArrivalMeters;
        string gpsLine       = BuildGpsLine();
        string refineLine    = showProximityRefinementHint ? ("\n" + BuildRefinementLine()) : "";
        string pathDbgLine   = string.Empty;
        if (showPathBuildDebugLine && pathFinder != null)
        {
            string raw = pathFinder.PathHudDebugLine;
            if (!pathFinder.isActiveAndEnabled)
            {
                raw = "Path: ARPathFinder khong chay (GO/Component OFF) — bat GO hoac HybridOutdoorNavigationRoot";
            }
            else if (string.IsNullOrEmpty(raw))
            {
                raw = "Path: (chua cap nhat — doi frame tiep theo)";
            }

            pathDbgLine = "\n<size=26><color=#ffcc66>" + raw + "</color></size>";
        }

        if (arrived)
        {
            wasArrived = true;
            statusText.text = $"<b>>>> DA DEN NOI! <<<</b>\n<b>{targetName}</b>  ({distText})\n{gpsLine}{refineLine}{pathDbgLine}";
        }
        else
        {
            wasArrived = false;
            statusText.text =
                $"<b>Diem den:</b>  {targetName}\n" +
                $"<b>Khoang cach:</b>  {distText}  {bearingText}\n" +
                $"{gpsLine}{refineLine}{pathDbgLine}";
        }
    }

    private string BuildRefinementLine()
    {
        NavigationProximityRefinement refine = FindFirstObjectByType<NavigationProximityRefinement>();
        if (refine == null || !refine.IsRefinementActive)
            return "<size=28><color=#aaaaaa>Gan Des: chua snap</color></size>";

        return $"<size=28><color=#88ffcc>Gan Des: snap ~{refine.ActiveRefinementMeters:F1}m</color></size>";
    }

    private TargetAnchor GetSelectedTarget()
    {
        if (targets == null || targets.Length == 0) return null;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, targets.Length - 1);
        return targets[selectedIndex];
    }

    private float GetDistanceMeters(TargetAnchor target)
    {
        if (target == null || userTransform == null) return -1f;
        if (pathFinder != null && pathFinder.TargetNode == target.transform && pathFinder.CurrentPathDistanceMeters > 0f)
            return pathFinder.CurrentPathDistanceMeters;
        return Vector3.Distance(userTransform.position, target.transform.position);
    }

    private string BuildBearingText(TargetAnchor target)
    {
        if (target == null || userTransform == null) return string.Empty;
        Vector3 dir = target.transform.position - userTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return string.Empty;
        float bearing = (Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 360f) % 360f;
        return $"[{BearingToCardinal(bearing)} {bearing:F0}deg]";
    }

    private string BuildGpsLine()
    {
        if (gpsTracker == null) return "GPS: --";

        if (!gpsTracker.HasLocationFix) return $"GPS: {gpsTracker.CurrentStatus}...";

        float  acc   = gpsTracker.CurrentHorizontalAccuracy;
        string badge = acc <= AccuracyGood ? "[TOT]" : acc <= AccuracyPoor ? "[TB]" : "[YEU]";
        string cal   = gpsTracker.HasCalibration ? " [CAL]" : "";
        string lat   = gpsTracker.CurrentLatitude.ToString("F6",  CultureInfo.InvariantCulture);
        string lon   = gpsTracker.CurrentLongitude.ToString("F6", CultureInfo.InvariantCulture);
        string line  = $"GPS {badge}{cal} +/-{acc:F0}m  |  {lat}, {lon}";

        if (pathFinder != null && pathFinder.IsNavigationPathBlockedByGpsGate &&
            gpsTracker.TryGetPathNavigationBlock(out string code))
        {
            line += "\n<size=28><color=#ffaa77>" +
                    FormatPathGateHint(code, gpsTracker.LastRejectedJumpMeters, gpsTracker.DistanceFromMapOriginXZ) +
                    "</color></size>";
        }

        return line;
    }

    private static string FormatPathGateHint(string code, float rejectJumpMeters, float distanceFromOrigin)
    {
        switch (code)
        {
            case "no_origin":
                return "Duong tam an — chua gan MapOrigin";
            case "no_location_fix":
                return "Duong tam an — chua ket noi GPS";
            case "no_first_fix":
                return "Duong tam an — doi fix GPS dau";
            case "north_pending":
                return "Duong tam an — dang canh huong Bac";
            case "gps_jump":
                if (rejectJumpMeters >= 0f)
                    return $"Duong tam an — GPS nhay {(int)rejectJumpMeters}m";
                return "Duong tam an — GPS bat thuong (jump)";
            case "off_map_bounds":
                return $"Duong tam an — xa goc ban do ({distanceFromOrigin:F0}m)";
            default:
                return "Duong tam an — GPS khong tin cay";
        }
    }

    private static string BearingToCardinal(float deg)
    {
        string[] d = { "B", "DB", "D", "DN", "N", "TN", "T", "TB" };
        return d[Mathf.RoundToInt(deg / 45f) % 8];
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Runtime UI construction
    // ──────────────────────────────────────────────────────────────────────────

    private static Canvas CreateRuntimeCanvas(Transform parent)
    {
        GameObject go = new GameObject(HudObjectName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        Canvas c = go.GetComponent<Canvas>();
        c.renderMode  = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        CanvasScaler s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1080f, 1920f);
        s.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        s.matchWidthOrHeight  = 0.5f;
        return c;
    }

    /// <summary>Creates the top status panel and outputs its background RectTransform for long-press wiring.</summary>
    private static Text CreateRuntimeStatusPanel(Transform parent, out RectTransform panelBg)
    {
        panelBg = CreatePanel(parent, "Status Panel", new Color(0.04f, 0.06f, 0.10f, 0.82f));
        panelBg.anchorMin = new Vector2(0f, 1f);
        panelBg.anchorMax = new Vector2(1f, 1f);
        panelBg.pivot     = new Vector2(0.5f, 1f);
        panelBg.anchoredPosition = new Vector2(0f, -28f);
        panelBg.sizeDelta        = new Vector2(-48f, 172f);

        // Accent bar — left edge
        RectTransform accent = CreatePanel(panelBg, "Accent Bar", new Color(0.18f, 0.72f, 1f, 1f));
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot     = new Vector2(0f, 0.5f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta        = new Vector2(6f, -16f);

        // Status text — full width now (no button on right)
        GameObject textGO = new GameObject("Status Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGO.transform.SetParent(panelBg, false);

        RectTransform tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(24f, 16f);
        tr.offsetMax = new Vector2(-16f, -16f);

        Text text = textGO.GetComponent<Text>();
        text.font               = GetDefaultFont();
        text.fontSize           = 36;
        text.alignment          = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow   = VerticalWrapMode.Overflow;
        text.color              = Color.white;
        text.supportRichText    = true;
        text.lineSpacing        = 1.2f;
        text.text               = "<b>Diem den:</b>  --\n<b>Khoang cach:</b>  --\nGPS: Dang ket noi...";

        return text;
    }

    public static void SetupStatusPanelLongPress(RectTransform panelBg, MobileNavigationHUD hud)
    {
        EventTrigger trigger = panelBg.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => hud.OnStatusPanelPressStart());
        trigger.triggers.Add(down);

        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => hud.OnStatusPanelPressEnd());
        trigger.triggers.Add(up);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => hud.OnStatusPanelPressEnd());
        trigger.triggers.Add(exit);
    }

    /// <summary>Hold dropdown area 2s — calibrate GPS at selected Des surveyed coordinates.</summary>
    public static void SetupDropdownLongPress(Dropdown dropdown, MobileNavigationHUD hud)
    {
        if (dropdown == null) return;
        RectTransform rt = dropdown.GetComponent<RectTransform>();
        if (rt == null) return;

        EventTrigger trigger = rt.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => hud.OnDropdownPanelPressStart());
        trigger.triggers.Add(down);

        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => hud.OnDropdownPanelPressEnd());
        trigger.triggers.Add(up);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => hud.OnDropdownPanelPressEnd());
        trigger.triggers.Add(exit);
    }

    private static Text CreateToastText(Transform parent)
    {
        GameObject go = new GameObject("Toast",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.SetActive(false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(-80f, 100f);

        Text t = go.GetComponent<Text>();
        t.font               = GetDefaultFont();
        t.fontSize           = 40;
        t.fontStyle          = FontStyle.Bold;
        t.alignment          = TextAnchor.MiddleCenter;
        t.color              = new Color(0.3f, 1f, 0.5f, 1f);
        t.supportRichText    = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;

        return t;
    }

    private static Dropdown CreateRuntimeDropdownPanel(Transform parent)
    {
        RectTransform panel = CreatePanel(parent, "Target Dropdown Panel", new Color(0.04f, 0.06f, 0.10f, 0.88f));
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot     = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 36f);
        panel.sizeDelta        = new Vector2(-48f, 196f);

        // Section label
        GameObject labelGO = new GameObject("Nav Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGO.transform.SetParent(panel, false);
        RectTransform lr = labelGO.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -10f);
        lr.sizeDelta = new Vector2(-32f, 52f);
        Text lt = labelGO.GetComponent<Text>();
        lt.font = GetDefaultFont(); lt.fontSize = 28; lt.fontStyle = FontStyle.Bold;
        lt.alignment = TextAnchor.MiddleCenter;
        lt.color = new Color(0.55f, 0.85f, 1f, 1f);
        lt.text = "DIEM DEN";

        // Separator
        RectTransform sep = CreatePanel(panel, "Separator", new Color(0.18f, 0.72f, 1f, 0.4f));
        sep.anchorMin = new Vector2(0f, 1f); sep.anchorMax = new Vector2(1f, 1f);
        sep.pivot = new Vector2(0.5f, 1f);
        sep.anchoredPosition = new Vector2(0f, -62f);
        sep.sizeDelta = new Vector2(-32f, 2f);

        // Dropdown
        DefaultControls.Resources res = new DefaultControls.Resources();
        GameObject ddGO = DefaultControls.CreateDropdown(res);
        ddGO.name = "Target Dropdown";
        ddGO.transform.SetParent(panel, false);

        Dropdown dd = ddGO.GetComponent<Dropdown>();
        RectTransform ddr = dd.GetComponent<RectTransform>();
        ddr.anchorMin = new Vector2(0f, 0f); ddr.anchorMax = new Vector2(1f, 0f);
        ddr.pivot = new Vector2(0.5f, 0f);
        ddr.anchoredPosition = new Vector2(0f, 14f);
        ddr.sizeDelta = new Vector2(-32f, 96f);

        Image ddImg = dd.GetComponent<Image>();
        if (ddImg != null) ddImg.color = new Color(0.10f, 0.16f, 0.26f, 1f);

        foreach (Text t in dd.GetComponentsInChildren<Text>(true))
        { t.font = GetDefaultFont(); t.fontSize = 36; t.color = Color.white; t.alignment = TextAnchor.MiddleLeft; }

        if (dd.captionText != null)
        {
            dd.captionText.fontSize = 38; dd.captionText.fontStyle = FontStyle.Bold;
            RectTransform cr = dd.captionText.GetComponent<RectTransform>();
            if (cr != null) cr.offsetMin = new Vector2(20f, cr.offsetMin.y);
        }

        if (dd.template != null)
        {
            EnsureDropdownAlphaFadeSafe(dd);
            dd.template.sizeDelta = new Vector2(dd.template.sizeDelta.x, 240f);
            Image ti = dd.template.GetComponent<Image>();
            if (ti != null) ti.color = new Color(0.08f, 0.13f, 0.22f, 0.97f);

            Toggle item = dd.template.GetComponentInChildren<Toggle>(true);
            if (item != null)
            {
                RectTransform ir = item.GetComponent<RectTransform>();
                if (ir != null) ir.sizeDelta = new Vector2(ir.sizeDelta.x, 80f);

                Image ib = item.GetComponent<Image>();
                if (ib != null) ib.color = new Color(0.10f, 0.16f, 0.26f, 1f);

                ColorBlock cb = item.colors;
                cb.normalColor = new Color(0.10f, 0.16f, 0.26f, 1f);
                cb.highlightedColor = new Color(0.18f, 0.30f, 0.50f, 1f);
                cb.selectedColor    = new Color(0.14f, 0.50f, 0.80f, 1f);
                cb.pressedColor     = new Color(0.10f, 0.40f, 0.70f, 1f);
                item.colors = cb;

                Text il = item.GetComponentInChildren<Text>(true);
                if (il != null)
                {
                    il.fontSize = 36; il.color = Color.white; il.alignment = TextAnchor.MiddleLeft;
                    RectTransform ilr = il.GetComponent<RectTransform>();
                    if (ilr != null) ilr.offsetMin = new Vector2(20f, ilr.offsetMin.y);
                }
            }
        }

        TryInitDropdownTweenRunnerEarly(dd);
        return dd;
    }

    /// <summary>
    /// <see cref="Dropdown"/> fades the list via <c>CanvasGroup</c> on the popup. Missing component ⇒ <c>AlphaFadeList</c> NRE.
    /// </summary>
    private static void EnsureDropdownAlphaFadeSafe(Dropdown dd)
    {
        if (dd == null || dd.template == null) return;
        if (dd.template.GetComponent<CanvasGroup>() == null)
            dd.template.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// Unity initialises the internal alpha tween runner in <c>Dropdown.Start</c>. Under HybridGPSMap the HUD lives under
    /// <see cref="HybridOutdoorNavigationRoot"/> and starts inactive — a fast tap can still race in edge builds; warmup avoids null runner.
    /// </summary>
    private static void TryInitDropdownTweenRunnerEarly(Dropdown dd)
    {
        if (dd == null || !Application.isPlaying) return;

        FieldInfo field = typeof(Dropdown).GetField(
            "m_AlphaTweenRunner",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return;
        if (field.GetValue(dd) != null) return;

        try
        {
            object runner = Activator.CreateInstance(field.FieldType);
            MethodInfo init = field.FieldType.GetMethod(
                "Init",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (init != null)
                init.Invoke(runner, new object[] { dd });

            field.SetValue(dd, runner);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[MobileNavigationHUD] Dropdown tween runner warmup failed (Unity UI version mismatch?): " + e.Message);
        }
    }

    /// <summary>Creates the 3D GPS accuracy circle ring in the scene.</summary>
    private static void CreateAccuracyCircle(Transform parentOrNull)
    {
        GameObject go = new GameObject("GPS Accuracy Circle", typeof(LineRenderer), typeof(GPSAccuracyCircle));
        if (parentOrNull != null)
        {
            go.transform.SetParent(parentOrNull, false);
        }

        // gpsTracker reference resolved in GPSAccuracyCircle.Start()
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    public static void EnsureRuntimeEventSystem()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return;
        }
        InputSystemUIInputModule m = es.GetComponent<InputSystemUIInputModule>();
        if (m == null) m = es.gameObject.AddComponent<InputSystemUIInputModule>();
        m.AssignDefaultActions();
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
