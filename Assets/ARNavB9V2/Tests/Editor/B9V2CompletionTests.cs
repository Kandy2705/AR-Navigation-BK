using System.Collections.Generic;
using System.Reflection;
using ARNavB9V2.Handover;
using ARNavB9V2.Indoor;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Tests
{
    public sealed class B9V2CompletionTests
    {
        [TestCase(1f)]
        [TestCase(-1f)]
        public void IndoorPose_WhenWalkingPastNavMeshEdge_PreservesMotionOutsideVolume(float direction)
        {
            var root = new GameObject("Exit regression fixture");
            NavMeshData data = null;
            NavMeshDataInstance instance = default;
            bool gyroEnabled = Input.gyro.enabled;
            bool compassEnabled = Input.compass.enabled;
            try
            {
                // Isolate this walkable patch from any NavMesh in the open scene.
                Vector3 origin = new Vector3(10000f, 0f, 10000f);
                var sources = new List<NavMeshBuildSource>
                {
                    new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        size = new Vector3(4f, 0.2f, 4f),
                        transform = Matrix4x4.TRS(
                            origin + Vector3.down * 0.1f, Quaternion.identity, Vector3.one),
                        area = 0,
                    },
                };
                data = NavMeshBuilder.BuildNavMeshData(
                    NavMesh.GetSettingsByIndex(0), sources,
                    new Bounds(origin, Vector3.one * 10f), Vector3.zero, Quaternion.identity);
                Assert.NotNull(data);
                instance = NavMesh.AddNavMeshData(data);
                var surface = root.AddComponent<NavMeshSurface>();
                surface.navMeshData = data;

                var cameraObject = new GameObject("Tracked camera");
                cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = origin + Vector3.up * 1.5f;
                var tracker = root.AddComponent<B9IndoorPoseTracker>();
                tracker.Configure(camera, surface, null);
                tracker.BeginTracking();
                MethodInfo update = typeof(B9IndoorPoseTracker).GetMethod(
                    "Update", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(update);

                for (int i = 0; i < 12; i++)
                {
                    camera.transform.position += Vector3.right * (direction * 0.25f);
                    update.Invoke(tracker, null);
                }

                var volumeObject = new GameObject("Indoor boundary");
                volumeObject.transform.SetParent(root.transform);
                volumeObject.transform.position = origin;
                var box = volumeObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(4f, 4f, 4f);
                var volume = volumeObject.AddComponent<B9HandoverVolume>();
                volume.Configure(B9HandoverVolume.VolumeKind.InnerLocalization, new[] { box });

                Assert.IsTrue(tracker.IsNavMeshConstrained);
                Assert.IsTrue(volume.ContainsWorldPoint(tracker.RawPosition),
                    "The navigation output should still project onto the indoor patch.");
                Assert.IsFalse(volume.ContainsWorldPoint(tracker.UnconstrainedPosition),
                    "The handover estimate must escape the boundary despite projection.");
                Assert.That(tracker.UnconstrainedPosition.x,
                    Is.EqualTo(camera.transform.position.x).Within(0.05f));

                tracker.StopTracking();
                camera.transform.position = origin + Vector3.up * 1.5f;
                tracker.BeginTracking();
                Assert.That(tracker.UnconstrainedPosition.x, Is.EqualTo(origin.x).Within(0.05f),
                    "A new indoor session must reset the accumulated exit displacement.");
            }
            finally
            {
                instance.Remove();
                Object.DestroyImmediate(root);
                if (data != null)
                    Object.DestroyImmediate(data);
                Input.gyro.enabled = gyroEnabled;
                Input.compass.enabled = compassEnabled;
            }
        }

        [Test]
        public void StepDetector_WhenPeakReleasedAndIntervalPassed_DetectsNextStep()
        {
            var detector = new B9StepDetector(0.1f, 0.04f, 0.25f);

            Assert.IsFalse(detector.Process(0.03f, 0f));
            Assert.IsTrue(detector.Process(0.12f, 0.3f));
            Assert.IsFalse(detector.Process(0.14f, 0.4f));
            Assert.IsFalse(detector.Process(0.02f, 0.5f));
            Assert.IsTrue(detector.Process(0.11f, 0.6f));
        }

        [Test]
        public void StepDetector_WhenPeakNeverReleases_DoesNotDoubleCount()
        {
            var detector = new B9StepDetector(0.1f, 0.04f, 0.2f);

            Assert.IsTrue(detector.Process(0.15f, 0.3f));
            Assert.IsFalse(detector.Process(0.13f, 0.6f));
            Assert.IsFalse(detector.Process(0.12f, 1f));
        }
    }
}
