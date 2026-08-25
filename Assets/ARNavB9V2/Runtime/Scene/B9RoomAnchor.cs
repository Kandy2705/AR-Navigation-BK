using UnityEngine;

namespace ARNavB9V2.Scene
{
    [DisallowMultipleComponent]
    public sealed class B9RoomAnchor : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField] private string floorId;

        public string RoomId => roomId;
        public string FloorId => floorId;

        public void Configure(string id, string floor)
        {
            roomId = id;
            floorId = floor;
        }
    }
}
