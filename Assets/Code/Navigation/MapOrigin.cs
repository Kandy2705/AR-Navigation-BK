using UnityEngine;

public class MapOrigin : MonoBehaviour
{
    [Header("Tọa độ GPS tại gốc (0, 0, 0) của Unity")]
    public double originLat = 10.7483806; 
    public double originLon = 106.713275;
    public double originAlt = 0.0;

    const double a = 6378137.0;
    const double e2 = 6.694380004e-3;
    public struct ECEF { public double x, y, z; }
    public struct ENU { public double e, n, u; }

    public Vector3 GetUnityPositionFromGPS(double targetLat, double targetLon, double targetAlt = 0)
    {
        ECEF refECEF = LatLonAltToECEF(originLat, originLon, originAlt);
        ECEF targetECEF = LatLonAltToECEF(targetLat, targetLon, targetAlt);
        ENU enu = ECEFToENU(targetECEF, refECEF, originLat, originLon);
        
        return new Vector3((float)enu.e, 0f, (float)enu.n);
    }

    private ECEF LatLonAltToECEF(double latDeg, double lonDeg, double altitude)
    {
        double latR = latDeg * Mathf.Deg2Rad;
        double lonR = lonDeg * Mathf.Deg2Rad;
        double sinLat = System.Math.Sin(latR);
        double cosLat = System.Math.Cos(latR);
        double cosLon = System.Math.Cos(lonR);
        double sinLon = System.Math.Sin(lonR);
        double N = a / System.Math.Sqrt(1.0 - e2 * sinLat * sinLat);
        return new ECEF {
            x = (N + altitude) * cosLat * cosLon,
            y = (N + altitude) * cosLat * sinLon,
            z = (N * (1.0 - e2) + altitude) * sinLat
        };
    }

    private ENU ECEFToENU(ECEF point, ECEF refPoint, double refLatDeg, double refLonDeg)
    {
        double refLat = refLatDeg * Mathf.Deg2Rad;
        double refLon = refLonDeg * Mathf.Deg2Rad;
        double dx = point.x - refPoint.x;
        double dy = point.y - refPoint.y;
        double dz = point.z - refPoint.z;
        double sinLat = System.Math.Sin(refLat);
        double cosLat = System.Math.Cos(refLat);
        double sinLon = System.Math.Sin(refLon);
        double cosLon = System.Math.Cos(refLon);
        return new ENU {
            e = -sinLon * dx + cosLon * dy,
            n = -sinLat * cosLon * dx - sinLat * sinLon * dy + cosLat * dz,
            u = cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz
        };
    }
}