#if UNITY_EDITOR
using System.Reflection;
using ARNav.Hybrid;
using NUnit.Framework;
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    [Category("TestAR")]
    public sealed class HybridHandoverSafetyTests
    {
        [Test]
        public void FallbackPose_BeforeLocalizationSuccess_IsNotFresh()
        {
            GameObject root = new GameObject("handover-provider-test");
            GameObject cameraGo = new GameObject("camera");
            GameObject mapSpaceGo = new GameObject("Map Space");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MultisetPoseProvider provider = root.AddComponent<MultisetPoseProvider>();
                SetField(provider, "arCamera", camera);
                SetField(provider, "mapSpace", mapSpaceGo.transform);

                Invoke(provider, "TryUpdateFromMapSpaceFallback");

                Assert.IsFalse(provider.HasVerifiedLocalization);
                Assert.IsFalse(provider.HasFreshPose,
                    "Camera + MapSpace không được tự biến thành VPS success.");
                Assert.AreEqual(MultisetPoseProvider.PoseSource.None, provider.Last.Source);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(mapSpaceGo);
            }
        }

        [Test]
        public void FallbackPose_AfterLocalizationSuccess_IsFresh()
        {
            GameObject root = new GameObject("handover-provider-test");
            GameObject cameraGo = new GameObject("camera");
            GameObject mapSpaceGo = new GameObject("Map Space");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MultisetPoseProvider provider = root.AddComponent<MultisetPoseProvider>();
                SetField(provider, "arCamera", camera);
                SetField(provider, "mapSpace", mapSpaceGo.transform);

                Invoke(provider, "OnNoArgLocalizationFired");

                Assert.IsTrue(provider.HasVerifiedLocalization);
                Assert.IsTrue(provider.HasFreshPose);
                Assert.AreEqual(MultisetPoseProvider.PoseSource.MapSpaceFallback, provider.Last.Source);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(mapSpaceGo);
            }
        }

        [Test]
        public void SetCurrentBuilding_WhenMapChanges_RevokesPreviousLocalization()
        {
            GameObject root = new GameObject("handover-provider-test");
            try
            {
                MultisetPoseProvider provider = root.AddComponent<MultisetPoseProvider>();
                Invoke(provider, "OnNoArgLocalizationFired");
                Assert.IsTrue(provider.HasVerifiedLocalization);

                provider.SetCurrentBuilding(BuildingId.B9, "F1");

                Assert.IsFalse(provider.HasVerifiedLocalization);
                Assert.IsFalse(provider.HasFreshPose);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalizationFailure_RevokesPreviousLocalization()
        {
            GameObject root = new GameObject("handover-provider-test");
            try
            {
                MultisetPoseProvider provider = root.AddComponent<MultisetPoseProvider>();
                Invoke(provider, "OnNoArgLocalizationFired");
                Assert.IsTrue(provider.HasVerifiedLocalization);

                Invoke(provider, "OnLocalizationFailureFired");

                Assert.IsFalse(provider.HasVerifiedLocalization);
                Assert.IsFalse(provider.HasFreshPose);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeCalibration_MapsHandoverPoseToCampusDoor()
        {
            GameObject root = new GameObject("handover-calibration-test");
            try
            {
                IndoorMapCalibration calibration = root.AddComponent<IndoorMapCalibration>();
                Vector3 localDoor = new Vector3(4f, 0f, -2f);
                Vector3 campusDoor = new Vector3(153f, 0f, -3f);
                Quaternion localRotation = Quaternion.Euler(0f, 30f, 0f);
                Quaternion campusRotation = Quaternion.Euler(0f, 90f, 0f);

                calibration.ConfigureRuntimeHandover(
                    localDoor, localRotation, campusDoor, campusRotation);
                calibration.MapLocalToCampusWorld(
                    localDoor, localRotation, out Vector3 mappedPosition, out Quaternion mappedRotation);

                Assert.That(Vector3.Distance(mappedPosition, campusDoor), Is.LessThan(0.001f));
                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(mappedRotation.eulerAngles.y, campusRotation.eulerAngles.y)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {name}");
            field.SetValue(instance, value);
        }

        private static void Invoke(object instance, string name)
        {
            MethodInfo method = instance.GetType().GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"Missing method {name}");
            method.Invoke(instance, null);
        }
    }
}
#endif
