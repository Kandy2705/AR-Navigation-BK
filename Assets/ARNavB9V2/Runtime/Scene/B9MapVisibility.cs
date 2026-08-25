using UnityEngine;

namespace ARNavB9V2.Scene
{
    /// <summary>
    /// Keeps the localization model out of the phone AR view while allowing the
    /// dedicated top-down minimap camera to render it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9MapVisibility : MonoBehaviour
    {
        [SerializeField] private Transform mapRoot;
        [SerializeField] private Camera arCamera;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private string mapLayerName = "MapPlane";

        private void Awake()
        {
            ApplyVisibilityPolicy();
        }

        public void Configure(Transform root, Camera displayCamera, Camera mapCamera)
        {
            mapRoot = root;
            arCamera = displayCamera;
            minimapCamera = mapCamera;
            ApplyVisibilityPolicy();
        }

        public bool ApplyVisibilityPolicy()
        {
            int mapLayer = LayerMask.NameToLayer(mapLayerName);
            if (mapLayer < 0 || mapRoot == null)
                return false;

            SetLayerRecursively(mapRoot.gameObject, mapLayer);

            if (arCamera != null)
                arCamera.cullingMask &= ~(1 << mapLayer);
            if (minimapCamera != null)
                minimapCamera.cullingMask = 1 << mapLayer;

            return true;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
