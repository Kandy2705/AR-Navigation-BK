using System.Collections.Generic;
using System.IO;
using ARNavB9V2.Data;
using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Reliability;
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
        [SerializeField] private B9CampusDestinationCatalog campusDestinations;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9OutdoorRouteController routeController;
        [SerializeField] private B9OutdoorMinimapController minimapController;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [SerializeField] private B9IndoorRouteController indoorRouteController;
        [SerializeField] private B9IndoorMinimapController indoorMinimapController;
        [SerializeField] private B9IndoorPoseTracker indoorPoseTracker;
        [SerializeField] private B9ExperimentLogger experimentLogger;
        [SerializeField] private B9ExperimentLogExporter logExporter;
        [SerializeField] private B9ReliableNavigationController reliabilityController;
        [SerializeField] private B9HarmonyExperimentController harmonyExperiment;

        private Label statusLabel;
        private Label gpsLabel;
        private Label harmonyProfileLabel;
        private Label destinationSummary;
        private DropdownField destinationDropdown;
        private VisualElement minimapFrame;
        private VisualElement minimapView;
        private Label minimapHint;
        private Button outdoorStartButton;
        private Button cancelNavigationButton;
        private Button exitBuildingButton;
        private Button retryVpsButton;
        private Label experimentLabel;
        private Button experimentToggleButton;
        private Button experimentMarkerButton;
        private Button logExportButton;
        private bool minimapExpanded;
        private float nextRefresh;
        private readonly List<Button> harmonyVersionButtons = new List<Button>();
        private readonly Dictionary<string, string> destinationChoiceValues =
            new Dictionary<string, string>();

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

        public void AttachResearchTools(
            B9IndoorPoseTracker poseTracker,
            B9ExperimentLogger logger)
        {
            indoorPoseTracker = poseTracker;
            experimentLogger = logger;
            RefreshStatus();
        }

        public void AttachReliability(B9ReliableNavigationController controller)
        {
            reliabilityController = controller;
            RefreshStatus();
        }

        public void AttachLogExporter(B9ExperimentLogExporter exporter)
        {
            logExporter = exporter;
            RefreshStatus();
        }

        public void AttachCampusDestinations(B9CampusDestinationCatalog catalog)
        {
            campusDestinations = catalog;
        }

        public void AttachHarmonyExperiment(B9HarmonyExperimentController controller)
        {
            harmonyExperiment = controller;
            RefreshHarmonySelector();
        }

        private void OnEnable()
        {
            BuildInterface();
        }

        private void Start()
        {
            BuildInterface();
            if (destinationDropdown != null)
                ApplyDestinationChoice(destinationDropdown.value);
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

            harmonyProfileLabel = new Label("HARMONY V5 · Full HARMONY");
            harmonyProfileLabel.style.color = new Color(0.72f, 0.88f, 1f, 1f);
            harmonyProfileLabel.style.fontSize = 16f;
            harmonyProfileLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            harmonyProfileLabel.style.marginTop = 10f;
            statusPanel.Add(harmonyProfileLabel);

            VisualElement versionRow = new VisualElement();
            versionRow.style.flexDirection = FlexDirection.Row;
            versionRow.style.marginTop = 7f;
            statusPanel.Add(versionRow);
            harmonyVersionButtons.Clear();
            B9HarmonyVersion[] versions =
            {
                B9HarmonyVersion.V1_FixedGeometric,
                B9HarmonyVersion.V2_ReliableHandover,
                B9HarmonyVersion.V3_NoDwellTime,
                B9HarmonyVersion.V4_NoMapIdCheck,
                B9HarmonyVersion.V5_FullHarmony,
                B9HarmonyVersion.BQ_QualityThreshold,
                B9HarmonyVersion.BT_QualityDwell,
            };
            for (int i = 0; i < versions.Length; i++)
            {
                B9HarmonyVersion version = versions[i];
                string buttonLabel = B9HarmonyExperimentProfile.For(version).VersionCode;
                var versionButton = new Button(() =>
                {
                    harmonyExperiment?.SelectVersion(version);
                    RefreshHarmonySelector();
                })
                {
                    text = buttonLabel
                };
                versionButton.style.flexGrow = 1f;
                versionButton.style.height = 40f;
                versionButton.style.marginRight = i < versions.Length - 1 ? 5f : 0f;
                versionButton.style.color = Color.white;
                versionButton.style.fontSize = 17f;
                versionButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                versionRow.Add(versionButton);
                harmonyVersionButtons.Add(versionButton);
            }

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

            Label title = new Label("ĐIỂM ĐẾN · KHUÔN VIÊN / B9");
            title.style.color = new Color(0.38f, 0.8f, 1f, 1f);
            title.style.fontSize = 21f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            destinationPanel.Add(title);

            List<string> choices = BuildDestinationChoices(out string initialChoice);
            destinationDropdown = new DropdownField("Chọn điểm đến", choices, initialChoice);
            destinationDropdown.style.marginTop = 10f;
            destinationDropdown.style.fontSize = 26f;
            destinationDropdown.RegisterValueChangedCallback(evt =>
            {
                ApplyDestinationChoice(evt.newValue);
                RefreshStatus();
            });
            destinationPanel.Add(destinationDropdown);

            outdoorStartButton = new Button(() =>
            {
                if (destinationDropdown != null)
                    ApplyDestinationChoice(destinationDropdown.value);
            })
            {
                text = "BẮT ĐẦU / ĐỔI ĐIỂM ĐẾN"
            };
            outdoorStartButton.style.height = 58f;
            outdoorStartButton.style.marginTop = 12f;
            outdoorStartButton.style.backgroundColor = new Color(0.02f, 0.42f, 0.94f, 1f);
            outdoorStartButton.style.color = Color.white;
            outdoorStartButton.style.fontSize = 23f;
            outdoorStartButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            destinationPanel.Add(outdoorStartButton);

            VisualElement navigationActions = new VisualElement();
            navigationActions.style.flexDirection = FlexDirection.Row;
            navigationActions.style.marginTop = 10f;
            destinationPanel.Add(navigationActions);

            cancelNavigationButton = new Button(() =>
            {
                if (reliabilityController != null)
                    reliabilityController.CancelNavigation();
                else
                {
                    routeController?.CancelNavigation();
                    indoorRouteController?.StopNavigation();
                }
                RefreshStatus();
            })
            {
                text = "HUỶ CHỈ ĐƯỜNG"
            };
            cancelNavigationButton.style.flexGrow = 1f;
            cancelNavigationButton.style.height = 50f;
            cancelNavigationButton.style.backgroundColor = new Color(0.42f, 0.12f, 0.14f, 1f);
            cancelNavigationButton.style.color = Color.white;
            cancelNavigationButton.style.fontSize = 18f;
            cancelNavigationButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            navigationActions.Add(cancelNavigationButton);

            exitBuildingButton = new Button(() =>
            {
                reliabilityController?.RequestExitToOutdoor();
                RefreshStatus();
            })
            {
                text = "DẪN RA NGOÀI"
            };
            exitBuildingButton.style.flexGrow = 1f;
            exitBuildingButton.style.height = 50f;
            exitBuildingButton.style.marginLeft = 10f;
            exitBuildingButton.style.backgroundColor = new Color(0.95f, 0.48f, 0.08f, 1f);
            exitBuildingButton.style.color = Color.white;
            exitBuildingButton.style.fontSize = 18f;
            exitBuildingButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            exitBuildingButton.style.display = DisplayStyle.None;
            navigationActions.Add(exitBuildingButton);

            destinationSummary = new Label("Ngoài trời → cửa B9 → phòng đã chọn");
            destinationSummary.style.color = new Color(0.78f, 0.86f, 0.95f, 1f);
            destinationSummary.style.fontSize = 19f;
            destinationSummary.style.marginTop = 10f;
            destinationPanel.Add(destinationSummary);

            VisualElement experimentRow = new VisualElement();
            experimentRow.style.flexDirection = FlexDirection.Row;
            experimentRow.style.alignItems = Align.Center;
            experimentRow.style.marginTop = 12f;
            destinationPanel.Add(experimentRow);

            experimentLabel = new Label("LOG · đang chuẩn bị");
            experimentLabel.style.flexGrow = 1f;
            experimentLabel.style.color = new Color(0.55f, 0.92f, 0.72f, 1f);
            experimentLabel.style.fontSize = 16f;
            experimentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            experimentRow.Add(experimentLabel);

            experimentMarkerButton = new Button(() =>
            {
                experimentLogger?.AddResearchMarker("manual_observation");
                RefreshExperimentStatus();
            })
            {
                text = "ĐÁNH DẤU"
            };
            experimentMarkerButton.style.height = 44f;
            experimentMarkerButton.style.marginLeft = 8f;
            experimentMarkerButton.style.backgroundColor = new Color(0.13f, 0.25f, 0.35f, 1f);
            experimentMarkerButton.style.color = Color.white;
            experimentMarkerButton.style.fontSize = 15f;
            experimentRow.Add(experimentMarkerButton);

            experimentToggleButton = new Button(() =>
            {
                experimentLogger?.ToggleTrial();
                RefreshExperimentStatus();
            })
            {
                text = "LƯU LẦN THỬ"
            };
            experimentToggleButton.style.height = 44f;
            experimentToggleButton.style.marginLeft = 8f;
            experimentToggleButton.style.backgroundColor = new Color(0.02f, 0.42f, 0.94f, 1f);
            experimentToggleButton.style.color = Color.white;
            experimentToggleButton.style.fontSize = 15f;
            experimentToggleButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            experimentRow.Add(experimentToggleButton);

            logExportButton = new Button(() =>
            {
                logExporter?.ExportLatestBundle();
                RefreshExperimentStatus();
            })
            {
                text = "XUẤT 3 CSV"
            };
            logExportButton.style.height = 44f;
            logExportButton.style.marginLeft = 8f;
            logExportButton.style.backgroundColor = new Color(0.12f, 0.52f, 0.33f, 1f);
            logExportButton.style.color = Color.white;
            logExportButton.style.fontSize = 15f;
            logExportButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            experimentRow.Add(logExportButton);

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

            RefreshExperimentStatus();
            RefreshHarmonySelector();
            RefreshActionButtons();

            if (reliabilityController != null
                && reliabilityController.PdrFallbackDestinationArrived)
            {
                RefreshPdrFallbackArrivalStatus();
                return;
            }

            if (reliabilityController != null
                && reliabilityController.State == B9NavigationState.ExitingWithPdr)
            {
                RefreshExitPdrStatus();
                return;
            }

            if (reliabilityController != null
                && reliabilityController.State == B9NavigationState.IndoorVps
                && reliabilityController.ExitRouteRequested)
            {
                RefreshIndoorExitStatus();
                return;
            }

            if (reliabilityController != null
                && (reliabilityController.State == B9NavigationState.EnteringWithPdr
                    || reliabilityController.State == B9NavigationState.VpsFailed))
            {
                RefreshTransitionPdrStatus();
                return;
            }

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
            else if (!routeController.HasDestination)
            {
                statusLabel.text = "Hãy chọn điểm đến";
                gpsLabel.text = $"GPS ±{locationProvider.HorizontalAccuracyMeters:0} m · sẵn sàng";
                destinationSummary.text = "Có thể chọn phòng B9 hoặc một tòa outdoor";
            }
            else
            {
                statusLabel.text = GetRouteMessage(routeController);
                gpsLabel.text = $"GPS ±{locationProvider.HorizontalAccuracyMeters:0} m · "
                                + $"còn {routeController.RemainingDistanceMeters:0} m";
                if (routeController.IsIndoorB9Destination)
                {
                    destinationSummary.text = routeController.HasArrivedAtEntrance
                        ? $"Đã tới cửa B9 · đích sau VPS: {routeController.SelectedRoomId}"
                        : $"Đang đi tới cửa B9 trước · đích cuối: {routeController.SelectedRoomId}";
                }
                else
                {
                    destinationSummary.text = routeController.HasArrivedAtDestination
                        ? "Đã tới " + routeController.SelectedDestinationName
                        : "Đang dẫn đường outdoor tới " + routeController.SelectedDestinationName;
                }
            }
        }

        private void RefreshTransitionPdrStatus()
        {
            bool failed = reliabilityController.State == B9NavigationState.VpsFailed;
            if (retryVpsButton != null)
                retryVpsButton.style.display = failed && vpsTransition != null
                                               && vpsTransition.RetryAvailable
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (outdoorStartButton != null)
                outdoorStartButton.style.display = DisplayStyle.None;

            string roomId = routeController != null
                            && !string.IsNullOrWhiteSpace(routeController.SelectedRoomId)
                ? routeController.SelectedRoomId
                : "phòng đã chọn";
            if (failed)
            {
                statusLabel.text = "Quét VPS chưa thành công";
                gpsLabel.text = string.IsNullOrWhiteSpace(vpsTransition?.FailureReason)
                    ? "Giữ camera hướng vào hành lang rồi quét lại"
                    : vpsTransition.FailureReason;
                destinationSummary.text = $"PDR vẫn giữ vị trí · đích cuối: {roomId}";
                return;
            }

            statusLabel.text = "Đang đi vào vùng quét B9";
            gpsLabel.text = $"PDR · {reliabilityController.TransitionRemainingDistanceMeters:0.0} m tới {roomId}";
            destinationSummary.text = "Đi theo đường liền mạch · VPS chỉ bật khi vào đúng vùng scan";
        }

        private void RefreshPdrFallbackArrivalStatus()
        {
            if (retryVpsButton != null)
                retryVpsButton.style.display = DisplayStyle.None;
            if (outdoorStartButton != null)
                outdoorStartButton.style.display = DisplayStyle.Flex;

            string roomId = indoorRouteController != null
                            && !string.IsNullOrWhiteSpace(indoorRouteController.DestinationRoomId)
                ? indoorRouteController.DestinationRoomId
                : "điểm đích";
            statusLabel.text = $"Đã đến {roomId}";
            gpsLabel.text = "PDR đã tới điểm đích · đã dừng quét VPS";
            destinationSummary.text = "Hoàn thành chỉ đường · có thể chọn điểm đến tiếp theo";
        }

        private void RefreshIndoorExitStatus()
        {
            if (retryVpsButton != null)
                retryVpsButton.style.display = DisplayStyle.None;
            if (outdoorStartButton != null)
                outdoorStartButton.style.display = DisplayStyle.Flex;
            statusLabel.text = "Đi theo mũi tên tới cửa ra gần nhất";
            gpsLabel.text = indoorRouteController != null
                ? $"Trong B9 · còn {indoorRouteController.RemainingDistanceMeters:0.0} m"
                  + GetIndoorTrackingSuffix()
                : "Trong B9 · đang tính đường tới cửa ra";
            destinationSummary.text = string.IsNullOrWhiteSpace(reliabilityController.ActiveExitName)
                ? "Đích: cửa ra gần nhất"
                : "Đích: " + reliabilityController.ActiveExitName;
        }

        private void RefreshExitPdrStatus()
        {
            if (retryVpsButton != null)
                retryVpsButton.style.display = DisplayStyle.None;
            if (outdoorStartButton != null)
                outdoorStartButton.style.display = DisplayStyle.None;
            statusLabel.text = "Đã ra khỏi B9 · đang bắt lại GPS";
            gpsLabel.text = $"PDR đang giữ vị trí · GPS ổn định "
                            + $"{reliabilityController.StableExitGpsSamples}/"
                            + reliabilityController.RequiredStableExitGpsSamples;
            destinationSummary.text = "Tiếp tục đi tự nhiên · app sẽ tự chuyển về GPS khi tín hiệu ổn định";
        }

        private void RefreshVpsStatus()
        {
            bool failed = vpsTransition.State == B9VpsTransitionController.TransitionState.Failed;
            if (retryVpsButton != null)
                retryVpsButton.style.display = failed && vpsTransition.RetryAvailable
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
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
            bool approximate = vpsTransition != null
                               && vpsTransition.IsApproximatePdrLocalization;
            string trackingLabel = approximate
                ? "PDR gần đúng · VPS quá 30 giây"
                : "VPS đã căn chỉnh";
            if (indoorRouteController == null)
            {
                statusLabel.text = "Đã định vị bên trong tòa B9";
                gpsLabel.text = "B9 · " + trackingLabel;
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
                    gpsLabel.text = $"Trong B9{floor} · {trackingLabel}";
                    destinationSummary.text = $"Đích trong tòa: {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.Navigating:
                    statusLabel.text = $"Đi theo mũi tên tới {roomId}";
                    gpsLabel.text = $"Trong B9{floor} · còn {indoorRouteController.RemainingDistanceMeters:0.0} m"
                                    + $" · {trackingLabel}"
                                    + GetIndoorTrackingSuffix();
                    destinationSummary.text = $"Đang dẫn đường bên trong tới {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.Arrived:
                    statusLabel.text = $"Đã đến {roomId}";
                    gpsLabel.text = $"Trong B9{floor} · đã tới điểm đích · {trackingLabel}";
                    destinationSummary.text = $"Hoàn thành tuyến tới {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.RouteUnavailable:
                    statusLabel.text = $"Chưa tìm được đường tới {roomId}";
                    gpsLabel.text = $"Trong B9{floor} · đang bắt lại lối đi"
                                    + GetIndoorTrackingSuffix();
                    destinationSummary.text = "NavMesh chưa bắt được vị trí hiện tại";
                    break;
                default:
                    statusLabel.text = "Đã định vị bên trong tòa B9";
                    gpsLabel.text = $"Trong B9{floor} · đang chuẩn bị tuyến · {trackingLabel}";
                    destinationSummary.text = $"Đích trong tòa: {roomId}";
                    break;
            }
        }

        private string GetIndoorTrackingSuffix()
        {
            if (indoorPoseTracker == null || !indoorPoseTracker.IsTracking)
                return string.Empty;
            return $" · {indoorPoseTracker.StepCount} bước · {indoorPoseTracker.SourceLabel}";
        }

        private void RefreshExperimentStatus()
        {
            if (experimentLabel == null || experimentToggleButton == null)
                return;
            if (experimentLogger == null)
            {
                experimentLabel.text = "LOG · chưa kết nối";
                experimentToggleButton.SetEnabled(false);
                if (experimentMarkerButton != null)
                    experimentMarkerButton.SetEnabled(false);
                return;
            }

            if (logExportButton != null)
                logExportButton.SetEnabled(logExporter != null);

            experimentToggleButton.SetEnabled(true);
            if (experimentMarkerButton != null)
                experimentMarkerButton.SetEnabled(experimentLogger.IsRecording);
            if (experimentLogger.IsRecording)
            {
                int elapsed = Mathf.FloorToInt(experimentLogger.ElapsedSeconds);
                experimentLabel.text = $"LOG · ĐANG GHI {elapsed / 60:00}:{elapsed % 60:00}"
                                       + $" · {experimentLogger.SampleCount} mẫu";
                experimentToggleButton.text = "LƯU LẦN THỬ";
            }
            else
            {
                string filename = string.IsNullOrWhiteSpace(experimentLogger.LastSavedFilePath)
                    ? "chưa có tệp"
                    : Path.GetFileName(experimentLogger.LastSavedFilePath);
                experimentLabel.text = "LOG · ĐÃ LƯU " + filename;
                experimentToggleButton.text = "BẮT ĐẦU LẦN MỚI";
            }

            if (logExporter != null && !string.IsNullOrWhiteSpace(logExporter.LastMessage))
                experimentLabel.text = "LOG · " + logExporter.LastMessage;
        }

        private void RefreshHarmonySelector()
        {
            if (harmonyProfileLabel == null)
                return;

            B9HarmonyExperimentProfile profile = harmonyExperiment != null
                ? harmonyExperiment.ActiveProfile
                : B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony);
            harmonyProfileLabel.text = $"HARMONY {profile.VersionCode} · {profile.DisplayName}  "
                                       + $"Q{Mark(profile.QualityThreshold)} "
                                       + $"D{Mark(profile.TemporalDwell)} "
                                       + $"M{Mark(profile.MapIdCheck)} "
                                       + $"R{Mark(profile.RecoveryFsm)} "
                                       + $"A{Mark(profile.AdaptiveGuidance)}";

            for (int i = 0; i < harmonyVersionButtons.Count; i++)
            {
                string buttonCode = harmonyVersionButtons[i].text;
                bool selected = string.Equals(
                    buttonCode,
                    profile.VersionCode,
                    System.StringComparison.Ordinal);
                harmonyVersionButtons[i].style.backgroundColor = selected
                    ? new Color(0.02f, 0.42f, 0.94f, 1f)
                    : new Color(0.12f, 0.2f, 0.29f, 1f);
            }
        }

        private static string Mark(bool enabled) => enabled ? "✓" : "–";

        private void ApplyDestinationChoice(string choice)
        {
            if (string.IsNullOrWhiteSpace(choice)
                || !destinationChoiceValues.TryGetValue(choice, out string target))
                return;

            if (target.StartsWith("outdoor:", System.StringComparison.Ordinal))
            {
                string destinationId = target.Substring("outdoor:".Length);
                if (reliabilityController != null)
                    reliabilityController.NavigateToOutdoorDestination(destinationId);
                else
                    routeController?.SetOutdoorDestination(destinationId);
            }
            else
            {
                string roomId = target.Substring("room:".Length);
                if (reliabilityController != null)
                    reliabilityController.NavigateToIndoorRoom(roomId);
                else
                {
                    routeController?.SetDestinationRoom(roomId);
                    indoorRouteController?.SetDestinationRoom(roomId);
                }
            }
        }

        private List<string> BuildDestinationChoices(out string initialChoice)
        {
            destinationChoiceValues.Clear();
            var choices = new List<string>();
            initialChoice = string.Empty;

            foreach (B9BuildingDefinition.RoomDefinition room in building.Rooms)
            {
                string label = "[Trong] B9 · " + room.RoomId;
                choices.Add(label);
                destinationChoiceValues[label] = "room:" + room.RoomId;
                if (room.RoomId == "B9-104")
                    initialChoice = label;
            }

            if (campusDestinations != null)
            {
                foreach (B9CampusDestinationCatalog.Destination destination
                         in campusDestinations.Destinations)
                {
                    if (destination == null || destination.IndoorNavigationAvailable)
                        continue;
                    string label = "[Ngoài] " + destination.DisplayName;
                    choices.Add(label);
                    destinationChoiceValues[label] = "outdoor:" + destination.Id;
                }
            }

            if (string.IsNullOrWhiteSpace(initialChoice) && choices.Count > 0)
                initialChoice = choices[0];
            return choices;
        }

        private void RefreshActionButtons()
        {
            if (cancelNavigationButton == null || exitBuildingButton == null)
                return;

            B9NavigationState state = reliabilityController != null
                ? reliabilityController.State
                : B9NavigationState.OutdoorGps;
            bool indoorLocalized = state == B9NavigationState.IndoorVps;
            bool activeOutdoorRoute = routeController != null
                                      && routeController.State != B9OutdoorRouteController.RouteState.NoDestination;
            bool activeIndoorRoute = indoorRouteController != null
                                     && indoorRouteController.NavigationActive;
            bool transitioning = state != B9NavigationState.OutdoorGps
                                 && state != B9NavigationState.IndoorVps
                                 && (reliabilityController == null
                                     || !reliabilityController.PdrFallbackDestinationArrived);
            cancelNavigationButton.style.display = activeOutdoorRoute || activeIndoorRoute || transitioning
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            exitBuildingButton.style.display = indoorLocalized
                                                && !reliabilityController.ExitRouteRequested
                ? DisplayStyle.Flex
                : DisplayStyle.None;
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

        private static string GetRouteMessage(B9OutdoorRouteController route)
        {
            string destination = route != null ? route.SelectedDestinationName : "điểm đến";
            return route != null ? route.State switch
            {
                B9OutdoorRouteController.RouteState.WaitingForGps => "Đang chờ GPS…",
                B9OutdoorRouteController.RouteState.Calculating => "Đang tính đường tới " + destination + "…",
                B9OutdoorRouteController.RouteState.NavigatingToB9Entrance => "Đi theo mũi tên tới " + destination,
                B9OutdoorRouteController.RouteState.ArrivedAtB9Entrance => "Đã đến " + destination,
                B9OutdoorRouteController.RouteState.RouteUnavailable => "Không tìm được đường trên SchoolGround",
                _ => "Hãy chọn phòng cần đến",
            } : "Chưa có tuyến đường";
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
