using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// KHÔNG dùng cho scene B9 pre-placed (mesh indoor đã ở đúng world position).
    /// Script này sẽ không drive XR Origin — chỉ log VPS pose để debug.
    /// Khi cần drive (ví dụ scene khác không pre-placed), bật <see cref="enableDrive"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class IndoorXrRigDriver : MonoBehaviour
    {
        [SerializeField] private MultisetPoseProvider poseProvider;
        [SerializeField] private bool enableDrive = false;
        [SerializeField] private float smoothTime = 0.4f;
        [SerializeField] private float maxSpeed = 10f;
        [SerializeField] private bool autoResolve = true;
        [SerializeField] private bool verboseLog = true;

        public bool IsDriving { get; private set; }

        private void OnEnable()
        {
            if (autoResolve) Resolve();
        }

        private void Resolve()
        {
            if (poseProvider == null)
                poseProvider = FindFirstObjectByType<MultisetPoseProvider>(FindObjectsInactive.Include);
        }

        private void LateUpdate()
        {
            if (poseProvider == null)
                return;

            if (!poseProvider.HasFreshPose)
            {
                IsDriving = false;
                return;
            }

            IsDriving = true;
        }
    }
}
