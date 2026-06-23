using UnityEngine;

namespace ARNav.Hybrid
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class MapSpaceAnchor : MonoBehaviour
    {
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private void Awake()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            if (transform.position != _initialPosition)
                transform.position = _initialPosition;
            if (transform.rotation != _initialRotation)
                transform.rotation = _initialRotation;
        }
    }
}
