using System.Collections;
using UnityEngine;

namespace ARNav.Hybrid
{
    /// <summary>
    /// Self-contained scenario tester cho Vòng 2 — không cần WASD, không cần scene setup,
    /// không cần GPS thật, không cần VPS thật.
    ///
    /// Cách dùng:
    ///   1. Tạo scene rỗng (hoặc dùng scene hiện có).
    ///   2. Tạo 1 empty GameObject "Hybrid Scenario", gắn component này.
    ///   3. Bấm Play.
    ///   4. Theo dõi Console + on-screen overlay (góc trên trái + dưới).
    ///
    /// Component sẽ:
    ///   - Tự tạo EntranceAnchor B9 ở (10, 0, 0), trigger 5m.
    ///   - Tự tạo OutdoorPoseProvider + MultisetPoseProvider + LocalizationQualityGate
    ///     + HybridLocalizationManager + cả 2 overlay.
    ///   - Coroutine timeline:
    ///       t=0..approach: user mock di chuyển từ (-15,0,0) → (15,0,0) đi ngang qua cửa.
    ///         Kỳ vọng: Outdoor → ApproachingEntrance → TransitionScanning.
    ///       Sau khi vào TransitionScanning: chờ <see cref="vpsAcquireDelaySeconds"/>
    ///         rồi set mockIndoorReady=true. Kỳ vọng: TransitionScanning → Indoor.
    ///       Indoor ổn định <see cref="indoorDwellSeconds"/>: set mockIndoorReady=false.
    ///         Kỳ vọng: Indoor → Relocalization.
    ///       Sau <see cref="relocRecoverSeconds"/>: set mockIndoorReady=true lại.
    ///         Kỳ vọng: Relocalization → Indoor.
    ///       Sau <see cref="indoorDwellSeconds"/>: gọi RequestExit() + set mockOutdoorReady=true.
    ///         Kỳ vọng: Indoor → ExitingIndoor → Outdoor.
    /// </summary>
    [DisallowMultipleComponent]
    public class HybridScenarioRunner : MonoBehaviour
    {
        [Header("Anchor placement")]
        [SerializeField] private BuildingId targetBuilding = BuildingId.B9;
        [SerializeField] private Vector3 anchorPosition = new Vector3(10f, 0f, 0f);
        [SerializeField] private float anchorTriggerRadius = 5f;

        [Header("Walk timeline")]
        [SerializeField] private Vector3 walkStart = new Vector3(-15f, 0f, 0f);
        [SerializeField] private Vector3 walkEnd = new Vector3(15f, 0f, 0f);
        [SerializeField] private float walkDurationSeconds = 12f;

        [Header("VPS scripted timeline")]
        [Tooltip("Sau khi vào TransitionScanning, chờ X giây rồi giả lập VPS đã localize.")]
        [SerializeField] private float vpsAcquireDelaySeconds = 1.5f;

        [Tooltip("Sau khi vào Indoor, giữ Indoor X giây rồi giả lập VPS lost.")]
        [SerializeField] private float indoorDwellSeconds = 3f;

        [Tooltip("Trong Relocalization, chờ X giây rồi giả lập VPS recover.")]
        [SerializeField] private float relocRecoverSeconds = 2f;

        [Header("Manager tuning override")]
        [SerializeField] private float approachingRadiusMultiplier = 2.5f;
        [SerializeField] private float indoorStableSeconds = 1.0f;
        [SerializeField] private float indoorLostGraceSeconds = 1.5f;
        [SerializeField] private float outdoorStableSeconds = 1.0f;

        [Header("Auto-run")]
        [SerializeField] private bool autoRunOnPlay = true;

        [Header("Route demo (Round 3)")]
        [Tooltip("Bật để tự dựng RouteCoordinator + LineRenderer path + arrow follower.")]
        [SerializeField] private bool buildRouteDemo = true;

        [Tooltip("Vị trí 'phòng đích' indoor (campus world). Coordinator sẽ route tới đây khi state=Indoor.")]
        [SerializeField] private Vector3 indoorDestinationPosition = new Vector3(15f, 0f, 0f);

        [Tooltip("Indoor start waypoint ngay sau cửa — gán làm linkedIndoorStartTransform cho anchor.")]
        [SerializeField] private Vector3 indoorStartPosition = new Vector3(11f, 0f, 0f);

        // ---------------------------------------------------------------------
        // Runtime
        // ---------------------------------------------------------------------

        private OutdoorPoseProvider _outdoor;
        private MultisetPoseProvider _indoor;
        private LocalizationQualityGate _gate;
        private HybridLocalizationManager _manager;
        private EntranceAnchor _anchor;
        private HybridRouteCoordinator _route;
        private HybridPathRenderer _pathRenderer;
        private HybridArrowFollower _arrow;
        private string _scenarioPhase = "idle";

        private void Start()
        {
            BuildScene();
            if (autoRunOnPlay) StartCoroutine(RunScenario());
        }

        [ContextMenu("Run Scenario")]
        public void RunScenarioContextMenu()
        {
            StopAllCoroutines();
            StartCoroutine(RunScenario());
        }

        private void BuildScene()
        {
            // Anchor.
            var anchorGo = new GameObject($"Entrance_{targetBuilding}_Auto");
            anchorGo.transform.position = anchorPosition;
            _anchor = anchorGo.AddComponent<EntranceAnchor>();
            // Trigger radius được dùng qua reflection trên SerializeField — set qua SendMessage không khả thi;
            // dùng default 5f là OK cho test. Nếu cần khác thì user chỉnh inspector trực tiếp.
            // Nhưng để chắc, ta set bằng JSON reflection.
            ApplyPrivateField(_anchor, "buildingId", targetBuilding);
            ApplyPrivateField(_anchor, "floorId", "F1");
            ApplyPrivateField(_anchor, "triggerRadiusMeters", anchorTriggerRadius);

            // Pose providers + gate + manager.
            var hub = new GameObject("Hybrid Hub");
            _outdoor = hub.AddComponent<OutdoorPoseProvider>();
            _indoor = hub.AddComponent<MultisetPoseProvider>();
            _gate = hub.AddComponent<LocalizationQualityGate>();
            _manager = hub.AddComponent<HybridLocalizationManager>();

            ApplyPrivateField(_gate, "indoorStableSeconds", indoorStableSeconds);
            ApplyPrivateField(_gate, "indoorLostGraceSeconds", indoorLostGraceSeconds);
            ApplyPrivateField(_gate, "outdoorStableSeconds", outdoorStableSeconds);
            ApplyPrivateField(_manager, "destinationBuilding", targetBuilding);
            ApplyPrivateField(_manager, "approachingRadiusMultiplier", approachingRadiusMultiplier);

            // Overlays — auto-find providers.
            hub.AddComponent<HybridStateDebugOverlay>();
            hub.AddComponent<MultisetPoseProviderDebugOverlay>();

            // Indoor start waypoint cho anchor (Phase 1 outdoor route target nằm ngay sau cửa).
            var startGo = new GameObject($"IndoorStart_{targetBuilding}_Auto");
            startGo.transform.position = indoorStartPosition;
            ApplyPrivateField(_anchor, "linkedIndoorStartTransform", startGo.transform);

            if (buildRouteDemo)
            {
                BuildRouteDemo();
            }

            Debug.Log($"[HybridScenarioRunner] Built test scene. Anchor at {anchorPosition}, building={targetBuilding}.");

            _manager.OnStateChanged += (prev, next, reason) =>
            {
                Debug.Log($"[HybridScenarioRunner]   STATE: <b>{prev} → {next}</b> ({reason}) at t={Time.time:0.00}s | phase={_scenarioPhase}");
            };
        }

        private void BuildRouteDemo()
        {
            // Destination POI (indoor, building = targetBuilding).
            var destGo = new GameObject($"Destination_Room_{targetBuilding}_Auto");
            destGo.transform.position = indoorDestinationPosition;

            // Route coordinator on shared "Hybrid Route" GO.
            var routeGo = new GameObject("Hybrid Route");
            _route = routeGo.AddComponent<HybridRouteCoordinator>();
            var dest = new HybridDestination
            {
                displayName = $"Room_{targetBuilding}_Auto",
                isIndoor = true,
                building = targetBuilding,
                floorId = "F1",
                targetTransform = destGo.transform,
                explicitCampusPosition = indoorDestinationPosition,
            };
            _route.SetDestination(dest);

            // Path renderer (LineRenderer + crossfade).
            var pathGo = new GameObject("Hybrid Path Renderer");
            pathGo.AddComponent<LineRenderer>();
            _pathRenderer = pathGo.AddComponent<HybridPathRenderer>();

            // Arrow follower với 1 cube nhỏ làm visual.
            var arrowGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowGo.name = "Hybrid Arrow";
            arrowGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            // Remove collider để không gây side effect physics.
            var col = arrowGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _arrow = arrowGo.AddComponent<HybridArrowFollower>();

            _route.OnPhaseChanged += (prev, next) =>
            {
                Debug.Log($"[HybridScenarioRunner]   ROUTE PHASE: <b>{prev} → {next}</b> source={_route.CurrentSource} target={_route.CurrentTarget.ToString("F1")} at t={Time.time:0.00}s");
            };
            _route.OnSourceChanged += (prev, next) =>
            {
                Debug.Log($"[HybridScenarioRunner]   ROUTE SOURCE: <b>{prev} → {next}</b> at t={Time.time:0.00}s — crossfade {0.7f}s");
            };
        }

        private static void ApplyPrivateField(object instance, string name, object value)
        {
            var t = instance.GetType();
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(instance, value);
        }

        private IEnumerator RunScenario()
        {
            yield return null; // chờ 1 frame cho OnEnable + Awake resolve.

            _scenarioPhase = "walking";
            Debug.Log("[HybridScenarioRunner] === Phase 1: walking from " + walkStart + " toward " + walkEnd + " ===");

            float t = 0f;
            bool sawScanning = false;
            while (t < walkDurationSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / walkDurationSeconds);
                Vector3 p = Vector3.Lerp(walkStart, walkEnd, k);
                _outdoor.SetMockUserPosition(p, accuracyMeters: 2f);

                if (!sawScanning && _manager.CurrentState == HybridNavigationState.TransitionScanning)
                {
                    sawScanning = true;
                    Debug.Log("[HybridScenarioRunner] entered TransitionScanning → freeze user, wait VPS …");
                    break;
                }
                yield return null;
            }

            if (!sawScanning)
            {
                Debug.LogWarning("[HybridScenarioRunner] Walk completed without entering TransitionScanning. " +
                                 "Anchor có đúng position? Walk path có đi qua anchor không?");
                yield break;
            }

            // Freeze user near door (anchor + offset).
            _outdoor.SetMockUserPosition(anchorPosition + new Vector3(0.5f, 0f, 0f), accuracyMeters: 2f);

            _scenarioPhase = "acquiring-vps";
            Debug.Log($"[HybridScenarioRunner] === Phase 2: wait {vpsAcquireDelaySeconds:0.0}s then mock VPS ready ===");
            yield return new WaitForSeconds(vpsAcquireDelaySeconds);
            _gate.SetMockIndoorReady(true);

            // Wait for Indoor.
            yield return WaitForState(HybridNavigationState.Indoor, timeout: 5f);

            _scenarioPhase = "indoor-dwell";
            Debug.Log($"[HybridScenarioRunner] === Phase 3: dwell Indoor {indoorDwellSeconds:0.0}s then mock VPS lost ===");
            yield return new WaitForSeconds(indoorDwellSeconds);
            _gate.SetMockIndoorReady(false);

            yield return WaitForState(HybridNavigationState.Relocalization, timeout: 5f);

            _scenarioPhase = "relocalizing";
            Debug.Log($"[HybridScenarioRunner] === Phase 4: wait {relocRecoverSeconds:0.0}s then mock VPS recover ===");
            yield return new WaitForSeconds(relocRecoverSeconds);
            _gate.SetMockIndoorReady(true);

            yield return WaitForState(HybridNavigationState.Indoor, timeout: 5f);

            _scenarioPhase = "exit-request";
            Debug.Log("[HybridScenarioRunner] === Phase 5: RequestExit + mock outdoor ready ===");
            yield return new WaitForSeconds(1f);
            _manager.RequestExit();
            _gate.SetMockOutdoorReady(true);

            yield return WaitForState(HybridNavigationState.Outdoor, timeout: 5f);

            _scenarioPhase = "done";
            Debug.Log("[HybridScenarioRunner] === DONE ✔  Full cycle Outdoor → Indoor → Outdoor verified ===");
        }

        private IEnumerator WaitForState(HybridNavigationState target, float timeout)
        {
            float deadline = Time.time + timeout;
            while (Time.time < deadline)
            {
                if (_manager.CurrentState == target) yield break;
                yield return null;
            }
            Debug.LogError($"[HybridScenarioRunner] ✖ Timeout waiting for state {target}. Stuck in {_manager.CurrentState}. Reason={_manager.LastTransitionReason}");
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(Screen.width - 360, 10, 350, 80),
                $"<b>HybridScenarioRunner</b>\nphase=<b>{_scenarioPhase}</b>\n" +
                $"mockUser={(_outdoor != null ? _outdoor.UserCampusPosition.ToString("F1") : "?")}\n" +
                $"anchor={anchorPosition.ToString("F1")} r={anchorTriggerRadius}m",
                new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.UpperLeft, fontSize = 14 });
        }
    }
}
