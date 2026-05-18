using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor utility: creates a top-down minimap in the GPSMapPlane scene.
///
/// Run from menu: Tools/TestAR/Setup Minimap Top-Down
///
/// What it creates:
///   Minimap Camera  — orthographic camera looking straight down, renders to a RenderTexture
///   Minimap Canvas  — Screen Space Overlay canvas (sortingOrder 200) containing:
///     ├── Minimap Border      — blue ring behind the circle
///     └── Minimap Circle Mask — circular Mask clipping the camera view
///             └── Minimap View   — RawImage showing the RenderTexture
///             └── North Label    — "N" label at the top of the circle
/// </summary>
public static class SetupMinimapTopDown
{
    private const string ScenePath    = "Assets/Scenes/GPSMapPlane.unity";
    private const string RT_PATH      = "Assets/Materials/MinimapRT.renderTexture";
    private const string SPRITE_PATH  = "Assets/Materials/MinimapCircle.png";
    private const int    RT_SIZE      = 640;
    /// <summary>Đường kính vòng minimap (pixel tham chiếu 1080×1920). Tăng số = vòng tròn to hơn trên màn hình.</summary>
    private const float  MINIMAP_PX           = 420f;
    /// <summary>Bán kính nhìn orthographic (nửa chiều cao thế giới hiển thị, mét). Giảm = zoom gần mặt đất hơn.</summary>
    private const float  MINIMAP_VIEW_RADIUS_M = 5f;

    // ──────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/TestAR/Setup Minimap Top-Down")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        RenderTexture   rt           = EnsureRenderTexture();
        Sprite          circleSprite = EnsureCircleSprite();

        EnsureMinimapCamera(rt);
        EnsureMinimapCanvas(rt, circleSprite);
        DisableLegacyMinimapHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[SetupMinimapTopDown] Done. Top-down minimap ready in GPSMapPlane.");
        EditorUtility.DisplayDialog(
            "Minimap Setup Complete",
            "Top-down minimap has been created in the Hierarchy and the scene has been saved.\n\n" +
            "Objects created:\n" +
            "  • Minimap Camera  (top-down orthographic)\n" +
            "  • Minimap Canvas  (top-right corner, sortingOrder 200)",
            "OK");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Assets
    // ──────────────────────────────────────────────────────────────────────────

    private static RenderTexture EnsureRenderTexture()
    {
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH);
        if (rt != null)
        {
            if (rt.width != RT_SIZE || rt.height != RT_SIZE)
            {
                SerializedObject rtSo = new SerializedObject(rt);
                SerializedProperty w = rtSo.FindProperty("m_Width");
                SerializedProperty h = rtSo.FindProperty("m_Height");
                if (w != null && h != null)
                {
                    w.intValue = RT_SIZE;
                    h.intValue = RT_SIZE;
                    rtSo.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(rt);
            }

            return rt;
        }

        rt = new RenderTexture(RT_SIZE, RT_SIZE, 16, RenderTextureFormat.ARGB32)
        {
            name = "MinimapRT",
            filterMode = FilterMode.Bilinear,
            antiAliasing = 2
        };
        AssetDatabase.CreateAsset(rt, RT_PATH);
        AssetDatabase.SaveAssets();
        return rt;
    }

    private static Sprite EnsureCircleSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);
        if (existing != null) return existing;

        // Generate a soft white circle and save as PNG
        Texture2D tex = GenerateCircleTexture(256);
        File.WriteAllBytes(SPRITE_PATH, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SPRITE_PATH);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(SPRITE_PATH);
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.filterMode          = FilterMode.Bilinear;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);
    }

    private static Texture2D GenerateCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx    = x - r + 0.5f;
            float dy    = y - r + 0.5f;
            float dist  = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01((r - dist) / 1.5f); // 1.5px soft edge
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return tex;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scene objects
    // ──────────────────────────────────────────────────────────────────────────

    private static void EnsureMinimapCamera(RenderTexture rt)
    {
        GameObject go = GameObject.Find("Minimap Camera");
        if (go == null) go = new GameObject("Minimap Camera");

        // Camera component
        Camera cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();

        cam.orthographic     = true;
        cam.orthographicSize = MINIMAP_VIEW_RADIUS_M;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
        cam.cullingMask     = ~(1 << LayerMask.NameToLayer("UI"));  // render everything except UI layer
        cam.depth           = -2;   // render before main camera
        cam.targetTexture   = rt;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 100f;

        if (go.GetComponent<MinimapTopDownCamera>() == null)
            go.AddComponent<MinimapTopDownCamera>();

        MinimapTopDownCamera follow = go.GetComponent<MinimapTopDownCamera>();
        if (follow != null)
        {
            SerializedObject so = new SerializedObject(follow);
            SerializedProperty radius = so.FindProperty("viewRadiusMeters");
            SerializedProperty height = so.FindProperty("heightAbovePlayer");
            if (radius != null) radius.floatValue = MINIMAP_VIEW_RADIUS_M;
            if (height != null) height.floatValue = 45f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Spawn position (gets overridden at runtime by MinimapTopDownCamera)
        go.transform.position = new Vector3(0f, 30f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void EnsureMinimapCanvas(RenderTexture rt, Sprite circleSprite)
    {
        // Remove any legacy minimap canvas
        GameObject oldCanvas = GameObject.Find("Minimap Canvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        // ── Canvas ────────────────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("Minimap Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;  // always on top

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        Transform canvasT = canvasGO.transform;

        // ── Border ring (drawn behind, slightly larger) ───────────────────────
        GameObject borderGO = new GameObject("Minimap Border",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGO.transform.SetParent(canvasT, false);
        RectTransform borderRt = borderGO.GetComponent<RectTransform>();
        AnchorTopRight(borderRt, MINIMAP_PX + 8f, -20f, -20f);
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.sprite = circleSprite;
        borderImg.color  = new Color(0.3f, 0.6f, 1f, 0.75f);
        borderImg.type   = Image.Type.Simple;

        // ── Circular mask container ───────────────────────────────────────────
        GameObject maskGO = new GameObject("Minimap Circle Mask",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        maskGO.transform.SetParent(canvasT, false);
        RectTransform maskRt = maskGO.GetComponent<RectTransform>();
        AnchorTopRight(maskRt, MINIMAP_PX, -24f, -24f);
        Image maskImg = maskGO.GetComponent<Image>();
        maskImg.sprite = circleSprite;
        maskImg.color  = Color.white;
        maskImg.type   = Image.Type.Simple;
        Mask mask = maskGO.GetComponent<Mask>();
        mask.showMaskGraphic = false; // hide the mask image; only use it for clipping

        // ── RawImage showing the render texture ───────────────────────────────
        GameObject rawGO = new GameObject("Minimap View",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawGO.transform.SetParent(maskGO.transform, false);
        RectTransform rawRt = rawGO.GetComponent<RectTransform>();
        rawRt.anchorMin       = Vector2.zero;
        rawRt.anchorMax       = Vector2.one;
        rawRt.offsetMin       = Vector2.zero;
        rawRt.offsetMax       = Vector2.zero;
        RawImage rawImg = rawGO.GetComponent<RawImage>();
        rawImg.texture = rt;
        rawImg.color   = Color.white;

        if (rawGO.GetComponent<MinimapRawViewFill>() == null)
            rawGO.AddComponent<MinimapRawViewFill>();

        // ── "N" label at the top of the circle ────────────────────────────────
        GameObject nGO = new GameObject("North Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        nGO.transform.SetParent(maskGO.transform, false);
        RectTransform nRt = nGO.GetComponent<RectTransform>();
        nRt.anchorMin        = nRt.anchorMax = new Vector2(0.5f, 1f);
        nRt.pivot            = new Vector2(0.5f, 1f);
        nRt.anchoredPosition = new Vector2(0f, -6f);
        nRt.sizeDelta        = new Vector2(34f, 26f);
        Text nTxt = nGO.GetComponent<Text>();
        nTxt.text      = "N";
        nTxt.font      = GetDefaultFont();
        nTxt.fontSize  = 24;
        nTxt.fontStyle = FontStyle.Bold;
        nTxt.alignment = TextAnchor.UpperCenter;
        nTxt.color     = new Color(1f, 0.85f, 0.3f, 0.9f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// Anchors a square RectTransform to the top-right corner.
    /// offsetX / offsetY are negative margins from the right/top edges.
    private static void AnchorTopRight(RectTransform rt, float sizePx, float offsetX, float offsetY)
    {
        rt.anchorMin        = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.sizeDelta        = new Vector2(sizePx, sizePx);
        rt.anchoredPosition = new Vector2(offsetX, offsetY);
    }

    private static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void DisableLegacyMinimapHUD()
    {
        // The old MinimapHUD script auto-creates a 2D icon minimap.
        // Disable its GameObject so it doesn't conflict with the new camera minimap.
        GameObject legacy = GameObject.Find("Minimap HUD");
        if (legacy != null)
        {
            legacy.SetActive(false);
            Debug.Log("[SetupMinimapTopDown] Disabled legacy Minimap HUD.");
        }
    }
}
