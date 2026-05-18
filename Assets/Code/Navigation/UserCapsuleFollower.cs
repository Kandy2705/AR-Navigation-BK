using UnityEngine;

/// <summary>
/// Đính script này lên GameObject Capsule (con của XR Origin) để capsule
/// luôn di chuyển theo vị trí camera real-time, thay vì chờ GPS cập nhật.
///
/// Tại sao cần script này?
///   - Camera được điều khiển bởi TrackedPoseDriver (dùng IMU của điện thoại) → cập nhật 60fps
///   - XR Origin được điều khiển bởi SimpleGPSTracker (GPS hardware) → cập nhật 1-4 giây/lần
///   - Capsule là con của XR Origin → nếu không có script này, capsule chỉ di chuyển theo GPS
///     và có độ trễ rõ ràng so với chuyển động thực của người dùng
///
/// Giải pháp: mỗi LateUpdate, đặt vị trí XZ của capsule = vị trí XZ của camera world.
///   → Capsule phản ánh chuyển động thực (IMU, không lag)
///   → XR Origin vẫn được GPS định vị tuyệt đối theo định kỳ (world anchor)
/// </summary>
public class UserCapsuleFollower : MonoBehaviour
{
    [Header("Tham chiếu Camera")]
    [Tooltip("Kéo Main Camera vào đây. Nếu để trống, script tự tìm Camera.main.")]
    public Camera mainCamera;

    void Start()
    {
        // Tự động tìm camera nếu chưa được gán trong Inspector
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            Debug.LogWarning("[UserCapsuleFollower] Không tìm thấy Main Camera. " +
                             "Hãy gán thủ công vào field 'mainCamera' trong Inspector.");
    }

    void LateUpdate()
    {
        // Chạy trong LateUpdate để đảm bảo TrackedPoseDriver đã cập nhật camera trước.
        // Nếu dùng Update() thứ tự thực thi không được đảm bảo.
        if (mainCamera == null) return;

        Vector3 camWorldPos = mainCamera.transform.position;

        // Chỉ đồng bộ XZ (ngang), giữ nguyên Y (chiều cao) của capsule
        transform.position = new Vector3(
            camWorldPos.x,
            transform.position.y,
            camWorldPos.z
        );
    }
}
