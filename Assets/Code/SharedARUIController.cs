using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SharedARUIController : MonoBehaviour
{
    [SerializeField] private HybridModeController hybridModeController;
    [SerializeField] private bool showInIndoor = false;
    [SerializeField] private bool showInOutdoor = true;
    [SerializeField] private string promptText = "Nhập địa chỉ nơi dùng về tọa";
    [SerializeField] private string assistantText = "Hãy hỏi tôi";
    [Tooltip("If assigned, skips runtime BuildUI and uses this Hierarchy canvas (needs CanvasGroup).")]
    [SerializeField] private GameObject prebuiltUiRoot;

    private HybridModeController.HybridMode lastKnownMode = (HybridModeController.HybridMode)(-1);

    private GameObject rootObject;
    private CanvasGroup canvasGroup;
    private readonly List<GameObject> legacyOutdoorRootsToHide = new List<GameObject>();
    private bool hasCachedLegacyOutdoorRoots;
    private static Sprite circleSprite;
    private static Sprite roundedPanelSprite;

    private void Awake()
    {
        if (hybridModeController == null)
        {
            hybridModeController = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        }

        if (prebuiltUiRoot != null)
        {
            rootObject = prebuiltUiRoot;
            canvasGroup = rootObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = rootObject.AddComponent<CanvasGroup>();
            }
        }
        else
        {
            BuildUI();
        }

        ApplyVisibility();
    }

    private void LateUpdate()
    {
        if (hybridModeController == null || canvasGroup == null) return;
        var mode = hybridModeController.CurrentMode;
        if (mode == lastKnownMode) return;
        lastKnownMode = mode;
        ApplyVisibility();
    }

    private void BuildUI()
    {
        if (rootObject != null)
        {
            return;
        }

        rootObject = new GameObject("Shared AR UI");
        rootObject.transform.SetParent(transform, false);

        Canvas canvas = rootObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4200;

        CanvasScaler scaler = rootObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390f, 844f);
        scaler.matchWidthOrHeight = 0.5f;

        rootObject.AddComponent<GraphicRaycaster>();
        canvasGroup = rootObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = rootObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        CreateBottomSearch(canvasRect);
        CreateFloatingButtons(canvasRect);
    }

    private void CreateBottomSearch(RectTransform parent)
    {
        GameObject bottom = CreateRect("Bottom HUD", parent);
        RectTransform rect = bottom.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 22f);
        rect.sizeDelta = new Vector2(-32f, 82f);

        HorizontalLayoutGroup layout = bottom.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject avatar = CreateCircle("Assistant Badge", bottom.transform, new Color(0.08f, 0.08f, 0.1f, 0.88f));
        SetLayoutSize(avatar, 78f, 78f);
        TextMeshProUGUI avatarText = CreateText("Assistant Icon", avatar.transform, "AI", 20f, FontStyles.Bold, TextAlignmentOptions.Center);
        avatarText.color = Color.white;

        GameObject inputGroup = CreateRect("Search Group", bottom.transform);
        SetLayoutSize(inputGroup, 258f, 58f);

        Image searchBg = inputGroup.AddComponent<Image>();
        searchBg.sprite = GetRoundedPanelSprite();
        searchBg.type = Image.Type.Sliced;
        searchBg.color = new Color(0.02f, 0.02f, 0.025f, 0.82f);

        RectTransform inputRect = inputGroup.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0.5f);
        inputRect.anchorMax = new Vector2(0f, 0.5f);
        inputRect.pivot = new Vector2(0f, 0.5f);

        TextMeshProUGUI label = CreateText("Prompt", inputGroup.transform, assistantText, 10f, FontStyles.Bold, TextAlignmentOptions.Left);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(16f, -22f);
        labelRect.offsetMax = new Vector2(-18f, -4f);
        label.color = Color.white;

        TextMeshProUGUI prompt = CreateText("Input Placeholder", inputGroup.transform, promptText, 13f, FontStyles.Normal, TextAlignmentOptions.Left);
        RectTransform promptRect = prompt.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0f, 0f);
        promptRect.anchorMax = new Vector2(1f, 1f);
        promptRect.offsetMin = new Vector2(16f, 8f);
        promptRect.offsetMax = new Vector2(-18f, -22f);
        prompt.color = new Color(1f, 1f, 1f, 0.58f);
    }

    private void CreateFloatingButtons(RectTransform parent)
    {
        GameObject search = CreateCircle("Search Button", parent, new Color(0.34f, 0.25f, 0.86f, 1f));
        RectTransform searchRect = search.GetComponent<RectTransform>();
        searchRect.anchorMin = new Vector2(1f, 0f);
        searchRect.anchorMax = new Vector2(1f, 0f);
        searchRect.pivot = new Vector2(1f, 0f);
        searchRect.anchoredPosition = new Vector2(-34f, 174f);
        searchRect.sizeDelta = new Vector2(54f, 54f);
        CreateText("Search Icon", search.transform, "Q", 26f, FontStyles.Bold, TextAlignmentOptions.Center).color = Color.white;

        GameObject scan = CreateCircle("Scan Button", parent, new Color(0.34f, 0.25f, 0.86f, 1f));
        RectTransform scanRect = scan.GetComponent<RectTransform>();
        scanRect.anchorMin = new Vector2(1f, 0f);
        scanRect.anchorMax = new Vector2(1f, 0f);
        scanRect.pivot = new Vector2(1f, 0f);
        scanRect.anchoredPosition = new Vector2(-34f, 112f);
        scanRect.sizeDelta = new Vector2(54f, 54f);
        CreateText("Scan Icon", scan.transform, "[]", 24f, FontStyles.Bold, TextAlignmentOptions.Center).color = Color.white;
    }

    private void ApplyVisibility()
    {
        if (canvasGroup == null || hybridModeController == null)
        {
            return;
        }

        bool visible = (showInIndoor && hybridModeController.CurrentMode == HybridModeController.HybridMode.Indoor) ||
            (showInOutdoor && hybridModeController.CurrentMode == HybridModeController.HybridMode.Outdoor);

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        SetLegacyOutdoorUIHidden(visible && hybridModeController.CurrentMode == HybridModeController.HybridMode.Outdoor);
    }

    private static GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName);
        gameObject.transform.SetParent(parent, false);
        gameObject.AddComponent<RectTransform>();
        return gameObject;
    }

    private static GameObject CreateCircle(string objectName, Transform parent, Color color)
    {
        GameObject gameObject = CreateRect(objectName, parent);
        Image image = gameObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = color;
        return gameObject;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
        {
            circleSprite = CreateRoundedSprite(64, 64, 32f, new Vector4(32f, 32f, 32f, 32f));
        }

        return circleSprite;
    }

    private static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite == null)
        {
            roundedPanelSprite = CreateRoundedSprite(96, 32, 16f, new Vector4(16f, 16f, 16f, 16f));
        }

        return roundedPanelSprite;
    }

    private static Sprite CreateRoundedSprite(int width, int height, float radius, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "SharedARUI Rounded Sprite";
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside =
                    IsInsideCorner(x, y, radius, radius, radius) &&
                    IsInsideCorner(width - 1 - x, y, radius, radius, radius) &&
                    IsInsideCorner(x, height - 1 - y, radius, radius, radius) &&
                    IsInsideCorner(width - 1 - x, height - 1 - y, radius, radius, radius);

                texture.SetPixel(x, y, inside ? solid : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static bool IsInsideCorner(float x, float y, float radiusX, float radiusY, float radius)
    {
        if (x >= radiusX || y >= radiusY)
        {
            return true;
        }

        float dx = radiusX - x;
        float dy = radiusY - y;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject gameObject = CreateRect(objectName, parent);
        TextMeshProUGUI textComponent = gameObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        textComponent.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return textComponent;
    }

    private void SetLegacyOutdoorUIHidden(bool hidden)
    {
        CacheLegacyOutdoorRoots();

        foreach (GameObject root in legacyOutdoorRootsToHide)
        {
            if (root != null)
            {
                root.SetActive(!hidden);
            }
        }
    }

    private void CacheLegacyOutdoorRoots()
    {
        if (hasCachedLegacyOutdoorRoots)
        {
            return;
        }

        GameObject outdoorEnvironment = GameObject.Find("OutdoorEnvironment");
        if (outdoorEnvironment == null)
        {
            hasCachedLegacyOutdoorRoots = true;
            return;
        }

        string[] namesToHide =
        {
            "AssistantImage",
            "InputManager",
            "InputField (Legacy)",
            "Button"
        };

        Transform[] children = outdoorEnvironment.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            foreach (string objectName in namesToHide)
            {
                if (child.name == objectName && !legacyOutdoorRootsToHide.Contains(child.gameObject))
                {
                    legacyOutdoorRootsToHide.Add(child.gameObject);
                }
            }
        }

        hasCachedLegacyOutdoorRoots = true;
    }

    private static void SetLayoutSize(GameObject gameObject, float width, float height)
    {
        LayoutElement layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
        layout.minWidth = width;
        layout.minHeight = height;
    }
}
