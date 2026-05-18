using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Xóa AR Session thừa bên trong OutdoorEnvironment.
/// SharedARRig đã có AR Session rồi — chỉ cần 1 cái duy nhất.
///
/// Cách dùng: Unity menu → Tools → Fix AR → Remove Duplicate AR Session
/// </summary>
public static class RemoveDuplicateARSession
{
    [MenuItem("Tools/Fix AR/Remove Duplicate AR Session in OutdoorEnvironment")]
    public static void Execute()
    {
        // Tìm OutdoorEnvironment
        GameObject outdoorEnv = GameObject.Find("OutdoorEnvironment");
        if (outdoorEnv == null)
        {
            EditorUtility.DisplayDialog("Không tìm thấy",
                "Không tìm thấy GameObject 'OutdoorEnvironment' trong scene hiện tại.\n" +
                "Hãy mở scene HybridGPSMap trước.", "OK");
            return;
        }

        // Tìm tất cả AR Session bên trong OutdoorEnvironment
        ARSession[] sessions = outdoorEnv.GetComponentsInChildren<ARSession>(true);
        if (sessions.Length == 0)
        {
            EditorUtility.DisplayDialog("Không tìm thấy",
                "Không có AR Session nào trong OutdoorEnvironment.\n" +
                "Có thể đã được xóa trước đó rồi.", "OK");
            return;
        }

        // Xác nhận trước khi xóa
        string names = "";
        foreach (var s in sessions)
            names += $"\n  • {s.gameObject.name}";

        bool confirm = EditorUtility.DisplayDialog(
            "Xác nhận xóa",
            $"Tìm thấy {sessions.Length} AR Session trong OutdoorEnvironment:{names}\n\n" +
            "SharedARRig đã có AR Session riêng rồi.\n" +
            "Xóa các AR Session thừa này đi?",
            "Xóa", "Hủy");

        if (!confirm) return;

        // Xóa
        int count = 0;
        foreach (var s in sessions)
        {
            string goName = s.gameObject.name;
            Undo.DestroyObjectImmediate(s.gameObject);
            Debug.Log($"[RemoveDuplicateARSession] Đã xóa GameObject '{goName}' khỏi OutdoorEnvironment.");
            count++;
        }

        // Lưu scene
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        EditorUtility.DisplayDialog("Hoàn thành",
            $"Đã xóa {count} AR Session khỏi OutdoorEnvironment.\n" +
            "Scene đã được đánh dấu dirty — nhớ Save (Ctrl+S).", "OK");
    }

    [MenuItem("Tools/Fix AR/Remove Duplicate AR Session in OutdoorEnvironment", true)]
    public static bool ValidateExecute()
    {
        // Chỉ enable khi đang có scene mở
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().IsValid();
    }
}
