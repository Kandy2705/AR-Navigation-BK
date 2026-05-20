using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Project.DestinationUI
{
    public class BuildingDestinationListController : MonoBehaviour
    {
        public RectTransform content;
        public Transform spawnPoint;
        public GameObject rowPrefab;
        public int heightOfPrefab = 150;

        public TMP_InputField searchField;
        public GameObject resetButtonSearchField;
        public GameObject placeholder;
        public GameObject destinationSelectUI;

        public List<BuildingPoiGroup> buildings = new();

        [Header("Navigation Guards")]
        [SerializeField] float agentNavMeshSampleDistance = 5f;
        [SerializeField] float targetNavMeshSampleDistance = 3f;
        [SerializeField] bool snapTargetColliderToNavMesh = true;

        readonly List<GameObject> spawnedRows = new();
        BuildingPoiGroup selectedBuilding;
        ViewMode currentMode = ViewMode.Buildings;

        enum ViewMode
        {
            Buildings,
            Pois
        }

        public void Toggle()
        {
            if (destinationSelectUI == null)
            {
                return;
            }

            if (destinationSelectUI.activeSelf)
            {
                Close();
                return;
            }

            destinationSelectUI.SetActive(true);
            ResetSearchTextOnly();

            // Auto-sync buildings từ BuildingSceneBindings nếu list rỗng hoặc poiRoot null.
            AutoSyncBuildingsIfNeeded();

            RenderBuildings();
        }

        public void CloseOrBack()
        {
            if (currentMode == ViewMode.Pois)
            {
                ResetSearchTextOnly();
                RenderBuildings();
                return;
            }

            Close();
        }

        public void Close()
        {
            if (destinationSelectUI != null)
            {
                destinationSelectUI.SetActive(false);
            }

            ResetSearchTextOnly();
            ClearRows();
            currentMode = ViewMode.Buildings;
            selectedBuilding = null;
        }

        public void RenderBuildings()
        {
            currentMode = ViewMode.Buildings;
            selectedBuilding = null;

            List<BuildingPoiGroup> filteredBuildings = FilterBuildings(GetSearchText());
            ClearRows();

            for (int i = 0; i < filteredBuildings.Count; i++)
            {
                DestinationRowUI row = SpawnRow(i);
                row.SetupBuilding(filteredBuildings[i], this);
            }

            ResizeContent(filteredBuildings.Count);
        }

        public void RenderPOIs(BuildingPoiGroup building)
        {
            currentMode = ViewMode.Pois;
            selectedBuilding = building;

            string searchText = GetSearchText();
            List<POI> filteredPOIs = FilterPOIs(building, searchText);
            if (filteredPOIs.Count == 0 && string.IsNullOrEmpty(searchText))
            {
                LogEmptyPoiResult(building);
            }

            ClearRows();

            for (int i = 0; i < filteredPOIs.Count; i++)
            {
                DestinationRowUI row = SpawnRow(i);
                row.SetupPOI(filteredPOIs[i], this);
            }

            ResizeContent(filteredPOIs.Count);
        }

        public void SearchOnChanged(string search)
        {
            bool hasSearch = !string.IsNullOrEmpty(search);

            if (resetButtonSearchField != null)
            {
                resetButtonSearchField.SetActive(hasSearch);
            }

            if (placeholder != null)
            {
                placeholder.SetActive(!hasSearch);
            }

            if (currentMode == ViewMode.Buildings)
            {
                RenderBuildings();
                return;
            }

            if (selectedBuilding != null)
            {
                RenderPOIs(selectedBuilding);
            }
        }

        public void ResetSearch()
        {
            ResetSearchTextOnly();

            if (currentMode == ViewMode.Buildings)
            {
                RenderBuildings();
                return;
            }

            if (selectedBuilding != null)
            {
                RenderPOIs(selectedBuilding);
            }
        }

        public void StartNavigationTo(POI poi)
        {
            if (!PrepareNavigationStart(poi))
            {
                return;
            }

            NavigationController.instance.SetPOIForNavigation(poi);

            if (destinationSelectUI != null)
            {
                destinationSelectUI.SetActive(false);
            }

            if (NavigationUIController.instance != null)
            {
                NavigationUIController.instance.stopButton.SetActive(true);
                NavigationUIController.instance.navigationProgressSlider.SetActive(true);
            }
        }

        DestinationRowUI SpawnRow(int index)
        {
            GameObject rowObject = Instantiate(rowPrefab, spawnPoint, false);
            if (rowObject.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = new Vector2(0, -index * heightOfPrefab);
            }
            else
            {
                rowObject.transform.localPosition = new Vector3(0, -index * heightOfPrefab, 0);
            }

            DestinationRowUI row = rowObject.GetComponent<DestinationRowUI>();
            if (row == null)
            {
                row = rowObject.AddComponent<DestinationRowUI>();
            }

            row.BindFromExistingListItem();
            spawnedRows.Add(rowObject);
            return row;
        }

        void ClearRows()
        {
            spawnedRows.Clear();

            if (spawnPoint == null)
            {
                return;
            }

            foreach (Transform child in spawnPoint)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        void ResizeContent(int itemCount)
        {
            if (content != null)
            {
                content.sizeDelta = new Vector2(content.sizeDelta.x, itemCount * heightOfPrefab);
            }
        }

        List<BuildingPoiGroup> FilterBuildings(string searchTerm)
        {
            string search = Normalize(searchTerm);

            return buildings
                .Where(building => building != null && !string.IsNullOrEmpty(building.displayName))
                .Where(building => string.IsNullOrEmpty(search) || Normalize(building.displayName).Contains(search))
                .OrderBy(building => building.displayName)
                .ToList();
        }

        List<POI> FilterPOIs(BuildingPoiGroup building, string searchTerm)
        {
            if (building == null || building.poiRoot == null)
            {
                return new List<POI>();
            }

            string search = Normalize(searchTerm);

            return building.poiRoot
                .GetComponentsInChildren<POI>(true)
                .Where(poi => string.IsNullOrEmpty(search) ||
                              Normalize(poi.listTitle).Contains(search) ||
                              Normalize(poi.poiName).Contains(search))
                .OrderBy(poi => !string.IsNullOrEmpty(poi.listTitle) ? poi.listTitle : poi.poiName)
                .ToList();
        }

        void LogEmptyPoiResult(BuildingPoiGroup building)
        {
            if (building == null)
            {
                Debug.LogWarning("[DestinationUI] Cannot show POIs: selected building is null.", this);
                return;
            }

            if (building.poiRoot == null)
            {
                Debug.LogWarning($"[DestinationUI] Cannot show POIs for {building.displayName}: POI root is missing. Check that the building map is not tagged EditorOnly in the build scene.", this);
                return;
            }

            Debug.LogWarning($"[DestinationUI] No POIs found for {building.displayName} under {building.poiRoot.name}.", building.poiRoot);
        }

        string GetSearchText()
        {
            return searchField != null ? searchField.text : string.Empty;
        }

        string Normalize(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.ToLowerInvariant();
        }

        void ResetSearchTextOnly()
        {
            if (searchField != null)
            {
                searchField.SetTextWithoutNotify(string.Empty);
            }

            if (resetButtonSearchField != null)
            {
                resetButtonSearchField.SetActive(false);
            }

            if (placeholder != null)
            {
                placeholder.SetActive(true);
            }
        }

        /// <summary>
        /// Tự động sync buildings list từ BuildingSceneBindings nếu list rỗng hoặc poiRoot null.
        /// Dùng reflection để tránh dependency vào assembly khác (BuildingSceneBindings ở Assembly-CSharp).
        /// </summary>
        void AutoSyncBuildingsIfNeeded()
        {
            // Kiểm tra xem buildings đã có data hợp lệ chưa.
            bool needsSync = buildings.Count == 0;
            if (!needsSync)
            {
                foreach (var b in buildings)
                {
                    if (b == null || b.poiRoot == null) { needsSync = true; break; }
                }
            }

            if (!needsSync) return;

            // Reflection: tìm component "BuildingSceneBindings" trong scene.
            MonoBehaviour bindingsComp = null;
            foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "BuildingSceneBindings" && mb.gameObject.scene.IsValid())
                {
                    bindingsComp = mb;
                    break;
                }
            }
            if (bindingsComp == null) return;

            var bindingsType = bindingsComp.GetType();
            var bindingsListProp = bindingsType.GetProperty("Bindings");
            if (bindingsListProp == null) return;

            var bindingsEnumerable = bindingsListProp.GetValue(bindingsComp) as System.Collections.IEnumerable;
            if (bindingsEnumerable == null) return;

            // Read Registry property + Find method to get displayName.
            var registryProp = bindingsType.GetProperty("Registry");
            object registry = registryProp?.GetValue(bindingsComp);
            var findMethod = registry?.GetType().GetMethod("Find");

            buildings.Clear();
            foreach (var b in bindingsEnumerable)
            {
                if (b == null) continue;
                var bType = b.GetType();
                var idField = bType.GetField("id");
                var rootField = bType.GetField("buildingRoot");
                var poiField = bType.GetField("poiContainer");
                if (idField == null || rootField == null) continue;

                var buildingRoot = rootField.GetValue(b) as GameObject;
                if (buildingRoot == null) continue;

                var poiContainer = poiField?.GetValue(b) as Transform;
                GameObject poiRoot = poiContainer != null ? poiContainer.gameObject : buildingRoot;

                // Lấy displayName từ Registry.Find(id).
                string displayName = idField.GetValue(b)?.ToString() ?? "Unknown";
                if (findMethod != null && registry != null)
                {
                    var entry = findMethod.Invoke(registry, new[] { idField.GetValue(b) });
                    var entryDisplayName = entry?.GetType().GetField("displayName")?.GetValue(entry) as string;
                    if (!string.IsNullOrEmpty(entryDisplayName)) displayName = entryDisplayName;
                }

                buildings.Add(new BuildingPoiGroup { displayName = displayName, poiRoot = poiRoot });
            }

            Debug.Log($"[BuildingDestinationList] Auto-synced {buildings.Count} buildings from BuildingSceneBindings.");
        }

        bool PrepareNavigationStart(POI poi)
        {
            if (poi == null)
            {
                Debug.LogWarning("[DestinationUI] Cannot start navigation: selected POI is null.", this);
                return false;
            }

            if (poi.poiCollider == null)
            {
                Debug.LogWarning($"[DestinationUI] Cannot start navigation to {poi.poiName}: POI collider is missing.", poi);
                return false;
            }

            NavigationController navigation = NavigationController.instance;
            if (navigation == null)
            {
                Debug.LogWarning("[DestinationUI] Cannot start navigation: NavigationController.instance is null.", this);
                return false;
            }

            if (navigation.agent == null)
            {
                Debug.LogWarning("[DestinationUI] Cannot start navigation: NavigationController agent is not assigned.", navigation);
                return false;
            }

            if (!EnsureAgentOnNavMesh(navigation.agent))
            {
                Debug.LogWarning($"[DestinationUI] Cannot start navigation to {poi.poiName}: agent is not on any nearby NavMesh.", navigation.agent);
                return false;
            }

            if (!EnsureTargetNearNavMesh(poi))
            {
                return false;
            }

            return true;
        }

        bool EnsureAgentOnNavMesh(NavMeshAgent agent)
        {
            if (agent.isOnNavMesh)
            {
                return true;
            }

            if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, agentNavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        bool EnsureTargetNearNavMesh(POI poi)
        {
            Transform target = poi.poiCollider.transform;
            if (!NavMesh.SamplePosition(target.position, out NavMeshHit hit, targetNavMeshSampleDistance, NavMesh.AllAreas))
            {
                Debug.LogWarning(
                    $"[DestinationUI] Cannot start navigation to {poi.poiName}: target collider is too far from NavMesh. " +
                    $"target={target.position}, sampleDistance={targetNavMeshSampleDistance:0.##}m",
                    poi);
                return false;
            }

            if (snapTargetColliderToNavMesh && Vector3.Distance(target.position, hit.position) > 0.01f)
            {
                target.position = hit.position;
            }

            return true;
        }
    }
}
