using System.Collections;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Pre-warm GPS service ngay khi app khởi động (trước cả khi scene HybridGPSMap load).
///
/// Mục đích:
///   Hiện code chỉ start GPS khi SimpleGPSTracker.Start() chạy — mà cái này phụ thuộc
///   user đã đi qua login/home tới AR scene. User chờ thêm 5-15 giây lock GPS = trải
///   nghiệm chậm. Bootstrap này start GPS ngay từ app boot, GPS đã có fix sẵn khi
///   user thực sự cần.
///
/// Flow:
///   1. AfterSceneLoad: tạo 1 GameObject persistent (DontDestroyOnLoad)
///   2. Request Location permission (Android) — chỉ Location, KHÔNG yêu cầu Camera vội
///   3. Khi user grant → Input.location.Start() chạy ngầm
///   4. GPS tích lũy fixes trong khi user navigate UI
///   5. Khi SimpleGPSTracker.Start() chạy sau này, thấy location đã Running →
///      skip init, dùng fix sẵn → instant POI placement
///
/// Camera permission KHÔNG request ở đây — sẽ request sau khi user tap "AR"
/// (HybridModeController.RequestModeWithPermissions handles that). Lý do: tránh
/// hỏi 2 permission cùng lúc lúc mở app = trải nghiệm áp lực.
/// </summary>
public class GpsBootstrap : MonoBehaviour
{
    private static GpsBootstrap _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBoot()
    {
        if (_instance != null) return;

        var go = new GameObject("[GpsBootstrap]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<GpsBootstrap>();
    }

    private IEnumerator Start()
    {
        Debug.Log("[GpsBootstrap] App boot — bắt đầu pre-warm GPS.");

#if UNITY_ANDROID && !UNITY_EDITOR
        // 1. Request Location permission ngay
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);

            // Đợi user phản hồi (max 30s — user có thể đọc dialog chậm)
            float timeoutSeconds = 30f;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) &&
                   timeoutSeconds > 0f)
            {
                timeoutSeconds -= 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.LogWarning("[GpsBootstrap] Location permission denied — bỏ qua pre-warm. " +
                             "SimpleGPSTracker sẽ retry khi user vào AR.");
            yield break;
        }
#endif

        // 2. Check user đã bật GPS trong system settings
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[GpsBootstrap] User chưa bật GPS trong cài đặt hệ thống. " +
                             "App sẽ retry sau.");
            yield break;
        }

        // 3. Start GPS service (chỉ khi chưa chạy, tránh restart cycle)
        if (Input.location.status == LocationServiceStatus.Stopped)
        {
            // desiredAccuracy=5m, updateDistance=1m — match SimpleGPSTracker config
            Input.location.Start(5f, 1f);
            Debug.Log("[GpsBootstrap] Input.location.Start() — đang chờ first fix...");
        }
        else
        {
            Debug.Log($"[GpsBootstrap] Location service đã ở trạng thái {Input.location.status}, " +
                      "skip Start().");
        }

        // 3b. Pre-warm COMPASS — bật ngay từ app boot để hardware có thời gian stabilize
        // trong khi user login/xem UI. Khi vào AR mode, AlignNorthAsync sẽ tìm được
        // compass đã settled → bắc chính xác ngay từ lần đầu (không cần restart app).
        if (!Input.compass.enabled)
        {
            Input.compass.enabled = true;
            Debug.Log("[GpsBootstrap] Input.compass.enabled = true — pre-warm compass cho AR mode.");
        }

        // 4. Đợi initialization xong (log progress)
        int maxWait = 30;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1f);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo data = Input.location.lastData;
            Debug.Log($"[GpsBootstrap] ✓ GPS ready. " +
                      $"Lat={data.latitude:F7} Lon={data.longitude:F7} " +
                      $"Acc=±{data.horizontalAccuracy:F1}m. " +
                      "Khi user vào AR, SimpleGPSTracker dùng fix này luôn.");
        }
        else
        {
            Debug.LogWarning($"[GpsBootstrap] GPS không lock được sau {maxWait}s. " +
                             $"Status={Input.location.status}. SimpleGPSTracker sẽ retry.");
        }
    }

    private void OnApplicationQuit()
    {
        // Dọn dẹp khi app close (Android system thường tự dọn nhưng explicit cho chắc)
        if (Input.location.status == LocationServiceStatus.Running)
        {
            Input.location.Stop();
        }
    }
}
