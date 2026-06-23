#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ARNav.Hybrid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNav.HybridEditor
{
    /// <summary>
    /// 1-click setup tool cho cả Hybrid pipeline (V1 + V2 + V3 + bridge sang ARPathFinder).
    /// Mở qua menu: <b>Tools → Hybrid → Pipeline Setup</b>.
    ///
    /// Bấm "Setup All" sẽ:
    ///   - Tìm/tạo "Hybrid Hub" với 4 component V1/V2 (OutdoorPoseProvider, MultisetPoseProvider,
    ///     LocalizationQualityGate, HybridLocalizationManager) + 2 overlay.
    ///   - Tìm/tạo "Hybrid Route" với HybridRouteCoordinator + IndoorMapCalibration.
    ///   - Tìm/tạo "Hybrid Arrow" Cube + HybridArrowFollower.
    ///   - Tìm/tạo Entrance_B9 + Entrance_B10 placeholder (đặt tạm ở scene origin — user move tay).
    ///   - Tìm ARPathFinder (production ribbon) và gắn HybridArPathFinderBridge.
    ///   - Tự tắt HybridPathRenderer debug nếu user chọn "use production ribbon".
    ///
    /// Idempotent: bấm nhiều lần KHÔNG nhân đôi GameObject. Đã có thì reuse.
    /// Dirty scene + tự mark Undo để user revert.
    /// </summary>
    public class HybridPipelineSetupWindow : EditorWindow
    {
        private bool useProductionRibbon = true;
        private bool spawnDebugLineRenderer = true;
        private bool spawnArrowVisual = true;
        private bool createEntrancePlaceholders = true;
        private Vector3 entranceB9Position = new Vector3(0f, 0f, 5f);
        private Vector3 entranceB10Position = new Vector3(5f, 0f, 0f);
        private BuildingId defaultDestinationBuilding = BuildingId.B9;
        private Vector3 indoorDestinationPosition = new Vector3(15f, 0f, 0f);
        private Vector3 indoorStartPosition = new Vector3(11f, 0f, 0f);

        [MenuItem("Tools/Hybrid/Pipeline Setup")]
        public static void Open()
        {
            var win = GetWindow<HybridPipelineSetupWindow>(true, "Hybrid Pipeline Setup", true);
            win.minSize = new Vector2(420, 540);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "1-click setup cho hybrid indoor/outdoor pipeline (V1 + V2 + V3).\n" +
                "Bấm 'Setup All' để dựng tất cả; idempotent — bấm lại sẽ reuse.\n" +
                "Sau khi setup, move Entrance_B9/B10 placeholder tới vị trí cửa thật.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            useProductionRibbon = EditorGUILayout.Toggle(
                new GUIContent("Use Production Ribbon", "True = nối ARPathFinder qua HybridArPathFinderBridge. False = bỏ qua bridge, chỉ dùng LineRenderer debug."),
                useProductionRibbon);
            spawnDebugLineRenderer = EditorGUILayout.Toggle(
                new GUIContent("Spawn Debug LineRenderer", "Vẫn tạo HybridPathRenderer (LineRenderer debug) song song để verify."),
                spawnDebugLineRenderer);
            spawnArrowVisual = EditorGUILayout.Toggle(
                new GUIContent("Spawn Arrow Cube", "Tạo cube nhỏ với HybridArrowFollower để verify arrow smoothing."),
                spawnArrowVisual);
            createEntrancePlaceholders = EditorGUILayout.Toggle(
                new GUIContent("Create Entrance Placeholders", "Tạo EntranceAnchor cho B9/B10 nếu chưa có."),
                createEntrancePlaceholders);

            using (new EditorGUI.DisabledScope(!createEntrancePlaceholders))
            {
                entranceB9Position = EditorGUILayout.Vector3Field("  Entrance B9 (campus world)", entranceB9Position);
                entranceB10Position = EditorGUILayout.Vector3Field("  Entrance B10 (campus world)", entranceB10Position);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Destination (inspector defaults)", EditorStyles.boldLabel);
            defaultDestinationBuilding = (BuildingId)EditorGUILayout.EnumPopup("Destination Building", defaultDestinationBuilding);
            indoorDestinationPosition = EditorGUILayout.Vector3Field("Indoor Destination Pos", indoorDestinationPosition);
            indoorStartPosition = EditorGUILayout.Vector3Field("Indoor Start (post-entrance)", indoorStartPosition);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Setup All (1 click)", GUILayout.Height(40)))
                {
                    SetupAll();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Piecewise", EditorStyles.boldLabel);
            if (GUILayout.Button("→ Hybrid Hub only (V1+V2)")) { SetupHub(); MarkSceneDirty(); }
            if (GUILayout.Button("→ Hybrid Route + IndoorMapCalibration (V3)")) { SetupRoute(); MarkSceneDirty(); }
            if (GUILayout.Button("→ Entrance Placeholders (B9/B10)")) { CreateEntrances(); MarkSceneDirty(); }
            if (GUILayout.Button("→ Auto-place anchors from TargetAnchor (CAMPUS WORLD)")) { AutoPlaceEntranceFromTargetAnchors(); MarkSceneDirty(); }
            if (GUILayout.Button("→ ARPathFinder Bridge (production ribbon)")) { AttachArPathFinderBridge(); MarkSceneDirty(); }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cleanup", EditorStyles.boldLabel);
            if (GUILayout.Button("Disable LineRenderer debug (use ribbon only)"))
            {
                DisableLineRendererDebug();
                MarkSceneDirty();
            }
            if (GUILayout.Button("Validate scene"))
            {
                Validate();
            }
            if (GUILayout.Button("Check parenting (catch GO inside Indoor/OutdoorEnvironment)"))
            {
                CheckParenting();
            }
        }

        // ---------------------------------------------------------------------
        // SETUP ALL
        // ---------------------------------------------------------------------

        private void SetupAll()
        {
            Undo.SetCurrentGroupName("Hybrid Pipeline Setup");
            int undoGroup = Undo.GetCurrentGroup();

            var hub = SetupHub();
            var route = SetupRoute();
            if (createEntrancePlaceholders) CreateEntrances();
            if (useProductionRibbon) AttachArPathFinderBridge();

            // Wire defaults trên manager + route inspector.
            var manager = hub.GetComponent<HybridLocalizationManager>();
            SetPrivateField(manager, "destinationBuilding", defaultDestinationBuilding);
            EditorUtility.SetDirty(manager);

            var coord = route.GetComponent<HybridRouteCoordinator>();
            var dest = new HybridDestination
            {
                displayName = $"Room_{defaultDestinationBuilding}",
                isIndoor = true,
                building = defaultDestinationBuilding,
                floorId = "F1",
                explicitCampusPosition = indoorDestinationPosition,
            };
            SetPrivateField(coord, "destination", dest);
            EditorUtility.SetDirty(coord);

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneDirty();
            ShowNotification(new GUIContent("✔ Hybrid pipeline ready"));
            Debug.Log("[HybridPipelineSetup] Done. Move Entrance_B9/B10 to real door positions, then Play.");
        }

        // ---------------------------------------------------------------------
        // HUB (V1 + V2 components)
        // ---------------------------------------------------------------------

        private GameObject SetupHub()
        {
            var hub = GameObject.Find("Hybrid Hub");
            if (hub == null)
            {
                hub = new GameObject("Hybrid Hub");
                Undo.RegisterCreatedObjectUndo(hub, "Create Hybrid Hub");
            }

            EnsureComponent<OutdoorPoseProvider>(hub);
            EnsureComponent<MultisetPoseProvider>(hub);
            EnsureComponent<LocalizationQualityGate>(hub);
            EnsureComponent<HybridLocalizationManager>(hub);
            EnsureComponent<HybridStateDebugOverlay>(hub);
            EnsureComponent<MultisetPoseProviderDebugOverlay>(hub);
            return hub;
        }

        // ---------------------------------------------------------------------
        // ROUTE (V3)
        // ---------------------------------------------------------------------

        private GameObject SetupRoute()
        {
            var route = GameObject.Find("Hybrid Route");
            if (route == null)
            {
                route = new GameObject("Hybrid Route");
                Undo.RegisterCreatedObjectUndo(route, "Create Hybrid Route");
            }

            EnsureComponent<HybridRouteCoordinator>(route);

            // IndoorMapCalibration mặc định cho destination building.
            var calibs = FindAllInScene<IndoorMapCalibration>();
            bool hasCalibForDest = calibs.Any(c => c != null && c.TargetBuilding == defaultDestinationBuilding);
            if (!hasCalibForDest)
            {
                var calib = EnsureComponent<IndoorMapCalibration>(route);
                SetPrivateField(calib, "targetBuilding", defaultDestinationBuilding);
                SetPrivateField(calib, "floorId", "F1");
                EditorUtility.SetDirty(calib);
            }

            // LineRenderer debug renderer.
            if (spawnDebugLineRenderer)
            {
                var rendererGo = GameObject.Find("Hybrid Path Renderer");
                if (rendererGo == null)
                {
                    rendererGo = new GameObject("Hybrid Path Renderer");
                    Undo.RegisterCreatedObjectUndo(rendererGo, "Create Hybrid Path Renderer");
                }
                var lineRenderer = EnsureComponent<LineRenderer>(rendererGo);
                EnsureComponent<HybridPathRenderer>(rendererGo);
            }

            // Arrow cube.
            if (spawnArrowVisual)
            {
                var arrow = GameObject.Find("Hybrid Arrow");
                if (arrow == null)
                {
                    arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arrow.name = "Hybrid Arrow";
                    arrow.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                    var col = arrow.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                    Undo.RegisterCreatedObjectUndo(arrow, "Create Hybrid Arrow");
                }
                EnsureComponent<HybridArrowFollower>(arrow);
            }

            return route;
        }

        // ---------------------------------------------------------------------
        // ENTRANCE PLACEHOLDERS
        // ---------------------------------------------------------------------

        private void CreateEntrances()
        {
            CreateOrUpdateEntrance("Entrance_B9", BuildingId.B9, entranceB9Position);
            CreateOrUpdateEntrance("Entrance_B10", BuildingId.B10, entranceB10Position);
        }

        /// <summary>
        /// Quét tất cả TargetAnchor đã có trong scene (vốn được place theo GPS bởi
        /// pipeline outdoor cũ). Tìm cái có TargetName chứa "B9"/"B10", lấy
        /// transform.position rồi MOVE EntranceAnchor tương ứng tới đó.
        /// </summary>
        private void AutoPlaceEntranceFromTargetAnchors()
        {
            var targets = Object.FindObjectsByType<TargetAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (targets.Length == 0)
            {
                Debug.LogWarning("[HybridPipelineSetup] Không tìm thấy TargetAnchor nào trong scene. " +
                                 "Pipeline outdoor có thể chưa init — Play trước, chờ TargetAnchor.Recalculate() chạy, rồi bấm lại.");
                return;
            }

            int placed = 0;
            placed += AutoPlaceOne(targets, BuildingId.B9, "B9");
            placed += AutoPlaceOne(targets, BuildingId.B10, "B10");

            if (placed == 0)
            {
                Debug.LogWarning("[HybridPipelineSetup] Không match được TargetName chứa 'B9' hoặc 'B10' trong các TargetAnchor có sẵn. " +
                                 "Danh sách TargetAnchor hiện có:\n  • " +
                                 string.Join("\n  • ", targets.Select(t => $"'{t.TargetName}' @ {t.transform.position}")));
            }
            else
            {
                Debug.Log($"[HybridPipelineSetup] Đã auto-place {placed} EntranceAnchor từ TargetAnchor positions.");
                ShowNotification(new GUIContent($"✔ Placed {placed} anchors"));
            }
        }

        private int AutoPlaceOne(TargetAnchor[] sources, BuildingId building, string nameSubstring)
        {
            var src = sources.FirstOrDefault(t => t != null && !string.IsNullOrEmpty(t.TargetName)
                                                  && t.TargetName.IndexOf(nameSubstring, System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (src == null) return 0;

            var existing = FindAllInScene<EntranceAnchor>()
                .FirstOrDefault(a => a != null && a.BuildingId == building);
            if (existing == null)
            {
                var go = new GameObject($"Entrance_{building}");
                Undo.RegisterCreatedObjectUndo(go, $"Create Entrance_{building}");
                existing = go.AddComponent<EntranceAnchor>();
                SetPrivateField(existing, "buildingId", building);
                SetPrivateField(existing, "floorId", "F1");
                SetPrivateField(existing, "triggerRadiusMeters", 5f);

                var startGo = new GameObject($"Entrance_{building}_IndoorStart");
                startGo.transform.SetParent(go.transform, false);
                startGo.transform.localPosition = new Vector3(0f, 0f, 1f);
                SetPrivateField(existing, "linkedIndoorStartTransform", startGo.transform);
            }

            Undo.RecordObject(existing.transform, $"Move Entrance_{building}");
            existing.transform.position = src.transform.position;
            EditorUtility.SetDirty(existing.transform);
            EditorUtility.SetDirty(existing);

            Debug.Log($"[HybridPipelineSetup] Entrance_{building} → '{src.TargetName}' @ {src.transform.position}");
            return 1;
        }

        private void CreateOrUpdateEntrance(string name, BuildingId building, Vector3 pos)
        {
            // Tìm existing anchor cho building này.
            var existing = FindAllInScene<EntranceAnchor>()
                .FirstOrDefault(a => a != null && a.BuildingId == building);
            if (existing != null)
            {
                Debug.Log($"[HybridPipelineSetup] Reuse existing EntranceAnchor for {building}: '{existing.name}'.");
                return;
            }

            var go = new GameObject(name);
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            var anchor = go.AddComponent<EntranceAnchor>();
            SetPrivateField(anchor, "buildingId", building);
            SetPrivateField(anchor, "floorId", "F1");
            SetPrivateField(anchor, "triggerRadiusMeters", 5f);

            // Indoor start waypoint dưới anchor.
            var startGo = new GameObject($"{name}_IndoorStart");
            startGo.transform.SetParent(go.transform, false);
            startGo.transform.localPosition = new Vector3(0f, 0f, 1f); // 1m vào bên trong cửa
            SetPrivateField(anchor, "linkedIndoorStartTransform", startGo.transform);

            EditorUtility.SetDirty(anchor);
        }

        // ---------------------------------------------------------------------
        // AR PATH FINDER BRIDGE
        // ---------------------------------------------------------------------

        private void AttachArPathFinderBridge()
        {
            var arPathFinder = Object.FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
            if (arPathFinder == null)
            {
                Debug.LogWarning("[HybridPipelineSetup] No ARPathFinder found in scene — bridge skipped. " +
                                 "Production ribbon will not draw until you add an ARPathFinder component.");
                return;
            }

            var bridge = arPathFinder.GetComponent<HybridArPathFinderBridge>();
            if (bridge == null)
            {
                bridge = Undo.AddComponent<HybridArPathFinderBridge>(arPathFinder.gameObject);
                Debug.Log($"[HybridPipelineSetup] Attached HybridArPathFinderBridge to '{arPathFinder.gameObject.name}'.");
            }
            EditorUtility.SetDirty(bridge);
        }

        private void DisableLineRendererDebug()
        {
            var debug = Object.FindFirstObjectByType<HybridPathRenderer>(FindObjectsInactive.Include);
            if (debug == null)
            {
                Debug.Log("[HybridPipelineSetup] No HybridPathRenderer to disable.");
                return;
            }
            Undo.RecordObject(debug, "Disable HybridPathRenderer");
            debug.enabled = false;
            EditorUtility.SetDirty(debug);
            Debug.Log("[HybridPipelineSetup] HybridPathRenderer disabled — production ribbon only.");
        }

        // ---------------------------------------------------------------------
        // VALIDATE
        // ---------------------------------------------------------------------

        private void Validate()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Hybrid Pipeline Validation ===");
            Check<HybridLocalizationManager>(sb, "HybridLocalizationManager");
            Check<OutdoorPoseProvider>(sb, "OutdoorPoseProvider");
            Check<MultisetPoseProvider>(sb, "MultisetPoseProvider");
            Check<LocalizationQualityGate>(sb, "LocalizationQualityGate");
            Check<HybridRouteCoordinator>(sb, "HybridRouteCoordinator");

            var anchors = FindAllInScene<EntranceAnchor>();
            sb.AppendLine($"EntranceAnchor count: {anchors.Count}");
            foreach (var a in anchors) sb.AppendLine($"   • {a.BuildingId}/{a.FloorId} '{a.name}' at {a.transform.position}");

            var calibs = FindAllInScene<IndoorMapCalibration>();
            sb.AppendLine($"IndoorMapCalibration count: {calibs.Count}");
            foreach (var c in calibs) sb.AppendLine($"   • {c.TargetBuilding}/{c.FloorId} '{c.name}'");

            var arpf = Object.FindFirstObjectByType<ARPathFinder>(FindObjectsInactive.Include);
            sb.AppendLine($"ARPathFinder in scene: {(arpf != null ? "OK ('" + arpf.gameObject.name + "')" : "missing")}");
            if (arpf != null)
            {
                sb.AppendLine($"HybridArPathFinderBridge: {(arpf.GetComponent<HybridArPathFinderBridge>() != null ? "OK" : "missing — bấm 'AR Path Finder Bridge' để gắn")}");
            }

            Debug.Log(sb.ToString());
        }

        private static void Check<T>(System.Text.StringBuilder sb, string label) where T : Component
        {
            var c = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            sb.AppendLine($"{label}: {(c != null ? "OK ('" + c.gameObject.name + "')" : "MISSING")}");
        }

        private void CheckParenting()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Hybrid Parenting Check ===");
            sb.AppendLine("Các GameObject sau PHẢI ở scene root (hoặc ngoài Indoor/OutdoorEnvironment):");
            sb.AppendLine();

            int problems = 0;
            problems += CheckParentRoot<HybridLocalizationManager>(sb, "Hybrid Hub");
            problems += CheckParentRoot<HybridRouteCoordinator>(sb, "Hybrid Route");
            problems += CheckParentRoot<HybridPathRenderer>(sb, "Hybrid Path Renderer");
            problems += CheckParentRoot<HybridArrowFollower>(sb, "Hybrid Arrow");

            foreach (var a in FindAllInScene<EntranceAnchor>())
            {
                problems += CheckSpecificParentRoot(sb, a.gameObject, $"EntranceAnchor ({a.BuildingId})");
            }

            sb.AppendLine();
            if (problems == 0) sb.AppendLine("✔ All hybrid objects are correctly parented.");
            else sb.AppendLine($"✖ {problems} object(s) nằm sai chỗ — move chúng ra scene root.");

            Debug.Log(sb.ToString());
            ShowNotification(new GUIContent(problems == 0 ? "✔ Parenting OK" : $"✖ {problems} sai chỗ"));
        }

        private static int CheckParentRoot<T>(System.Text.StringBuilder sb, string label) where T : Component
        {
            var c = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (c == null)
            {
                sb.AppendLine($"  • {label}: (not found, skip)");
                return 0;
            }
            return CheckSpecificParentRoot(sb, c.gameObject, label);
        }

        private static int CheckSpecificParentRoot(System.Text.StringBuilder sb, GameObject go, string label)
        {
            // Walk up parent chain, look for IndoorEnvironment / OutdoorEnvironment.
            for (var t = go.transform.parent; t != null; t = t.parent)
            {
                string n = t.name;
                if (n == "IndoorEnvironment" || n == "OutdoorEnvironment"
                    || n.StartsWith("Indoor") || n.StartsWith("Outdoor"))
                {
                    sb.AppendLine($"  ✖ {label} '{go.name}' đang nằm dưới '{n}' — sẽ tắt khi đổi mode. Drag ra scene root.");
                    return 1;
                }
            }
            sb.AppendLine($"  ✔ {label} '{go.name}' OK (scene root hoặc parent neutral).");
            return 0;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
            {
                c = Undo.AddComponent<T>(go);
            }
            return c;
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var t = instance.GetType();
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Public);
            if (f == null)
            {
                Debug.LogWarning($"[HybridPipelineSetup] Field '{name}' not found on {t.Name}");
                return;
            }
            f.SetValue(instance, value);
        }

        private static List<T> FindAllInScene<T>() where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
        }

        private static void MarkSceneDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
