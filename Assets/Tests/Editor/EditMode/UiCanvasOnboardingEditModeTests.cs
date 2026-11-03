#if UNITY_EDITOR
using System.Reflection;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Phase 1 of the UI Toolkit → Canvas migration: the Splash, Intro and Welcome prefabs, the AppShell
    /// scene that hosts them and the router behaviour for Canvas-only pages. When an asset is missing
    /// run Tools ▸ UI Canvas ▸ Run Phase 1 Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasOnboardingEditModeTests
    {
        private const string SetupHint = " — run Tools/UI Canvas/Run Phase 1 Setup";

        [TestCase(UiCanvasOnboardingBuilder.SplashName, PageID.SplashPage)]
        [TestCase(UiCanvasOnboardingBuilder.IntroName, PageID.Onboarding)]
        [TestCase(UiCanvasOnboardingBuilder.WelcomeName, PageID.WelcomePage)]
        public void ScreenPrefab_WhenBuilt_IsAUiPageWithItsPageId(string screenName, PageID expected)
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(page, screenName + ".prefab" + SetupHint);
            Assert.AreEqual(expected, page.Id);
        }

        [Test]
        public void IntroPrefab_WhenBuilt_HasContinueButtonWired()
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(UiCanvasOnboardingBuilder.IntroName));
            Assert.NotNull(page, "IntroUI.prefab" + SetupHint);
            Assert.NotNull(ReadReference<Button>(page, "continueButton"), "continueButton");
        }

        [Test]
        public void WelcomePrefab_WhenBuilt_HasLoginAndRegisterButtonsWired()
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(UiCanvasOnboardingBuilder.WelcomeName));
            Assert.NotNull(page, "WelcomeUI.prefab" + SetupHint);
            Assert.NotNull(ReadReference<Button>(page, "loginButton"), "loginButton");
            Assert.NotNull(ReadReference<Button>(page, "registerButton"), "registerButton");
        }

        [TestCase(UiCanvasOnboardingBuilder.IntroName, "ContinueButton")]
        [TestCase(UiCanvasOnboardingBuilder.WelcomeName, "LoginButton")]
        [TestCase(UiCanvasOnboardingBuilder.WelcomeName, "RegisterButton")]
        public void Button_WhenBuilt_IsFigmaPillSize(string screenName, string buttonName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(prefab, screenName + ".prefab" + SetupHint);

            Transform button = prefab.transform.Find("Content/BottomGroup/" + buttonName);
            Assert.NotNull(button, buttonName);
            Assert.AreEqual(new Vector2(343f, UiTheme.PillHeight), ((RectTransform)button).sizeDelta);
        }

        // The art takes the space above the bottom-pinned text block, so no screen height can make them overlap.
        [TestCase(UiCanvasOnboardingBuilder.IntroName, "ArtArea", false)]
        [TestCase(UiCanvasOnboardingBuilder.WelcomeName, "Content/IllustrationArea", true)]
        public void ArtArea_WhenScreenIsShortOrTall_StaysAboveTheTextBlock(string screenName, string artPath, bool insideContent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(prefab, screenName + ".prefab" + SetupHint);

            var art = (RectTransform)prefab.transform.Find(artPath);
            var group = (RectTransform)prefab.transform.Find("Content/BottomGroup");
            Assert.NotNull(art, artPath);
            Assert.NotNull(group, "Content/BottomGroup");
            Assert.AreEqual(0f, group.anchorMin.y, "text block is pinned to the bottom");
            Assert.AreEqual(0f, group.anchorMax.y, "text block is pinned to the bottom");

            // Distance from the bottom edge to the art's lower edge must clear the text block's top edge.
            Assert.GreaterOrEqual(art.offsetMin.y, group.sizeDelta.y, "art starts above the text block");

            foreach (float frameHeight in new[] { 667f, 812f, 900f })
            {
                float artHeight = frameHeight - art.offsetMin.y + art.offsetMax.y;
                Assert.Greater(artHeight, 150f, "art keeps a usable size at height " + frameHeight);
            }

            Assert.IsNotNull(art.GetComponentInChildren<AspectRatioFitter>(), "art keeps its aspect ratio");
            Assert.IsTrue(insideContent == (art.parent.name == "Content"));
        }

        [Test]
        public void ShellScene_WhenBuilt_RoutesSplashIntroWelcomeThroughAppCanvas()
        {
            Scene scene = EditorSceneManager.OpenScene(UiCanvasPaths.ShellScene, OpenSceneMode.Additive);
            try
            {
                NavigationManager navigation = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (navigation == null)
                    {
                        navigation = root.GetComponentInChildren<NavigationManager>(true);
                    }
                }

                Assert.NotNull(navigation, "NavigationManager in AppShell" + SetupHint);
                Assert.AreEqual(PageID.SplashPage, navigation.firstPage);
                Assert.AreEqual(3, navigation.pages.Count);
                foreach (PageRoute route in navigation.pages)
                {
                    Assert.NotNull(route.canvasPage, "canvas page for " + route.id);
                }

                var serialized = new SerializedObject(navigation);
                Assert.NotNull(serialized.FindProperty("canvasRoot").objectReferenceValue, "canvasRoot");
                Assert.NotNull(serialized.FindProperty("canvasPageContainer").objectReferenceValue, "canvasPageContainer");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NavigationManager_WhenOnlyCanvasPages_StartsOnFirstPageAndSwitchesPages()
        {
            var canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            var container = new GameObject("Pages", typeof(RectTransform)).GetComponent<RectTransform>();
            container.SetParent(canvasGo.transform, false);
            var navigationGo = new GameObject("TestNavigation");
            navigationGo.SetActive(false);
            try
            {
                var navigation = navigationGo.AddComponent<NavigationManager>();
                var serialized = new SerializedObject(navigation);
                SerializedProperty routes = serialized.FindProperty("pages");
                routes.arraySize = 3;
                SetRoute(routes.GetArrayElementAtIndex(0), PageID.SplashPage, UiCanvasOnboardingBuilder.SplashName);
                SetRoute(routes.GetArrayElementAtIndex(1), PageID.Onboarding, UiCanvasOnboardingBuilder.IntroName);
                SetRoute(routes.GetArrayElementAtIndex(2), PageID.WelcomePage, UiCanvasOnboardingBuilder.WelcomeName);
                serialized.FindProperty("firstPage").enumValueIndex = (int)PageID.SplashPage;
                serialized.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
                serialized.FindProperty("canvasPageContainer").objectReferenceValue = container;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Invoke(navigation, "Awake");
                Invoke(navigation, "OnEnable");

                Assert.IsTrue(navigation.HasRoute(PageID.Onboarding));
                Assert.IsFalse(navigation.HasRoute(PageID.Login), "Login is not a Canvas route yet");
                Assert.IsTrue(IsActive(container, UiCanvasOnboardingBuilder.SplashName), "first page shown");

                navigation.Navigate(PageID.Onboarding);
                Assert.IsTrue(IsActive(container, UiCanvasOnboardingBuilder.IntroName), "intro shown");
                Assert.IsFalse(IsActive(container, UiCanvasOnboardingBuilder.SplashName), "splash hidden");

                navigation.Navigate(PageID.WelcomePage);
                Assert.IsTrue(IsActive(container, UiCanvasOnboardingBuilder.WelcomeName), "welcome shown");
                Assert.IsFalse(IsActive(container, UiCanvasOnboardingBuilder.IntroName), "intro hidden");
            }
            finally
            {
                NavigationManager.pageHistory.Clear();
                Object.DestroyImmediate(navigationGo);
                Object.DestroyImmediate(canvasGo);
            }
        }

        private static void SetRoute(SerializedProperty route, PageID id, string screenName)
        {
            route.FindPropertyRelative("id").enumValueIndex = (int)id;
            route.FindPropertyRelative("canvasPage").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
        }

        private static bool IsActive(Transform container, string pageName)
        {
            Transform page = container.Find(pageName + "(Clone)");
            return page != null && page.gameObject.activeSelf;
        }

        private static T ReadReference<T>(Object owner, string field) where T : Object
        {
            return new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
        }

        private static void Invoke(object target, string method)
        {
            target.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(target, null);
        }
    }
}
#endif
