using System.Collections.Generic;
using ARNavB9V2.Handover;
using NUnit.Framework;
using UnityEngine;

namespace ARNavB9V2.Tests
{
    public sealed class B9V2HandoverGeometryTests
    {
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] != null)
                    Object.DestroyImmediate(spawnedObjects[i]);
            }
            spawnedObjects.Clear();
        }

        [Test]
        public void ContainsWorldPoint_WhenBoxIsRotated_UsesColliderLocalSpace()
        {
            GameObject root = Create("Volume");
            root.transform.SetPositionAndRotation(
                new Vector3(10f, 1f, -4f),
                Quaternion.Euler(0f, 35f, 0f));
            B9HandoverVolume volume = root.AddComponent<B9HandoverVolume>();

            GameObject segment = Create("Segment", root.transform);
            segment.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            BoxCollider box = segment.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 8f);
            volume.Configure(
                B9HandoverVolume.VolumeKind.InnerLocalization,
                new[] { box });

            Assert.IsTrue(volume.ContainsWorldPoint(
                segment.transform.TransformPoint(new Vector3(1.5f, 0f, 3.5f))));
            Assert.IsFalse(volume.ContainsWorldPoint(
                segment.transform.TransformPoint(new Vector3(2.5f, 0f, 0f))));
        }

        [Test]
        public void PortalMapping_WhenRoundTripped_PreservesPointAndRotation()
        {
            Transform outdoor = Create("Outdoor").transform;
            outdoor.SetPositionAndRotation(
                new Vector3(150f, 0f, -3f),
                Quaternion.Euler(0f, 25f, 0f));
            Transform indoor = Create("Indoor").transform;
            indoor.SetPositionAndRotation(
                new Vector3(-2.6f, -1.7f, -6.6f),
                Quaternion.Euler(0f, -112f, 0f));
            B9PortalAnchor portal = Create("Portal").AddComponent<B9PortalAnchor>();
            portal.Configure("B9-MAIN", "Main", "F1", true, outdoor, indoor);

            Vector3 campusPoint = outdoor.TransformPoint(new Vector3(1.2f, 0.4f, 4.8f));
            Quaternion campusRotation = outdoor.rotation * Quaternion.Euler(0f, 18f, 0f);
            Vector3 mapPoint = portal.CampusToMapWorldPoint(campusPoint);
            Quaternion mapRotation = portal.CampusToMapWorldRotation(campusRotation);

            Assert.That(
                Vector3.Distance(portal.MapWorldToCampusPoint(mapPoint), campusPoint),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(portal.MapWorldToCampusRotation(mapRotation), campusRotation),
                Is.LessThan(0.001f));
        }

        private GameObject Create(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            spawnedObjects.Add(gameObject);
            return gameObject;
        }
    }
}
