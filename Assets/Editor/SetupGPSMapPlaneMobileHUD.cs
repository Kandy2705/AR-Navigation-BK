using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupGPSMapPlaneMobileHUD
{
    private const string ScenePath = "Assets/Scenes/GPSMapPlane.unity";
    private const string HudName = "Mobile Navigation HUD";

    [MenuItem("Tools/TestAR/Setup GPSMapPlane Mobile HUD")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureEventSystem();

        Canvas canvas = EnsureCanvas();
        Text statusText = EnsureStatusPanel(canvas.transform);
        Dropdown dropdown = EnsureDropdownPanel(canvas.transform);

        MobileNavigationHUD hud = canvas.GetComponent<MobileNavigationHUD>();
        if (hud == null) hud = canvas.gameObject.AddComponent<MobileNavigationHUD>();

        hud.pathFinder = Object.FindFirstObjectByType<ARPathFinder>();
        hud.gpsTracker = Object.FindFirstObjectByType<SimpleGPSTracker>();
        hud.userTransform = hud.gpsTracker != null ? hud.gpsTracker.xrOrigin : null;
        hud.targets = Object.FindObjectsByType<TargetAnchor>(FindObjectsSortMode.None)
            .OrderBy(target => target.gameObject.name)
            .ToArray();
        hud.statusText = statusText;
        hud.targetDropdown = dropdown;

        dropdown.ClearOptions();
        dropdown.AddOptions(hud.targets.Select(target => target.TargetName).ToList());
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupGPSMapPlaneMobileHUD] Mobile HUD is ready in GPSMapPlane.");
    }

    private static Canvas EnsureCanvas()
    {
        GameObject canvasObject = GameObject.Find(HudName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(HudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static Text EnsureStatusPanel(Transform parent)
    {
        RectTransform panel = EnsurePanel(parent, "Status Panel", new Color(0f, 0f, 0f, 0.68f));
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -36f);
        panel.sizeDelta = new Vector2(-64f, 250f);

        Text text = EnsureText(panel, "Status Text");
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(32f, 20f);
        textRect.offsetMax = new Vector2(-32f, -20f);

        text.fontSize = 38;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.text = "Target: Des1\nDistance: --\nLat: Waiting for GPS\nLong: Waiting for GPS";

        return text;
    }

    private static Dropdown EnsureDropdownPanel(Transform parent)
    {
        RectTransform panel = EnsurePanel(parent, "Target Dropdown Panel", new Color(0f, 0f, 0f, 0.68f));
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 48f);
        panel.sizeDelta = new Vector2(-64f, 128f);

        Dropdown dropdown = panel.GetComponentInChildren<Dropdown>(true);
        if (dropdown == null)
        {
            DefaultControls.Resources resources = new DefaultControls.Resources();
            GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
            dropdownObject.name = "Target Dropdown";
            dropdownObject.transform.SetParent(panel, false);
            dropdown = dropdownObject.GetComponent<Dropdown>();
        }

        RectTransform dropdownRect = dropdown.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0f, 0.5f);
        dropdownRect.anchorMax = new Vector2(1f, 0.5f);
        dropdownRect.pivot = new Vector2(0.5f, 0.5f);
        dropdownRect.anchoredPosition = Vector2.zero;
        dropdownRect.sizeDelta = new Vector2(-48f, 76f);

        Image image = dropdown.GetComponent<Image>();
        if (image != null) image.color = new Color(0.08f, 0.11f, 0.14f, 0.95f);

        foreach (Text text in dropdown.GetComponentsInChildren<Text>(true))
        {
            text.font = GetDefaultFont();
            text.fontSize = 34;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
        }

        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = 36;
            dropdown.captionText.fontStyle = FontStyle.Bold;
        }

        if (dropdown.template != null)
        {
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 180f);
        }

        return dropdown;
    }

    private static RectTransform EnsurePanel(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject panelObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        panelObject.transform.SetParent(parent, false);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;

        return panelObject.GetComponent<RectTransform>();
    }

    private static Text EnsureText(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));

        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = GetDefaultFont();
        return text;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            Object.DestroyImmediate(eventSystem.GetComponent<BaseInputModule>());
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        inputModule.AssignDefaultActions();
    }
}
