using System.Collections.Generic;
using UnityEngine;

namespace ARNavB9V2.Handover
{
    /// <summary>
    /// A compound, editor-authored trigger volume used for localization handover.
    /// Containment is evaluated explicitly so it does not depend on Rigidbody events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9HandoverVolume : MonoBehaviour
    {
        public enum VolumeKind
        {
            OuterHandover,
            InnerLocalization,
        }

        [SerializeField] private VolumeKind kind;
        [SerializeField] private List<BoxCollider> segments = new List<BoxCollider>();
        [SerializeField] private bool showGizmos = true;

        public VolumeKind Kind => kind;
        public IReadOnlyList<BoxCollider> Segments => segments;

        public void Configure(VolumeKind volumeKind, IReadOnlyList<BoxCollider> volumeSegments)
        {
            kind = volumeKind;
            segments = volumeSegments != null
                ? new List<BoxCollider>(volumeSegments)
                : new List<BoxCollider>();
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                BoxCollider segment = segments[i];
                if (segment != null && ContainsBoxPoint(segment, worldPoint))
                    return true;
            }

            return false;
        }

        public bool ContainsPoint(Vector3 point, Transform sourceSpace)
        {
            Vector3 worldPoint = sourceSpace != null
                ? sourceSpace.TransformPoint(point)
                : point;
            return ContainsWorldPoint(worldPoint);
        }

        public float DistanceToWorldPoint(Vector3 worldPoint)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < segments.Count; i++)
            {
                BoxCollider segment = segments[i];
                if (segment == null || !segment.enabled || !segment.gameObject.activeInHierarchy)
                    continue;

                float distance = Vector3.Distance(worldPoint, segment.ClosestPoint(worldPoint));
                nearest = Mathf.Min(nearest, distance);
            }

            return nearest;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (segments == null || segments.Count == 0)
                return Fail($"{kind} has no collider segments", out reason);

            for (int i = 0; i < segments.Count; i++)
            {
                BoxCollider segment = segments[i];
                if (segment == null)
                    return Fail($"{kind} segment {i} is missing", out reason);
                if (!segment.isTrigger)
                    return Fail($"{kind} segment {i} must be a trigger", out reason);
                if (segment.size.x <= 0f || segment.size.y <= 0f || segment.size.z <= 0f)
                    return Fail($"{kind} segment {i} has invalid size", out reason);
            }

            reason = string.Empty;
            return true;
        }

        private static bool ContainsBoxPoint(BoxCollider box, Vector3 worldPoint)
        {
            if (!box.enabled || !box.gameObject.activeInHierarchy)
                return false;

            Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            const float epsilon = 0.001f;
            return Mathf.Abs(localPoint.x) <= half.x + epsilon
                   && Mathf.Abs(localPoint.y) <= half.y + epsilon
                   && Mathf.Abs(localPoint.z) <= half.z + epsilon;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || segments == null)
                return;

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.color = kind == VolumeKind.InnerLocalization
                ? new Color(0.1f, 0.95f, 0.9f, 0.82f)
                : new Color(1f, 0.58f, 0.08f, 0.72f);

            for (int i = 0; i < segments.Count; i++)
            {
                BoxCollider segment = segments[i];
                if (segment == null)
                    continue;
                Gizmos.matrix = segment.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(segment.center, segment.size);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
