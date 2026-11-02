using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ARNavB9V2.Editor
{
    /// <summary>Creates a placeable scan asset without changing navigation or the open scene.</summary>
    public static class B10MapPrefabBuilder
    {
        public const string MapCode = "MAP_51D7QG8HJNYS";
        public const string MeshPath = "Assets/MultiSet/MapData/" + MapCode + "/" + MapCode + ".glb";
        public const string PrefabPath = "Assets/MultiSet/MapData/B10_" + MapCode + ".prefab";

        [MenuItem("Tools/AR Navigation V2/Maps/Create B10 Map Prefab")]
        public static void CreatePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the B10 prefab.");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                return;
            }

            AssetDatabase.ImportAsset(MeshPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MeshPath);
            if (model == null)
                throw new InvalidOperationException("B10 textured GLB has not been imported: " + MeshPath);

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                // Preserve every scan-local transform; the wrapper is the placement handle.
                var root = new GameObject("B10_" + MapCode);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                var scan = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                scan.transform.SetParent(root.transform, false);
                scan.name = MapCode;

                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                if (filters.Length == 0 || renderers.Length == 0)
                    throw new InvalidOperationException("B10 scan contains no mesh renderers.");
                long vertices = 0;
                foreach (var filter in filters)
                {
                    if (filter.sharedMesh == null)
                        throw new InvalidOperationException("B10 scan contains a missing mesh.");
                    vertices += filter.sharedMesh.vertexCount;
                }
                foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                        throw new InvalidOperationException("B10 scan contains a missing material/shader.");
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || prefab == null)
                    throw new InvalidOperationException("Could not save B10 prefab.");
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[B10 Map Prefab] Created {PrefabPath}; meshes={filters.Length}, "
                          + $"renderers={renderers.Length}, vertices={vertices}; root position=0, rotation=identity, scale=1.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
