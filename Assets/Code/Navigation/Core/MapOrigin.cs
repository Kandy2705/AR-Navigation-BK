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

    /// <summary>
    /// Chọn MapOrigin outdoor một cách xác định. HybridGPSMap còn chứa một RefPoint
    /// legacy; FindFirstObjectByType có thể lấy nhầm origin cách campus vài kilomet.
    /// </summary>
    public static MapOrigin FindPrimary()
    {
        MapOrigin[] origins = Object.FindObjectsByType<MapOrigin>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (origins.Length == 0) return null;
        if (origins.Length == 1) return origins[0];

        GPSMarker marker = Object.FindFirstObjectByType<GPSMarker>(
            FindObjectsInactive.Exclude);
        if (marker != null)
        {
            MapOrigin best = null;
            double bestMetersSquared = double.MaxValue;
            foreach (MapOrigin origin in origins)
            {
                if (origin == null) continue;
                double northMeters = (origin.originLat - marker.refLat) * 111320.0;
                double eastMeters = (origin.originLon - marker.refLon) * 111320.0 *
                                    System.Math.Cos(marker.refLat * System.Math.PI / 180.0);
                double sqr = northMeters * northMeters + eastMeters * eastMeters;
                if (sqr >= bestMetersSquared) continue;
                bestMetersSquared = sqr;
                best = origin;
            }
            if (best != null) return best;
        }

        foreach (MapOrigin origin in origins)
        {
            for (Transform t = origin.transform; t != null; t = t.parent)
            {
                if (t.name == "BKMAP" || t.name == "OutdoorEnvironment")
                    return origin;
            }
        }

        Debug.LogWarning(
            $"[MapOrigin] Có {origins.Length} MapOrigin nhưng không xác định được primary; dùng '{origins[0].name}'.");
        return origins[0];
    }

    public Vector3 GetUnityPositionFromGPS(double targetLat, double targetLon, double targetAlt = 0)
    {
        ECEF refECEF = LatLonAltToECEF(originLat, originLon, originAlt);
        ECEF targetECEF = LatLonAltToECEF(targetLat, targetLon, targetAlt);
        ENU enu = ECEFToENU(targetECEF, refECEF, originLat, originLon);
        
        return new Vector3((float)enu.e, 0f, (float)enu.n);
    }

    /// <summary>
    /// Ngược của <see cref="GetUnityPositionFromGPS"/>: world XZ (East, North) → lat/lon WGS84.
    /// Dùng khi đặt TargetAnchor theo EntranceAnchor world position đã canh trên map.
    /// </summary>
    public void GetGPSFromUnityPosition(Vector3 unityPos, out double lat, out double lon)
    {
        // Xấp xỉ local ENU flat (đủ cho campus ~km).
        double metersPerDegLat = 111320.0;
        double metersPerDegLon = 111320.0 * System.Math.Cos(originLat * System.Math.PI / 180.0);
        if (System.Math.Abs(metersPerDegLon) < 1e-6) metersPerDegLon = 1e-6;
        lat = originLat + (unityPos.z / metersPerDegLat);
        lon = originLon + (unityPos.x / metersPerDegLon);
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
