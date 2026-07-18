using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Component setup nhanh destination indoor POI bằng tên — tự resolve Transform
    /// ngay cả khi parent (IndoorEnvironment/MapB9/POIs-B9) đang inactive.
    ///
    /// Cách dùng:
    ///   1. Gắn lên cùng GameObject với <see cref="HybridRouteCoordinator"/>.
    ///   2. Set <see cref="building"/> + <see cref="floorId"/> + <see cref="poiName"/>.
    ///   3. Optional: <see cref="poiContainerNameHint"/> = "POIs-B9" để giới hạn scan.
    ///   4. Play. Component sẽ tìm Transform theo tên và gắn vào coordinator.destination.
    ///
    /// Resolution priority:
    ///   - Nếu có <see cref="explicitParent"/> → scan children theo tên.
    ///   - Else: scan toàn scene (kể cả inactive) cho 1 Transform tên trùng <see cref="poiName"/>
    ///     mà parent chain có chứa <see cref="poiContainerNameHint"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridDestinationByName : MonoBehaviour
    {
        [SerializeField] private HybridRouteCoordinator coordinator;
        [SerializeField] private HybridLocalizationManager manager;

        [Header("Destination identity")]
        [SerializeField] private string poiName = "P101";
        [SerializeField] private BuildingId building = BuildingId.B9;
        [SerializeField] private string floorId = "F1";

        [Header("Lookup hints")]
        [Tooltip("Substring tên parent mong đợi (vd 'POIs-B9'). Giúp tránh trùng tên nhầm GO khác.")]
        [SerializeField] private string poiContainerNameHint = "POIs-";

        [Tooltip("Nếu gán, scan dưới parent này thay vì toàn scene. Hữu ích khi nhiều tòa có POI cùng tên.")]
        [SerializeField] private Transform explicitParent;

        [Header("Behavior")]
        [Tooltip("MẶC ĐỊNH TẮT. Bật chỉ khi dev muốn hardcode 1 POI lúc Play. " +
                 "Production dùng UI (MobileNavigationHUD dropdown/search hoặc BuildingDestinationList).")]
        [SerializeField] private bool autoApplyOnEnable = false;

        [Tooltip("Retry mỗi N giây nếu chưa resolve được (parent có thể spawn trễ).")]
        [SerializeField] private float retryIntervalSeconds = 1f;

        [SerializeField] private bool verboseLog = true;

        private float _nextRetryTime;
        private Transform _resolved;

        private void OnEnable()
        {
            if (coordinator == null) coordinator = GetComponent<HybridRouteCoordinator>();
            if (coordinator == null) coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (manager == null) manager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
            if (!autoApplyOnEnable)
            {
                if (verboseLog)
                    Debug.Log("[HybridDestinationByName] autoApplyOnEnable=false — chờ UI chọn điểm đến (HybridDestinationService).");
                return;
            }
            TryResolveAndApply();
        }

        private void Update()
        {
            if (!autoApplyOnEnable) return;
            if (_resolved != null) return;
            if (Time.time < _nextRetryTime) return;
            _nextRetryTime = Time.time + retryIntervalSeconds;
            TryResolveAndApply();
        }

        private void TryResolveAndApply()
        {
            _resolved = ResolvePoiTransform();
            if (_resolved == null)
            {
                if (verboseLog) Debug.Log($"[HybridDestinationByName] POI '{poiName}' chưa resolve được — retry sau {retryIntervalSeconds:0.0}s.");
                return;
            }

            if (coordinator == null)
            {
                if (verboseLog) Debug.LogWarning("[HybridDestinationByName] No HybridRouteCoordinator found.");
                return;
            }

            var dest = new HybridDestination
            {
                displayName = poiName,
                isIndoor = true,
                building = building,
                floorId = floorId,
                targetTransform = _resolved,
                explicitCampusPosition = _resolved.position,
            };
            coordinator.SetDestination(dest);

            if (manager != null)
            {
                manager.SetDestinationBuilding(building, null);
            }

            if (verboseLog) Debug.Log($"[HybridDestinationByName] Resolved POI '{poiName}' → '{_resolved.GetHierarchyPath()}' at {_resolved.position}. Destination applied.");
        }

        private Transform ResolvePoiTransform()
        {
            if (explicitParent != null)
            {
                return FindChildRecursive(explicitParent, poiName);
            }

            // Scan toàn scene kể cả inactive.
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null) continue;
                if (t.name != poiName) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                if (!string.IsNullOrEmpty(poiContainerNameHint))
                {
                    bool ok = false;
                    for (var p = t.parent; p != null; p = p.parent)
                    {
                        if (p.name.Contains(poiContainerNameHint)) { ok = true; break; }
                    }
                    if (!ok) continue;
                }
                return t;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var deeper = FindChildRecursive(c, name);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }

    internal static class TransformPathExtensions
    {
        public static string GetHierarchyPath(this Transform t)
        {
            if (t == null) return "<null>";
            var sb = new System.Text.StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent)
            {
                sb.Insert(0, p.name + "/");
            }
            return sb.ToString();
        }
    }
}
