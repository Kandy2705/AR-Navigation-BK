using System;
using ARNav.Hybrid;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Theo dõi khoảng cách user → điểm đích hybrid/outdoor và bật
/// <see cref="ArrivalBanner"/> giữa màn hình khi vào bán kính arrival.
///
/// Nguồn đích: <see cref="HybridDestinationService.Selected"/> hoặc
/// <see cref="HybridRouteCoordinator.Destination"/> / ARPathFinder target.
/// </summary>
[DefaultExecutionOrder(50)]
public class ArrivalWatcher : MonoBehaviour
{
    public static event Action<string> Arrived;
    [Header("Arrival")]
    [Tooltip("Bán kính coi là đã đến (m) — XZ.")]
    [SerializeField] private float arrivalRadiusMeters = 3f;

    [Tooltip("Phải ra ngoài bán kính này (m) mới cho thông báo lại cùng đích.")]
    [SerializeField] private float leaveRadiusMeters = 6f;

    [SerializeField] private float checkIntervalSeconds = 0.15f;

    [Header("Google Maps-style end nav")]
    [Tooltip("Khi đến nơi: tắt path ribbon, clear destination (giống Google Maps end route).")]
    [SerializeField] private bool endNavigationOnArrival = true;

    [Header("Refs (auto)")]
    [SerializeField] private HybridDestinationService destinationService;
    [SerializeField] private HybridRouteCoordinator routeCoordinator;
    [SerializeField] private HybridLocalizationManager localizationManager;
    [SerializeField] private ARPathFinder pathFinder;
    [SerializeField] private Transform userTransform;

    private float _nextCheck;
    private string _latchedKey;
    private bool _inside;
    private bool _navEndedForLatch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        string n = SceneManager.GetActiveScene().name;
        if (n != "HybridGPSMap" && n != "Hybrid Navigation" && n != "GPSMapPlane") return;
        if (FindFirstObjectByType<ArrivalWatcher>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("ArrivalWatcher");
        go.AddComponent<ArrivalWatcher>();
    }

    private void Awake()
    {
        Resolve();
        ArrivalBanner.EnsureExists();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + checkIntervalSeconds;
        Resolve();
        Evaluate();
    }

    private void Resolve()
    {
        if (destinationService == null)
            destinationService = HybridDestinationService.Instance
                                 ?? FindFirstObjectByType<HybridDestinationService>(FindObjectsInactive.Include);
        if (routeCoordinator == null)
            routeCoordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
        if (localizationManager == null)
            localizationManager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
        if (pathFinder == null)
            pathFinder = FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);

        if (userTransform == null)
        {
            if (Camera.main != null) userTransform = Camera.main.transform;
            else
            {
                var gps = FindFirstObjectByType<SimpleGPSTracker>(FindObjectsInactive.Include);
                if (gps != null && gps.ArCamera != null) userTransform = gps.ArCamera.transform;
            }
        }
    }

    private void Evaluate()
    {
        if (userTransform == null) return;
        if (!TryGetDestination(out Vector3 destPos, out string displayName, out string key))
        {
            // Đã clear dest sau arrival — sẵn sàng cho lần chọn đích mới.
            _inside = false;
            return;
        }

        float dist = HorizontalDistance(userTransform.position, destPos);

        // Đổi điểm đến mới (user chọn lại) → bỏ trạng thái "đang trong vùng" cũ.
        if (_inside && _latchedKey != null && _latchedKey != key)
        {
            _inside = false;
            _navEndedForLatch = false;
        }

        if (_inside)
        {
            if (dist > leaveRadiusMeters)
            {
                _inside = false;
                _latchedKey = null;
                _navEndedForLatch = false;
                ArrivalBanner.EnsureExists().ResetArrivalLatch(key);
            }
            return;
        }

        if (dist <= arrivalRadiusMeters)
        {
            bool firstEnter = _latchedKey != key;
            _inside = true;
            _latchedKey = key;

            if (firstEnter)
            {
                ArrivalBanner.EnsureExists().Show(displayName, key);
                Arrived?.Invoke(displayName);
                Debug.Log($"[ArrivalWatcher] Arrived → '{displayName}' dist={dist:F1}m");
            }

            // Tắt chỉ đường ngay khi đến (Google Maps-style).
            if (endNavigationOnArrival && !_navEndedForLatch)
            {
                _navEndedForLatch = true;
                EndNavigation(displayName);
            }
        }
    }

    /// <summary>
    /// Xóa path + destination giống Google Maps khi "You've arrived".
    /// Banner vẫn hiện; path ribbon biến mất.
    /// </summary>
    private void EndNavigation(string displayName)
    {
        Resolve();

        if (destinationService != null)
        {
            destinationService.Clear();
        }
        else
        {
            if (routeCoordinator != null) routeCoordinator.ClearDestination();
            if (pathFinder != null) pathFinder.SetTarget(null);
            TargetAnchor.CurrentSelectedDestination = null;
        }

        // Mọi ARPathFinder trong scene (outdoor + bridge) + tắt MinimapPathMirror.
        var finders = FindObjectsByType<ARPathFinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < finders.Length; i++)
        {
            if (finders[i] != null) finders[i].ClearNavigationVisuals();
        }

        // Belt-and-suspenders: tìm mọi GO tên MinimapPathMirror còn sót.
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null || t.name != "MinimapPathMirror") continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }

        // Multiset indoor SDK (nếu đang chạy) — best-effort.
        try
        {
            if (NavigationController.instance != null)
            {
                var nc = NavigationController.instance;
                var stop = nc.GetType().GetMethod("StopNavigation",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (stop != null) stop.Invoke(nc, null);
                var clear = nc.GetType().GetMethod("ClearPath",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (clear != null) clear.Invoke(nc, null);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ArrivalWatcher] Multiset stop nav skipped: {ex.Message}");
        }

        // HUD toast ngắn (nếu có).
        var hud = FindFirstObjectByType<MobileNavigationHUD>(FindObjectsInactive.Include);
        if (hud != null)
            hud.ShowToast($"Da den: {displayName}. Tat chi duong.");

        Debug.Log($"[ArrivalWatcher] Navigation ended at '{displayName}' (path cleared).");
    }

    private bool TryGetDestination(out Vector3 pos, out string displayName, out string key)
    {
        pos = default;
        displayName = null;
        key = null;

        // 1) Hybrid catalog selection
        if (destinationService != null && destinationService.HasSelection)
        {
            var e = destinationService.Selected;
            if (e != null)
            {
                pos = e.targetTransform != null ? e.targetTransform.position : e.explicitCampusPosition;
                displayName = e.displayName;
                key = (e.isIndoor ? "in:" : "out:") + e.displayName + ":" + e.building;
                return true;
            }
        }

        // 2) HybridRouteCoordinator destination — luôn dùng ĐÍCH CUỐI (POI),
        // không dùng CurrentTarget (có thể là cửa khi phase OutdoorToEntrance).
        if (routeCoordinator != null && routeCoordinator.Destination != null && routeCoordinator.Destination.IsValid)
        {
            var d = routeCoordinator.Destination;
            pos = d.CampusPosition;
            displayName = !string.IsNullOrEmpty(d.displayName) ? d.displayName : "Điểm đến";
            key = "route:" + displayName + ":" + d.building + ":" + d.isIndoor;
            return d.targetTransform != null || pos != Vector3.zero || d.isIndoor;
        }

        // 3) ARPathFinder target
        if (pathFinder != null && pathFinder.TargetNode != null)
        {
            pos = pathFinder.TargetNode.position;
            displayName = pathFinder.TargetNode.name;
            if (displayName == "HybridRouteTarget" && routeCoordinator != null && routeCoordinator.Destination != null)
                displayName = routeCoordinator.Destination.displayName;
            key = "path:" + displayName;
            return true;
        }

        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
