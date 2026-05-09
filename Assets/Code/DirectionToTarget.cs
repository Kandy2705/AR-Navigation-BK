using UnityEngine;

/// <summary>
/// Rotates a direction indicator to point from its origin to a target.
/// Intended for the `XR Origin/icon/direction` hierarchy in Hybrid Navigation.
/// </summary>
public class DirectionToTarget : MonoBehaviour
{
    [Header("References (optional)")]
    [Tooltip("If set, target will be read from GPSMarker.targetObject unless overrideTarget is assigned.")]
    [SerializeField] private GPSMarker gpsMarker;

    [Tooltip("Optional direct target override (highest priority).")]
    [SerializeField] private Transform overrideTarget;

    [Tooltip("Origin to aim from. If null, uses this transform.")]
    [SerializeField] private Transform origin;

    [Tooltip("Transform to rotate. If null, rotates this transform.")]
    [SerializeField] private Transform rotateTarget;

    [Header("Rotation")]
    [Tooltip("Rotate only around world Y so the arrow stays upright.")]
    [SerializeField] private bool yawOnly = true;

    [Tooltip("If your arrow mesh points along a different local axis, apply an extra yaw offset here (degrees).")]
    [SerializeField] private float yawOffsetDegrees = 0f;

    void Awake()
    {
        if (origin == null) origin = transform;
        if (rotateTarget == null) rotateTarget = transform;
        if (gpsMarker == null) gpsMarker = FindFirstObjectByType<GPSMarker>(FindObjectsInactive.Include);
    }

    void LateUpdate()
    {
        Transform target = overrideTarget;
        if (target == null && gpsMarker != null && gpsMarker.targetObject != null)
            target = gpsMarker.targetObject.transform;

        if (origin == null || target == null) return;

        Vector3 from = origin.position;
        Vector3 to = target.position;
        Vector3 dir = to - from;

        if (yawOnly)
            dir.y = 0f;

        if (dir.sqrMagnitude < 0.000001f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        if (yawOffsetDegrees != 0f)
            look *= Quaternion.Euler(0f, yawOffsetDegrees, 0f);

        rotateTarget.rotation = look;
    }
}

