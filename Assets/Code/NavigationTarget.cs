using UnityEngine;

public class NavigationTarget : MonoBehaviour
{
    [SerializeField] private GPSMarker gpsMarker;
    [SerializeField] private Transform mapPlane;
    [SerializeField] private bool autoUpdatePositionFromGps = false;
    [SerializeField] private bool applyAltitude = false;
    private bool warnedMissingReferences;

    [Header("Target GPS")]
    public double targetLat;
    public double targetLon;
    public double targetAlt = 0.0;

    private void Awake()
    {
        TryResolveReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryResolveReferences();
    }
#endif

    private void Update()
    {
        if (!autoUpdatePositionFromGps)
        {
            return;
        }

        if (gpsMarker == null || mapPlane == null)
        {
            TryResolveReferences();
            if (!warnedMissingReferences)
            {
                Debug.LogWarning($"NavigationTarget on '{name}' is missing GPSMarker/mapPlane reference.");
                warnedMissingReferences = true;
            }
            return;
        }

        warnedMissingReferences = false;

        var refECEF = gpsMarker.GetRefECEF();
        // if (IsNearZero(refECEF.x) && IsNearZero(refECEF.y) && IsNearZero(refECEF.z))
        // {
        //     return;
        // }

        var refLat = gpsMarker.refLat;
        var refLon = gpsMarker.refLon;

        var targetECEF = gpsMarker.LatLonAltToECEF(targetLat, targetLon, targetAlt);
        var enu = gpsMarker.ECEFToENU(targetECEF, refECEF, refLat, refLon);

        float y = applyAltitude ? (float)enu.u : 0f;
        // Keep target placement independent of mapPlane rotation (compass may rotate the minimap visual).
        Vector3 localPos = new Vector3((float)enu.e, y, (float)enu.n);
        Vector3 basePos = mapPlane != null ? mapPlane.position : Vector3.zero;
        Vector3 worldPos = basePos + new Vector3(localPos.x, localPos.y, localPos.z);
        if (!float.IsFinite(worldPos.x) || !float.IsFinite(worldPos.y) || !float.IsFinite(worldPos.z))
        {
            return;
        }

        transform.position = worldPos;
    }

    private static bool IsNearZero(double value)
    {
        return System.Math.Abs(value) < 0.000001d;
    }

    private void TryResolveReferences()
    {
        if (gpsMarker == null)
        {
            gpsMarker = FindObjectOfType<GPSMarker>();
        }

        if (mapPlane == null && gpsMarker != null)
        {
            mapPlane = gpsMarker.mapPlane;
        }
    }
}
