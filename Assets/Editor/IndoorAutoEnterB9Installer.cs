#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tool — tự động setup component <see cref="IndoorAutoEnterB9"/> và wire reference
/// trên <c>MultisetIndoorBootstrap</c>.
///
/// Việc làm:
///   1. Tìm/tạo GameObject "Indoor Auto Enter B9" (sibling của "Indoor Bootstrap" nếu có,
///      else đặt ở scene root).
///   2. Add component <see cref="IndoorAutoEnterB9"/>, wire field hybridModeController +
///      indoorMapSwitcher qua FindFirstObjectByType.
///   3. Trên GO chứa <c>MultisetIndoorBootstrap</c>, set field <c>hybridModeController</c>
///      qua SerializedObject (field mới thêm).
///   4. MarkSceneDirty + Save scene.
///
/// Menu: Tools/Indoor/Setup IndoorAutoEnterB9
/// </summary>
public static class IndoorAutoEnterB9Installer
{
    private const string GoName = "Indoor Auto Enter B9";

    [MenuItem("Tools/Indoor/Setup IndoorAutoEnterB9")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[AutoEnterInstaller] Mở scene trước.");
            return;
        }

        // 1. Resolve references trong scene.
        var hybrid = Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid == null)
        {
            Debug.LogError("[AutoEnterInstaller] Không tìm thấy HybridModeController trong scene.");
            return;
        }

        var switcher = Object.FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        if (switcher == null)
        {
            Debug.LogWarning("[AutoEnterInstaller] Không tìm thấy IndoorMapSwitcher trong scene. " +
                             "Component sẽ tự FindFirstObjectByType ở Awake — nhưng nên gán inspector cho rõ.");
        }

        var bootstrap = Object.FindFirstObjectByType<MultisetIndoorBootstrap>(FindObjectsInactive.Include);
        if (bootstrap == null)
        {
            Debug.LogWarning("[AutoEnterInstaller] Không tìm thấy MultisetIndoorBootstrap trong scene. " +
                             "Skip wiring bootstrap.hybridModeController.");
        }

        // 2. Tìm/tạo GO "Indoor Auto Enter B9".
        GameObject autoGo = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == GoName && t.gameObject.scene.IsValid())
            {
                autoGo = t.gameObject;
                break;
            }
        }

        if (autoGo == null)
        {
            autoGo = new GameObject(GoName);
            Undo.RegisterCreatedObjectUndo(autoGo, "Create Indoor Auto Enter B9");

            // Đặt làm sibling của "Indoor Bootstrap" nếu có, else scene root.
            if (bootstrap != null)
            {
                var bootstrapParent = bootstrap.transform.parent;
                autoGo.transform.SetParent(bootstrapParent, worldPositionStays: false);
            }
            Debug.Log($"[AutoEnterInstaller] Tạo GameObject '{GoName}'.");
        }
        else
        {
            Debug.Log($"[AutoEnterInstaller] Tìm thấy '{GoName}' sẵn có — chỉ update component.");
        }

        // 3. Add hoặc lấy IndoorAutoEnterB9 component.
        var auto = autoGo.GetComponent<IndoorAutoEnterB9>();
        if (auto == null)
        {
            auto = Undo.AddComponent<IndoorAutoEnterB9>(autoGo);
        }

        // 4. Wire fields qua SerializedObject (an toàn cho [SerializeField] private).
        var so = new SerializedObject(auto);
        var hybridProp = so.FindProperty("hybridModeController");
        var switcherProp = so.FindProperty("indoorMapSwitcher");

        if (hybridProp != null) hybridProp.objectReferenceValue = hybrid;
        if (switcherProp != null && switcher != null) switcherProp.objectReferenceValue = switcher;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[AutoEnterInstaller] IndoorAutoEnterB9 wired:" +
                  $" hybrid={(hybrid != null ? hybrid.name : "NULL")}," +
                  $" switcher={(switcher != null ? switcher.name : "NULL")}");

        // 5. Wire MultisetIndoorBootstrap.hybridModeController (field mới thêm).
        if (bootstrap != null)
        {
            var bso = new SerializedObject(bootstrap);
            var bField = bso.FindProperty("hybridModeController");
            if (bField != null)
            {
                bField.objectReferenceValue = hybrid;
                bso.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[AutoEnterInstaller] MultisetIndoorBootstrap.hybridModeController wired.");
            }
            else
            {
                Debug.LogWarning("[AutoEnterInstaller] MultisetIndoorBootstrap không có field 'hybridModeController' — " +
                                 "có thể script chưa recompile. Chạy lại menu sau khi Unity compile xong.");
            }
        }

        // 6. Save scene.
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AutoEnterInstaller] Save scene: {(saved ? "OK" : "FAILED")}.");

        EditorUtility.DisplayDialog("Indoor Auto Enter B9",
            "Đã setup IndoorAutoEnterB9:\n" +
            $"• GO: {GoName}\n" +
            $"• hybridModeController: {(hybrid != null ? hybrid.name : "?")}\n" +
            $"• indoorMapSwitcher: {(switcher != null ? switcher.name : "?")}\n" +
            $"• MultisetIndoorBootstrap.hybridModeController: {(bootstrap != null ? "wired" : "n/a")}\n\n" +
            $"Scene đã được save: {(saved ? "OK" : "FAILED")}.",
            "OK");
    }
}
#endif
