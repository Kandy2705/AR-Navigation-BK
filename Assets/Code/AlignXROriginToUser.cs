using UnityEngine;
using Unity.XR.CoreUtils;

public class AlignXROriginToUser : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform userIcon;
    public GPSMarker gpsMarker;

    public bool aligned = false;

    void Update()
    {
        if (aligned) return;
        if (gpsMarker == null || !gpsMarker.HasRecentGoodFix) return;
        if (!gpsMarker.aligned) return;

        Transform camera = xrOrigin.Camera.transform;

        Vector3 offset = userIcon.position - camera.position;
        offset.y = 0f; // keep tracking origin height stable; align only in XZ
        xrOrigin.transform.position += offset;

        aligned = true;

        Debug.Log("XR Origin aligned to UserENU");
    }
}