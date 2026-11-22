#if UNITY_EDITOR
using ARNavB9V2.UI;
using Game.Features.Home;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// The Canvas guidance overlay (NavigationHudUI) that replaced the UI Toolkit HUD of the B9 scene, and its link with
    /// the app shell's AR page. A fake model stands in for the B9 navigation logic. When the prefab is missing run
    /// Tools ▸ UI Canvas ▸ Run Navigation HUD Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasNavigationHudEditModeTests
    {
        private const string KeyPrefix = "UiCanvasNavigationHudTest_";
        private const string Panel = "Content/DestinationPanel/";
        private const string Research = "Content/ResearchPanel/Card/";

        [SetUp]
        public void SetUp()
        {
            ArNavigationBridge.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ArNavigationBridge.Reset();
        }

        // ------------------------------------------------------------------ prefab

        [Test]
        public void HudPrefab_WhenBuilt_HasEveryReferenceWired()
        {
            GameObject prefab = LoadPrefab();
            Assert.NotNull(prefab.GetComponent<NavigationHudUI>());
            PrefabAsserts.AssertAllReferencesWired(prefab, UiCanvasNavigationHudBuilder.HudName);
        }

        [Test]
        public void HudPrefab_WhenBuilt_HasItsOwnCanvasUnderTheAppCanvas()
        {
            GameObject prefab = LoadPrefab();
            var canvas = prefab.GetComponent<Canvas>();
            var appCanvas = AssetDatabase.LoadAssetAtPath<Canvas>(UiCanvasPaths.AppCanvasPrefab);

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Less(canvas.sortingOrder, appCanvas.sortingOrder, "the AR page's buttons must stay on top");
            Assert.NotNull(prefab.GetComponent<GraphicRaycaster>());
            Assert.NotNull(prefab.GetComponent<UiEventSystemEnsurer>(), "the B9 scene has no EventSystem of its own");
            Assert.AreEqual(UiTheme.ReferenceResolution, prefab.GetComponent<CanvasScaler>().referenceResolution);
        }

        [Test]
        public void HudPrefab_WhenBuilt_HasNoUiToolkitLeft()
        {
            foreach (Component component in LoadPrefab().GetComponentsInChildren<Component>(true))
            {
                Assert.AreNotEqual("UnityEngine.UIElements.UIDocument", component.GetType().FullName);
            }
        }

        // ------------------------------------------------------------------ drawing the state

        [Test]
        public void Render_WhenBound_ShowsTheThreeStatusLines()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                model.State.Status = "Đi theo mũi tên tới B9-104";
                model.State.Detail = "GPS ±4 m · còn 126 m";
                model.State.Summary = "Đang đi tới cửa B9 trước · đích cuối: B9-104";
                model.RaiseChanged();

                Assert.AreEqual("Đi theo mũi tên tới B9-104", harness.Find<TMP_Text>("Content/StatusCard/Status").text);
                Assert.AreEqual("GPS ±4 m · còn 126 m", harness.Find<TMP_Text>("Content/StatusCard/Detail").text);
                Assert.AreEqual("Đang đi tới cửa B9 trước · đích cuối: B9-104", harness.Find<TMP_Text>("Content/StatusCard/Summary").text);
                Assert.AreEqual("[Trong] B9 · B9-104", harness.Find<TMP_Text>(Panel + "DestinationField/Label").text);
            }
        }

        [Test]
        public void Render_WhenTheStateChanges_ShowsOnlyTheButtonsItAllows()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                model.State.RetryVisible = true;
                model.State.StartVisible = false;
                model.State.CancelVisible = false;
                model.State.ExitVisible = false;
                model.RaiseChanged();

                Assert.IsTrue(Active(harness, "Content/RetryButton"));
                Assert.IsFalse(Active(harness, Panel + "StartButton"));
                Assert.IsFalse(Active(harness, Panel + "Actions"), "an empty button row takes no room");

                model.State.ExitVisible = true;
                model.RaiseChanged();
                Assert.IsTrue(Active(harness, Panel + "Actions"));
                Assert.IsTrue(Active(harness, Panel + "Actions/ExitButton"));
            }
        }

        [Test]
        public void Render_WhenRunningAlone_OffersItsOwnCancelButton()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                model.State.CancelVisible = true;
                model.RaiseChanged();

                Assert.IsTrue(Active(harness, Panel + "Actions/CancelButton"));
                Assert.AreEqual(-20f, harness.Find<RectTransform>("Content/StatusCard").anchoredPosition.y, 0.01f);
            }
        }

        [Test]
        public void Render_WhenInsideTheShell_LeavesCancellingToTheArPageAndMovesClearOfIt()
        {
            using (var ar = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                ar.Open<ArHudUI>();
                model.State.CancelVisible = true;
                model.RaiseChanged();

                Assert.IsFalse(Active(harness, Panel + "Actions/CancelButton"));
                Assert.AreEqual(-104f, harness.Find<RectTransform>("Content/StatusCard").anchoredPosition.y, 0.01f);
                Assert.AreEqual(124f, harness.Find<RectTransform>("Content/DestinationPanel").anchoredPosition.y, 0.01f);
            }
        }

        [Test]
        public void Render_WhenTheMinimapIsExpanded_GrowsItAndChangesTheHint()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                var frame = harness.Find<RectTransform>("Content/Minimap");
                float small = frame.sizeDelta.x;

                model.State.MinimapExpanded = true;
                model.State.MinimapHint = B9NavigationHudState.ExpandedMinimapHint;
                model.RaiseChanged();

                Assert.Greater(frame.sizeDelta.x, small);
                Assert.AreEqual(B9NavigationHudState.ExpandedMinimapHint, harness.Find<TMP_Text>("Content/Minimap/Hint").text);
            }
        }

        [Test]
        public void Render_WhenTheResearchStateChanges_HighlightsTheActiveVersionAndDisablesButtons()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                model.State.ActiveVersionCode = "V2";
                model.State.HarmonyProfile = "HARMONY V2 · Reliable  Q✓ D–";
                model.State.MarkerEnabled = false;
                model.State.ExportEnabled = false;
                model.State.ExperimentToggleLabel = B9NavigationHudState.NewTrialLabel;
                model.RaiseChanged();

                Assert.AreEqual(UiTheme.Accent, harness.Find<Image>(Research + "Versions/Version_V2").color);
                Assert.AreEqual(UiTheme.Surface, harness.Find<Image>(Research + "Versions/Version_V1").color);
                Assert.IsFalse(harness.Find<Button>(Research + "LogButtons/MarkerButton").interactable);
                Assert.IsFalse(harness.Find<Button>(Research + "LogButtons/ExportButton").interactable);
                Assert.AreEqual(B9NavigationHudState.NewTrialLabel, harness.Find<TMP_Text>(Research + "LogButtons/TrialButton/Label").text);
                string harmony = harness.Find<TMP_Text>(Research + "Harmony").text;
                StringAssert.DoesNotContain("✓", harmony, "the fonts have no check mark glyph");
                StringAssert.Contains("●", harmony);
            }
        }

        // ------------------------------------------------------------------ taps

        [Test]
        public void Buttons_WhenTapped_SendTheirCommandToTheModel()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                model.State.RetryVisible = true;
                model.State.ExitVisible = true;
                model.RaiseChanged();

                harness.Click("Content/RetryButton");
                harness.Click("Content/Minimap");
                harness.Click(Panel + "StartButton");
                harness.Click(Panel + "Actions/CancelButton");
                harness.Click(Panel + "Actions/ExitButton");
                harness.Click(Research + "LogButtons/MarkerButton");
                harness.Click(Research + "LogButtons/TrialButton");
                harness.Click(Research + "LogButtons/ExportButton");
                harness.Click(Research + "Versions/Version_V1");

                CollectionAssert.AreEqual(
                    new[] { "retry", "minimap", "start", "cancel", "exit", "marker", "trial", "export", "version:V1" },
                    model.Commands);
            }
        }

        [Test]
        public void DestinationField_WhenAChoiceIsPicked_SelectsItInTheModel()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                harness.Click(Panel + "DestinationField");
                Assert.IsTrue(harness.Sheet.IsVisible);
                Assert.AreEqual(NavigationHudUI.PickerTitle, harness.Find<TMP_Text>("Content/PickerSheet/Panel/Title").text);

                harness.Find<UiOptionColumn>("Content/PickerSheet/Panel/Columns/Column1").Select(2, false);
                harness.Click("Content/PickerSheet/Panel/DoneButton");

                CollectionAssert.AreEqual(new[] { "select:[Ngoài] Thư viện" }, model.Commands);
            }
        }

        [Test]
        public void ResearchPanel_WhenOpened_SitsAboveTheAppCanvasUntilClosed()
        {
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                var canvas = harness.Root.GetComponent<Canvas>();
                int normal = canvas.sortingOrder;

                harness.Click("Content/StatusCard/ResearchButton");
                Assert.IsTrue(Active(harness, "Content/ResearchPanel"));
                Assert.Greater(canvas.sortingOrder, 100, "the assistant bar must not cover the panel");

                harness.Click(Research + "CloseButton");
                Assert.IsFalse(Active(harness, "Content/ResearchPanel"));
                Assert.AreEqual(normal, canvas.sortingOrder);
            }
        }

        // ------------------------------------------------------------------ link with the AR page

        [Test]
        public void Bridge_WhenARouteIsActive_ShowsItOnTheArPage()
        {
            using (var ar = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            using (Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                ArHudUI page = ar.Open<ArHudUI>();
                model.State.CancelVisible = true;
                model.State.ActiveDestination = "B9-104";
                model.RaiseChanged();

                Assert.AreEqual("B9-104", page.Destination);
                Assert.AreEqual("Đến B9-104", ar.Find<TMP_Text>("Content/PromptBubble/Label").text);

                model.State.CancelVisible = false;
                model.RaiseChanged();
                Assert.IsFalse(page.HasRoute);
            }
        }

        [Test]
        public void Bridge_WhenTheArPageClearsTheRoute_CancelsNavigation()
        {
            using (var ar = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                ar.Open<ArHudUI>();
                InvokeOnEnable(hud);
                model.State.CancelVisible = true;
                model.State.ActiveDestination = "B9-104";
                model.RaiseChanged();

                ar.Click("Content/ClearRouteButton");

                CollectionAssert.AreEqual(new[] { "cancel" }, model.Commands);
            }
        }

        [Test]
        public void Bridge_WhenAnotherAppPageCoversTheCamera_HidesTheHud()
        {
            using (var ar = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            using (var harness = Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                ArHudUI page = ar.Open<ArHudUI>();
                InvokeOnEnable(hud);
                Assert.IsTrue(hud.IsShown);

                page.Hide();
                Assert.IsFalse(hud.IsShown);

                page.Show();
                Assert.IsTrue(hud.IsShown);
            }
        }

        [Test]
        public void Bridge_WhenTheNavigationSceneRunsAlone_KeepsTheHudVisible()
        {
            using (Open(out NavigationHudUI hud, out FakeNavigationHudModel model))
            {
                Assert.IsFalse(ArNavigationBridge.ShellAttached);
                Assert.IsTrue(hud.IsShown);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(UiCanvasNavigationHudBuilder.HudName));
            Assert.NotNull(prefab, "NavigationHudUI.prefab — run Tools/UI Canvas/Run Navigation HUD Setup");
            return prefab;
        }

        private static PageHarness Open(out NavigationHudUI hud, out FakeNavigationHudModel model)
        {
            var harness = new PageHarness(UiCanvasNavigationHudBuilder.HudName, KeyPrefix);
            UiCanvasPreviewRenderer.FitNestedCanvas(harness.Root);
            PageHarness.Wake(harness.Root);
            hud = harness.Root.GetComponent<NavigationHudUI>();
            model = new FakeNavigationHudModel();
            hud.Bind(model);
            return harness;
        }

        /// <summary>Edit mode does not run OnEnable; the HUD subscribes to the bridge there.</summary>
        private static void InvokeOnEnable(NavigationHudUI hud)
        {
            typeof(NavigationHudUI)
                .GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(hud, null);
        }

        private static bool Active(PageHarness harness, string path)
        {
            Transform child = harness.Root.transform.Find(path);
            Assert.NotNull(child, path);
            return child.gameObject.activeSelf;
        }
    }
}
#endif
