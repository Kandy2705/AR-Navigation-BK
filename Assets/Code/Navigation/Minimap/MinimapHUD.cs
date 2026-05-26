using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Circular minimap shown at the top-right corner of the AR view.
///
/// Displays:
///   - Player dot at center (real-time camera position)
///   - Destination anchors as colored dots with distance labels
///   - GPS XR Origin cross (shows where GPS thinks the player is vs. camera)
///   - North "N" indicator at the top of the circle
///   - Scale ring for reference
///
/// Useful for diagnosing GPS anchor drift: if the GPS cross drifts away from
/// the camera center, it means GPS and ARCore tracking have diverged.
/// </summary>
public class MinimapHUD : MonoBehaviour
{
    // ── Configuration ─────────────────────────────────────────────────────────
    [Header("Minimap Size")]
    [Tooltip("Radius of the minimap circle on screen (pixels at 1080×1920 reference).")]
    [SerializeField] private float mapRadiusPx  = 72f;
    [Tooltip("World distance (meters) represented by the full map radius.")]
    [SerializeField] private float worldRadiusM = 50f;

    [Header("Dot Sizes (pixels)")]
    [SerializeField] private float destDotRadius   = 8f;
    [SerializeField] private float playerDotRadius = 6f;
    [SerializeField] private float gpsDotRadius    = 5f;

    // ── Runtime references ─────────────────────────────────────────────────────
    private SimpleGPSTracker   _gpsTracker;
    private Camera             _arCamera;
    private TargetAnchor[]     _anchors;

    // ── UI elements ────────────────────────────────────────────────────────────
    private Canvas                                _canvas;
    private RectTransform                         _mapRoot;      // clipping container
    private RectTransform                         _playerDot;    // white center dot
    private RectTransform                         _gpsDot;       // yellow cross = GPS XR Origin
    private RectTransform                         _headingLine;  // thin line = camera facing
    private RectTransform                         _northLabel;   // "N" text
    private List<(RectTransform dot, Text label)> _destDots = new List<(RectTransform, Text)>();

    // ──────────────────────────────────────────────────────────────────────────
    // Auto-create
    // ──────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForGPSMapPlane()
    {
        if (!GpsOutdoorSceneNames.Includes(SceneManager.GetActiveScene().name)) return;
        if (FindFirstObjectByType<MinimapHUD>() != null) return;

        // If the scene already has a top-down minimap camera, skip the 2D icon minimap.
        if (FindFirstObjectByType<MinimapTopDownCamera>() != null) return;

        GameObject go = new GameObject("Minimap HUD");
        go.AddComponent<MinimapHUD>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        _gpsTracker = FindFirstObjectByType<SimpleGPSTracker>();
        _arCamera   = Camera.main;
        _anchors    = FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None);
        BuildUI();
    }

    void Update()
    {
        if (_mapRoot == null) return;
        UpdatePlayerDot();
        UpdateGpsDot();
        UpdateHeadingLine();
        UpdateDestDots();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // UI Construction
    // ──────────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Always create a dedicated canvas so the minimap is never hidden by other UI
        GameObject cGO = new GameObject("Minimap Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cGO.transform.SetParent(transform, false);
        _canvas = cGO.GetComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;   // on top of all other UI
        CanvasScaler scaler = cGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;

        Transform canvasT = _canvas.transform;

        // ── Map background (dark circle) ───────────────────────────────────────
        GameObject bgGO = CreateImageGO("Minimap BG", canvasT,
            MakeCircleTex(128, new Color(0.05f, 0.07f, 0.12f, 0.82f)));
        _mapRoot = bgGO.GetComponent<RectTransform>();
        _mapRoot.anchorMin = new Vector2(1f, 1f);
        _mapRoot.anchorMax = new Vector2(1f, 1f);
        _mapRoot.pivot     = new Vector2(1f, 1f);
        float d = mapRadiusPx * 2f;
        _mapRoot.sizeDelta        = new Vector2(d, d);
        _mapRoot.anchoredPosition = new Vector2(-20f, -20f); // 20px margin from top-right

        // ── Scale ring (faint circle outline, half radius = 15m reference) ────
        GameObject ringGO = CreateImageGO("Scale Ring", _mapRoot.transform,
            MakeRingTex(128, new Color(1f, 1f, 1f, 0.12f), 0.48f, 0.50f));
        RectTransform ringRt = ringGO.GetComponent<RectTransform>();
        ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
        ringRt.pivot     = new Vector2(0.5f, 0.5f);
        ringRt.sizeDelta = new Vector2(d, d); // same as map = worldRadiusM ring

        // ── Outer border ring ──────────────────────────────────────────────────
        GameObject borderGO = CreateImageGO("Border Ring", _mapRoot.transform,
            MakeRingTex(128, new Color(0.3f, 0.6f, 1f, 0.55f), 0.47f, 0.50f));
        RectTransform borderRt = borderGO.GetComponent<RectTransform>();
        borderRt.anchorMin = borderRt.anchorMax = new Vector2(0.5f, 0.5f);
        borderRt.pivot     = new Vector2(0.5f, 0.5f);
        borderRt.sizeDelta = new Vector2(d, d);

        // ── North label ────────────────────────────────────────────────────────
        GameObject northGO = new GameObject("North Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        northGO.transform.SetParent(_mapRoot.transform, false);
        _northLabel = northGO.GetComponent<RectTransform>();
        _northLabel.anchorMin = _northLabel.anchorMax = new Vector2(0.5f, 1f);
        _northLabel.pivot     = new Vector2(0.5f, 1f);
        _northLabel.anchoredPosition = new Vector2(0f, -4f);
        _northLabel.sizeDelta        = new Vector2(30f, 22f);
        Text northTxt = northGO.GetComponent<Text>();
        northTxt.text      = "N";
        northTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        northTxt.fontSize  = 18;
        northTxt.fontStyle = FontStyle.Bold;
        northTxt.alignment = TextAnchor.UpperCenter;
        northTxt.color     = new Color(1f, 0.85f, 0.3f, 0.9f);

        // ── Heading line (player facing direction) ─────────────────────────────
        GameObject hlineGO = CreateImageGO("Heading Line", _mapRoot.transform,
            MakeSolidTex(new Color(1f, 1f, 1f, 0.7f)));
        _headingLine = hlineGO.GetComponent<RectTransform>();
        _headingLine.anchorMin = _headingLine.anchorMax = new Vector2(0.5f, 0.5f);
        _headingLine.pivot     = new Vector2(0.5f, 0f);   // pivot at bottom = player center
        _headingLine.sizeDelta = new Vector2(2f, mapRadiusPx * 0.5f);
        _headingLine.anchoredPosition = Vector2.zero;

        // ── GPS origin dot (yellow cross shape = where GPS places XR Origin) ───
        _gpsDot = CreateDot("GPS Dot", _mapRoot.transform,
            new Color(1f, 0.9f, 0.1f, 0.8f), gpsDotRadius * 2f);

        // ── Destination dots (created per anchor) ─────────────────────────────
        Color[] destColors = {
            new Color(1.0f, 0.25f, 0.25f, 1f),  // red
            new Color(0.25f, 0.8f, 1.0f, 1f),   // cyan
            new Color(0.4f, 1.0f, 0.4f, 1f),    // green
            new Color(1.0f, 0.6f, 0.1f, 1f),    // orange
        };

        if (_anchors != null)
        {
            for (int i = 0; i < _anchors.Length; i++)
            {
                Color col = destColors[i % destColors.Length];
                RectTransform dot = CreateDot($"Dest {i}", _mapRoot.transform, col, destDotRadius * 2f);

                // Distance label below dot
                GameObject lblGO = new GameObject($"Dest {i} Label",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGO.transform.SetParent(dot, false);
                RectTransform lblRt = lblGO.GetComponent<RectTransform>();
                lblRt.anchorMin = lblRt.anchorMax = new Vector2(0.5f, 0f);
                lblRt.pivot     = new Vector2(0.5f, 1f);
                lblRt.anchoredPosition = new Vector2(0f, -2f);
                lblRt.sizeDelta        = new Vector2(50f, 16f);
                Text lbl = lblGO.GetComponent<Text>();
                lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.fontSize  = 13;
                lbl.alignment = TextAnchor.UpperCenter;
                lbl.color     = col;

                _destDots.Add((dot, lbl));
            }
        }

        // ── Player dot (white, always at center, drawn on top) ────────────────
        _playerDot = CreateDot("Player Dot", _mapRoot.transform,
            Color.white, playerDotRadius * 2f);
        _playerDot.anchoredPosition = Vector2.zero;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void UpdatePlayerDot()
    {
        // Player dot is always center — no movement needed
    }

    private void UpdateGpsDot()
    {
        if (_gpsDot == null || _gpsTracker == null || _arCamera == null) return;

        // Offset = where GPS thinks player is vs. where camera actually is
        Vector3 gpsPos    = _gpsTracker.SmoothedWorldPosition;
        Vector3 cameraPos = _arCamera.transform.position;
        Vector2 offset    = WorldToMinimap(gpsPos.x - cameraPos.x, gpsPos.z - cameraPos.z);
        _gpsDot.anchoredPosition = offset;
    }

    private void UpdateHeadingLine()
    {
        if (_headingLine == null || _arCamera == null) return;

        // Rotate heading line to show camera facing direction (North-up map)
        float yaw = _arCamera.transform.eulerAngles.y; // degrees CW from Unity +Z = North
        _headingLine.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }

    private void UpdateDestDots()
    {
        if (_anchors == null || _arCamera == null) return;

        Vector3 cameraPos = _arCamera.transform.position;

        for (int i = 0; i < _destDots.Count && i < _anchors.Length; i++)
        {
            if (_anchors[i] == null) continue;

            Vector3 destPos = _anchors[i].transform.position;
            float dx = destPos.x - cameraPos.x;
            float dz = destPos.z - cameraPos.z;
            float distM = Mathf.Sqrt(dx * dx + dz * dz);

            Vector2 mapPos = WorldToMinimap(dx, dz);

            // Clamp to inside the circle if too far
            if (mapPos.magnitude > mapRadiusPx - destDotRadius)
                mapPos = mapPos.normalized * (mapRadiusPx - destDotRadius);

            _destDots[i].dot.anchoredPosition = mapPos;

            // Show distance label
            _destDots[i].label.text = distM < 1000f
                ? $"{distM:F0}m"
                : $"{distM / 1000f:F1}k";

            // Hide if very far (> 2× world radius)
            bool visible = distM <= worldRadiusM * 2f;
            _destDots[i].dot.gameObject.SetActive(visible);
        }
    }

    // Converts (deltaX_east, deltaZ_north) world offsets → minimap pixel position
    // North-up: +Z world → up on map (+Y pixels), +X world → right on map (+X pixels)
    private Vector2 WorldToMinimap(float deltaX, float deltaZ)
    {
        float scale = mapRadiusPx / worldRadiusM;
        return new Vector2(deltaX * scale, deltaZ * scale);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // UI factory helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static RectTransform CreateDot(string name, Transform parent, Color color, float sizePx)
    {
        GameObject go = CreateImageGO(name, parent, MakeCircleTex(32, color));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin          = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot              = new Vector2(0.5f, 0.5f);
        rt.sizeDelta          = new Vector2(sizePx, sizePx);
        rt.anchoredPosition   = Vector2.zero;
        return rt;
    }

    private static GameObject CreateImageGO(string name, Transform parent, Texture2D tex)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));
        img.type = Image.Type.Simple;
        return go;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Texture generators
    // ──────────────────────────────────────────────────────────────────────────

    private static Texture2D MakeCircleTex(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size / 2f;
        float r2 = r * r;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                // Anti-alias at edge
                float d2 = dx * dx + dy * dy;
                float alpha = Mathf.Clamp01((r2 - d2) / (r * 2f));
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeRingTex(int size, Color color, float innerFrac, float outerFrac)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r      = size / 2f;
        float inner2 = (r * innerFrac * 2) * (r * innerFrac * 2);
        float outer2 = (r * outerFrac * 2) * (r * outerFrac * 2);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d2 = dx * dx + dy * dy;
                bool inRing = d2 >= inner2 && d2 <= outer2;
                tex.SetPixel(x, y, inRing ? color : Color.clear);
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeSolidTex(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
