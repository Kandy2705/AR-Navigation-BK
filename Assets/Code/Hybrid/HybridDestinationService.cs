using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Catalog điểm đến runtime cho HybridGPSMap: Outdoor (<see cref="TargetAnchor"/>)
    /// + Indoor (<see cref="POI"/> theo <see cref="BuildingSceneBindings"/>).
    ///
    /// UI (dropdown outdoor / list indoor / search) gọi <see cref="Apply"/> /
    /// <see cref="ApplyIndoorPoi"/> — không hardcode destination trên Inspector.
    ///
    /// Gắn 1 instance trên Hybrid Hub (hoặc bất kỳ GO persistent). Auto-create nếu thiếu.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)]
    public class HybridDestinationService : MonoBehaviour
    {
        public static HybridDestinationService Instance { get; private set; }

        [Serializable]
        public class Entry
        {
            public string displayName;
            public string searchKey;
            public bool isIndoor;
            public BuildingId building = BuildingId.None;
            public string floorId = "";
            public Transform targetTransform;
            public Vector3 explicitCampusPosition;
            public TargetAnchor outdoorAnchor;
            public POI indoorPoi;

            public string UiLabel
            {
                get
                {
                    if (isIndoor)
                    {
                        string b = building != BuildingId.None ? building.ToString() : "Indoor";
                        return $"[Trong] {b} · {displayName}";
                    }
                    return $"[Ngoài] {displayName}";
                }
            }

            public HybridDestination ToHybridDestination()
            {
                return new HybridDestination
                {
                    displayName = displayName,
                    isIndoor = isIndoor,
                    building = building,
                    floorId = floorId ?? "",
                    targetTransform = targetTransform,
                    explicitCampusPosition = targetTransform != null
                        ? targetTransform.position
                        : explicitCampusPosition,
                };
            }
        }

        [Header("References (auto-resolve)")]
        [SerializeField] private HybridRouteCoordinator coordinator;
        [SerializeField] private HybridLocalizationManager localizationManager;
        [SerializeField] private BuildingSceneBindings sceneBindings;
        [SerializeField] private ARPathFinder outdoorPathFinder;

        [Header("Catalog")]
        [Tooltip("Gồm TargetAnchor outdoor vào danh sách.")]
        [SerializeField] private bool includeOutdoorAnchors = true;

        [Tooltip("Gồm POI indoor từ BuildingSceneBindings.")]
        [SerializeField] private bool includeIndoorPois = true;

        [Tooltip("Refresh catalog mỗi lần Apply/Search nếu true — an toàn khi POI spawn trễ.")]
        [SerializeField] private bool refreshBeforeQuery = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        private readonly List<Entry> _entries = new List<Entry>();
        private Entry _selected;

        public IReadOnlyList<Entry> Entries => _entries;
        public Entry Selected => _selected;
        public bool HasSelection => _selected != null && !string.IsNullOrEmpty(_selected.displayName);

        public event Action<Entry> OnDestinationApplied;
        public event Action OnCatalogRefreshed;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HybridDestinationService] Duplicate instance — destroying this one.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            ResolveRefs();
            RefreshCatalog();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            ResolveRefs();
        }

        /// <summary>Tìm hoặc tạo service trong scene hybrid.</summary>
        public static HybridDestinationService EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<HybridDestinationService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var hub = GameObject.Find("Hybrid Hub");
            if (hub == null)
            {
                hub = new GameObject("Hybrid Hub");
            }
            var svc = hub.GetComponent<HybridDestinationService>();
            if (svc == null) svc = hub.AddComponent<HybridDestinationService>();
            Instance = svc;
            return svc;
        }

        // ---------------------------------------------------------------------
        // Catalog
        // ---------------------------------------------------------------------

        public void RefreshCatalog()
        {
            ResolveRefs();
            _entries.Clear();

            if (includeOutdoorAnchors)
            {
                var anchors = FindObjectsByType<TargetAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Array.Sort(anchors, (a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    return string.Compare(a.TargetName, b.TargetName, StringComparison.OrdinalIgnoreCase);
                });

                foreach (var a in anchors)
                {
                    if (a == null) continue;
                    if (!a.gameObject.scene.IsValid()) continue;
                    _entries.Add(new Entry
                    {
                        displayName = a.TargetName,
                        searchKey = BuildSearchKey(a.TargetName, a.gameObject.name, "outdoor", "ngoai"),
                        isIndoor = false,
                        building = BuildingId.None,
                        targetTransform = a.transform,
                        explicitCampusPosition = a.transform.position,
                        outdoorAnchor = a,
                    });
                }
            }

            if (includeIndoorPois)
            {
                CollectIndoorFromBindings();
                // HybridGPSMap gắn Multiset SDK POI (assembly MultiSet), KHÔNG phải Assets/Code/POI.cs.
                // Luôn chạy collector Multiset + fallback tên container.
                CollectMultisetPoisEverywhere();
                if (_entries.FindIndex(e => e.isIndoor) < 0)
                {
                    CollectProjectPoisFallback();
                }
                if (_entries.FindIndex(e => e.isIndoor) < 0)
                {
                    CollectIndoorByContainerNameFallback();
                }
            }

            if (verboseLog)
            {
                int outdoor = 0, indoor = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].isIndoor) indoor++;
                    else outdoor++;
                }
                Debug.Log($"[HybridDestinationService] Catalog: {_entries.Count} dest (outdoor={outdoor}, indoor={indoor}). " +
                          $"bindings={(sceneBindings != null)}, multisetPoiType={(ResolveMultisetPoiType()?.FullName ?? "null")}");
            }

            OnCatalogRefreshed?.Invoke();
        }

        private void CollectIndoorFromBindings()
        {
            if (sceneBindings == null) return;
            var bindings = sceneBindings.Bindings;
            if (bindings == null) return;

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null) continue;
                Transform poiRoot = b.ResolvedPoiContainer;
                if (poiRoot == null) continue;

                string buildingLabel = b.id.ToString();
                if (sceneBindings.Registry != null)
                {
                    var meta = sceneBindings.Registry.Find(b.id);
                    if (meta != null && !string.IsNullOrEmpty(meta.displayName))
                        buildingLabel = meta.displayName;
                }

                // Project POI (Assets/Code) — thường không có trên HybridGPSMap.
                var projectPois = poiRoot.GetComponentsInChildren<POI>(true);
                foreach (var poi in projectPois)
                {
                    if (poi == null) continue;
                    AddIndoorPoiEntry(poi, b.id, buildingLabel);
                }

                // Multiset SDK POI dưới cùng container.
                CollectMultisetPoisUnder(poiRoot, b.id, buildingLabel);
            }
        }

        /// <summary>
        /// Multiset <c>POI</c> sống trong assembly MultiSet-SDK — khác type với <see cref="POI"/> project.
        /// Dùng Type + GetComponents / FindObjectsByType(Type) để không dính CS1503.
        /// </summary>
        private void CollectMultisetPoisEverywhere()
        {
            Type multisetPoi = ResolveMultisetPoiType();
            if (multisetPoi == null) return;

            // Unity 2023+ / 6: FindObjectsByType(Type, ...)
            UnityEngine.Object[] found;
            try
            {
                found = FindObjectsByType(multisetPoi, FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            catch
            {
                found = Resources.FindObjectsOfTypeAll(multisetPoi);
            }

            if (found == null) return;
            for (int i = 0; i < found.Length; i++)
            {
                var mb = found[i] as MonoBehaviour;
                if (mb == null || !mb.gameObject.scene.IsValid()) continue;
                if (IsAlreadyIndoorTransform(mb.transform)) continue;

                BuildingId building = GuessBuildingIdFromHierarchy(mb.transform);
                string label = building != BuildingId.None ? building.ToString() : "Indoor";
                AddIndoorFromMultisetComponent(mb, building, label);
            }
        }

        private void CollectMultisetPoisUnder(Transform root, BuildingId building, string buildingLabel)
        {
            if (root == null) return;
            Type multisetPoi = ResolveMultisetPoiType();
            if (multisetPoi == null) return;

            var comps = root.GetComponentsInChildren(multisetPoi, true);
            if (comps == null) return;
            for (int i = 0; i < comps.Length; i++)
            {
                var mb = comps[i] as MonoBehaviour;
                if (mb == null) continue;
                if (IsAlreadyIndoorTransform(mb.transform)) continue;
                AddIndoorFromMultisetComponent(mb, building, buildingLabel);
            }
        }

        private void AddIndoorFromMultisetComponent(MonoBehaviour mb, BuildingId building, string buildingLabel)
        {
            if (mb == null) return;
            string name = ReadStringMember(mb, "poiName");
            if (string.IsNullOrEmpty(name)) name = ReadStringMember(mb, "listTitle");
            if (string.IsNullOrEmpty(name)) name = mb.gameObject.name;

            Transform t = mb.transform;
            // Prefer poiCollider transform if present (Multiset field).
            var col = ReadUnityObjectMember(mb, "poiCollider") as Component;
            if (col != null) t = col.transform;

            AddIndoorEntryRaw(name, t, building, buildingLabel, projectPoi: null);
        }

        private void CollectProjectPoisFallback()
        {
            var pois = FindObjectsByType<POI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var poi in pois)
            {
                if (poi == null || !poi.gameObject.scene.IsValid()) continue;
                if (IsAlreadyIndoorTransform(poi.transform)) continue;

                BuildingId building = GuessBuildingIdFromHierarchy(poi.transform);
                AddIndoorPoiEntry(poi, building, building != BuildingId.None ? building.ToString() : "Indoor");
            }
        }

        /// <summary>
        /// Fallback cuối: children của POIs-B9 / POIs-B10 (kể cả inactive) theo hierarchy name.
        /// </summary>
        private void CollectIndoorByContainerNameFallback()
        {
            string[] roots = { "POIs-B9", "POIs-B10", "POIs" };
            foreach (var rootName in roots)
            {
                Transform root = FindTransformByNameIncludingInactive(rootName);
                if (root == null) continue;
                BuildingId building = GuessBuildingIdFromHierarchy(root);
                if (building == BuildingId.None && rootName.Contains("B9")) building = BuildingId.B9;
                if (building == BuildingId.None && rootName.Contains("B10")) building = BuildingId.B10;
                string label = building != BuildingId.None ? building.ToString() : rootName;

                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    if (child == null) continue;
                    // Skip pure mesh/utility nodes without room-like name.
                    string n = child.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.StartsWith("material_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsAlreadyIndoorTransform(child)) continue;
                    AddIndoorEntryRaw(n, child, building, label, projectPoi: null);
                }
            }
        }

        private static Transform FindTransformByNameIncludingInactive(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != name) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                return t;
            }
            return null;
        }

        private bool IsAlreadyIndoorTransform(Transform t)
        {
            if (t == null) return true;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null || !e.isIndoor) continue;
                if (e.targetTransform == t) return true;
                if (e.targetTransform != null && e.targetTransform.IsChildOf(t) && e.displayName == t.name)
                    return true;
                // Same Multiset GO already listed
                if (e.targetTransform != null && e.targetTransform.gameObject == t.gameObject) return true;
            }
            return false;
        }

        private void AddIndoorPoiEntry(POI poi, BuildingId building, string buildingLabel)
        {
            string name = !string.IsNullOrEmpty(poi.listTitle)
                ? poi.listTitle
                : (!string.IsNullOrEmpty(poi.poiName) ? poi.poiName : poi.gameObject.name);

            Transform t = poi.poiCollider != null ? poi.poiCollider.transform : poi.transform;
            AddIndoorEntryRaw(name, t, building, buildingLabel, poi);
        }

        private void AddIndoorEntryRaw(string name, Transform t, BuildingId building, string buildingLabel, POI projectPoi)
        {
            if (t == null || string.IsNullOrEmpty(name)) return;
            if (IsAlreadyIndoorTransform(t)) return;

            _entries.Add(new Entry
            {
                displayName = name,
                searchKey = BuildSearchKey(name, t.gameObject.name, buildingLabel, "indoor", "trong", building.ToString()),
                isIndoor = true,
                building = building,
                floorId = "",
                targetTransform = t,
                explicitCampusPosition = t.position,
                indoorPoi = projectPoi,
            });
        }

        private static Type _cachedMultisetPoiType;
        private static bool _resolvedMultisetPoiType;

        private static Type ResolveMultisetPoiType()
        {
            if (_resolvedMultisetPoiType) return _cachedMultisetPoiType;
            _resolvedMultisetPoiType = true;

            // Prefer MultiSet assembly — avoid project Assets/Code/POI.cs (same type name).
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name ?? "";
                if (an.IndexOf("MultiSet", StringComparison.OrdinalIgnoreCase) < 0
                    && an.IndexOf("multiset", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Type t = asm.GetType("POI") ?? asm.GetType("MultiSet.POI");
                if (t != null && typeof(MonoBehaviour).IsAssignableFrom(t))
                {
                    _cachedMultisetPoiType = t;
                    return t;
                }

                // Scan exported types if not top-level.
                try
                {
                    foreach (var et in asm.GetExportedTypes())
                    {
                        if (et.Name == "POI" && typeof(MonoBehaviour).IsAssignableFrom(et))
                        {
                            _cachedMultisetPoiType = et;
                            return et;
                        }
                    }
                }
                catch
                {
                    // ignore dynamic/reflection-only
                }
            }

            // Last resort: any non-Assembly-CSharp type named POI.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name ?? "";
                if (an == "Assembly-CSharp") continue;
                try
                {
                    Type t = asm.GetType("POI");
                    if (t != null && typeof(MonoBehaviour).IsAssignableFrom(t))
                    {
                        _cachedMultisetPoiType = t;
                        return t;
                    }
                }
                catch { /* ignore */ }
            }

            return null;
        }

        private static string ReadStringMember(object obj, string member)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(member, System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
                return f.GetValue(obj) as string;
            var p = t.GetProperty(member, System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string) && p.CanRead)
                return p.GetValue(obj) as string;
            return null;
        }

        private static UnityEngine.Object ReadUnityObjectMember(object obj, string member)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(member, System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                return f.GetValue(obj) as UnityEngine.Object;
            return null;
        }

        private static BuildingId GuessBuildingIdFromHierarchy(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                // Check B10 before B9 (B10 string doesn't contain B9, but "MapB10" etc.)
                if (n.IndexOf("B10", StringComparison.OrdinalIgnoreCase) >= 0) return BuildingId.B10;
                if (n.IndexOf("B9", StringComparison.OrdinalIgnoreCase) >= 0) return BuildingId.B9;
            }
            return BuildingId.None;
        }

        private static string BuildSearchKey(params string[] parts)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(parts[i].ToLowerInvariant());
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // Query
        // ---------------------------------------------------------------------

        public List<Entry> Search(string query)
        {
            if (refreshBeforeQuery) RefreshCatalog();
            string q = string.IsNullOrWhiteSpace(query) ? "" : query.Trim().ToLowerInvariant();
            var result = new List<Entry>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null) continue;
                if (q.Length == 0
                    || (e.searchKey != null && e.searchKey.Contains(q))
                    || (e.displayName != null && e.displayName.ToLowerInvariant().Contains(q))
                    || (e.UiLabel != null && e.UiLabel.ToLowerInvariant().Contains(q)))
                {
                    result.Add(e);
                }
            }
            return result;
        }

        public bool TryFindByName(string nameOrQuery, out Entry entry)
        {
            entry = null;
            var list = Search(nameOrQuery);
            if (list.Count == 0) return false;
            // Prefer exact displayName match.
            string q = nameOrQuery != null ? nameOrQuery.Trim() : "";
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].displayName, q, StringComparison.OrdinalIgnoreCase))
                {
                    entry = list[i];
                    return true;
                }
            }
            entry = list[0];
            return true;
        }

        // ---------------------------------------------------------------------
        // Apply destination (core API for UI)
        // ---------------------------------------------------------------------

        public bool Apply(Entry entry)
        {
            if (entry == null)
            {
                Debug.LogWarning("[HybridDestinationService] Apply(null).");
                return false;
            }

            ResolveRefs();
            _selected = entry;

            var dest = entry.ToHybridDestination();
            if (coordinator != null)
            {
                coordinator.SetDestination(dest);
            }
            else if (verboseLog)
            {
                Debug.LogWarning("[HybridDestinationService] HybridRouteCoordinator missing — destination not set on hybrid path.");
            }

            if (localizationManager != null)
            {
                if (entry.isIndoor && entry.building != BuildingId.None)
                    localizationManager.SetDestinationBuilding(entry.building, null);
                else
                    localizationManager.SetDestinationBuilding(BuildingId.None, null);
            }

            // Outdoor ribbon: target transform when outdoor dest.
            // Indoor dest: HybridArPathFinderBridge drives ARPathFinder from coordinator.
            if (!entry.isIndoor && outdoorPathFinder != null && entry.targetTransform != null)
            {
                outdoorPathFinder.SetTarget(entry.targetTransform);
            }

            if (entry.outdoorAnchor != null)
            {
                TargetAnchor.CurrentSelectedDestination = entry.outdoorAnchor;
            }
            else
            {
                // Indoor (hoặc outdoor không có anchor): không filter TargetAnchor theo selection cũ.
                TargetAnchor.CurrentSelectedDestination = null;
            }

            // Không gọi NavigationController.SetPOIForNavigation ở đây:
            // project POI (Assets/Code/POI.cs) ≠ Multiset SDK POI → CS1503.
            // Indoor path hybrid đi qua HybridRouteCoordinator + HybridArPathFinderBridge.
            // List indoor Multiset (BuildingDestinationListController) vẫn gọi SDK khi cần.

            if (verboseLog)
            {
                Debug.Log($"[HybridDestinationService] Applied → {entry.UiLabel}  indoor={entry.isIndoor} building={entry.building} pos={dest.CampusPosition}");
            }

            OnDestinationApplied?.Invoke(entry);
            return true;
        }

        public bool ApplyAtIndex(int index)
        {
            if (refreshBeforeQuery) RefreshCatalog();
            if (index < 0 || index >= _entries.Count) return false;
            return Apply(_entries[index]);
        }

        public bool ApplyIndoorPoi(POI poi, BuildingId buildingHint = BuildingId.None)
        {
            if (poi == null) return false;
            if (refreshBeforeQuery) RefreshCatalog();

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].indoorPoi == poi)
                    return Apply(_entries[i]);
            }

            // Not in catalog yet — build on the fly.
            BuildingId b = buildingHint != BuildingId.None ? buildingHint : GuessBuildingIdFromHierarchy(poi.transform);
            string name = !string.IsNullOrEmpty(poi.listTitle) ? poi.listTitle
                : (!string.IsNullOrEmpty(poi.poiName) ? poi.poiName : poi.gameObject.name);
            Transform t = poi.poiCollider != null ? poi.poiCollider.transform : poi.transform;
            var entry = new Entry
            {
                displayName = name,
                searchKey = BuildSearchKey(name, "indoor"),
                isIndoor = true,
                building = b,
                targetTransform = t,
                explicitCampusPosition = t.position,
                indoorPoi = poi,
            };
            return Apply(entry);
        }

        public bool ApplyOutdoorAnchor(TargetAnchor anchor)
        {
            if (anchor == null) return false;
            if (refreshBeforeQuery) RefreshCatalog();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].outdoorAnchor == anchor)
                    return Apply(_entries[i]);
            }

            var entry = new Entry
            {
                displayName = anchor.TargetName,
                searchKey = BuildSearchKey(anchor.TargetName, "outdoor"),
                isIndoor = false,
                targetTransform = anchor.transform,
                explicitCampusPosition = anchor.transform.position,
                outdoorAnchor = anchor,
            };
            return Apply(entry);
        }

        public bool ApplySearchQuery(string query)
        {
            if (!TryFindByName(query, out var entry))
            {
                if (verboseLog) Debug.LogWarning($"[HybridDestinationService] No destination matches '{query}'.");
                return false;
            }
            return Apply(entry);
        }

        public void Clear()
        {
            ResolveRefs();
            _selected = null;
            if (coordinator != null) coordinator.ClearDestination();
            if (outdoorPathFinder != null) outdoorPathFinder.SetTarget(null);

            // Clear mọi path ribbon + MinimapPathMirror (outdoor + hybrid bridge).
            var finders = FindObjectsByType<ARPathFinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < finders.Length; i++)
            {
                if (finders[i] != null) finders[i].ClearNavigationVisuals();
            }

            TargetAnchor.CurrentSelectedDestination = null;
            if (localizationManager != null) localizationManager.SetDestinationBuilding(BuildingId.None, null);

            if (verboseLog) Debug.Log("[HybridDestinationService] Cleared destination + paths.");
        }

        // ---------------------------------------------------------------------
        // Refs
        // ---------------------------------------------------------------------

        private void ResolveRefs()
        {
            if (coordinator == null)
                coordinator = FindFirstObjectByType<HybridRouteCoordinator>(FindObjectsInactive.Include);
            if (localizationManager == null)
                localizationManager = FindFirstObjectByType<HybridLocalizationManager>(FindObjectsInactive.Include);
            if (sceneBindings == null)
                sceneBindings = FindFirstObjectByType<BuildingSceneBindings>(FindObjectsInactive.Include);
            if (outdoorPathFinder == null)
            {
                var outdoorRoot = GameObject.Find("OutdoorEnvironment");
                if (outdoorRoot != null)
                    outdoorPathFinder = outdoorRoot.GetComponentInChildren<ARPathFinder>(true);
                if (outdoorPathFinder == null)
                    outdoorPathFinder = FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
            }
        }
    }
}
