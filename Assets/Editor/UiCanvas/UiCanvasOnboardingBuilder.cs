#if UNITY_EDITOR
using System.IO;
using Game.Features.Onboarding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Game.Foundation.UI.EditorTools.UiCanvasBuilderKit;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Phase 1: builds the SplashUI, IntroUI and WelcomeUI prefabs from the Figma frames and the
    /// AppShell scene that hosts them. All coordinates are the measured Figma values (375x812 frame).
    /// </summary>
    public static class UiCanvasOnboardingBuilder
    {
        public const string SplashName = "SplashUI";
        public const string IntroName = "IntroUI";
        public const string WelcomeName = "WelcomeUI";

        private const float ButtonWidth = 343f;
        private const float ButtonBottomMargin = 33f;
        private const float ButtonGap = 16f;
        private const float FrameHeight = 812f;

        // Heights of the bottom-pinned text blocks, measured from the Figma frame (812 - title top).
        private const float IntroTextHeight = 282f;     // title top at y=530
        private const float IntroArtGap = 30f;          // art ends at y=500
        private const float WelcomeTextHeight = 353.5f; // title top at y=458.5
        private const float WelcomeArtGap = 64.5f;      // illustration ends at y=394
        private const float WelcomeArtTopMargin = 51f;

        [MenuItem("Tools/UI Canvas/Build Onboarding Screens (Phase 1)")]
        public static void Build()
        {
            // In Play mode AddComponent runs Awake at once, before the fields are wired, and NewScene is not allowed.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before building the onboarding screens.");
                return;
            }

            BuildSplash();
            BuildIntro();
            BuildWelcome();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildShellScene();
            Debug.Log("[UiCanvas] Onboarding prefabs and " + UiCanvasPaths.ShellScene + " built");
        }

        private static void BuildSplash()
        {
            var page = NewPage<SplashUI>(SplashName);
            SetPageId(page, PageID.SplashPage);

            AddBackground(page.transform);
            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            // Splash is centered on the screen; offsets are measured from the middle of the 812 frame.
            RectTransform logo = NewRect(content, "Logo", Middle, new Vector2(0f, FromMiddle(286.5f)), new Vector2(230f, 228f));
            AddImage(logo, LoadSprite(UiCanvasPaths.ScreenArt(SplashName, "splash_logo.png")), Color.white);

            AddText(content, "Title", "Agentic AR", "Bold", 28f, UiTheme.Primary, TextAlignmentOptions.Center,
                Middle, new Vector2(0f, FromMiddle(457f)), new Vector2(300f, 44f));

            RectTransform loader = NewRect(content, "Loader", Middle, new Vector2(0f, FromMiddle(573.5f)), new Vector2(68f, 68f));
            AddImage(loader, LoadSprite(UiCanvasPaths.CircleSprite), UiTheme.Primary);

            RectTransform arc = NewRect(loader, "Arc", Middle, Vector2.zero, new Vector2(31f, 32f));
            AddImage(arc, LoadSprite(UiCanvasPaths.CommonArt("spinner_arc.png")), Color.white);
            arc.gameObject.AddComponent<UiSpinner>();

            SavePrefab(page.gameObject, SplashName);
        }

        private static void BuildIntro()
        {
            var page = NewPage<IntroUI>(IntroName);
            SetPageId(page, PageID.Onboarding);

            AddBackground(page.transform);

            // The text block keeps its Figma size and sticks to the bottom; the art takes the space above it,
            // so a shorter screen (e.g. 375x667) shrinks the phone instead of overlapping the text.
            // On the 812 frame the art area is exactly the 375x500 Figma slot.
            RectTransform artArea = NewBand(page.transform, "ArtArea", new Vector2(0f, IntroTextHeight + IntroArtGap), Vector2.zero);
            RectTransform gradient = NewStretch(artArea, "Gradient");
            AddImage(gradient, LoadSprite(UiCanvasPaths.ScreenArt(IntroName, "onboarding_gradient.png")), Color.white);
            AddFittedImage(artArea, "Phone", LoadSprite(UiCanvasPaths.ScreenArt(IntroName, "onboarding_phone_ar.png")), 375f / 500f);

            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();
            RectTransform text = NewBottomGroup(content, "BottomGroup", IntroTextHeight);

            // Figma sets the 28px title at 120% line height; Poppins' natural line is 150%, hence the negative spacing.
            AddText(text, "Title", "Di chuyển trong khuôn viên dễ dàng", "Bold", 28f, UiTheme.TextTitle,
                TextAlignmentOptions.Center, TopCenter, Vector2.zero, new Vector2(ButtonWidth, 80f), lineSpacing: -20f);
            AddText(text, "Subtitle", "Sử dụng dẫn đường AR để đến bất kỳ toà nhà nào nhanh và chính xác.", "Regular", 16f,
                UiTheme.TextBody, TextAlignmentOptions.Center, TopCenter, new Vector2(0f, -86f), new Vector2(ButtonWidth, 50f));

            Button next = AddPillButton(text, "ContinueButton", "Tiếp tục", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);
            SetReference(page, "continueButton", next);

            SavePrefab(page.gameObject, IntroName);
        }

        private static void BuildWelcome()
        {
            var page = NewPage<WelcomeUI>(WelcomeName);
            SetPageId(page, PageID.WelcomePage);

            AddBackground(page.transform);
            RectTransform content = NewStretch(page.transform, "Content");
            content.gameObject.AddComponent<UiSafeArea>();

            // Title, body and buttons form one block pinned to the bottom; the illustration fills what is left
            // above it. On the 812 frame that is exactly the 343x343 Figma slot, on 667 it shrinks.
            RectTransform artArea = NewBand(content, "IllustrationArea",
                new Vector2(16f, WelcomeTextHeight + WelcomeArtGap), new Vector2(-16f, -WelcomeArtTopMargin));
            AddFittedImage(artArea, "Illustration", LoadSprite(UiCanvasPaths.ScreenArt(WelcomeName, "welcome_illustration.png")), 1f);

            RectTransform text = NewBottomGroup(content, "BottomGroup", WelcomeTextHeight);
            AddText(text, "Title", "Chào mừng bạn!", "SemiBold", 28f, UiTheme.TextTitle,
                TextAlignmentOptions.Center, TopCenter, Vector2.zero, new Vector2(ButtonWidth, 44f));
            AddText(text, "Body",
                "Chúc bạn sẽ có những trải nghiệm tuyệt vời với Agentic AR. Hãy bắt đầu đăng nhập hoặc nếu chưa có tài khoản xin hãy bấm vào nút đăng ký.",
                "Regular", 16f, UiTheme.TextBody, TextAlignmentOptions.Top,
                TopCenter, new Vector2(0f, -50.5f), new Vector2(ButtonWidth, 96f));

            Button login = AddPillButton(text, "LoginButton", "Đăng nhập", UiTheme.Primary,
                BottomCenter, new Vector2(0f, ButtonBottomMargin + UiTheme.PillHeight + ButtonGap), ButtonWidth);
            Button register = AddPillButton(text, "RegisterButton", "Đăng ký", UiTheme.Surface,
                BottomCenter, new Vector2(0f, ButtonBottomMargin), ButtonWidth);
            SetReference(page, "loginButton", login);
            SetReference(page, "registerButton", register);

            SavePrefab(page.gameObject, WelcomeName);
        }

        private static void BuildShellScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.Background;

            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.AppCanvasPrefab);
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
            var pages = (RectTransform)canvas.transform.Find(UiCanvasPrefabBuilder.PagesName);

            var navigationGo = new GameObject("NavigationManager");
            var navigation = navigationGo.AddComponent<NavigationManager>();

            var serialized = new SerializedObject(navigation);
            SerializedProperty routes = serialized.FindProperty("pages");
            routes.arraySize = 3;
            SetRoute(routes.GetArrayElementAtIndex(0), PageID.SplashPage, SplashName);
            SetRoute(routes.GetArrayElementAtIndex(1), PageID.Onboarding, IntroName);
            SetRoute(routes.GetArrayElementAtIndex(2), PageID.WelcomePage, WelcomeName);
            serialized.FindProperty("firstPage").enumValueIndex = (int)PageID.SplashPage;
            serialized.FindProperty("canvasRoot").objectReferenceValue = canvas;
            serialized.FindProperty("canvasPageContainer").objectReferenceValue = pages;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(UiCanvasPaths.ShellScene));
            EditorSceneManager.SaveScene(scene, UiCanvasPaths.ShellScene);
        }

        private static void SetRoute(SerializedProperty route, PageID id, string screenName)
        {
            route.FindPropertyRelative("id").enumValueIndex = (int)id;
            route.FindPropertyRelative("canvasPage").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
        }

        private static void AddBackground(Transform page)
        {
            RectTransform background = NewStretch(page, "Background");
            AddImage(background, null, UiTheme.Background);
        }

        private static float FromMiddle(float yFromTop)
        {
            return FrameHeight * 0.5f - yFromTop;
        }

        private static void SavePrefab(GameObject root, string screenName)
        {
            string path = UiCanvasPaths.ScreenPrefab(screenName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
