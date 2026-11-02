using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Scales a fixed-size target RectTransform so it fits inside this RectTransform.
    /// Attach to a stretched container; the target keeps its designed size and is uniformly scaled down.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class ScaleToFitParent : UIBehaviour
    {
        [SerializeField]
        private RectTransform _target;

        [SerializeField]
        private Vector2 _referenceSize = new(1080f, 1680f);

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void Apply()
        {
            if (_target == null || _referenceSize.x <= 0f || _referenceSize.y <= 0f)
            {
                return;
            }

            var rect = ((RectTransform)transform).rect;
            var scale = Mathf.Min(1f, rect.height / _referenceSize.y, rect.width / _referenceSize.x);
            _target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
