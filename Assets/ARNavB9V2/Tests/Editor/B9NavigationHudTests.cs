using System.Reflection;
using ARNavB9V2.Data;
using ARNavB9V2.Experiment;
using ARNavB9V2.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ARNavB9V2.Tests
{
    /// <summary>
    /// The HUD logic without its controllers: the states it reports when nothing is connected, the destination
    /// list built from the B9 definition, and the commands behind the buttons. Edit mode does not run OnEnable,
    /// so the tests call the same private BuildInterface the component runs when it wakes up.
    /// </summary>
    public sealed class B9NavigationHudTests
    {
        private const string DefinitionPath = "Assets/ARNavB9V2/Data/Buildings/B9BuildingDefinition.asset";
        private const string Room104 = "[Trong] B9 · B9-104";

        private GameObject host;
        private B9NavigationHud hud;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("HudTest");
            hud = host.AddComponent<B9NavigationHud>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void BuildInterface_WhenNoControllerIsConnected_AsksForGpsAndHidesRouteButtons()
        {
            Build(withBuilding: true);

            B9NavigationHudState state = hud.State;
            Assert.AreEqual("Không tìm thấy bộ GPS", state.Status);
            Assert.AreEqual("SchoolGround · chưa có vị trí ổn định", state.Detail);
            Assert.AreEqual(B9NavigationHudState.InitialSummary, state.Summary, "the summary keeps its first text until a route exists");
            Assert.IsTrue(state.StartVisible);
            Assert.IsFalse(state.RetryVisible);
            Assert.IsFalse(state.CancelVisible);
            Assert.IsFalse(state.ExitVisible);
        }

        [Test]
        public void BuildInterface_WhenNoLoggerIsConnected_DisablesTheTrialButtons()
        {
            Build(withBuilding: true);

            Assert.AreEqual("LOG · chưa kết nối", hud.State.Experiment);
            Assert.IsFalse(hud.State.ExperimentToggleEnabled);
            Assert.IsFalse(hud.State.MarkerEnabled);
        }

        [Test]
        public void BuildInterface_WithoutHarmonyController_ShowsTheFullHarmonyProfile()
        {
            Build(withBuilding: true);

            B9HarmonyExperimentProfile v5 = B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony);
            StringAssert.StartsWith("HARMONY " + v5.VersionCode + " · " + v5.DisplayName, hud.State.HarmonyProfile);
            Assert.AreEqual(v5.VersionCode, hud.State.ActiveVersionCode);
            Assert.AreEqual(9, hud.HarmonyVersionCodes.Count);
        }

        [Test]
        public void BuildInterface_WithTheB9Definition_ListsItsRoomsAndSelectsRoom104()
        {
            Build(withBuilding: true);

            B9BuildingDefinition definition = AssetDatabase.LoadAssetAtPath<B9BuildingDefinition>(DefinitionPath);
            foreach (B9BuildingDefinition.RoomDefinition room in definition.Rooms)
                CollectionAssert.Contains(hud.DestinationChoices, "[Trong] B9 · " + room.RoomId);
            Assert.AreEqual(Room104, hud.SelectedDestination);
        }

        [Test]
        public void BuildInterface_WithoutABuilding_StaysOnTheFirstState()
        {
            Build(withBuilding: false);

            Assert.AreEqual(B9NavigationHudState.InitialStatus, hud.State.Status);
            Assert.AreEqual(0, hud.DestinationChoices.Count);
            hud.SelectDestination(Room104);
            Assert.AreEqual(string.Empty, hud.SelectedDestination);
        }

        [Test]
        public void SelectDestination_WhenTheChoiceIsListed_SelectsItAndReportsTheRoom()
        {
            Build(withBuilding: true);
            string other = FirstChoiceOtherThan(Room104);
            int changes = 0;
            hud.Changed += () => changes++;

            hud.SelectDestination(other);

            Assert.AreEqual(other, hud.SelectedDestination);
            Assert.AreEqual(other.Substring("[Trong] B9 · ".Length), hud.State.ActiveDestination);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SelectDestination_WhenTheChoiceIsUnknownOrAlreadySelected_DoesNothing()
        {
            Build(withBuilding: true);
            int changes = 0;
            hud.Changed += () => changes++;

            hud.SelectDestination("[Trong] B9 · không có");
            hud.SelectDestination(Room104);

            Assert.AreEqual(Room104, hud.SelectedDestination);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void CancelNavigation_WhenARouteWasApplied_ForgetsTheDestination()
        {
            Build(withBuilding: true);
            hud.SelectDestination(FirstChoiceOtherThan(Room104));

            hud.CancelNavigation();

            Assert.AreEqual(string.Empty, hud.State.ActiveDestination);
            Assert.IsFalse(hud.State.CancelVisible);
        }

        [Test]
        public void ToggleMinimap_WhenTapped_SwitchesBetweenOverviewAndCloseUp()
        {
            Build(withBuilding: true);

            hud.ToggleMinimap();
            Assert.IsTrue(hud.State.MinimapExpanded);
            Assert.AreEqual(B9NavigationHudState.ExpandedMinimapHint, hud.State.MinimapHint);

            hud.ToggleMinimap();
            Assert.IsFalse(hud.State.MinimapExpanded);
            Assert.AreEqual(B9NavigationHudState.CollapsedMinimapHint, hud.State.MinimapHint);
        }

        [Test]
        public void Commands_WhenNoControllerIsConnected_DoNotThrow()
        {
            Build(withBuilding: true);

            Assert.DoesNotThrow(() =>
            {
                hud.StartNavigation();
                hud.ExitBuilding();
                hud.RetryVps();
                hud.AddResearchMarker();
                hud.ToggleTrial();
                hud.ExportLogs();
                hud.SelectHarmonyVersion("V1");
            });
        }

        private void Build(bool withBuilding)
        {
            if (withBuilding)
            {
                var serialized = new SerializedObject(hud);
                serialized.FindProperty("building").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<B9BuildingDefinition>(DefinitionPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            typeof(B9NavigationHud)
                .GetMethod("BuildInterface", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hud, null);
        }

        private string FirstChoiceOtherThan(string choice)
        {
            foreach (string candidate in hud.DestinationChoices)
            {
                if (candidate != choice && candidate.StartsWith("[Trong] B9 · "))
                    return candidate;
            }

            Assert.Inconclusive("The B9 definition has a single room.");
            return null;
        }
    }
}
