using UnityEngine;

/// <summary>
/// Tự động thêm SphereCollider (trigger) vào Main Camera khi AR được kích hoạt.
///
/// MultiSet SDK NavigationController yêu cầu SphereCollider trên Main Camera
/// để phát hiện proximity với POI — nếu thiếu sẽ throw MissingComponentException mỗi frame.
///
/// Cách dùng: Đặt script này lên bất kỳ GameObject nào luôn active trong scene
/// (ví dụ: HybridRuntime hoặc ARPageController).
/// </summary>
public class CameraColliderAutoSetup : MonoBehaviour
{
    [Tooltip("Bán kính SphereCollider được thêm vào Main Camera (metres).")]
    [SerializeField] private float colliderRadius = 0.5f;

    private void OnEnable()
    {
        NavigationManager.OnAREntered += OnAREntered;
    }

    private void OnDisable()
    {
        NavigationManager.OnAREntered -= OnAREntered;
    }

    private void OnAREntered()
    {
        // Camera.main đã được retag bởi HybridModeController.ApplyMode(Outdoor)
        // trước khi OnAREntered được fire, nên Camera.main ở đây là outdoor camera.
        Camera main = Camera.main;
        if (main == null)
        {
            Debug.LogWarning("[CameraColliderAutoSetup] Camera.main is null when AR entered — will retry next frame.");
            StartCoroutine(RetryNextFrame());
            return;
        }

        EnsureCollider(main.gameObject);
    }

    private System.Collections.IEnumerator RetryNextFrame()
    {
        yield return null; // đợi 1 frame để HybridModeController retag camera
        Camera main = Camera.main;
        if (main != null)
            EnsureCollider(main.gameObject);
        else
            Debug.LogWarning("[CameraColliderAutoSetup] Camera.main still null after retry — SphereCollider not added.");
    }

    private void EnsureCollider(GameObject cameraGO)
    {
        if (cameraGO.GetComponent<SphereCollider>() != null)
            return; // đã có rồi, không cần thêm

        SphereCollider col = cameraGO.AddComponent<SphereCollider>();
        col.radius    = colliderRadius;
        col.isTrigger = true;

        Debug.Log($"[CameraColliderAutoSetup] Đã thêm SphereCollider (trigger, radius={colliderRadius}m) vào '{cameraGO.name}'.");
    }
}
