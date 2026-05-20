#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reset Indoor XR Origin về cấu hình chuẩn cho AR mobile + Multiset VPS:
///   - Position = (0, 0, 0), Rotation = identity, Scale = (1, 1, 1)
///   - TrackingOriginMode = Device
///   - CameraYOffset = 0 (để Multiset SDK tự align Map Space sau localize)
///   - ARCamera tag = MainCamera (HybridModeController vẫn override runtime, nhưng set
///     tag tĩnh giúp Awake-order race ít xảy ra hơn)
///   - HybridModeController.disableIndoorARSessionDuplicates = true (chỉ 1 AR Session
///     active một lúc, tránh conflict)
///
/// Sau khi chạy, scene sẽ được save.
///
/// Menu: Tools/Indoor/Reset XR Origin To Standard
/// </summary>
public static class IndoorXROriginReset
{
    [MenuItem("Tools/Indoor/Reset XR Origin To Standard")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[XROriginReset] Mở scene trước.");
            return;
        }

        // 1. Tìm Indoor XR Origin qua path scan (bao gồm inactive).
        XROrigin indoorOrigin = null;
        foreach (var xr in Resources.FindObjectsOfTypeAll<XROrigin>())
        {
            if (xr == null || !xr.gameObject.scene.IsValid()) continue;
            var path = GetPath(xr.transform);
            if (path.Contains("IndoorEnvironment"))
            {
                indoorOrigin = xr;
                break;
            }
        }
        if (indoorOrigin == null)
        {
            Debug.LogError("[XROriginReset] Không tìm thấy XR Origin dưới IndoorEnvironment.");
            return;
        }

        Undo.RecordObject(indoorOrigin.transform, "Reset Indoor XR Origin transform");
        Undo.RecordObject(indoorOrigin, "Reset Indoor XR Origin");

        // 2. Reset transform.
        indoorOrigin.transform.localPosition = Vector3.zero;
        indoorOrigin.transform.localRotation = Quaternion.identity;
        indoorOrigin.transform.localScale = Vector3.one;

        // 3. Set tracking mode + camera offset.
        indoorOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        indoorOrigin.CameraYOffset = 0f;

        // Camera Offset GO localPos cũng reset về 0 cho gọn.
        if (indoorOrigin.CameraFloorOffsetObject != null)
        {
            Undo.RecordObject(indoorOrigin.CameraFloorOffsetObject.transform, "Reset Camera Offset");
            indoorOrigin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
            indoorOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.identity;
        }

        EditorUtility.SetDirty(indoorOrigin);
        EditorUtility.SetDirty(indoorOrigin.transform);

        Debug.Log($"[XROriginReset] Reset '{GetPath(indoorOrigin.transform)}':\n" +
                  $"  pos={indoorOrigin.transform.position}, rot={indoorOrigin.transform.eulerAngles},\n" +
                  $"  TrackingOriginMode={indoorOrigin.RequestedTrackingOriginMode}, CameraYOffset={indoorOrigin.CameraYOffset}");

        // 4. Set ARCamera tag = MainCamera.
        var cam = indoorOrigin.Camera;
        if (cam != null)
        {
            Undo.RecordObject(cam.gameObject, "Tag ARCamera");
            cam.gameObject.tag = "MainCamera";
            EditorUtility.SetDirty(cam.gameObject);
            Debug.Log($"[XROriginReset] '{cam.name}' tag = MainCamera.");
        }

        // 5. HybridModeController.disableIndoorARSessionDuplicates = true.
        var hybrid = Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        if (hybrid != null)
        {
            var so = new SerializedObject(hybrid);
            var prop = so.FindProperty("disableIndoorARSessionDuplicates");
            if (prop != null)
            {
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[XROriginReset] HybridModeController.disableIndoorARSessionDuplicates = true.");
            }
        }

        // 6. Save scene.
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[XROriginReset] Done. Scene saved.");
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        var sb = new System.Text.StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null)
        {
            sb.Insert(0, "/");
            sb.Insert(0, cur.name);
            cur = cur.parent;
        }
        return sb.ToString();
    }
}
#endif
