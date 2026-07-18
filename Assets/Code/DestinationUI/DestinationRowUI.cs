using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.DestinationUI
{
    public class DestinationRowUI : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI distance;
        [SerializeField] GameObject startNavigationButton;
        [SerializeField] Button itemSelectButton;

        POI poi;
        BuildingDestinationListController controller;
        System.Action pendingAction;

        public void BindFromExistingListItem()
        {
            ListItemUI sdkListItem = GetComponent<ListItemUI>();
            if (sdkListItem == null)
            {
                return;
            }

            title ??= sdkListItem.title;
            distance ??= sdkListItem.distance;
            startNavigationButton ??= sdkListItem.startNavigationButton;
            itemSelectButton ??= sdkListItem.itemSelectButton;

            sdkListItem.enabled = false;
        }

        /// <summary>
        /// Row generic cho list đích chung (outdoor + indoor catalog) — bấm cả hàng để chạy action.
        /// </summary>
        public void SetupActionRow(string label, System.Action onSelect, BuildingDestinationListController owner)
        {
            poi = null;
            pendingAction = onSelect;
            controller = owner;

            if (title != null)
            {
                title.text = label ?? "";
            }

            if (distance != null)
            {
                distance.gameObject.SetActive(false);
            }

            if (startNavigationButton != null)
            {
                startNavigationButton.SetActive(true);
            }

            ClearButtonListeners();
            if (itemSelectButton != null)
            {
                itemSelectButton.onClick.AddListener(() => pendingAction?.Invoke());
            }

            Button goButton = startNavigationButton != null ? startNavigationButton.GetComponent<Button>() : null;
            if (goButton != null)
            {
                goButton.onClick.RemoveAllListeners();
                goButton.onClick.AddListener(() => pendingAction?.Invoke());
            }
        }

        public void SetupBuilding(BuildingPoiGroup building, BuildingDestinationListController owner)
        {
            poi = null;
            pendingAction = null;
            controller = owner;

            if (title != null)
            {
                title.text = building.displayName;
            }

            if (distance != null)
            {
                distance.gameObject.SetActive(false);
            }

            if (startNavigationButton != null)
            {
                startNavigationButton.SetActive(false);
            }

            ClearButtonListeners();
            if (itemSelectButton != null)
            {
                itemSelectButton.onClick.AddListener(() => controller.RenderPOIs(building));
            }
        }

        public void SetupPOI(POI targetPoi, BuildingDestinationListController owner)
        {
            poi = targetPoi;
            pendingAction = null;
            controller = owner;

            if (title != null)
            {
                title.text = !string.IsNullOrEmpty(poi.listTitle) ? poi.listTitle : poi.poiName;
            }

            if (distance != null)
            {
                distance.gameObject.SetActive(true);
                distance.text = GetDistanceText();
            }

            if (startNavigationButton != null)
            {
                startNavigationButton.SetActive(true);
            }

            ClearButtonListeners();
            if (itemSelectButton != null)
            {
                itemSelectButton.onClick.AddListener(() => controller.StartNavigationTo(poi));
            }

            Button goButton = startNavigationButton != null ? startNavigationButton.GetComponent<Button>() : null;
            if (goButton != null)
            {
                goButton.onClick.RemoveAllListeners();
                goButton.onClick.AddListener(() => controller.StartNavigationTo(poi));
            }
        }

        void Update()
        {
            if (poi == null || distance == null || !distance.gameObject.activeSelf)
            {
                return;
            }

            distance.text = GetDistanceText();
        }

        void ClearButtonListeners()
        {
            if (itemSelectButton != null)
            {
                itemSelectButton.onClick.RemoveAllListeners();
            }

            Button goButton = startNavigationButton != null ? startNavigationButton.GetComponent<Button>() : null;
            if (goButton != null)
            {
                goButton.onClick.RemoveAllListeners();
            }
        }

        string GetDistanceText()
        {
            if (poi == null || poi.poiCollider == null || PathEstimationUtils.instance == null ||
                NavigationController.instance == null || NavigationController.instance.agent == null ||
                !NavigationController.instance.agent.isOnNavMesh)
            {
                return "-";
            }

            float estimatedDistance = PathEstimationUtils.instance.EstimateDistanceToPosition(poi);
            if (estimatedDistance > 0)
            {
                return Mathf.RoundToInt(estimatedDistance) + " m";
            }

            if (estimatedDistance == -2)
            {
                return "Unreachable";
            }

            return "-";
        }
    }
}
