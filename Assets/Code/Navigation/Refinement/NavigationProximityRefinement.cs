using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// When the user is near the active navigation target (surveyed Des on the map),
/// blends GPS-driven XR position toward the target XZ so arrival and AR alignment
/// are closer to sub-meter without VPS.
/// </summary>
public class NavigationProximityRefinement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SimpleGPSTracker gpsTracker;
    [SerializeField] private ARPathFinder pathFinder;

    [Header("Blend zone")]
    [Tooltip("Start snap when camera OR GPS is within this XZ distance to Des (m). Slightly larger than HUD 'arrival' feels best.")]
    [SerializeField] private float blendStartRadiusMeters = 6f;
    [Tooltip("Full alignment to surveyed target XZ within this distance (m).")]
    [SerializeField] private float fullSnapRadiusMeters = 2f;
    [Tooltip("Skip blend when GPS accuracy is worse than this (m), unless already inside fullSnap radius.")]
    [SerializeField] private float maxGpsAccuracyForBlendMeters = 6f;
    [Tooltip("How fast session refinement offset fades in/out per second.")]
    [SerializeField] private float refinementBlendSpeed = 5f;

    private Vector3 _currentRefinement;
    private Vector3 _targetRefinement;

    public bool IsRefinementActive => _currentRefinement.sqrMagnitude > 0.01f;
    public float ActiveRefinementMeters => _currentRefinement.magnitude;

    // Auto-spawn đã tắt — refinement gây hiệu ứng "POI dịch chuyển khi gần đích" + báo
    // arrival sớm so với thực tế. Đã quyết định bỏ để giữ vị trí mọi vật cố định.
    // Để bật lại: thêm RuntimeInitializeOnLoadMethod attribute như trước.
    private static void CreateForGPSMapPlane_Disabled()
    {
        if (!GpsOutdoorSceneNames.Includes(SceneManager.GetActiveScene().name)) return;
        if (FindFirstObjectByType<NavigationProximityRefinement>() != null) return;

        SimpleGPSTracker tracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (tracker == null) return;

        tracker.gameObject.AddComponent<NavigationProximityRefinement>();
    }

    void Awake()
    {
        if (gpsTracker == null) gpsTracker = GetComponent<SimpleGPSTracker>();
        if (gpsTracker == null) gpsTracker = FindFirstObjectByType<SimpleGPSTracker>();
        if (pathFinder == null) pathFinder = FindFirstObjectByType<ARPathFinder>();
    }

    void LateUpdate()
    {
        if (gpsTracker == null || !gpsTracker.HasFirstFix)
        {
            FadeRefinementToZero();
            return;
        }

        Transform target = pathFinder != null ? pathFinder.TargetNode : null;
        if (target == null)
        {
            FadeRefinementToZero();
            return;
        }

        Vector3 gpsXZ = gpsTracker.GpsOnlySmoothedPosition;
        gpsXZ.y = 0f;
        Vector3 destXZ = target.position;
        destXZ.y = 0f;

        float gpsDist = Vector3.Distance(gpsXZ, destXZ);

        float cameraDist = gpsDist;
        Camera cam = gpsTracker.ArCamera != null ? gpsTracker.ArCamera : Camera.main;
        if (cam != null)
        {
            Vector3 camXZ = cam.transform.position;
            camXZ.y = 0f;
            cameraDist = Vector3.Distance(camXZ, destXZ);
        }

        // HUD distance follows the user rig; snap zone uses the closer of GPS vs camera XZ.
        float proximityDist = Mathf.Min(gpsDist, cameraDist);
        float blendStart = Mathf.Max(fullSnapRadiusMeters + 0.1f, blendStartRadiusMeters);

        if (proximityDist > blendStart)
        {
            _targetRefinement = Vector3.zero;
        }
        else
        {
            float acc = gpsTracker.CurrentHorizontalAccuracy;
            if (acc > maxGpsAccuracyForBlendMeters && proximityDist > fullSnapRadiusMeters)
            {
                _targetRefinement = Vector3.zero;
            }
            else
            {
                float t = 1f - Mathf.InverseLerp(fullSnapRadiusMeters, blendStart, proximityDist);
                _targetRefinement = (destXZ - gpsXZ) * t;
            }
        }

        _currentRefinement = Vector3.MoveTowards(
            _currentRefinement,
            _targetRefinement,
            refinementBlendSpeed * Time.deltaTime);

        gpsTracker.SetSessionRefinementOffset(_currentRefinement);
    }

    private void FadeRefinementToZero()
    {
        _targetRefinement = Vector3.zero;
        _currentRefinement = Vector3.MoveTowards(
            _currentRefinement,
            Vector3.zero,
            refinementBlendSpeed * Time.deltaTime);

        if (gpsTracker != null)
            gpsTracker.SetSessionRefinementOffset(_currentRefinement);
    }
}
