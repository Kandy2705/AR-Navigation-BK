#if UNITY_EDITOR
using System.Reflection;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// The Canvas router: history, Back, tab switching and logout reset. Three light onboarding screens stand in for the
    /// AR, history and chat pages (the router only cares about the id each route is registered under).
    /// </summary>
    [Category("TestAR")]
    public sealed class NavigationManagerEditModeTests
    {
        private GameObject canvasGo;
        private GameObject navigationGo;
        private RectTransform container;
        private NavigationManager navigation;

        [SetUp]
        public void SetUp()
        {
            NavigationManager.pageHistory.Clear();
            canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            container = new GameObject("Pages", typeof(RectTransform)).GetComponent<RectTransform>();
            container.SetParent(canvasGo.transform, false);
            navigationGo = new GameObject("TestNavigation");
            navigationGo.SetActive(false);
            navigation = navigationGo.AddComponent<NavigationManager>();

            var serialized = new SerializedObject(navigation);
            SerializedProperty routes = serialized.FindProperty("pages");
            routes.arraySize = 3;
            SetRoute(routes.GetArrayElementAtIndex(0), PageID.ARPage, UiCanvasOnboardingBuilder.SplashName);
            SetRoute(routes.GetArrayElementAtIndex(1), PageID.HistoryPage, UiCanvasOnboardingBuilder.IntroName);
            SetRoute(routes.GetArrayElementAtIndex(2), PageID.Chatbox, UiCanvasOnboardingBuilder.WelcomeName);
            serialized.FindProperty("firstPage").enumValueIndex = (int)PageID.None;
            serialized.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
            serialized.FindProperty("canvasPageContainer").objectReferenceValue = container;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Invoke("Awake");
            Invoke("OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(navigationGo);
            Object.DestroyImmediate(canvasGo);
            NavigationManager.pageHistory.Clear();
        }

        [Test]
        public void Navigate_DrawsTheMovingPageOnTop()
        {
            navigation.Navigate(PageID.ARPage);
            Transform ar = container.GetChild(container.childCount - 1);
            navigation.Navigate(PageID.HistoryPage);
            Transform history = container.GetChild(container.childCount - 1);
            Assert.AreNotEqual(ar, history);
            ar.SetAsLastSibling(); // make the order wrong on purpose: the next push must fix it
            navigation.Navigate(PageID.Chatbox);
            navigation.Navigate(PageID.HistoryPage);
            Assert.Greater(history.GetSiblingIndex(), ar.GetSiblingIndex(), "a pushed page slides in over the old one");

            navigation.Navigate(PageID.ARPage, true);
            Assert.Greater(history.GetSiblingIndex(), ar.GetSiblingIndex(), "going back, the leaving page slides off on top");

            navigation.Navigate(PageID.HistoryPage);
            navigation.ResetTo(PageID.ARPage);
            Assert.Greater(ar.GetSiblingIndex(), history.GetSiblingIndex(), "a fading page settles in on top");
        }

        [Test]
        public void GoBack_WhenReturningToTheHistoryPage_PopsExactlyOnePage()
        {
            // The UI Toolkit router pushed instead of popped when Back landed on the history page, so the stack grew.
            navigation.Navigate(PageID.ARPage);
            navigation.Navigate(PageID.HistoryPage);
            navigation.Navigate(PageID.Chatbox);

            Assert.IsTrue(navigation.GoBack());

            Assert.AreEqual(2, NavigationManager.pageHistory.Count);
            Assert.AreEqual(PageID.HistoryPage, NavigationManager.pageHistory.Peek());
            Assert.IsTrue(IsShown(UiCanvasOnboardingBuilder.IntroName));
            Assert.IsFalse(IsShown(UiCanvasOnboardingBuilder.WelcomeName));

            Assert.IsTrue(navigation.GoBack());
            Assert.AreEqual(1, NavigationManager.pageHistory.Count);
            Assert.AreEqual(PageID.ARPage, NavigationManager.pageHistory.Peek());
            Assert.IsFalse(navigation.GoBack(), "nothing below the first page");
        }

        [Test]
        public void Navigate_WhenTheIdIsUnknown_LogsAndKeepsTheHistory()
        {
            navigation.Navigate(PageID.ARPage);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[NavigationManager] No page registered for Login.");

            navigation.Navigate(PageID.Login);

            Assert.AreEqual(1, NavigationManager.pageHistory.Count);
            Assert.IsFalse(navigation.HasRoute(PageID.Login));
            Assert.IsTrue(navigation.HasRoute(PageID.HistoryPage));
        }

        [Test]
        public void BottomNav_WhenSwitchingTabs_KeepsTheArPageAsTheBottomOfTheHistory()
        {
            navigation.Navigate(PageID.ARPage);

            UiBottomNav.Navigate(navigation, UiBottomNav.HistoryTab);
            Assert.AreEqual(2, NavigationManager.pageHistory.Count);
            Assert.AreEqual(PageID.HistoryPage, NavigationManager.pageHistory.Peek());

            UiBottomNav.Navigate(navigation, UiBottomNav.HistoryTab);
            Assert.AreEqual(2, NavigationManager.pageHistory.Count, "tapping the current tab again does not stack it");

            UiBottomNav.Navigate(navigation, UiBottomNav.ArTab);
            Assert.AreEqual(1, NavigationManager.pageHistory.Count);
            Assert.AreEqual(PageID.ARPage, NavigationManager.pageHistory.Peek());
            Assert.IsTrue(IsShown(UiCanvasOnboardingBuilder.SplashName));
        }

        [Test]
        public void BottomNav_WhenTheTabHasNoScreen_DoesNothing()
        {
            navigation.Navigate(PageID.ARPage);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, "[UiBottomNav] MainSettings screen is not available.");

            UiBottomNav.Navigate(navigation, UiBottomNav.SettingsTab);

            Assert.AreEqual(1, NavigationManager.pageHistory.Count);
        }

        [Test]
        public void ReturnTo_WhenThePageIsInTheHistory_DropsEverythingAfterIt()
        {
            navigation.Navigate(PageID.ARPage);
            navigation.Navigate(PageID.HistoryPage);
            navigation.Navigate(PageID.Chatbox);

            Assert.IsTrue(navigation.ReturnTo(PageID.ARPage));
            Assert.AreEqual(1, NavigationManager.pageHistory.Count);
            Assert.IsFalse(navigation.ReturnTo(PageID.HistoryPage), "no longer in the history");
        }

        [Test]
        public void ResetTo_WhenCalled_LeavesOnlyTheTargetInTheHistory()
        {
            navigation.Navigate(PageID.ARPage);
            navigation.Navigate(PageID.HistoryPage);

            navigation.ResetTo(PageID.Chatbox);

            Assert.AreEqual(1, NavigationManager.pageHistory.Count);
            Assert.AreEqual(PageID.Chatbox, NavigationManager.pageHistory.Peek());
            Assert.IsTrue(IsShown(UiCanvasOnboardingBuilder.WelcomeName));
        }

        private void Invoke(string method)
        {
            typeof(NavigationManager)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(navigation, null);
        }

        private static void SetRoute(SerializedProperty route, PageID id, string screenName)
        {
            route.FindPropertyRelative("id").enumValueIndex = (int)id;
            route.FindPropertyRelative("canvasPage").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
        }

        private bool IsShown(string pageName)
        {
            Transform page = container.Find(pageName + "(Clone)");
            return page != null && page.gameObject.activeSelf;
        }
    }
}
#endif
