using System.Linq;
using ARNavB9V2.Data;
using ARNavB9V2.Scene;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

namespace ARNavB9V2.Tests
{
    public sealed class B9V2FoundationTests
    {
        private const string DefinitionPath = "Assets/ARNavB9V2/Data/Buildings/B9BuildingDefinition.asset";
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";

        [Test]
        public void BuildingDefinition_ContainsB9AndRoom104()
        {
            B9BuildingDefinition definition =
                AssetDatabase.LoadAssetAtPath<B9BuildingDefinition>(DefinitionPath);

            Assert.NotNull(definition);
            Assert.AreEqual("B9", definition.BuildingId);
            Assert.AreEqual("MAP_9LME2PB7Y3EN", definition.PrimaryMapCode);
            Assert.IsTrue(definition.IsAcceptedMapId("MAP_9LME2PB7Y3EN"));
            Assert.IsTrue(definition.TryGetRoom("B9-104", out _));
            Assert.AreEqual(
                definition.Rooms.Count,
                definition.Rooms.Select(room => room.RoomId).Distinct().Count(),
                "Room IDs must be unique.");
        }

        [Test]
        public void FoundationScene_HasSingleArRigAndValidB9Context()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            B9SceneContext context =
                UnityEngine.Object.FindFirstObjectByType<B9SceneContext>();

            Assert.NotNull(context);
            Assert.IsTrue(context.ValidateConfiguration(out string failure), failure);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<ARSession>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<XROrigin>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, context.ArCamera.GetComponents<TrackedPoseDriver>().Length);
            Assert.IsFalse(context.ArCamera.cullingMask == context.MinimapCamera.cullingMask);
        }
    }
}
