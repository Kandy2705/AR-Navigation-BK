using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Đảm bảo các điều kiện tiên quyết cho NavigationController (MultiSet SDK) trước khi nó Start():
///   1. AR Camera có SphereCollider (trigger) — SDK dùng để phát hiện arrival tại POI.
///   2. NavMeshAgent được đặt lên NavMesh nếu spawn position không khớp.
///
/// Cách dùng: Đặt script này trên CÙNG GameObject với NavigationController.
/// DefaultExecutionOrder(-100) đảm bảo Awake/Start của script này chạy TRƯỚC NavigationController.
/// </summary>
[DefaultExecutionOrder(-100)]
public class NavigationControllerSetup : MonoBehaviour
{
    [Tooltip("Bán kính SphereCollider thêm vào AR Camera (metres). Dùng để phát hiện khi người dùng đến gần POI.")]
    [SerializeField] private float cameraColliderRadius = 0.5f;

    [Tooltip("Bán kính tìm kiếm NavMesh khi agent chưa ở trên NavMesh (metres).")]
    [SerializeField] private float navMeshSearchRadius = 10f;

    private void Awake()
    {
        EnsureCameraCollider();
    }

    private void Start()
    {
        EnsureAgentOnNavMesh();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Camera collider
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// NavigationController.Awake() lấy Camera.main và Start() gọi GetComponent&lt;SphereCollider&gt;().
    /// Nếu camera chưa có collider → NullRef mỗi frame trong Update().
    /// Script này thêm SphereCollider (trigger) trước khi NavigationController.Start() chạy.
    /// </summary>
    private void EnsureCameraCollider()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // Camera chưa được tag lúc Awake — thử lại ở Start
            return;
        }

        AddColliderIfMissing(cam.gameObject);
    }

    private void AddColliderIfMissing(GameObject camGO)
    {
        if (camGO.GetComponent<SphereCollider>() != null) return;

        SphereCollider col = camGO.AddComponent<SphereCollider>();
        col.radius    = cameraColliderRadius;
        col.isTrigger = true;
        Debug.Log($"[NavigationControllerSetup] SphereCollider (trigger, r={cameraColliderRadius}m) đã được thêm vào '{camGO.name}'.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NavMesh agent
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// NavigationController.Start() đọc agent.transform.position ngay lập tức.
    /// Nếu agent chưa nằm trên NavMesh (spawn point không trùng với baked mesh),
    /// Unity in "Failed to create agent — not close enough to NavMesh".
    /// Script này Warp agent về điểm gần nhất trên NavMesh trước khi NavigationController.Start() chạy.
    /// </summary>
    private void EnsureAgentOnNavMesh()
    {
        NavigationController navCtrl = GetComponent<NavigationController>();
        if (navCtrl == null) return;

        NavMeshAgent agent = navCtrl.agent;
        if (agent == null || agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log($"[NavigationControllerSetup] NavMeshAgent warped từ {agent.transform.position} → {hit.position}.");
        }
        else
        {
            Debug.LogWarning($"[NavigationControllerSetup] Không tìm thấy NavMesh trong bán kính {navMeshSearchRadius}m quanh agent. " +
                             "Hãy bake NavMesh cho scene này: Window → AI → Navigation → Bake.");
        }
    }
}
