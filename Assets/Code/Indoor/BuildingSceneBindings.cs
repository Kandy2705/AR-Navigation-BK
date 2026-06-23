using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour sống trong scene, giữ reference đến các GameObject "Building Root"
/// và "POI Container" của từng tòa.
///
/// Cách dùng:
///   - Gắn lên 1 GameObject persistent trong scene, ví dụ "UI Home Screen" hoặc "Map Space".
///   - Gán registry tới asset metadata.
///   - Trong Inspector, mỗi entry là BuildingId, buildingRoot, poiContainer.
///   - Code khác query qua TryGet hoặc Find thay vì query trực tiếp registry.
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

        [Tooltip("Transform cha chứa các POI con. Để trống = dùng buildingRoot.transform.")]
        public Transform poiContainer;

        public Transform ResolvedPoiContainer
        {
            get
            {
                if (poiContainer != null)
                {
                    return poiContainer;
                }

                return buildingRoot != null ? buildingRoot.transform : null;
            }
        }
    }

    [Header("Metadata")]
    [SerializeField] private BuildingRegistry registry;

    [Header("Scene Bindings")]
    [SerializeField] private List<Binding> bindings = new List<Binding>();

    public BuildingRegistry Registry => registry;
    public IReadOnlyList<Binding> Bindings => bindings;

    /// <summary>
    /// Tìm scene binding theo BuildingId.
    /// </summary>
    public Binding Find(BuildingId id)
    {
        if (id == BuildingId.None)
        {
            return null;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            Binding binding = bindings[i];

            if (binding != null && binding.id == id)
            {
                return binding;
            }
        }

        return null;
    }

    /// <summary>
    /// Lấy đồng thời metadata trong BuildingRegistry và scene binding trong scene.
    /// </summary>
    public bool TryGet(BuildingId id, out BuildingRegistry.Entry meta, out Binding scene)
    {
        meta = null;
        scene = null;

        if (id == BuildingId.None)
        {
            Debug.LogWarning("[BuildingSceneBindings] TryGet called with BuildingId.None.");
            return false;
        }

        if (registry == null)
        {
            Debug.LogError("[BuildingSceneBindings] BuildingRegistry chưa được gán.");
            return false;
        }

        meta = registry.Find(id);

        if (meta == null)
        {
            Debug.LogError($"[BuildingSceneBindings] Không tìm thấy metadata cho {id} trong BuildingRegistry.");
            return false;
        }

        scene = Find(id);

        if (scene == null)
        {
            Debug.LogError($"[BuildingSceneBindings] Không tìm thấy scene binding cho {id}.");
            return false;
        }

        if (scene.buildingRoot == null)
        {
            Debug.LogError($"[BuildingSceneBindings] Binding {id} chưa gán buildingRoot.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tắt toàn bộ building root đang gán trong bindings.
    /// </summary>
    public void DeactivateAllBuildings()
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            Binding binding = bindings[i];

            if (binding == null || binding.buildingRoot == null)
            {
                continue;
            }

            if (binding.buildingRoot.activeSelf)
            {
                binding.buildingRoot.SetActive(false);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bindings == null)
        {
            bindings = new List<Binding>();
            return;
        }

        HashSet<BuildingId> usedIds = new HashSet<BuildingId>();

        for (int i = 0; i < bindings.Count; i++)
        {
            Binding binding = bindings[i];

            if (binding == null)
            {
                continue;
            }

            if (binding.id == BuildingId.None)
            {
                continue;
            }

            if (!usedIds.Add(binding.id))
            {
                Debug.LogWarning($"[BuildingSceneBindings] Duplicate binding id detected: {binding.id}", this);
            }

            if (binding.poiContainer == null && binding.buildingRoot != null)
            {
                binding.poiContainer = binding.buildingRoot.transform;
            }
        }
    }
#endif
}