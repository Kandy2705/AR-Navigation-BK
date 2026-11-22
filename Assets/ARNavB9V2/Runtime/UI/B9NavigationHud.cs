using System;
using System.Collections.Generic;
using System.IO;
using ARNavB9V2.Data;
using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Reliability;
using ARNavB9V2.Scene;
using ARNavB9V2.Vps;
using UnityEngine;

namespace ARNavB9V2.UI
{
    /// <summary>
    /// The navigation HUD's logic: it reads the GPS, VPS, PDR, indoor route and research controllers five times a
    /// second and turns them into a <see cref="B9NavigationHudState"/> (texts and which buttons show), and runs the
    /// command behind each button. It draws nothing: the Canvas prefab NavigationHudUI renders it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9NavigationHud : MonoBehaviour, IB9NavigationHudModel
    {
        private const string OutdoorPrefix = "outdoor:";
        private const string RoomPrefix = "room:";

        private static readonly B9HarmonyVersion[] HarmonyVersions =
        {
            B9HarmonyVersion.V1_FixedGeometric,
            B9HarmonyVersion.V2_ReliableHandover,
            B9HarmonyVersion.V3_NoDwellTime,
            B9HarmonyVersion.V4_NoMapIdCheck,
            B9HarmonyVersion.V5_FullHarmony,
            B9HarmonyVersion.BQ_QualityThreshold,
            B9HarmonyVersion.BT_QualityDwell,
            B9HarmonyVersion.TF_TunedFixed,
            B9HarmonyVersion.V6_AdaptivePolicy,
        };

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
        [SerializeField] private B10BuildingAuthoring b10Building;

        private readonly B9NavigationHudState state = new B9NavigationHudState();
        private readonly List<string> destinationChoices = new List<string>();
        private readonly Dictionary<string, string> destinationChoiceValues =
            new Dictionary<string, string>();
        private readonly List<string> harmonyVersionCodes = new List<string>();
        private string selectedChoice = string.Empty;
        private bool built;
        private float nextRefresh;

        public event Action Changed;

        public B9NavigationHudState State => state;

        public IReadOnlyList<string> DestinationChoices => destinationChoices;

        public string SelectedDestination => selectedChoice;

        public IReadOnlyList<string> HarmonyVersionCodes
        {
            get
            {
                if (harmonyVersionCodes.Count == 0)
                {
                    foreach (B9HarmonyVersion version in HarmonyVersions)
                        harmonyVersionCodes.Add(B9HarmonyExperimentProfile.For(version).VersionCode);
                }

                return harmonyVersionCodes;
            }
        }

        public Texture MinimapTexture =>
            minimapController != null ? minimapController.RenderedTexture : null;

        public void Configure(
            B9BuildingDefinition buildingDefinition,
            B9OutdoorLocationProvider provider,
            B9OutdoorRouteController route,
            B9OutdoorMinimapController minimap)
        {
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
            if (built && !string.IsNullOrWhiteSpace(selectedChoice))
                indoorRouteController?.SetDestinationRoom(selectedChoice);
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

        // ------------------------------------------------------------------ commands (one per HUD button)

        public void SelectDestination(string choice)
        {
            if (string.IsNullOrWhiteSpace(choice)
                || string.Equals(choice, selectedChoice, StringComparison.Ordinal)
                || !destinationChoiceValues.ContainsKey(choice))
                return;

            selectedChoice = choice;
            ApplyDestinationChoice(choice);
            RefreshStatus();
            NotifyChanged();
        }

        public void StartNavigation()
        {
            ApplyDestinationChoice(selectedChoice);
            NotifyChanged();
        }

        public void CancelNavigation()
        {
            if (reliabilityController != null)
                reliabilityController.CancelNavigation();
            else
            {
                routeController?.CancelNavigation();
                indoorRouteController?.StopNavigation();
            }

            state.ActiveDestination = string.Empty;
            RefreshStatus();
            NotifyChanged();
        }

        public void ExitBuilding()
        {
            reliabilityController?.RequestExitToOutdoor();
            RefreshStatus();
            NotifyChanged();
        }

        public void RetryVps()
        {
            vpsTransition?.RetryLocalization();
            NotifyChanged();
        }

        public void ToggleMinimap()
        {
            state.MinimapExpanded = !state.MinimapExpanded;
            ApplyMinimapPresentation();
            NotifyChanged();
        }

        public void SelectHarmonyVersion(string versionCode)
        {
            foreach (B9HarmonyVersion version in HarmonyVersions)
            {
                if (!string.Equals(B9HarmonyExperimentProfile.For(version).VersionCode, versionCode, StringComparison.Ordinal))
                    continue;

                harmonyExperiment?.SelectVersion(version);
                break;
            }

            RefreshHarmonySelector();
            NotifyChanged();
        }

        public void AddResearchMarker()
        {
            experimentLogger?.AddResearchMarker("manual_observation");
            RefreshExperimentStatus();
            NotifyChanged();
        }

        public void ToggleTrial()
        {
            experimentLogger?.ToggleTrial();
            RefreshExperimentStatus();
            NotifyChanged();
        }

        public void ExportLogs()
        {
            logExporter?.ExportLatestBundle();
            RefreshExperimentStatus();
            NotifyChanged();
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            if (b10Building == null)
                b10Building = FindFirstObjectByType<B10BuildingAuthoring>(FindObjectsInactive.Include);
            BuildInterface();
        }

        private void Start()
        {
            BuildInterface();
            if (built)
                ApplyDestinationChoice(selectedChoice);
            NotifyChanged();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + 0.2f;
            RefreshStatus();
            NotifyChanged();
        }

        /// <summary>Fills the destination list, selects the default (B9-104) and computes the first state.</summary>
        private void BuildInterface()
        {
            if (building == null)
                return;

            built = true;
            selectedChoice = BuildDestinationChoices();
            ApplyMinimapPresentation();
            RefreshStatus();
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private void ApplyMinimapPresentation()
        {
            if (!built)
                return;

            state.MinimapHint = state.MinimapExpanded
                ? B9NavigationHudState.ExpandedMinimapHint
                : B9NavigationHudState.CollapsedMinimapHint;
            if (vpsTransition != null
                && vpsTransition.State == B9VpsTransitionController.TransitionState.IndoorLocalized
                && indoorMinimapController != null)
            {
                indoorMinimapController.SetOverviewMode(state.MinimapExpanded);
            }
            else
            {
                minimapController?.SetOverviewMode(state.MinimapExpanded);
            }
        }

        // ------------------------------------------------------------------ state (unchanged wording and rules)

        private void RefreshStatus()
        {
            if (!built)
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

            state.RetryVisible = false;
            state.StartVisible = true;

            if (locationProvider == null || !locationProvider.HasReliableFix)
            {
                state.Status = GetLocationMessage();
                state.Detail = "SchoolGround · chưa có vị trí ổn định";
            }
            else if (routeController == null)
            {
                state.Status = "Chưa có bộ điều khiển tuyến đường";
            }
            else if (!routeController.HasDestination)
            {
                state.Status = "Hãy chọn điểm đến";
                state.Detail = $"GPS ±{locationProvider.HorizontalAccuracyMeters:0} m · sẵn sàng";
                state.Summary = "Có thể chọn phòng B9 hoặc một tòa outdoor";
            }
            else
            {
                state.Status = GetRouteMessage(routeController);
                state.Detail = $"GPS ±{locationProvider.HorizontalAccuracyMeters:0} m · "
                               + $"còn {routeController.RemainingDistanceMeters:0} m";
                if (routeController.IsIndoorB9Destination)
                {
                    state.Summary = routeController.HasArrivedAtEntrance
                        ? $"Đã tới cửa B9 · đích sau VPS: {routeController.SelectedRoomId}"
                        : $"Đang đi tới cửa B9 trước · đích cuối: {routeController.SelectedRoomId}";
                }
                else
                {
                    state.Summary = routeController.HasArrivedAtDestination
                        ? "Đã tới " + routeController.SelectedDestinationName
                        : "Đang dẫn đường outdoor tới " + routeController.SelectedDestinationName;
                }
            }
        }

        private void RefreshTransitionPdrStatus()
        {
            bool failed = reliabilityController.State == B9NavigationState.VpsFailed;
            state.RetryVisible = failed && vpsTransition != null && vpsTransition.RetryAvailable;
            state.StartVisible = false;

            string roomId = routeController != null
                            && !string.IsNullOrWhiteSpace(routeController.SelectedRoomId)
                ? routeController.SelectedRoomId
                : "phòng đã chọn";
            if (failed)
            {
                state.Status = "Quét VPS chưa thành công";
                state.Detail = string.IsNullOrWhiteSpace(vpsTransition?.FailureReason)
                    ? "Giữ camera hướng vào hành lang rồi quét lại"
                    : vpsTransition.FailureReason;
                state.Summary = $"PDR vẫn giữ vị trí · đích cuối: {roomId}";
                return;
            }

            string activeBuilding = GetActiveBuildingLabel();
            state.Status = $"Đang đi vào vùng quét {activeBuilding}";
            state.Detail = $"PDR · {reliabilityController.TransitionRemainingDistanceMeters:0.0} m tới {roomId}";
            state.Summary = "Đi theo đường liền mạch · VPS chỉ bật khi vào đúng vùng scan";
        }

        private void RefreshPdrFallbackArrivalStatus()
        {
            state.RetryVisible = false;
            state.StartVisible = true;

            string roomId = indoorRouteController != null
                            && !string.IsNullOrWhiteSpace(indoorRouteController.DestinationRoomId)
                ? indoorRouteController.DestinationRoomId
                : "điểm đích";
            state.Status = $"Đã đến {roomId}";
            state.Detail = "PDR đã tới điểm đích · đã dừng quét VPS";
            state.Summary = "Hoàn thành chỉ đường · có thể chọn điểm đến tiếp theo";
        }

        private void RefreshIndoorExitStatus()
        {
            state.RetryVisible = false;
            state.StartVisible = true;
            string activeBuilding = GetActiveBuildingLabel();
            state.Status = "Đi theo mũi tên tới cửa ra gần nhất";
            state.Detail = indoorRouteController != null
                ? $"Trong {activeBuilding} · còn {indoorRouteController.RemainingDistanceMeters:0.0} m"
                  + GetIndoorTrackingSuffix()
                : $"Trong {activeBuilding} · đang tính đường tới cửa ra";
            state.Summary = string.IsNullOrWhiteSpace(reliabilityController.ActiveExitName)
                ? "Đích: cửa ra gần nhất"
                : "Đích: " + reliabilityController.ActiveExitName;
        }

        private void RefreshExitPdrStatus()
        {
            state.RetryVisible = false;
            state.StartVisible = false;
            state.Status = $"Đã ra khỏi {GetActiveBuildingLabel()} · đang bắt lại GPS";
            state.Detail = $"PDR đang giữ vị trí · GPS ổn định "
                           + $"{reliabilityController.StableExitGpsSamples}/"
                           + reliabilityController.RequiredStableExitGpsSamples;
            state.Summary = "Tiếp tục đi tự nhiên · app sẽ tự chuyển về GPS khi tín hiệu ổn định";
        }

        private void RefreshVpsStatus()
        {
            bool failed = vpsTransition.State == B9VpsTransitionController.TransitionState.Failed;
            state.RetryVisible = failed && vpsTransition.RetryAvailable;
            state.StartVisible = false;

            string roomId = vpsTransition.State == B9VpsTransitionController.TransitionState.IndoorLocalized
                            && indoorRouteController != null
                            && !string.IsNullOrWhiteSpace(indoorRouteController.DestinationRoomId)
                ? indoorRouteController.DestinationRoomId
                : routeController != null && !string.IsNullOrWhiteSpace(routeController.SelectedRoomId)
                    ? routeController.SelectedRoomId
                    : "phòng đã chọn";
            string activeBuilding = GetActiveBuildingLabel();

            switch (vpsTransition.State)
            {
                case B9VpsTransitionController.TransitionState.StartingVps:
                    state.Status = $"Đã tới {activeBuilding} · đang bật VPS…";
                    state.Detail = $"{activeBuilding} · chuẩn bị camera";
                    state.Summary = $"VPS tự động → sau đó dẫn tới {roomId}";
                    break;
                case B9VpsTransitionController.TransitionState.Scanning:
                    state.Status = $"Đang quét VPS {activeBuilding}…";
                    state.Detail = "Lia chậm điện thoại quanh sảnh và giữ camera ổn định";
                    state.Summary = $"Đang xác định cửa vào · đích cuối: {roomId}";
                    break;
                case B9VpsTransitionController.TransitionState.IndoorLocalized:
                    indoorMinimapController?.SetOverviewMode(state.MinimapExpanded);
                    RefreshIndoorStatus(roomId);
                    break;
                case B9VpsTransitionController.TransitionState.Failed:
                    state.Status = "Quét VPS chưa thành công";
                    state.Detail = string.IsNullOrWhiteSpace(vpsTransition.FailureReason)
                        ? "Hãy hướng camera quanh sảnh rồi quét lại"
                        : vpsTransition.FailureReason;
                    state.Summary = $"Giữ nguyên đích {roomId} · nhấn Quét lại VPS";
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
            string activeBuilding = GetActiveBuildingLabel();
            if (indoorRouteController == null)
            {
                state.Status = $"Đã định vị bên trong tòa {activeBuilding}";
                state.Detail = $"{activeBuilding} · {trackingLabel}";
                state.Summary = $"Đã vào {activeBuilding} · đang chuẩn bị đường tới {roomId}";
                return;
            }

            string floor = string.IsNullOrWhiteSpace(indoorRouteController.DestinationFloorId)
                ? string.Empty
                : $" · {indoorRouteController.DestinationFloorId}";
            switch (indoorRouteController.State)
            {
                case B9IndoorRouteController.IndoorRouteState.Calculating:
                    state.Status = $"Đang tính đường tới {roomId}…";
                    state.Detail = $"Trong {activeBuilding}{floor} · {trackingLabel}";
                    state.Summary = $"Đích trong tòa: {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.Navigating:
                    state.Status = $"Đi theo mũi tên tới {roomId}";
                    state.Detail = $"Trong {activeBuilding}{floor} · còn {indoorRouteController.RemainingDistanceMeters:0.0} m"
                                   + $" · {trackingLabel}"
                                   + GetIndoorTrackingSuffix();
                    state.Summary = $"Đang dẫn đường bên trong tới {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.Arrived:
                    state.Status = $"Đã đến {roomId}";
                    state.Detail = $"Trong {activeBuilding}{floor} · đã tới điểm đích · {trackingLabel}";
                    state.Summary = $"Hoàn thành tuyến tới {roomId}";
                    break;
                case B9IndoorRouteController.IndoorRouteState.RouteUnavailable:
                    state.Status = $"Chưa tìm được đường tới {roomId}";
                    state.Detail = $"Trong {activeBuilding}{floor} · đang bắt lại lối đi"
                                   + GetIndoorTrackingSuffix();
                    state.Summary = "NavMesh chưa bắt được vị trí hiện tại";
                    break;
                default:
                    state.Status = $"Đã định vị bên trong tòa {activeBuilding}";
                    state.Detail = $"Trong {activeBuilding}{floor} · đang chuẩn bị tuyến · {trackingLabel}";
                    state.Summary = $"Đích trong tòa: {roomId}";
                    break;
            }
        }

        private string GetIndoorTrackingSuffix()
        {
            if (indoorPoseTracker == null || !indoorPoseTracker.IsTracking)
                return string.Empty;
            return $" · {indoorPoseTracker.StepCount} bước · {indoorPoseTracker.SourceLabel}";
        }

        private string GetActiveBuildingLabel()
        {
            if (reliabilityController != null
                && !string.IsNullOrWhiteSpace(reliabilityController.ActiveBuildingId))
                return reliabilityController.ActiveBuildingId;
            if (vpsTransition != null
                && !string.IsNullOrWhiteSpace(vpsTransition.ActiveBuildingId))
                return vpsTransition.ActiveBuildingId;
            if (indoorRouteController != null
                && !string.IsNullOrWhiteSpace(indoorRouteController.ActiveBuildingId))
                return indoorRouteController.ActiveBuildingId;
            return "B9";
        }

        private void RefreshExperimentStatus()
        {
            if (!built)
                return;
            if (experimentLogger == null)
            {
                state.Experiment = "LOG · chưa kết nối";
                state.ExperimentToggleEnabled = false;
                state.MarkerEnabled = false;
                return;
            }

            state.ExportEnabled = logExporter != null;
            state.ExperimentToggleEnabled = true;
            state.MarkerEnabled = experimentLogger.IsRecording;
            if (experimentLogger.IsRecording)
            {
                int elapsed = Mathf.FloorToInt(experimentLogger.ElapsedSeconds);
                state.Experiment = $"LOG · ĐANG GHI {elapsed / 60:00}:{elapsed % 60:00}"
                                   + $" · {experimentLogger.SampleCount} mẫu";
                state.ExperimentToggleLabel = B9NavigationHudState.SaveTrialLabel;
            }
            else
            {
                string filename = string.IsNullOrWhiteSpace(experimentLogger.LastSavedFilePath)
                    ? "chưa có tệp"
                    : Path.GetFileName(experimentLogger.LastSavedFilePath);
                state.Experiment = "LOG · ĐÃ LƯU " + filename;
                state.ExperimentToggleLabel = B9NavigationHudState.NewTrialLabel;
            }

            if (logExporter != null && !string.IsNullOrWhiteSpace(logExporter.LastMessage))
                state.Experiment = "LOG · " + logExporter.LastMessage;
        }

        private void RefreshHarmonySelector()
        {
            if (!built)
                return;

            B9HarmonyExperimentProfile profile = harmonyExperiment != null
                ? harmonyExperiment.ActiveProfile
                : B9HarmonyExperimentProfile.For(B9HarmonyVersion.V5_FullHarmony);
            state.HarmonyProfile = $"HARMONY {profile.VersionCode} · {profile.DisplayName}  "
                                   + $"Q{Mark(profile.QualityThreshold)} "
                                   + $"D{Mark(profile.TemporalDwell)} "
                                   + $"M{Mark(profile.MapIdCheck)} "
                                   + $"R{Mark(profile.RecoveryFsm)} "
                                   + $"A{Mark(profile.AdaptiveGuidance)}";
            state.ActiveVersionCode = profile.VersionCode;
        }

        private static string Mark(bool enabled) => enabled ? "✓" : "–";

        private void ApplyDestinationChoice(string choice)
        {
            if (string.IsNullOrWhiteSpace(choice)
                || !destinationChoiceValues.TryGetValue(choice, out string target))
                return;

            if (target.StartsWith(OutdoorPrefix, StringComparison.Ordinal))
            {
                string destinationId = target.Substring(OutdoorPrefix.Length);
                if (reliabilityController != null)
                    reliabilityController.NavigateToOutdoorDestination(destinationId);
                else
                    routeController?.SetOutdoorDestination(destinationId);
                state.ActiveDestination = DestinationName(destinationId);
            }
            else
            {
                string roomId = target.Substring(RoomPrefix.Length);
                if (reliabilityController != null)
                    reliabilityController.NavigateToIndoorRoom(roomId);
                else
                {
                    routeController?.SetDestinationRoom(roomId);
                    indoorRouteController?.SetDestinationRoom(roomId);
                }
                state.ActiveDestination = roomId;
            }
        }

        private string DestinationName(string destinationId)
        {
            if (campusDestinations != null)
            {
                foreach (B9CampusDestinationCatalog.Destination destination in campusDestinations.Destinations)
                {
                    if (destination != null && destination.Id == destinationId)
                        return destination.DisplayName;
                }
            }

            return destinationId;
        }

        /// <summary>Rebuilds the picker list (B9 rooms, B10 rooms, outdoor places) and returns the default choice.</summary>
        private string BuildDestinationChoices()
        {
            destinationChoiceValues.Clear();
            destinationChoices.Clear();
            string initialChoice = string.Empty;

            foreach (B9BuildingDefinition.RoomDefinition room in building.Rooms)
            {
                string label = "[Trong] B9 · " + room.RoomId;
                destinationChoices.Add(label);
                destinationChoiceValues[label] = RoomPrefix + room.RoomId;
                if (room.RoomId == "B9-104")
                    initialChoice = label;
            }

            if (b10Building == null)
                b10Building = FindFirstObjectByType<B10BuildingAuthoring>(FindObjectsInactive.Include);
            if (b10Building != null)
            {
                var b10Rooms = new List<B9RoomAnchor>(b10Building.RoomAnchors);
                b10Rooms.Sort((left, right) => string.Compare(
                    left != null ? left.RoomId : string.Empty,
                    right != null ? right.RoomId : string.Empty,
                    StringComparison.OrdinalIgnoreCase));
                var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < b10Rooms.Count; i++)
                {
                    B9RoomAnchor room = b10Rooms[i];
                    if (room == null
                        || string.IsNullOrWhiteSpace(room.RoomId)
                        || !room.RoomId.StartsWith("B10-", StringComparison.OrdinalIgnoreCase)
                        || !usedIds.Add(room.RoomId))
                        continue;

                    string label = "[Trong] B10 · " + room.RoomId;
                    destinationChoices.Add(label);
                    destinationChoiceValues[label] = RoomPrefix + room.RoomId;
                }
            }

            if (campusDestinations != null)
            {
                foreach (B9CampusDestinationCatalog.Destination destination
                         in campusDestinations.Destinations)
                {
                    if (destination == null || destination.IndoorNavigationAvailable)
                        continue;
                    string label = "[Ngoài] " + destination.DisplayName;
                    destinationChoices.Add(label);
                    destinationChoiceValues[label] = OutdoorPrefix + destination.Id;
                }
            }

            if (string.IsNullOrWhiteSpace(initialChoice) && destinationChoices.Count > 0)
                initialChoice = destinationChoices[0];
            return initialChoice;
        }

        private void RefreshActionButtons()
        {
            B9NavigationState navigationState = reliabilityController != null
                ? reliabilityController.State
                : B9NavigationState.OutdoorGps;
            bool indoorLocalized = navigationState == B9NavigationState.IndoorVps;
            bool activeOutdoorRoute = routeController != null
                                      && routeController.State != B9OutdoorRouteController.RouteState.NoDestination;
            bool activeIndoorRoute = indoorRouteController != null
                                     && indoorRouteController.NavigationActive;
            bool transitioning = navigationState != B9NavigationState.OutdoorGps
                                 && navigationState != B9NavigationState.IndoorVps
                                 && (reliabilityController == null
                                     || !reliabilityController.PdrFallbackDestinationArrived);
            state.CancelVisible = activeOutdoorRoute || activeIndoorRoute || transitioning;
            state.ExitVisible = indoorLocalized && !reliabilityController.ExitRouteRequested;
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
    }
}
