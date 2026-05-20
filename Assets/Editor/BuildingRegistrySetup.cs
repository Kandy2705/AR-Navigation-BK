#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utility:
///   1. Tạo BuildingRegistry asset (metadata B9 / B10).
///   2. Thêm component BuildingSceneBindings vào "UI Home Screen" với 2 binding trỏ tới
///      MapB9 / MapB10 GameObject thực trong scene.
///
/// Menu: Tools/Indoor/Setup BuildingRegistry.
/// </summary>
public static class BuildingRegistrySetup
{
    private const string AssetPath = "Assets/Code/Indoor/BuildingRegistry.asset";

    [MenuItem("Tools/Indoor/Setup BuildingRegistry")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[BuildingRegistrySetup] Mở scene HybridGPSMap trước khi chạy.");
            return;
        }

        GameObject mapB9 = FindByPath("IndoorEnvironment/UI Home Screen/Map Space/MapB9");
        GameObject mapB10 = FindByPath("IndoorEnvironment/UI Home Screen/Map Space/MapB10");
        GameObject homeScreen = FindByPath("IndoorEnvironment/UI Home Screen");

        if (mapB9 == null || mapB10 == null || homeScreen == null)
        {
            Debug.LogError($"[BuildingRegistrySetup] Không tìm thấy MapB9 ({mapB9 != null}) / MapB10 ({mapB10 != null}) / UI Home Screen ({homeScreen != null}). Kiểm tra hierarchy.");
            return;
        }

        Transform poisB9 = FindTransformByName("POIs-B9");
        Transform poisB10 = FindTransformByName("POIs-B10");
        
        if (poisB9 == null) Debug.LogWarning("[BuildingRegistrySetup] POIs-B9 không tìm thấy. Gán thủ công trong Inspector.");
        if (poisB10 == null) Debug.LogWarning("[BuildingRegistrySetup] POIs-B10 không tìm thấy. Gán thủ công trong Inspector.");

        // ──────────────────────────────────────────────────────────────────
        // 1. Tạo / cập nhật BuildingRegistry asset (metadata only, không scene refs).
        // ──────────────────────────────────────────────────────────────────
        BuildingRegistry registry = AssetDatabase.LoadAssetAtPath<BuildingRegistry>(AssetPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<BuildingRegistry>();
            AssetDatabase.CreateAsset(registry, AssetPath);
        }

        var soReg = new SerializedObject(registry);
        SerializedProperty list = soReg.FindProperty("buildings");
        list.arraySize = 2;

        SetEntry(list.GetArrayElementAtIndex(0), BuildingId.B9, "Tòa B9", "MAP_9LME2PB7Y3EN", BuildingRegistry.MultisetLocalizationKind.Map);
        SetEntry(list.GetArrayElementAtIndex(1), BuildingId.B10, "Tòa B10", "MSET_AWDJFJNAVVFM", BuildingRegistry.MultisetLocalizationKind.MapSet);

        soReg.ApplyModifiedProperties();
        EditorUtility.SetDirty(registry);

        // ──────────────────────────────────────────────────────────────────
        // 2. Thêm/cấu hình BuildingSceneBindings trên UI Home Screen.
        // ──────────────────────────────────────────────────────────────────
        var sceneBindings = homeScreen.GetComponent<BuildingSceneBindings>();
        if (sceneBindings == null)
        {
            sceneBindings = Undo.AddComponent<BuildingSceneBindings>(homeScreen);
        }

        var soSb = new SerializedObject(sceneBindings);
        soSb.FindProperty("registry").objectReferenceValue = registry;

        SerializedProperty bindings = soSb.FindProperty("bindings");
        bindings.arraySize = 2;

        SetBinding(bindings.GetArrayElementAtIndex(0), BuildingId.B9, mapB9, poisB9);
        SetBinding(bindings.GetArrayElementAtIndex(1), BuildingId.B10, mapB10, poisB10);

        soSb.ApplyModifiedProperties();
        EditorUtility.SetDirty(sceneBindings);

        // ──────────────────────────────────────────────────────────────────
        // 3. Tự wire IndoorMapSwitcher trên cùng GameObject (nếu có).
        // ──────────────────────────────────────────────────────────────────
        var switcher = homeScreen.GetComponent<IndoorMapSwitcher>();
        if (switcher != null)
        {
            var soSw = new SerializedObject(switcher);

            soSw.FindProperty("sceneBindings").objectReferenceValue = sceneBindings;

            // Tìm MapLocalizationManager component theo TÊN class (string) — không cần ref DLL.
            var localizerGo = FindByPath("IndoorEnvironment/UI Home Screen/MapLocalizationManager");
            if (localizerGo != null)
            {
                Component localizerComp = null;
                foreach (var c in localizerGo.GetComponents<Component>())
                {
                    if (c == null) continue;
                    if (c.GetType().Name == "MapLocalizationManager") { localizerComp = c; break; }
                }

                if (localizerComp != null)
                {
                    soSw.FindProperty("mapLocalizationManager").objectReferenceValue = localizerComp;
                }
                else
                {
                    Debug.LogWarning("[BuildingRegistrySetup] Không tìm thấy component MapLocalizationManager. Wire thủ công trong Inspector.");
                }
            }

            // HybridModeController
            var hybridCtrl = Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
            if (hybridCtrl != null)
            {
                soSw.FindProperty("hybridModeController").objectReferenceValue = hybridCtrl;
            }
            else
            {
                Debug.LogWarning("[BuildingRegistrySetup] Không tìm thấy HybridModeController trong scene.");
            }

            soSw.ApplyModifiedProperties();
            EditorUtility.SetDirty(switcher);
        }
        else
        {
            Debug.LogWarning("[BuildingRegistrySetup] IndoorMapSwitcher chưa có trên 'UI Home Screen'. Add component thủ công rồi chạy lại setup.");
        }

        EditorSceneManager.MarkSceneDirty(scene);

        // Đảm bảo MapB9/MapB10 inactive mặc định (IndoorMapSwitcher sẽ bật đúng cái cần).
        if (mapB9.activeSelf) mapB9.SetActive(false);
        if (mapB10.activeSelf) mapB10.SetActive(false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = registry;
        EditorGUIUtility.PingObject(registry);

        Debug.Log("[BuildingRegistrySetup] Done. Registry + scene bindings đã sẵn sàng. Save scene để giữ thay đổi.");
    }

    private static void SetEntry(SerializedProperty entry, BuildingId id, string displayName, string mapsetCode, BuildingRegistry.MultisetLocalizationKind kind)
    {
        SetEnumByValue(entry.FindPropertyRelative("id"), (int)id);
        entry.FindPropertyRelative("displayName").stringValue = displayName;
        entry.FindPropertyRelative("mapsetCode").stringValue = mapsetCode;
        SetEnumByValue(entry.FindPropertyRelative("kind"), (int)kind);
        entry.FindPropertyRelative("entranceTriggerRadiusMeters").floatValue = 30f;
    }

    private static void SetBinding(SerializedProperty binding, BuildingId id, GameObject buildingRoot, Transform poiContainer)
    {
        SetEnumByValue(binding.FindPropertyRelative("id"), (int)id);
        binding.FindPropertyRelative("buildingRoot").objectReferenceValue = buildingRoot;
        binding.FindPropertyRelative("poiContainer").objectReferenceValue = poiContainer;
    }

    /// <summary>Set enum property by integer VALUE (not index). Works for enums with non-sequential values.</summary>
    private static void SetEnumByValue(SerializedProperty prop, int value)
    {
        // enumValueIndex is the index in the enum names array, not the integer value.
        // For enums with gaps (None=0, B9=9, B10=10), we must find the correct index.
        string[] names = prop.enumNames;
        var enumType = System.Enum.GetValues(typeof(BuildingId)); // fallback
        
        // Use intValue directly — Unity 2021+ supports this for enum backing fields.
        prop.intValue = value;
    }

    private static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;

        // Tìm root (có thể inactive) bằng Resources.FindObjectsOfTypeAll.
        Transform root = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == parts[0] && t.parent == null && t.gameObject.scene.IsValid())
            {
                root = t;
                break;
            }
        }
        if (root == null) return null;

        Transform current = root;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }
        return current.gameObject;
    }

    /// <summary>Tìm Transform theo tên trong toàn scene, bao gồm inactive objects.</summary>
    private static Transform FindTransformByName(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == name && t.gameObject.scene.IsValid())
                return t;
        }
        return null;
    }
}
#endif
