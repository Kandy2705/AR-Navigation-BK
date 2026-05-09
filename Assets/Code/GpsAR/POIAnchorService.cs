using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace TestAR.GpsAR
{
    /// <summary>
    /// Singleton service that wraps ARAnchorManager.
    /// Other components (typically <see cref="AnchoredPOI"/>) request an ARAnchor at a world pose
    /// so ARCore SLAM can keep the visual stable while the rig translates with GPS.
    ///
    /// Why a service: AR Foundation 6 prefers the "AddComponent&lt;ARAnchor&gt; on a GameObject"
    /// pattern. Centralizing creation makes lifecycle, fallback (no AR session), and re-anchor
    /// behaviour easier to reason about than scattering it across each POI.
    /// </summary>
    public class POIAnchorService : MonoBehaviour
    {
        public static POIAnchorService Instance { get; private set; }

        [Header("References")]
        [Tooltip("If null, the service will look one up via FindFirstObjectByType at Awake.")]
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private bool autoFindManager = true;

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        private readonly Dictionary<int, Transform> anchors = new Dictionary<int, Transform>();

        public bool HasAnchorManager => anchorManager != null;
        public int ActiveAnchorCount => anchors.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (anchorManager == null && autoFindManager)
            {
                anchorManager = FindFirstObjectByType<ARAnchorManager>(FindObjectsInactive.Include);
            }

            if (anchorManager == null)
            {
                Debug.LogWarning(
                    "[POIAnchorService] ARAnchorManager not found. Anchors will be created as plain " +
                    "GameObjects so editor flow still works, but they will NOT be SLAM-tracked on device.");
            }
        }

        private void OnDestroy()
        {
            foreach (var kv in anchors)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            anchors.Clear();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Create or replace an anchor for the given owner. Returns the anchor's Transform
        /// (an ARAnchor when AR Foundation is available, or a plain GameObject otherwise).
        /// </summary>
        public Transform CreateOrReplaceAnchor(int ownerId, Vector3 worldPos, Quaternion worldRot)
        {
            DestroyAnchor(ownerId);

            var go = new GameObject($"POIAnchor_{ownerId}");
            go.transform.SetPositionAndRotation(worldPos, worldRot);

            Transform anchorTransform;
            if (anchorManager != null)
            {
                var arAnchor = go.AddComponent<ARAnchor>();
                anchorTransform = arAnchor.transform;
            }
            else
            {
                anchorTransform = go.transform;
            }

            anchors[ownerId] = anchorTransform;

            if (logEvents)
            {
                Debug.Log($"[POIAnchorService] Anchor for {ownerId} at {worldPos} (AR={HasAnchorManager}).");
            }

            return anchorTransform;
        }

        public void DestroyAnchor(int ownerId)
        {
            if (anchors.TryGetValue(ownerId, out var t) && t != null)
            {
                Destroy(t.gameObject);
            }
            anchors.Remove(ownerId);
        }

        public bool HasAnchor(int ownerId) => anchors.ContainsKey(ownerId);
    }
}
