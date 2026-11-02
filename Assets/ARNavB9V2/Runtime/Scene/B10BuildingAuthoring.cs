using ARNavB9V2.Handover;
using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace ARNavB9V2.Scene
{
    /// <summary>Authored B10 geometry; runtime routing is connected after portals and POIs are supplied.</summary>
    [DisallowMultipleComponent]
    public sealed class B10BuildingAuthoring : MonoBehaviour
    {
        [SerializeField] private string buildingId = "B10";
        [SerializeField] private string mapCode = "MAP_51D7QG8HJNYS";
        [SerializeField] private Transform campusPlacement;
        [SerializeField] private Transform campusModel;
        [SerializeField] private Transform indoorMapSpace;
        [SerializeField] private Transform indoorModel;
        [SerializeField] private NavMeshSurface indoorNavMesh;
        [SerializeField] private B9HandoverVolume outerTransitionVolume;
        [SerializeField] private B9HandoverVolume innerScanVolume;
        [SerializeField] private Transform portalsRoot;
        [SerializeField] private Transform roomsRoot;
        [SerializeField] private float scannedFloorLocalY;
        [TextArea(3, 6)]
        [SerializeField] private string setupStatus = "Runtime connected: five shared entrance/exit anchors, B10 room POIs, MultiSet map-space switching, PDR handover, and dedicated indoor NavMesh.";

        public string BuildingId => buildingId;
        public string MapCode => mapCode;
        public Transform CampusPlacement => campusPlacement;
        public Transform CampusModel => campusModel;
        public Transform IndoorMapSpace => indoorMapSpace;
        public Transform IndoorModel => indoorModel;
        public NavMeshSurface IndoorNavMesh => indoorNavMesh;
        public B9HandoverVolume OuterTransitionVolume => outerTransitionVolume;
        public B9HandoverVolume InnerScanVolume => innerScanVolume;
        public Transform PortalsRoot => portalsRoot;
        public Transform RoomsRoot => roomsRoot;
        public IReadOnlyList<B9RoomAnchor> RoomAnchors => roomsRoot != null
            ? roomsRoot.GetComponentsInChildren<B9RoomAnchor>(true)
            : Array.Empty<B9RoomAnchor>();

        public Vector3 CampusToMapWorldPoint(Vector3 campusWorldPoint)
        {
            if (campusPlacement == null || indoorMapSpace == null)
                return campusWorldPoint;
            return indoorMapSpace.TransformPoint(
                campusPlacement.InverseTransformPoint(campusWorldPoint));
        }

        public Vector3 MapWorldToCampusPoint(Vector3 mapWorldPoint)
        {
            if (campusPlacement == null || indoorMapSpace == null)
                return mapWorldPoint;
            return campusPlacement.TransformPoint(
                indoorMapSpace.InverseTransformPoint(mapWorldPoint));
        }

        public Quaternion CampusToMapWorldRotation(Quaternion campusWorldRotation)
        {
            if (campusPlacement == null || indoorMapSpace == null)
                return campusWorldRotation;
            return indoorMapSpace.rotation
                   * Quaternion.Inverse(campusPlacement.rotation)
                   * campusWorldRotation;
        }

        public bool ContainsOuterCampusPoint(Vector3 campusWorldPoint)
        {
            return outerTransitionVolume != null
                   && outerTransitionVolume.ContainsWorldPoint(campusWorldPoint);
        }

        public bool ContainsInnerCampusPoint(Vector3 campusWorldPoint)
        {
            return innerScanVolume != null
                   && innerScanVolume.ContainsWorldPoint(campusWorldPoint);
        }

        public bool ContainsInnerMapWorldPoint(Vector3 mapWorldPoint)
        {
            return ContainsInnerCampusPoint(MapWorldToCampusPoint(mapWorldPoint));
        }

        public bool TryGetNearestCampusEntrance(
            Vector3 campusWorldPoint,
            out Transform entrance)
        {
            entrance = null;
            if (portalsRoot == null)
                return false;

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < portalsRoot.childCount; i++)
            {
                Transform candidate = portalsRoot.GetChild(i);
                float distance = Vector3.Distance(campusWorldPoint, candidate.position);
                if (distance >= nearestDistance)
                    continue;
                nearestDistance = distance;
                entrance = candidate;
            }
            return entrance != null;
        }

        public bool TryGetNearestMapEntrance(
            Vector3 mapWorldPoint,
            out Transform campusEntrance,
            out Vector3 indoorMapPoint)
        {
            campusEntrance = null;
            indoorMapPoint = Vector3.zero;
            if (portalsRoot == null)
                return false;

            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < portalsRoot.childCount; i++)
            {
                Transform candidate = portalsRoot.GetChild(i);
                Vector3 candidateMapPoint = CampusToMapWorldPoint(candidate.position);
                float distance = Vector3.Distance(mapWorldPoint, candidateMapPoint);
                if (distance >= nearestDistance)
                    continue;
                nearestDistance = distance;
                campusEntrance = candidate;
                indoorMapPoint = candidateMapPoint;
            }
            return campusEntrance != null;
        }

        public bool ValidateRuntimeConfiguration(out string reason)
        {
            if (string.IsNullOrWhiteSpace(mapCode))
                return Fail("B10 MultiSet map code is missing", out reason);
            if (campusPlacement == null || indoorMapSpace == null || indoorModel == null)
                return Fail("B10 campus/map-space hierarchy is incomplete", out reason);
            if (indoorNavMesh == null || indoorNavMesh.navMeshData == null)
                return Fail("B10 indoor NavMesh is missing", out reason);
            if (outerTransitionVolume == null || innerScanVolume == null)
                return Fail("B10 handover volumes are missing", out reason);
            if (portalsRoot == null || portalsRoot.childCount == 0)
                return Fail("B10 entrance anchors are missing", out reason);
            if (roomsRoot == null || RoomAnchors.Count == 0)
                return Fail("B10 room anchors are missing", out reason);

            reason = string.Empty;
            return true;
        }

        public bool TryGetRoom(string roomId, out B9RoomAnchor room)
        {
            room = null;
            if (string.IsNullOrWhiteSpace(roomId))
                return false;

            IReadOnlyList<B9RoomAnchor> anchors = RoomAnchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                B9RoomAnchor candidate = anchors[i];
                if (candidate != null && string.Equals(
                        candidate.RoomId,
                        roomId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    room = candidate;
                    return true;
                }
            }

            return false;
        }

        public void Configure(Transform placement, Transform placedModel, Transform mapSpace,
            Transform localModel, NavMeshSurface surface, B9HandoverVolume outer,
            B9HandoverVolume inner, Transform portals, Transform rooms, float floorY)
        {
            campusPlacement = placement;
            campusModel = placedModel;
            indoorMapSpace = mapSpace;
            indoorModel = localModel;
            indoorNavMesh = surface;
            outerTransitionVolume = outer;
            innerScanVolume = inner;
            portalsRoot = portals;
            roomsRoot = rooms;
            scannedFloorLocalY = floorY;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
