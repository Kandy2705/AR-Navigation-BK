using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Helper component đặt cùng GameObject với <see cref="NavigationController"/> (Multiset SDK).
/// Chuẩn bị 2 precondition mà SDK cần ở thời điểm <c>Awake/Start</c>:
///   1. Thêm <see cref="SphereCollider"/> trigger vào <c>Camera.main</c>.
///   2. Warp <see cref="NavMeshAgent"/> về NavMesh gần nhất nếu chưa onNavMesh.
///
/// Ghi chú: Nếu <c>Camera.main</c> chưa tag MainCamera lúc Awake (timing race khi
/// IndoorEnvironment vừa được bật), 2 việc trên sẽ thất bại — đó là lý do
/// <see cref="MultisetIndoorBootstrap"/> tồn tại để tiếp tục poll và reflection-patch
/// SDK runtime. Component này chỉ là "fast path" cho trường hợp setup chuẩn.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(NavigationController))]
public class NavigationControllerSetup : MonoBehaviour
{
    [Tooltip("Bán kính SphereCollider thêm vào Camera.main (m). Dùng để phát hiện arrival tại POI.")]
    [SerializeField] private float cameraColliderRadius = 0.5f;

    [Tooltip("Bán kính tìm NavMesh khi agent chưa onNavMesh (m).")]
    [SerializeField] private float navMeshSearchRadius = 10f;

    [Tooltip("In log chi tiết.")]
    [SerializeField] private bool verboseLog = true;

    private void Awake()
    {
        EnsureCameraCollider();
    }

    private void Start()
    {
        EnsureCameraCollider();
        WarpAgentToNavMesh();
    }

    private void EnsureCameraCollider()
    {
        var cam = Camera.main;
        if (cam == null) return;

        if (cam.GetComponent<SphereCollider>() == null)
        {
            var col = cam.gameObject.AddComponent<SphereCollider>();
            col.radius = cameraColliderRadius;
            col.isTrigger = true;
            if (verboseLog)
            {
                Debug.Log($"[NavigationControllerSetup] SphereCollider added to Camera.main '{cam.name}'.");
            }
        }
    }

    private void WarpAgentToNavMesh()
    {
        var navCtrl = GetComponent<NavigationController>();
        if (navCtrl == null) return;

        var agent = navCtrl.agent;
        if (agent == null || agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            if (verboseLog) Debug.Log($"[NavigationControllerSetup] Agent warped → {hit.position}.");
        }
    }
}
