using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARNavB9V2.Handover
{
    /// <summary>
    /// Authoritative B9 transition geometry. A georeferenced campus proxy keeps
    /// the scan and transition volumes fixed over the real B9 footprint while
    /// the original map remains under MultiSet's movable Map Space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9BuildingTransitionGeometry : MonoBehaviour
    {
        [SerializeField] private Transform mapSpace;
        [SerializeField] private Transform campusProxyRoot;
        [SerializeField] private Transform campusModelProxy;
        [SerializeField] private B9HandoverVolume outerHandoverVolume;
        [SerializeField] private B9HandoverVolume innerLocalizationVolume;
        [SerializeField] private List<B9PortalAnchor> portals = new List<B9PortalAnchor>();

        public Transform MapSpace => mapSpace;
        public Transform CampusProxyRoot => campusProxyRoot;
        public Transform CampusModelProxy => campusModelProxy;
        public B9HandoverVolume OuterHandoverVolume => outerHandoverVolume;
        public B9HandoverVolume InnerLocalizationVolume => innerLocalizationVolume;
        public IReadOnlyList<B9PortalAnchor> Portals => portals;

        public B9PortalAnchor PrimaryPortal
        {
            get
            {
                for (int i = 0; i < portals.Count; i++)
                {
                    if (portals[i] != null && portals[i].Primary)
                        return portals[i];
                }

                return portals.Count > 0 ? portals[0] : null;
            }
        }

        public void Configure(
            Transform mapSpaceRoot,
            Transform georeferencedCampusRoot,
            Transform georeferencedModel,
            B9HandoverVolume outerVolume,
            B9HandoverVolume innerVolume,
            IReadOnlyList<B9PortalAnchor> portalAnchors)
        {
            mapSpace = mapSpaceRoot;
            campusProxyRoot = georeferencedCampusRoot;
            campusModelProxy = georeferencedModel;
            outerHandoverVolume = outerVolume;
            innerLocalizationVolume = innerVolume;
            portals = portalAnchors != null
                ? new List<B9PortalAnchor>(portalAnchors)
                : new List<B9PortalAnchor>();
        }

        public bool ContainsOuterMapWorldPoint(
            Vector3 mapWorldPoint,
            B9PortalAnchor portal = null)
        {
            portal ??= PrimaryPortal;
            return portal != null
                   && ContainsOuterCampusPoint(portal.MapWorldToCampusPoint(mapWorldPoint));
        }

        public bool ContainsInnerMapWorldPoint(
            Vector3 mapWorldPoint,
            B9PortalAnchor portal = null)
        {
            portal ??= PrimaryPortal;
            return portal != null
                   && ContainsInnerCampusPoint(portal.MapWorldToCampusPoint(mapWorldPoint));
        }

        public bool ContainsOuterCampusPoint(
            Vector3 campusWorldPoint,
            B9PortalAnchor portal = null)
        {
            return outerHandoverVolume != null
                   && outerHandoverVolume.ContainsWorldPoint(campusWorldPoint);
        }

        public bool ContainsInnerCampusPoint(
            Vector3 campusWorldPoint,
            B9PortalAnchor portal = null)
        {
            return innerLocalizationVolume != null
                   && innerLocalizationVolume.ContainsWorldPoint(campusWorldPoint);
        }

        public bool TryGetPortal(string portalId, out B9PortalAnchor portal)
        {
            portal = null;
            if (string.IsNullOrWhiteSpace(portalId))
                return false;

            for (int i = 0; i < portals.Count; i++)
            {
                B9PortalAnchor candidate = portals[i];
                if (candidate != null && string.Equals(
                        candidate.PortalId,
                        portalId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    portal = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNearestCampusPortal(Vector3 campusWorldPoint, out B9PortalAnchor portal)
        {
            portal = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < portals.Count; i++)
            {
                B9PortalAnchor candidate = portals[i];
                if (candidate == null || candidate.OutdoorCampusAnchor == null)
                    continue;

                float distance = Vector3.Distance(
                    campusWorldPoint,
                    candidate.OutdoorCampusAnchor.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    portal = candidate;
                }
            }

            return portal != null;
        }

        public bool TryGetNearestMapPortal(Vector3 mapWorldPoint, out B9PortalAnchor portal)
        {
            portal = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < portals.Count; i++)
            {
                B9PortalAnchor candidate = portals[i];
                if (candidate == null || candidate.IndoorMapAnchor == null)
                    continue;

                float distance = Vector3.Distance(mapWorldPoint, candidate.IndoorMapAnchor.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    portal = candidate;
                }
            }

            return portal != null;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (mapSpace == null)
                return Fail("Map Space is missing", out reason);
            if (campusProxyRoot == null)
                return Fail("Georeferenced B9 campus proxy is missing", out reason);
            if (campusModelProxy == null)
                return Fail("Georeferenced B9 scan model is missing", out reason);
            if (outerHandoverVolume == null)
                return Fail("Outer handover volume is missing", out reason);
            if (!outerHandoverVolume.ValidateConfiguration(out reason))
                return false;
            if (outerHandoverVolume.Kind != B9HandoverVolume.VolumeKind.OuterHandover)
                return Fail("Outer volume has the wrong kind", out reason);
            if (innerLocalizationVolume == null)
                return Fail("Inner localization volume is missing", out reason);
            if (!innerLocalizationVolume.ValidateConfiguration(out reason))
                return false;
            if (innerLocalizationVolume.Kind != B9HandoverVolume.VolumeKind.InnerLocalization)
                return Fail("Inner volume has the wrong kind", out reason);
            if (portals == null || portals.Count == 0)
                return Fail("B9 has no portal pairs", out reason);

            var portalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int primaryCount = 0;
            for (int i = 0; i < portals.Count; i++)
            {
                B9PortalAnchor portal = portals[i];
                if (portal == null)
                    return Fail($"Portal {i} is missing", out reason);
                if (!portal.ValidateConfiguration(out reason))
                    return false;
                if (!portalIds.Add(portal.PortalId))
                    return Fail($"Duplicate portal ID: {portal.PortalId}", out reason);
                if (portal.Primary)
                    primaryCount++;
            }

            if (primaryCount != 1)
                return Fail($"B9 requires exactly one primary portal, found {primaryCount}", out reason);
            if (!ValidateCampusAlignment(PrimaryPortal, out reason))
                return false;
            if (!OuterContainsInner())
                return Fail("Outer handover volume does not contain the inner localization volume", out reason);

            reason = string.Empty;
            return true;
        }

        private bool ValidateCampusAlignment(B9PortalAnchor portal, out string reason)
        {
            Vector3 indoorLocalPosition = mapSpace.InverseTransformPoint(
                portal.IndoorMapAnchor.position);
            Quaternion indoorLocalRotation = Quaternion.Inverse(mapSpace.rotation)
                                                * portal.IndoorMapAnchor.rotation;
            Vector3 mappedPosition = campusProxyRoot.TransformPoint(indoorLocalPosition);
            Quaternion mappedRotation = campusProxyRoot.rotation * indoorLocalRotation;

            float positionError = Vector3.Distance(
                mappedPosition,
                portal.OutdoorCampusAnchor.position);
            float rotationError = Quaternion.Angle(
                mappedRotation,
                portal.OutdoorCampusAnchor.rotation);
            if (positionError > 0.05f || rotationError > 0.5f)
            {
                return Fail(
                    $"B9 campus proxy is misaligned with {portal.PortalId}: "
                    + $"position error={positionError:0.000}m, rotation error={rotationError:0.00}deg",
                    out reason);
            }

            reason = string.Empty;
            return true;
        }

        private bool OuterContainsInner()
        {
            IReadOnlyList<BoxCollider> innerSegments = innerLocalizationVolume.Segments;
            for (int i = 0; i < innerSegments.Count; i++)
            {
                BoxCollider box = innerSegments[i];
                if (box == null)
                    return false;

                Vector3 half = box.size * 0.5f;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = box.center + Vector3.Scale(
                        half,
                        new Vector3(x, y, z));
                    Vector3 worldCorner = box.transform.TransformPoint(localCorner);
                    if (!outerHandoverVolume.ContainsWorldPoint(worldCorner))
                        return false;
                }
            }

            return true;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
