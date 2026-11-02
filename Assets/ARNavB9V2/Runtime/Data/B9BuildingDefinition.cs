using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNavB9V2.Data
{
    /// <summary>
    /// Authoritative B9 metadata for the clean V2 navigation stack.
    /// Positions named MapLocal are expressed relative to the V2 Map Space root.
    /// </summary>
    [CreateAssetMenu(fileName = "B9BuildingDefinition", menuName = "AR Navigation V2/B9 Building Definition")]
    public sealed class B9BuildingDefinition : ScriptableObject
    {
        public const string CurrentMapCode = "MAP_FYN7L0XV6XBY";

        [Serializable]
        public sealed class RoomDefinition
        {
            [SerializeField] private string roomId;
            [SerializeField] private string displayName;
            [SerializeField] private string floorId;
            [SerializeField] private Vector3 mapLocalPosition;
            [SerializeField] private Quaternion mapLocalRotation = Quaternion.identity;

            public string RoomId => roomId;
            public string DisplayName => displayName;
            public string FloorId => floorId;
            public Vector3 MapLocalPosition => mapLocalPosition;
            public Quaternion MapLocalRotation => mapLocalRotation;

            public RoomDefinition(
                string roomId,
                string displayName,
                string floorId,
                Vector3 mapLocalPosition,
                Quaternion mapLocalRotation)
            {
                this.roomId = roomId;
                this.displayName = displayName;
                this.floorId = floorId;
                this.mapLocalPosition = mapLocalPosition;
                this.mapLocalRotation = mapLocalRotation;
            }
        }

        [Serializable]
        public sealed class PortalDefinition
        {
            [SerializeField] private string portalId;
            [SerializeField] private string displayName;
            [SerializeField] private string floorId;
            [SerializeField] private bool primary;
            [SerializeField] private Vector3 outdoorCampusPosition;
            [SerializeField] private Quaternion outdoorCampusRotation = Quaternion.identity;
            [SerializeField] private Vector3 indoorMapLocalPosition;
            [SerializeField] private Quaternion indoorMapLocalRotation = Quaternion.identity;

            public string PortalId => portalId;
            public string DisplayName => displayName;
            public string FloorId => floorId;
            public bool Primary => primary;
            public Vector3 OutdoorCampusPosition => outdoorCampusPosition;
            public Quaternion OutdoorCampusRotation => outdoorCampusRotation;
            public Vector3 IndoorMapLocalPosition => indoorMapLocalPosition;
            public Quaternion IndoorMapLocalRotation => indoorMapLocalRotation;

            public PortalDefinition(
                string portalId,
                string displayName,
                string floorId,
                bool primary,
                Vector3 outdoorCampusPosition,
                Quaternion outdoorCampusRotation,
                Vector3 indoorMapLocalPosition,
                Quaternion indoorMapLocalRotation)
            {
                this.portalId = portalId;
                this.displayName = displayName;
                this.floorId = floorId;
                this.primary = primary;
                this.outdoorCampusPosition = outdoorCampusPosition;
                this.outdoorCampusRotation = outdoorCampusRotation;
                this.indoorMapLocalPosition = indoorMapLocalPosition;
                this.indoorMapLocalRotation = indoorMapLocalRotation;
            }
        }

        [Serializable]
        public sealed class HandoverSegmentDefinition
        {
            [SerializeField] private Vector3 startMapLocalPosition;
            [SerializeField] private Vector3 endMapLocalPosition;
            [Min(0.5f)] [SerializeField] private float innerWidthMeters = 5f;
            [Min(1f)] [SerializeField] private float heightMeters = 5f;
            [SerializeField] private float verticalCenterMeters = 0.4f;

            public Vector3 StartMapLocalPosition => startMapLocalPosition;
            public Vector3 EndMapLocalPosition => endMapLocalPosition;
            public float InnerWidthMeters => innerWidthMeters;
            public float HeightMeters => heightMeters;
            public float VerticalCenterMeters => verticalCenterMeters;

            public HandoverSegmentDefinition(
                Vector3 startMapLocalPosition,
                Vector3 endMapLocalPosition,
                float innerWidthMeters,
                float heightMeters,
                float verticalCenterMeters)
            {
                this.startMapLocalPosition = startMapLocalPosition;
                this.endMapLocalPosition = endMapLocalPosition;
                this.innerWidthMeters = innerWidthMeters;
                this.heightMeters = heightMeters;
                this.verticalCenterMeters = verticalCenterMeters;
            }
        }

        [Header("Identity")]
        [SerializeField] private string buildingId = "B9";
        [SerializeField] private string displayName = "Tòa B9";
        [SerializeField] private string primaryMapCode = CurrentMapCode;
        [SerializeField] private List<string> acceptedMapIds = new List<string>();

        [Header("Outdoor entrance")]
        [SerializeField] private double entranceLatitude = 10.7734d;
        [SerializeField] private double entranceLongitude = 106.660375d;
        [SerializeField] private Vector3 entranceCampusPosition;
        [Min(1f)] [SerializeField] private float transitionRadiusMeters = 15f;

        [Header("Indoor reference")]
        [SerializeField] private Vector3 indoorEntranceMapLocalPosition;
        [SerializeField] private Quaternion indoorEntranceMapLocalRotation = Quaternion.identity;

        [Header("Handover geometry")]
        [Min(0.5f)] [SerializeField] private float outerPaddingMeters = 3f;
        [SerializeField] private List<HandoverSegmentDefinition> handoverSegments = new List<HandoverSegmentDefinition>();
        [SerializeField] private List<PortalDefinition> portals = new List<PortalDefinition>();

        [Header("Rooms")]
        [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

        public string BuildingId => buildingId;
        public string DisplayName => displayName;
        public string PrimaryMapCode => primaryMapCode;
        public IReadOnlyList<string> AcceptedMapIds => acceptedMapIds;
        public double EntranceLatitude => entranceLatitude;
        public double EntranceLongitude => entranceLongitude;
        public Vector3 EntranceCampusPosition => entranceCampusPosition;
        public float TransitionRadiusMeters => transitionRadiusMeters;
        public Vector3 IndoorEntranceMapLocalPosition => indoorEntranceMapLocalPosition;
        public Quaternion IndoorEntranceMapLocalRotation => indoorEntranceMapLocalRotation;
        public float OuterPaddingMeters => outerPaddingMeters;
        public IReadOnlyList<HandoverSegmentDefinition> HandoverSegments => handoverSegments;
        public IReadOnlyList<PortalDefinition> Portals => portals;
        public IReadOnlyList<RoomDefinition> Rooms => rooms;

        public bool TryGetRoom(string roomId, out RoomDefinition room)
        {
            room = null;
            if (string.IsNullOrWhiteSpace(roomId))
                return false;

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomDefinition candidate = rooms[i];
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

        public bool IsAcceptedMapId(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return false;

            for (int i = 0; i < acceptedMapIds.Count; i++)
            {
                if (string.Equals(acceptedMapIds[i], mapId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public void Configure(
            double latitude,
            double longitude,
            Vector3 campusEntrance,
            Vector3 indoorEntrance,
            Quaternion indoorEntranceRotation,
            IReadOnlyList<RoomDefinition> importedRooms)
        {
            buildingId = "B9";
            displayName = "Tòa B9";
            primaryMapCode = CurrentMapCode;
            acceptedMapIds = new List<string> { primaryMapCode };
            entranceLatitude = latitude;
            entranceLongitude = longitude;
            entranceCampusPosition = campusEntrance;
            transitionRadiusMeters = 15f;
            indoorEntranceMapLocalPosition = indoorEntrance;
            indoorEntranceMapLocalRotation = indoorEntranceRotation;
            rooms = importedRooms != null
                ? new List<RoomDefinition>(importedRooms)
                : new List<RoomDefinition>();
        }

        public void ConfigureHandoverGeometry(
            float outerPadding,
            IReadOnlyList<HandoverSegmentDefinition> segments,
            IReadOnlyList<PortalDefinition> portalDefinitions)
        {
            outerPaddingMeters = Mathf.Max(0.5f, outerPadding);
            handoverSegments = segments != null
                ? new List<HandoverSegmentDefinition>(segments)
                : new List<HandoverSegmentDefinition>();
            portals = portalDefinitions != null
                ? new List<PortalDefinition>(portalDefinitions)
                : new List<PortalDefinition>();
        }

        public void RemapIndoorCoordinates(Vector3 translation, Quaternion rotation)
        {
            indoorEntranceMapLocalPosition = rotation * indoorEntranceMapLocalPosition + translation;
            indoorEntranceMapLocalRotation = rotation * indoorEntranceMapLocalRotation;

            var remappedRooms = new List<RoomDefinition>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomDefinition room = rooms[i];
                remappedRooms.Add(new RoomDefinition(
                    room.RoomId,
                    room.DisplayName,
                    room.FloorId,
                    rotation * room.MapLocalPosition + translation,
                    rotation * room.MapLocalRotation));
            }
            rooms = remappedRooms;

            var remappedSegments = new List<HandoverSegmentDefinition>(handoverSegments.Count);
            for (int i = 0; i < handoverSegments.Count; i++)
            {
                HandoverSegmentDefinition segment = handoverSegments[i];
                remappedSegments.Add(new HandoverSegmentDefinition(
                    rotation * segment.StartMapLocalPosition + translation,
                    rotation * segment.EndMapLocalPosition + translation,
                    segment.InnerWidthMeters,
                    segment.HeightMeters,
                    segment.VerticalCenterMeters));
            }
            handoverSegments = remappedSegments;

            var remappedPortals = new List<PortalDefinition>(portals.Count);
            for (int i = 0; i < portals.Count; i++)
            {
                PortalDefinition portal = portals[i];
                remappedPortals.Add(new PortalDefinition(
                    portal.PortalId,
                    portal.DisplayName,
                    portal.FloorId,
                    portal.Primary,
                    portal.OutdoorCampusPosition,
                    portal.OutdoorCampusRotation,
                    rotation * portal.IndoorMapLocalPosition + translation,
                    rotation * portal.IndoorMapLocalRotation));
            }
            portals = remappedPortals;
        }

        private void OnValidate()
        {
            transitionRadiusMeters = Mathf.Max(1f, transitionRadiusMeters);
            outerPaddingMeters = Mathf.Max(0.5f, outerPaddingMeters);
            acceptedMapIds ??= new List<string>();
            handoverSegments ??= new List<HandoverSegmentDefinition>();
            portals ??= new List<PortalDefinition>();
            rooms ??= new List<RoomDefinition>();
        }
    }
}
