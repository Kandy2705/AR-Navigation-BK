using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bootstrap toàn cục cho Multiset Indoor stack.
/// Vá <see cref="NavigationController"/> của Multiset SDK để loại bỏ NullReferenceException
/// ở <c>Update()</c> khi <c>ARCameraCollider</c> chưa được gán.
///
/// Vì SDK lưu <c>ARCamera</c> + <c>ARCameraCollider</c> ở field private và chỉ gán một
/// lần trong <c>Awake/Start</c>, nếu lúc đó <c>Camera.main</c> hoặc <c>SphereCollider</c>
/// chưa có (timing race khi IndoorEnvironment vừa được bật), 2 field đó sẽ null và
/// <c>Update()</c> sẽ NRE mỗi frame.
///
/// Bootstrap này:
///   1. Mỗi lần poll, đảm bảo <c>Camera.main</c> có <c>SphereCollider</c> (trigger).
///   2. Dùng reflection set lại 2 field private <c>ARCamera</c> + <c>ARCameraCollider</c>
///      trên instance <see cref="NavigationController"/> đang active.
///   3. Warp <c>NavMeshAgent</c> về NavMesh nếu chưa onNavMesh.
///
/// KHÔNG tắt <c>NavigationController.enabled</c> — để SDK chạy Awake/Start bình thường.
///
/// Đặt component này trên GameObject persistent (vd "Indoor Bootstrap" ở scene root).
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class MultisetIndoorBootstrap : MonoBehaviour
{
    [Header("Hybrid integration (optional)")]
    [Tooltip("Khi gán, dùng HybridModeController.GetActiveARCamera() trước khi fallback Camera.main. Tránh race với ApplyMainCameraTag khi vừa switch mode.")]
    [SerializeField] private HybridModeController hybridModeController;

    [Header("SphereCollider on Camera")]
    [Tooltip("Bán kính SphereCollider thêm vào Camera.main (m). SDK dùng để phát hiện arrival.")]
    [SerializeField] private float cameraColliderRadius = 0.5f;

    [Header("NavMesh Agent")]
    [Tooltip("Bán kính tìm NavMesh khi agent off-mesh (m).")]
    [SerializeField] private float navMeshSearchRadius = 10f;

    [Header("Polling")]
    [Tooltip("Khoảng giữa các lần poll (giây).")]
    [SerializeField] private float pollIntervalSeconds = 0.2f;

    [Header("Logging")]
    [SerializeField] private bool verboseLog = true;

    // Reflection cache
    private static FieldInfo _arCameraField;
    private static FieldInfo _arCameraColliderField;

    private static FieldInfo ArCameraField =>
        _arCameraField ??= typeof(NavigationController).GetField(
            "ARCamera", BindingFlags.Instance | BindingFlags.NonPublic);

    private static FieldInfo ArCameraColliderField =>
        _arCameraColliderField ??= typeof(NavigationController).GetField(
            "ARCameraCollider", BindingFlags.Instance | BindingFlags.NonPublic);

    private void OnEnable()
    {
        StartCoroutine(BootstrapLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator BootstrapLoop()
    {
        var pollWait = new WaitForSeconds(pollIntervalSeconds);

        while (true)
        {
            EnsureCameraCollider();

            var navCtrl = FindFirstObjectByType<NavigationController>(FindObjectsInactive.Exclude);
            if (navCtrl != null)
            {
                PatchNavigationController(navCtrl);
                TryWarpAgent(navCtrl);
            }

            yield return pollWait;
        }
    }

    /// <summary>Resolve camera ưu tiên HybridModeController.GetActiveARCamera(), fallback Camera.main.</summary>
    private Camera ResolveCamera()
    {
        if (hybridModeController != null)
        {
            var c = hybridModeController.GetActiveARCamera();
            if (c != null) return c;
        }
        return Camera.main;
    }

    /// <summary>Đảm bảo camera AR có SphereCollider (trigger).</summary>
    private void EnsureCameraCollider()
    {
        var cam = ResolveCamera();
        if (cam == null) return;

        var col = cam.GetComponent<SphereCollider>();
        if (col == null)
        {
            col = cam.gameObject.AddComponent<SphereCollider>();
            col.radius = cameraColliderRadius;
            col.isTrigger = true;
            if (verboseLog) Debug.Log($"[MultisetBootstrap] SphereCollider added to Camera.main '{cam.name}'.");
        }
        else
        {
            if (!col.isTrigger) col.isTrigger = true;
            if (col.radius <= 0f) col.radius = cameraColliderRadius;
        }
    }

    /// <summary>
    /// Reflection-patch 2 field private (<c>ARCamera</c>, <c>ARCameraCollider</c>) của
    /// <see cref="NavigationController"/> để Update không NRE.
    /// </summary>
    private void PatchNavigationController(NavigationController navCtrl)
    {
        var cam = ResolveCamera();
        if (cam == null) return;

        var col = cam.GetComponent<SphereCollider>();
        if (col == null) return;

        var camField = ArCameraField;
        if (camField != null)
        {
            var current = camField.GetValue(navCtrl) as Camera;
            if (current == null || current != cam)
            {
                camField.SetValue(navCtrl, cam);
                if (verboseLog) Debug.Log($"[MultisetBootstrap] NavigationController.ARCamera ← '{cam.name}'.");
            }
        }

        var colField = ArCameraColliderField;
        if (colField != null)
        {
            var current = colField.GetValue(navCtrl) as SphereCollider;
            if (current == null || current != col)
            {
                colField.SetValue(navCtrl, col);
                if (verboseLog) Debug.Log("[MultisetBootstrap] NavigationController.ARCameraCollider ← Camera.main collider.");
            }
        }
    }

    private void TryWarpAgent(NavigationController navCtrl)
    {
        var agent = navCtrl.agent;
        if (agent == null) return;
        if (agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            if (verboseLog) Debug.Log($"[MultisetBootstrap] NavMeshAgent warped → {hit.position}.");
        }
    }
}
