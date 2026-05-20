using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour sống trong scene, giữ reference đến các GameObject "Building Root"
/// và "POI Container" của từng tòa.
///
/// Cách dùng:
///   - Gắn lên 1 GameObject persistent trong scene (vd "UI Home Screen" hoặc "Map Space").
///   - Gán <see cref="registry"/> tới asset metadata.
///   - Trong inspector, mỗi entry là (BuildingId, GameObject buildingRoot, Transform poiContainer).
///   - Code khác query qua <see cref="GetBindings"/> thay vì query trực tiếp registry.
/// </summary>
[DisallowMultipleComponent]
public class BuildingSceneBindings : MonoBehaviour
{
    [Serializable]
    public class Binding
    {
        public BuildingId id = BuildingId.None;

        [Tooltip("GameObject chứa mesh + POI + NavMeshSurface của tòa này. Active khi switch tới tòa.")]
        public GameObject buildingRoot;

        [Tooltip("Transform cha chứa các POI con (component POI). Để trống = dùng buildingRoot.")]
        public Transform poiContainer;
    }

    [SerializeField] private BuildingRegistry registry;
    [SerializeField] private List<Binding> bindings = new List<Binding>();

    public BuildingRegistry Registry => registry;
    public IReadOnlyList<Binding> Bindings => bindings;

    public Binding Find(BuildingId id)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i] != null && bindings[i].id == id) return bindings[i];
        }
        return null;
    }

    /// <summary>Combo metadata + scene binding cho 1 tòa.</summary>
    public bool TryGet(BuildingId id, out BuildingRegistry.Entry meta, out Binding scene)
    {
        meta = registry != null ? registry.Find(id) : null;
        scene = Find(id);
        return meta != null && scene != null;
    }
}
