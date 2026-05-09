using UnityEngine;

namespace TestAR.GpsAR
{
    /// <summary>
    /// Drop on a POI GameObject. After a stabilization delay, requests an ARAnchor at the POI's
    /// current world pose and reparents the POI under the anchor so ARCore keeps it visually stable.
    ///
    /// Notes for outdoor GPS-only navigation:
    /// - Best for static POIs (NavigationTarget.autoUpdatePositionFromGps == false).
    ///   For continuously GPS-driven objects (e.g. UserIcon) do NOT add this component, otherwise
    ///   per-frame position writes will fight the anchor.
    /// - When the POI's world position drifts (e.g. due to GPS jump or compass-driven map rotation)
    ///   the anchor is recreated automatically.
    /// </summary>
    public class AnchoredPOI : MonoBehaviour
    {
        [Header("Behavior")]
        [SerializeField] private bool anchorOnEnable = true;
        [Tooltip("Wait this long after enable before anchoring (lets GPS-driven position settle).")]
        [SerializeField] private float stabilizationDelaySeconds = 1.5f;
        [Tooltip("Wait until the global GpsArBootstrap signals ready (recommended).")]
        [SerializeField] private bool waitForBootstrap = true;

        [Header("Re-anchoring")]
        [Tooltip("If world position drifts more than this from the anchor pose, recreate the anchor.")]
        [SerializeField] private float reanchorThresholdMeters = 6f;
        [Tooltip("Minimum seconds between re-anchor checks.")]
        [SerializeField] private float reanchorCheckInterval = 1f;

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        private Transform anchorTransform;
        private Transform originalParent;
        private bool hasOriginalParent;
        private bool anchored;
        private float enableTime;
        private float lastReanchorCheckTime;
        private Vector3 anchorPositionWhenSet;

        public bool IsAnchored => anchored;

        private void OnEnable()
        {
            originalParent = transform.parent;
            hasOriginalParent = true;
            anchored = false;
            enableTime = Time.time;
            lastReanchorCheckTime = Time.time;
        }

        private void OnDisable()
        {
            ReleaseAnchor();
        }

        private void Update()
        {
            var service = POIAnchorService.Instance;
            if (service == null) return;

            if (!anchored)
            {
                if (!anchorOnEnable) return;
                if (Time.time - enableTime < stabilizationDelaySeconds) return;
                if (waitForBootstrap && (GpsArBootstrap.Active == null || !GpsArBootstrap.Active.Aligned)) return;
                TryAnchor();
                return;
            }

            if (Time.time - lastReanchorCheckTime < reanchorCheckInterval) return;
            lastReanchorCheckTime = Time.time;

            float drift = Vector3.Distance(transform.position, anchorPositionWhenSet);
            if (drift > reanchorThresholdMeters)
            {
                if (logEvents)
                {
                    Debug.Log($"[AnchoredPOI:{name}] Drift {drift:0.0} m exceeds threshold; re-anchoring.");
                }
                TryAnchor();
            }
        }

        /// <summary>
        /// Force (re)create the anchor at the POI's current world pose.
        /// Useful to call after a known large pose change (e.g. compass recalibration).
        /// </summary>
        public void TryAnchor()
        {
            var service = POIAnchorService.Instance;
            if (service == null) return;

            if (anchored && hasOriginalParent)
            {
                transform.SetParent(originalParent, true);
                service.DestroyAnchor(GetInstanceID());
                anchored = false;
            }

            anchorTransform = service.CreateOrReplaceAnchor(
                GetInstanceID(),
                transform.position,
                transform.rotation);

            if (anchorTransform != null)
            {
                transform.SetParent(anchorTransform, worldPositionStays: true);
                anchorPositionWhenSet = transform.position;
                anchored = true;
            }
        }

        private void ReleaseAnchor()
        {
            if (anchored && hasOriginalParent)
            {
                transform.SetParent(originalParent, true);
            }
            POIAnchorService.Instance?.DestroyAnchor(GetInstanceID());
            anchored = false;
        }
    }
}
