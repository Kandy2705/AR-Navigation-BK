using ARNavB9V2.Data;
using ARNavB9V2.Scene;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ARNavB9V2.Tests
{
    public sealed class B9V2OutdoorTests
    {
        private const string DefinitionPath = "Assets/ARNavB9V2/Data/Buildings/B9OutdoorMapDefinition.asset";
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";

        [Test]
        public void OutdoorDefinition_MapsEntranceGpsBackToEntranceAnchor()
        {
            B9OutdoorMapDefinition definition =
                AssetDatabase.LoadAssetAtPath<B9OutdoorMapDefinition>(DefinitionPath);

            Assert.NotNull(definition);
            Assert.AreEqual("SchoolGround", definition.MapId);
            Assert.AreEqual(10.7734854d, definition.OriginLatitude, 0.00000001d);
            Assert.AreEqual(106.6590233d, definition.OriginLongitude, 0.00000001d);
            Assert.Less(
                definition.GpsToCampus(
                    definition.OriginLatitude,
                    definition.OriginLongitude).sqrMagnitude,
                0.000001f);
            Assert.Greater(definition.SchoolGroundBounds.size.x, 900f);
            Assert.Greater(definition.SchoolGroundBounds.size.z, 500f);
            Assert.Less(
                Vector3.Distance(
                    definition.GpsToCampus(
                        definition.EntranceLatitude,
                        definition.EntranceLongitude),
                    definition.EntranceCampusPosition),
                0.1f);

            definition.CampusToGps(
                definition.EditorMockStartCampusPosition,
                out double mockLatitude,
                out double mockLongitude);
            Assert.Less(
                Vector3.Distance(
                    definition.GpsToCampus(mockLatitude, mockLongitude),
                    new Vector3(
                        definition.EditorMockStartCampusPosition.x,
                        0f,
                        definition.EditorMockStartCampusPosition.z)),
                0.02f);
        }

        [Test]
        public void OutdoorScene_HasSchoolGroundRouteMinimapAndKeepsIndoorModelOff()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            B9SceneContext foundation = Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            B9OutdoorSceneContext outdoor = Object.FindFirstObjectByType<B9OutdoorSceneContext>(
                FindObjectsInactive.Include);

            Assert.NotNull(foundation);
            Assert.NotNull(outdoor);
            Assert.IsTrue(outdoor.ValidateConfiguration(out string failure), failure);
            Assert.IsFalse(foundation.ModelRoot.gameObject.activeSelf);
            Assert.AreEqual("B9-104", outdoor.RouteController.SelectedRoomId);
            Assert.NotNull(outdoor.SchoolGroundNavMesh.navMeshData);
            Assert.IsTrue(outdoor.MinimapController.RenderedTexture.width >= 512);
        }

        [Test]
        public void OutdoorRoute_RoutesToEntranceAndKeepsGuideWhileVpsIsPending()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            B9OutdoorSceneContext outdoor = Object.FindFirstObjectByType<B9OutdoorSceneContext>(
                FindObjectsInactive.Include);
            Assert.NotNull(outdoor);

            outdoor.LocationProvider.SetSimulatedCampusPosition(
                outdoor.OutdoorMap.EditorMockStartCampusPosition);
            Assert.IsTrue(outdoor.RouteController.SetDestinationRoom("B9-104"));
            outdoor.RouteController.RefreshNow();
            Assert.AreEqual(
                ARNavB9V2.Outdoor.B9OutdoorRouteController.RouteState.NavigatingToB9Entrance,
                outdoor.RouteController.State);
            Assert.IsTrue(outdoor.RibbonRenderer.HasVisiblePath);
            Assert.NotNull(outdoor.RibbonRenderer.RouteMesh);
            Assert.AreEqual(2, outdoor.RibbonRenderer.RouteMesh.subMeshCount);
            Assert.NotNull(outdoor.RibbonRenderer.RouteMeshRenderer);
            Assert.AreEqual(2, outdoor.RibbonRenderer.RouteMeshRenderer.sharedMaterials.Length);
            Assert.NotNull(outdoor.RibbonRenderer.RouteMeshRenderer.sharedMaterials[0].mainTexture);
            Assert.AreEqual("RouteBlue", outdoor.RibbonRenderer.RouteMeshRenderer.sharedMaterials[0].name);
            Assert.AreEqual("RouteBorder", outdoor.RibbonRenderer.RouteMeshRenderer.sharedMaterials[1].name);

            outdoor.LocationProvider.SetSimulatedCampusPosition(
                outdoor.OutdoorMap.EntranceCampusPosition);
            outdoor.RouteController.RefreshNow();
            Assert.AreEqual(
                ARNavB9V2.Outdoor.B9OutdoorRouteController.RouteState.ArrivedAtB9Entrance,
                outdoor.RouteController.State);
            Assert.IsTrue(outdoor.RibbonRenderer.HasVisiblePath);
        }
    }
}
