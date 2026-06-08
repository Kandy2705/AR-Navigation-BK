using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Draws a heading arrow on the circular top-down minimap (world north-up).
/// Rotates with the AR / main camera yaw so you see which way you face on the map.
/// Host is <c>Minimap Circle Mask</c> (GPSMapPlane-style) or <c>Minimap</c> with <see cref="RawImage"/> (ManScene / Hybrid Navigation).
/// </summary>
[DisallowMultipleComponent]
public class MinimapHeadingIndicator : MonoBehaviour
{
    private static bool _warnedNoCamera;

    [Header("References (optional)")]
    [SerializeField] private Camera headingCamera;
    [SerializeField] private SimpleGPSTracker gpsTracker;

    [Header("Arrow look")]
    [SerializeField] private float arrowWidthPx = 7f;
    [SerializeField] [Range(0.12f, 0.55f)] private float arrowLengthFractionOfRadius = 0.3f;
    [SerializeField] private Color arrowColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color arrowHeadColor = new Color(0.35f, 0.85f, 1f, 0.95f);

    [Header("Smoothing (chống rung mũi tên)")]
    [Tooltip("Tốc độ lerp yaw (deg/giây tương đối). 0 = không smooth (rung theo IMU noise). 8 = mượt vừa. 15 = phản hồi nhanh hơn nhưng kém mượt.")]
    [SerializeField] [Range(0f, 20f)] private float yawLerpSpeed = 8f;

    private RectTransform _arrowRoot;
    private RectTransform _maskRect;
    private float _smoothedYaw;
    private bool _hasSmoothedYaw;

    private static Sprite _cachedTriangleSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForGPSMapPlane()
    {
        TryEnsureForActiveScene();
    }

    /// <summary>
    /// Adds <see cref="MinimapHeadingIndicator"/> on the minimap host if missing.
    /// Safe when outdoor UI was inactive at load (<see cref="HybridOutdoorNavigationRoot"/>) — searches inactive objects, not <see cref="GameObject.Find"/> alone.
    /// </summary>
    public static void TryEnsureForActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!GpsOutdoorSceneNames.ShouldAutoSpawnMinimapHeadingIndicator(scene.name)) return;

        GameObject host = FindMinimapHeadingHost(scene);
        if (host == null) return;

        if (host.GetComponentInChildren<MinimapHeadingIndicator>(true) != null) return;

        host.AddComponent<MinimapHeadingIndicator>();
    }

    /// <summary>GPSMapPlane uses a masked circle; ManScene / Hybrid Navigation use a flat RawImage named Minimap.</summary>
    private static GameObject FindMinimapHeadingHost(Scene scene)
    {
        GameObject circleMask = GameObject.Find("Minimap Circle Mask");
        if (circleMask != null && circleMask.scene == scene) return circleMask;

        foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt == null || rt.gameObject.scene != scene) continue;
            if (rt.name != "Minimap Circle Mask") continue;
            return rt.gameObject;
        }

        GameObject minimap = GameObject.Find("Minimap");
        if (minimap != null && minimap.scene == scene && minimap.GetComponent<RawImage>() != null)
            return minimap;

        foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt == null || rt.gameObject.scene != scene) continue;
            if (rt.name != "Minimap") continue;
            if (rt.GetComponent<RawImage>() == null) continue;
            return rt.gameObject;
        }

        // Top-down minimap from SetupMinimapTopDown: RawImage may be "Minimap View" under a mask — use circle mask if found.
        foreach (MinimapRawViewFill fill in FindObjectsByType<MinimapRawViewFill>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (fill == null || fill.gameObject.scene != scene) continue;
            Transform walk = fill.transform;
            for (int i = 0; i < 8 && walk != null; i++)
            {
                if (walk.name == "Minimap Circle Mask")
                {
                    return walk.gameObject;
                }

                walk = walk.parent;
            }
        }

        return null;
    }

    private void Awake()
    {
        _maskRect = transform as RectTransform;
        if (_maskRect == null) _maskRect = GetComponentInParent<RectTransform>();
    }

    private IEnumerator Start()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        EnsureUiBuilt();
        PlaceAboveMinimapView();
        if (_arrowRoot != null)
            _arrowRoot.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        if (_arrowRoot == null) return;

        float yaw = headingCamera != null ? headingCamera.transform.eulerAngles.y : 0f;

        if (yawLerpSpeed <= 0f || !_hasSmoothedYaw)
        {
            _smoothedYaw = yaw;
            _hasSmoothedYaw = true;
        }
        else
        {
            _smoothedYaw = Mathf.LerpAngle(_smoothedYaw, yaw, yawLerpSpeed * Time.deltaTime);
        }

        _arrowRoot.localRotation = Quaternion.Euler(0f, 0f, -_smoothedYaw);
    }

    private void PlaceAboveMinimapView()
    {
        if (_arrowRoot == null || _maskRect == null) return;
        _arrowRoot.SetAsLastSibling();
    }

    private void ResolveReferences()
    {
        if (gpsTracker == null) gpsTracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (headingCamera == null && gpsTracker != null) headingCamera = gpsTracker.ArCamera;
        if (headingCamera == null) headingCamera = Camera.main;

        if (headingCamera == null)
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (cam.CompareTag("MainCamera"))
                {
                    headingCamera = cam;
                    break;
                }
            }
        }

        if (headingCamera == null)
        {
            GameObject xr = GameObject.Find("XR Origin");
            if (xr != null)
                headingCamera = xr.GetComponentInChildren<Camera>();
        }

#if UNITY_EDITOR
        if (headingCamera == null && !_warnedNoCamera)
        {
            _warnedNoCamera = true;
            Debug.LogWarning(
                "[MinimapHeadingIndicator] No camera for heading — arrow points north-only. " +
                "Assign Ar Camera on SimpleGPSTracker or add a MainCamera-tagged AR camera.");
        }
#endif
    }

    private void EnsureUiBuilt()
    {
        if (_arrowRoot != null) return;
        Transform parent = _maskRect != null ? _maskRect.transform : transform;

        GameObject root = new GameObject("Heading Indicator",
            typeof(RectTransform));
        root.transform.SetParent(parent, false);
        _arrowRoot = root.GetComponent<RectTransform>();
        _arrowRoot.anchorMin = _arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _arrowRoot.pivot = new Vector2(0.5f, 0f);
        _arrowRoot.sizeDelta = Vector2.zero;
        _arrowRoot.anchoredPosition = Vector2.zero;
        _arrowRoot.localRotation = Quaternion.identity;
        _arrowRoot.localScale = Vector3.one;

        float parentSize = (_maskRect != null)
            ? Mathf.Min(_maskRect.rect.width, _maskRect.rect.height)
            : 200f;
        if (parentSize < 10f) parentSize = 200f;
        float stemH = parentSize * 0.5f * arrowLengthFractionOfRadius;
        float headH = stemH * 0.5f;
        float headW = arrowWidthPx * 2.6f;

        GameObject stemGO = new GameObject("Arrow Stem",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stemGO.transform.SetParent(_arrowRoot, false);
        RectTransform stem = stemGO.GetComponent<RectTransform>();
        stem.anchorMin = stem.anchorMax = new Vector2(0.5f, 0f);
        stem.pivot = new Vector2(0.5f, 0f);
        stem.sizeDelta = new Vector2(arrowWidthPx, stemH);
        stem.anchoredPosition = Vector2.zero;
        Image stemImg = stemGO.GetComponent<Image>();
        SetImageSolid(stemImg, arrowColor);
        stemImg.raycastTarget = false;

        GameObject headGO = new GameObject("Arrow Head",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        headGO.transform.SetParent(_arrowRoot, false);
        RectTransform head = headGO.GetComponent<RectTransform>();
        head.anchorMin = head.anchorMax = new Vector2(0.5f, 0f);
        head.pivot = new Vector2(0.5f, 0f);
        head.sizeDelta = new Vector2(headW, headH);
        head.anchoredPosition = new Vector2(0f, stemH);
        Image headImg = headGO.GetComponent<Image>();
        headImg.sprite = GetTriangleSprite();
        headImg.type = Image.Type.Simple;
        headImg.color = arrowHeadColor;
        headImg.raycastTarget = false;
    }

    private static Sprite GetTriangleSprite()
    {
        if (_cachedTriangleSprite != null) return _cachedTriangleSprite;
        _cachedTriangleSprite = CreateTriangleSprite();
        return _cachedTriangleSprite;
    }

    private static void SetImageSolid(Image img, Color color)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        img.type = Image.Type.Simple;
        img.color = color;
    }

    /// <summary>Up-pointing triangle; pivot bottom-center.</summary>
    private static Sprite CreateTriangleSprite()
    {
        const int h = 32;
        const int w = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 bl = new Vector2(0f, 0f);
        Vector2 br = new Vector2(w - 1f, 0f);
        Vector2 tip = new Vector2((w - 1f) * 0.5f, h - 1f);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2(x, y);
            bool inside = PointInTriangle(p, bl, br, tip);
            tex.SetPixel(x, y, inside ? Color.white : Color.clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Cross(b - a, p - a);
        float s2 = Cross(c - b, p - b);
        float s3 = Cross(a - c, p - c);
        bool neg = (s1 < 0f) || (s2 < 0f) || (s3 < 0f);
        bool pos = (s1 > 0f) || (s2 > 0f) || (s3 > 0f);
        return !(neg && pos);
    }

    private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
}
