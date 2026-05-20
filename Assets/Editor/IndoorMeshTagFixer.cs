#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor tool — quản lý tag "EditorOnly" cho mesh quét VPS (MAP_xxx / MSET_xxx).
/// Scan toàn bộ scene (bao gồm inactive objects) bằng Resources.FindObjectsOfTypeAll.
/// </summary>
public static class IndoorMeshTagFixer
{
    private const string EditorOnlyTag = "EditorOnly";
    private const string UntaggedTag = "Untagged";

    [MenuItem("Tools/Indoor/Remove EditorOnly Tag from All Map Meshes")]
    public static void RemoveEditorOnlyTag()
    {
        int changed = ScanAndSetTag(EditorOnlyTag, UntaggedTag);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[MeshTagFixer] Removed EditorOnly from {changed} MAP_/MSET_ GameObject(s).");
    }

    [MenuItem("Tools/Indoor/Restore EditorOnly Tag (No Confirm)")]
    public static void RestoreEditorOnlyTagSilent()
    {
        int changed = ScanAndSetTag(UntaggedTag, EditorOnlyTag);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[MeshTagFixer] Restored EditorOnly for {changed} MAP_/MSET_ GameObject(s).");
    }

    [MenuItem("Tools/Indoor/Restore EditorOnly Tag to All Map Meshes")]
    public static void RestoreEditorOnlyTag()
    {
        if (!EditorUtility.DisplayDialog("Restore EditorOnly?",
            "Gắn lại tag EditorOnly cho mọi MAP_/MSET_ GameObject.\nMesh sẽ bị strip khỏi build.",
            "Restore", "Cancel"))
            return;

        RestoreEditorOnlyTagSilent();
    }

    /// <summary>
    /// Scan toàn bộ scene tìm GO có tên bắt đầu MAP_ hoặc MSET_,
    /// đổi tag từ <paramref name="fromTag"/> sang <paramref name="toTag"/>.
    /// </summary>
    private static int ScanAndSetTag(string fromTag, string toTag)
    {
        int changed = 0;
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();

        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue; // Bỏ qua prefab/asset.

            bool isMapMesh = t.name.StartsWith("MAP_") || t.name.StartsWith("MSET_");
            if (!isMapMesh) continue;

            try
            {
                if (t.gameObject.CompareTag(fromTag))
                {
                    Undo.RecordObject(t.gameObject, "Change Tag");
                    t.gameObject.tag = toTag;
                    changed++;
                    Debug.Log($"[MeshTagFixer] '{t.name}' tag: {fromTag} → {toTag}");
                }
            }
            catch (UnityException) { }
        }

        return changed;
    }
}
#endif
