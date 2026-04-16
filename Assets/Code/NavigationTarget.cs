using UnityEngine;

public class NavigationTarget : MonoBehaviour
{
    [SerializeField] private GPSMarker gpsMarker;
    [SerializeField] private Transform mapPlane;

    [Header("Target GPS")]
    public double targetLat;
    public double targetLon;
    public double targetAlt = 0.0;

    void Update()
    {
        if (gpsMarker == null || mapPlane == null) return;

        // Lấy refECEF và refLat/Lon từ marker
        var refECEF = gpsMarker.GetRefECEF();
        var refLat = gpsMarker.refLat;
        var refLon = gpsMarker.refLon;

        // Tính ECEF của target
        var targetECEF = gpsMarker.LatLonAltToECEF(targetLat, targetLon, targetAlt);

        // Chuyển sang ENU
        var enu = gpsMarker.ECEFToENU(targetECEF, refECEF, refLat, refLon);

        // Chuyển sang Unity local position
        Vector3 localPos = new Vector3((float)enu.e, (float)enu.u, (float)enu.n);
        Vector3 worldPos = mapPlane.TransformPoint(localPos);

        transform.position = worldPos;
    }
}
