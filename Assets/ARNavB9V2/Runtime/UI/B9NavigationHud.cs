using System.Collections.Generic;
using System.Linq;
using ARNavB9V2.Data;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Vps;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARNavB9V2.UI
{
    [DisallowMultipleComponent]
    public sealed class B9NavigationHud : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private B9BuildingDefinition building;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9OutdoorRouteController routeController;
        [SerializeField] private B9OutdoorMinimapController minimapController;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9IndoorRouteController indoorRouteController;
        [SerializeField] private B9IndoorMinimapController indoorMinimapController;

        private Label statusLabel;
        private Label gpsLabel;
        private Label destinationSummary;
        private DropdownField destinationDropdown;
        private VisualElement minimapFrame;
        private VisualElement minimapView;
        private Label minimapHint;
        private Button outdoorStartButton;
        private Button retryVpsButton;
        private bool minimapExpanded;
        private float nextRefresh;

        public void Configure(
            UIDocument uiDocument,
            B9BuildingDefinition buildingDefinition,
            B9OutdoorLocationProvider provider,
            B9OutdoorRouteController route,
            B9OutdoorMinimapController minimap)
        {
            document = uiDocument;
            building = buildingDefinition;
            locationProvider = provider;
            routeController = route;
            minimapController = minimap;
        }

        public void AttachVpsTransition(B9VpsTransitionController transition)
        {
            vpsTransition = transition;
            RefreshStatus();
        }

        public void AttachIndoorNavigation(
            B9IndoorRouteController indoorRoute,
            B9IndoorMinimapController indoorMinimap)
        {
            indoorRouteController = indoorRoute;
            indoorMinimapController = indoorMinimap;
            if (destinationDropdown != null && !string.IsNullOrWhiteSpace(destinationDropdown.value))
                indoorRouteController?.SetDestinationRoom(destinationDropdown.value);
            RefreshStatus();
        }

        private void OnEnable()
        {
            BuildInterface();
        }

        private void Start()
        {
            BuildInterface();
            if (routeController != null && destinationDropdown != null)
                routeController.SetDestinationRoom(destinationDropdown.value);
            if (indoorRouteController != null && destinationDropdown != null)
                indoorRouteController.SetDestinationRoom(destinationDropdown.value);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + 0.2f;
            RefreshStatus();
        }

        private void BuildInterface()
        {
            if (document == null || document.rootVisualElement == null || building == null)
                return;

            VisualElement root = document.rootVisualElement;
            root.Clear();
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.right = 0f;
            root.style.top = 0f;
            root.style.bottom = 0f;
            root.pickingMode = PickingMode.Ignore;

            VisualElement statusPanel = CreatePanel(new Color(0.025f, 0.055f, 0.1f, 0.9f), 18f);
            statusPanel.style.position = Position.Absolute;
            statusPanel.style.left = 24f;
            statusPanel.style.top = 38f;
            statusPanel.style.width = 520f;
            statusPanel.style.paddingLeft = 22f;
            statusPanel.style.paddingRight = 22f;
            statusPanel.style.paddingTop = 16f;
            statusPanel.style.paddingBottom = 16f;
            statusPanel.pickingMode = PickingMode.Position;
            root.Add(statusPanel);

            statusLabel = new Label("Đang chuẩn bị GPS…");
            statusLabel.style.color = Color.white;
            statusLabel.style.fontSize = 27f;
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusPanel.Add(statusLabel);

            gpsLabel = new Label("SchoolGround · GPS");
            gpsLabel.style.color = new Color(0.45f, 0.82f, 1f, 1f);
            gpsLabel.style.fontSize = 20f;
            gpsLabel.style.marginTop = 6f;
            statusPanel.Add(gpsLabel);

            retryVpsButton = new Button(() => vpsTransition?.RetryLocalization())
            {
                text = "QUÉT LẠI VPS"
            };
            retryVpsButton.style.height = 52f;
            retryVpsButton.style.marginTop = 12f;
            retryVpsButton.style.backgroundColor = new Color(0.02f, 0.42f, 0.94f, 1f);
            retryVpsButton.style.color = Color.white;
            retryVpsButton.style.fontSize = 21f;
            retryVpsButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            retryVpsButton.style.display = DisplayStyle.None;
            statusPanel.Add(retryVpsButton);

            minimapFrame = CreatePanel(new Color(0.04f, 0.09f, 0.14f, 0.94f), 999f);
            minimapFrame.style.position = Position.Absolute;
            minimapFrame.style.right = 24f;
            minimapFrame.style.paddingLeft = 8f;
            minimapFrame.style.paddingRight = 8f;
            minimapFrame.style.paddingTop = 8f;
            minimapFrame.style.paddingBottom = 8f;
            minimapFrame.style.overflow = Overflow.Hidden;
            minimapFrame.pickingMode = PickingMode.Position;
            minimapFrame.RegisterCallback<ClickEvent>(_ => ToggleMinimapPresentation());
            root.Add(minimapFrame);

            minimapView = new VisualElement();
            minimapView.pickingMode = PickingMode.Ignore;
            minimapView.style.flexGrow = 1f;
            minimapView.style.borderTopLeftRadius = 999f;
            minimapView.style.borderTopRightRadius = 999f;
            minimapView.style.borderBottomLeftRadius = 999f;
            minimapView.style.borderBottomRightRadius = 999f;
            minimapView.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            minimapFrame.Add(minimapView);

            Label north = new Label("N");
            north.style.position = Position.Absolute;
            north.style.top = 10f;
            north.style.left = 0f;
            north.style.right = 0f;
            north.style.unityTextAlign = TextAnchor.MiddleCenter;
            north.style.color = new Color(1f, 0.82f, 0.2f, 1f);
            north.style.fontSize = 24f;
            north.style.unityFontStyleAndWeight = FontStyle.Bold;
            north.pickingMode = PickingMode.Ignore;
            minimapFrame.Add(north);

            minimapHint = new Label();
            minimapHint.style.position = Position.Absolute;
            minimapHint.style.left = 0f;
            minimapHint.style.right = 0f;
            minimapHint.style.bottom = 12f;
            minimapHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            minimapHint.style.color = new Color(0.85f, 0.94f, 1f, 0.9f);
            minimapHint.style.fontSize = 15f;
            minimapHint.style.unityFontStyleAndWeight = FontStyle.Bold;
            minimapHint.pickingMode = PickingMode.Ignore;
            minimapFrame.Add(minimapHint);

            VisualElement destinationPanel = CreatePanel(new Color(0.025f, 0.05f, 0.09f, 0.94f), 20f);
            destinationPanel.style.position = Position.Absolute;
            destinationPanel.style.left = 24f;
            destinationPanel.style.right = 24f;
            destinationPanel.style.bottom = 42f;
            destinationPanel.style.paddingLeft = 24f;
            destinationPanel.style.paddingRight = 24f;
            destinationPanel.style.paddingTop = 18f;
            destinationPanel.style.paddingBottom = 20f;
            destinationPanel.pickingMode = PickingMode.Position;
            root.Add(destinationPanel);

            Label title = new Label("ĐIỂM ĐẾN · TÒA B9");
            title.style.color = new Color(0.38f, 0.8f, 1f, 1f);
            title.style.fontSize = 21f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            destinationPanel.Add(title);

            List<string> rooms = building.Rooms.Select(room => room.RoomId).ToList();
            string initialRoom = rooms.Contains("B9-104") ? "B9-104" : rooms.FirstOrDefault();
            destinationDropdown = new DropdownField("Chọn phòng", rooms, initialRoom);
            destinationDropdown.style.marginTop = 10f;
            destinationDropdown.style.fontSize = 26f;
            destinationDropdown.RegisterValueChangedCallback(evt =>
            {
                routeController?.SetDestinationRoom(evt.newValue);
                indoorRouteController?.SetDestinationRoom(evt.newValue);
                RefreshStatus();
            });
            destinationPanel.Add(destinationDropdown);

            outdoorStartButton = new Button(() =>
            {
                if (destinationDropdown != null)
                {
                    routeController?.SetDestinationRoom(destinationDropdown.value);
                    indoorRouteController?.SetDestinationRoom(destinationDropdown.value);
                }
            })
            {
                text = "DẪN ĐẾN CỬA B9"
            };
            outdoorStartButton.style.height = 58f;
            outdoorStartButton.style.marginTop = 12f;
            outdoorStartButton.style.backgroundColor = new Color(0.02f, 0.42f, 0.94f, 1f);
            outdoorStartButton.style.color = Color.white;
            outdoorStartButton.style.fontSize = 23f;
            outdoorStartButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            destinationPanel.Add(outdoorStartButton);

            destinationSummary = new Label("Ngoài trời → cửa B9 → phòng đã chọn");
            destinationSummary.style.color = new Color(0.78f, 0.86f, 0.95f, 1f);
            destinationSummary.style.fontSize = 19f;
            destinationSummary.style.marginTop = 10f;
            destinationPanel.Add(destinationSummary);

            if (minimapController != null && minimapController.RenderedTexture != null)
            {
                minimapView.style.backgroundImage = new StyleBackground(
                    Background.FromRenderTexture(minimapController.RenderedTexture));
            }
            ApplyMinimapPresentation();
            RefreshStatus();
        }

        private void ToggleMinimapPresentation()
        {
            minimapExpanded = !minimapExpanded;
            ApplyMinimapPresentation();
        }

        private void ApplyMinimapPresentation()
        {
            if (minimapFrame == null)
                return;

            float size = minimapExpanded ? 660f : 330f;
            minimapFrame.style.width = size;
            minimapFrame.style.height = size;
            minimapFrame.style.top = minimapExpanded ? 200f : 34f;
            if (minimapHint != null)
                minimapHint.text = minimapExpanded
                    ? "CHẠM ĐỂ THU NHỎ"
                    : "CHẠM XEM TỔNG QUAN";
            if (vpsTransition != null
                && vpsTransition.State == B9VpsTransitionController.TransitionState.IndoorLocalized
                && indoorMinimapController != null)
            {
                indoorMinimapController.SetOverviewMode(minimapExpanded);
            }
            else
            {
                minimapController?.SetOverviewMode(minimapExpanded);
            }
        }

        private void RefreshStatus()
        {
            if (statusLabel == null || gpsLabel == null || destinationSummary == null)
                return;

            if (vpsTransition != null
                && vpsTransition.State != B9VpsTransitionController.TransitionState.WaitingForEntrance)
            {
                RefreshVpsStatus();
                return;
            }

            if (retryVpsButton != null)
                retryVpsButton.style.display = DisplayStyle.None;
            if (outdoorStartButton != null)
                outdoorStartButton.style.display = DisplayStyle.Flex;

            if (locationProvider == null || !locationProvider.HasReliableFix)
            {
                statusLabel.text = GetLocationMessage();
                gpsLabel.text = "SchoolGround · chưa có vị trí ổn định";
            }
            else if (routeController == null)
            {
                statusLabel.text = "Chưa có bộ điều khiển tuyến đường";
            }
            else
            {
                statusLabel.text = GetRouteMessage(routeController.State);
                gpsLabel.text = $"GPS ±{locationProvider.HorizontalAccuracyMeters:0} m · "
                                + $"còn {routeController.RemainingDistanceMeters:0} m";
                destinationSummary.text = routeController.HasArrivedAtEntrance
                    ? $"Đã tới cửa B9 · đích sau VPS: {routeController.SelectedRoomId}"
                    : $"Đang đi tới cửa B9 trước · đích cuối: {routeController.SelectedRoomId}";
            }
        }

        private void RefreshVpsStatus()
        {
            bool failed = vpsTransition.State == B9VpsTransitionController.TransitionState.Failed;
            if (retryVpsButton != null)
                retryVpsButton.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;
            if (outdoorStartButton != null)
                outdoorStartButton.style.display = DisplayStyle.None;

            string roomId = vpsTransition.State == B9VpsTransitionController.TransitionState.IndoorLocalized
                            && indoorRouteController != null
                            && !string.IsNullOrWhiteSpace(indoorRouteController.DestinationRoomId)
                ? indoorRouteController.DestinationRoomId
                : routeController != null && !string.IsNullOrWhiteSpace(routeController.SelectedRoomId)
                    ? routeController.SelectedRoomId
                    : "phòng đã chọn";

            switch (vpsTransition.State)
            {
                case B9VpsTransitionController.TransitionState.StartingVps:
                    statusLabel.text = "Đã tới B9 · đang bật VPS…";
                    gpsLabel.text = "B9 · chuẩn bị camera";
                    destinationSummary.text = $"VPS tự động → sau đó dẫn tới {roomId}";
                    break;
                case B9VpsTransitionController.TransitionState.Scanning:
                    statusLabel.text = "Đang quét VPS B9…";
                    gpsLabel.text = "Lia chậm điện thoại quanh sảnh và giữ camera ổn định";
                    destinationSummary.text = $"Đang xác định cửa vào · đích cuối: {roomId}";
                    break;
                case B9VpsTransitionController.TransitionState.IndoorLocalized:
                    indoorMinimapController?.SetOverviewMode(minimapExpanded);
                    RefreshIndoorStatus(roomId);
                    break;
                case B9VpsTransitionController.TransitionState.Failed:
                    statusLabel.text = "Quét VPS chưa thành công";
                    gpsLabel.text = string.IsNullOrWhiteSpace(vpsTransition.FailureReason)
                        ? "Hãy hướng camera quanh sảnh rồi quét lại"
                        : vpsTransition.FailureReason;
                    destinationSummary.text = $"Giữ nguyên đích {roomId} · nhấn Quét lại VPS";
                    break;
            }
        }

        private void RefreshIndoorStatus(string roomId)
        {
            if (indoorRouteController == null)
            {
                statusLabel.text = "Đã định vị bên trong tòa B9";
                gpsLabel.text = "B9 · VPS đã căn chỉnh";
                destinationSummary.text = $"Đã vào B9 · đang chuẩn bị đường tới {roomId}";
                return;
            }

            string floor = string.IsNullOrWhiteSpace(indoorRouteController.DestinationFloorId)
                ? string.Empty
                : $" · {indoorRouteController.DestinationFloorId}";
            switch (indoorRouteController.State)
            {
                case B9IndoorRouteController.IndoorRouteState.Calculating:
                    statusLabel.text = $"Đang tính đường tới {roomId}…";
                    gpsLabel.text = $"Trong B9{floor} · VPS đã căn chỉnh";
                    destinationSummary.text = $"Đích trong tòa: {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.Navigating:
                    statusLabel.text = $"Đi theo mũi tên tới {roomId}";
                    gpsLabel.text = $"Trong B9{floor} · còn {indoorRouteController.RemainingDistanceMeters:0.0} m";
                    destinationSummary.text = $"Đang dẫn đường bên trong tới {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.Arrived:
                    statusLabel.text = $"Đã đến {roomId}";
                    gpsLabel.text = $"Trong B9{floor} · đã tới điểm đích";
                    destinationSummary.text = $"Hoàn thành tuyến tới {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.RouteUnavailable:
                    statusLabel.text = $"Chưa tìm được đường tới {roomId}";
                    gpsLabel.text = $"Trong B9{floor} · hãy đứng gần lối đi";
                    destinationSummary.text = "NavMesh chưa bắt được vị trí hiện tại";
                    break;
                default:
                    statusLabel.text = "Đã định vị bên trong tòa B9";
                    gpsLabel.text = $"Trong B9{floor} · đang chuẩn bị tuyến";
                    destinationSummary.text = $"Đích trong tòa: {roomId}";
                    break;
            }
        }

        private string GetLocationMessage()
        {
            if (locationProvider == null) return "Không tìm thấy bộ GPS";
            return locationProvider.State switch
            {
                B9OutdoorLocationProvider.LocationState.Initializing => "Đang lấy tín hiệu GPS…",
                B9OutdoorLocationProvider.LocationState.PermissionDenied => "Cần quyền vị trí để dẫn đường",
                B9OutdoorLocationProvider.LocationState.PoorAccuracy => "GPS yếu, đang chờ tín hiệu tốt hơn…",
                B9OutdoorLocationProvider.LocationState.TimedOut => "GPS phản hồi quá lâu",
                _ => "Chưa nhận được vị trí GPS",
            };
        }

        private static string GetRouteMessage(B9OutdoorRouteController.RouteState state)
        {
            return state switch
            {
                B9OutdoorRouteController.RouteState.WaitingForGps => "Đang chờ GPS…",
                B9OutdoorRouteController.RouteState.Calculating => "Đang tính đường tới cửa B9…",
                B9OutdoorRouteController.RouteState.NavigatingToB9Entrance => "Đi theo mũi tên tới cửa B9",
                B9OutdoorRouteController.RouteState.ArrivedAtB9Entrance => "Đã đến cửa B9",
                B9OutdoorRouteController.RouteState.RouteUnavailable => "Không tìm được đường trên SchoolGround",
                _ => "Hãy chọn phòng cần đến",
            };
        }

        private static VisualElement CreatePanel(Color color, float radius)
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = color;
            panel.style.borderTopLeftRadius = radius;
            panel.style.borderTopRightRadius = radius;
            panel.style.borderBottomLeftRadius = radius;
            panel.style.borderBottomRightRadius = radius;
            return panel;
        }
    }
}
