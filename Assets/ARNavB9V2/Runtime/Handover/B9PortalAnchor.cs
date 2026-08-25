using UnityEngine;

namespace ARNavB9V2.Handover
{
    /// <summary>
    /// Pairs one measured campus-space doorway with the corresponding doorway in
    /// the MultiSet map. The pair is the deterministic bridge used before VPS has
    /// enough information to re-anchor Map Space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9PortalAnchor : MonoBehaviour
    {
        [SerializeField] private string portalId = "B9-MAIN";
        [SerializeField] private string displayName = "Cửa chính B9";
        [SerializeField] private string floorId = "F1";
        [SerializeField] private bool primary = true;
        [SerializeField] private Transform outdoorCampusAnchor;
        [SerializeField] private Transform indoorMapAnchor;

        public string PortalId => portalId;
        public string DisplayName => displayName;
        public string FloorId => floorId;
        public bool Primary => primary;
        public Transform OutdoorCampusAnchor => outdoorCampusAnchor;
        public Transform IndoorMapAnchor => indoorMapAnchor;

        public void Configure(
            string id,
            string label,
            string floor,
            bool isPrimary,
            Transform outdoorAnchor,
            Transform indoorAnchor)
        {
            portalId = string.IsNullOrWhiteSpace(id) ? "B9-PORTAL" : id.Trim();
            displayName = string.IsNullOrWhiteSpace(label) ? portalId : label.Trim();
            floorId = string.IsNullOrWhiteSpace(floor) ? "F1" : floor.Trim();
            primary = isPrimary;
            outdoorCampusAnchor = outdoorAnchor;
            indoorMapAnchor = indoorAnchor;
        }

        public Vector3 CampusToMapWorldPoint(Vector3 campusWorldPoint)
        {
            if (!HasValidPair)
                return campusWorldPoint;
            Vector3 portalLocal = outdoorCampusAnchor.InverseTransformPoint(campusWorldPoint);
            return indoorMapAnchor.TransformPoint(portalLocal);
        }

        public Vector3 MapWorldToCampusPoint(Vector3 mapWorldPoint)
        {
            if (!HasValidPair)
                return mapWorldPoint;
            Vector3 portalLocal = indoorMapAnchor.InverseTransformPoint(mapWorldPoint);
            return outdoorCampusAnchor.TransformPoint(portalLocal);
        }

        public Quaternion CampusToMapWorldRotation(Quaternion campusWorldRotation)
        {
            if (!HasValidPair)
                return campusWorldRotation;
            return indoorMapAnchor.rotation
                   * Quaternion.Inverse(outdoorCampusAnchor.rotation)
                   * campusWorldRotation;
        }

        public Quaternion MapWorldToCampusRotation(Quaternion mapWorldRotation)
        {
            if (!HasValidPair)
                return mapWorldRotation;
            return outdoorCampusAnchor.rotation
                   * Quaternion.Inverse(indoorMapAnchor.rotation)
                   * mapWorldRotation;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (string.IsNullOrWhiteSpace(portalId))
                return Fail("Portal ID is empty", out reason);
            if (outdoorCampusAnchor == null)
                return Fail($"{portalId} outdoor anchor is missing", out reason);
            if (indoorMapAnchor == null)
                return Fail($"{portalId} indoor anchor is missing", out reason);

            reason = string.Empty;
            return true;
        }

        private bool HasValidPair => outdoorCampusAnchor != null && indoorMapAnchor != null;

        private void OnDrawGizmos()
        {
            if (outdoorCampusAnchor != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.08f, 0.95f);
                Gizmos.DrawWireSphere(outdoorCampusAnchor.position, 0.65f);
                Gizmos.DrawRay(outdoorCampusAnchor.position, outdoorCampusAnchor.forward * 1.5f);
            }

            if (indoorMapAnchor != null)
            {
                Gizmos.color = new Color(0.1f, 0.95f, 0.9f, 0.95f);
                Gizmos.DrawWireSphere(indoorMapAnchor.position, 0.65f);
                Gizmos.DrawRay(indoorMapAnchor.position, indoorMapAnchor.forward * 1.5f);
            }
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
