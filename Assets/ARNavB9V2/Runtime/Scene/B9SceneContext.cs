using System.Collections.Generic;
using ARNavB9V2.Data;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ARNavB9V2.Scene
{
    /// <summary>All authored references required by the clean B9 V2 foundation scene.</summary>
    [DisallowMultipleComponent]
    public sealed class B9SceneContext : MonoBehaviour
    {
        [SerializeField] private B9BuildingDefinition building;
        [SerializeField] private ARSession arSession;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Camera arCamera;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private Transform mapSpace;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private Transform outdoorEntranceAnchor;
        [SerializeField] private Transform indoorEntranceAnchor;
        [SerializeField] private List<B9RoomAnchor> roomAnchors = new List<B9RoomAnchor>();

        public B9BuildingDefinition Building => building;
        public ARSession ArSession => arSession;
        public XROrigin XrOrigin => xrOrigin;
        public Camera ArCamera => arCamera;
        public Camera MinimapCamera => minimapCamera;
        public Transform MapSpace => mapSpace;
        public Transform ModelRoot => modelRoot;
        public NavMeshSurface NavMeshSurface => navMeshSurface;
        public Transform OutdoorEntranceAnchor => outdoorEntranceAnchor;
        public Transform IndoorEntranceAnchor => indoorEntranceAnchor;
        public IReadOnlyList<B9RoomAnchor> RoomAnchors => roomAnchors;

        public void Configure(
            B9BuildingDefinition definition,
            ARSession session,
            XROrigin origin,
            Camera displayCamera,
            Camera mapCamera,
            Transform mapSpaceRoot,
            Transform model,
            NavMeshSurface surface,
            Transform outdoorEntrance,
            Transform indoorEntrance,
            IReadOnlyList<B9RoomAnchor> anchors)
        {
            building = definition;
            arSession = session;
            xrOrigin = origin;
            arCamera = displayCamera;
            minimapCamera = mapCamera;
            mapSpace = mapSpaceRoot;
            modelRoot = model;
            navMeshSurface = surface;
            outdoorEntranceAnchor = outdoorEntrance;
            indoorEntranceAnchor = indoorEntrance;
            roomAnchors = anchors != null
                ? new List<B9RoomAnchor>(anchors)
                : new List<B9RoomAnchor>();
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (building == null) return Fail("B9BuildingDefinition missing", out reason);
            if (arSession == null) return Fail("ARSession missing", out reason);
            if (xrOrigin == null) return Fail("XROrigin missing", out reason);
            if (arCamera == null) return Fail("AR camera missing", out reason);
            if (minimapCamera == null) return Fail("Minimap camera missing", out reason);
            if (mapSpace == null || modelRoot == null) return Fail("B9 map hierarchy missing", out reason);
            if (navMeshSurface == null || navMeshSurface.navMeshData == null)
                return Fail("B9 NavMesh data missing", out reason);
            if (outdoorEntranceAnchor == null || indoorEntranceAnchor == null)
                return Fail("B9 entrance anchors missing", out reason);
            if (roomAnchors == null || roomAnchors.Count == 0)
                return Fail("B9 room anchors missing", out reason);

            bool has104 = false;
            for (int i = 0; i < roomAnchors.Count; i++)
            {
                if (roomAnchors[i] != null && roomAnchors[i].RoomId == "B9-104")
                {
                    has104 = true;
                    break;
                }
            }
            if (!has104) return Fail("B9-104 anchor missing", out reason);

            reason = string.Empty;
            return true;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
