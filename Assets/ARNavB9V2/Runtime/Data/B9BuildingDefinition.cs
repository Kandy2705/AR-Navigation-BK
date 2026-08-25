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

        [Header("Identity")]
        [SerializeField] private string buildingId = "B9";
        [SerializeField] private string displayName = "Tòa B9";
        [SerializeField] private string primaryMapCode = "MAP_9LME2PB7Y3EN";
        [SerializeField] private List<string> acceptedMapIds = new List<string>();

        [Header("Outdoor entrance")]
        [SerializeField] private double entranceLatitude = 10.7734d;
        [SerializeField] private double entranceLongitude = 106.660375d;
        [SerializeField] private Vector3 entranceCampusPosition;
        [Min(1f)] [SerializeField] private float transitionRadiusMeters = 15f;

        [Header("Indoor reference")]
        [SerializeField] private Vector3 indoorEntranceMapLocalPosition;
        [SerializeField] private Quaternion indoorEntranceMapLocalRotation = Quaternion.identity;

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
            primaryMapCode = "MAP_9LME2PB7Y3EN";
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

        private void OnValidate()
        {
            transitionRadiusMeters = Mathf.Max(1f, transitionRadiusMeters);
            acceptedMapIds ??= new List<string>();
            rooms ??= new List<RoomDefinition>();
        }
    }
}
