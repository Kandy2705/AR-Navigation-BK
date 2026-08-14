using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Draws a navigation ribbon from the player to a target.
/// Default: corners along <see cref="UnityEngine.AI.NavMesh"/> (routing).
/// Optional: single straight segment in world space (e.g. debug); optional straight fallback when NavMesh fails.
///
/// Start point priority:
///   1. arCamera.position — AR-tracked
///   2. xrOrigin.position — GPS fallback
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ARPathFinder : MonoBehaviour
{
    public const string PathMeshRootName = "PathMeshRoot";

    public enum PathGeometryMode
    {
        StraightWorldLine,
        NavMeshRoute,
    }

    [Header("Path shape")]
    [Tooltip("NavMeshRoute: path along NavMesh (normal). StraightWorldLine: one segment start→target (no NavMesh); use only if you intentionally skip baking.")]
    [SerializeField] private PathGeometryMode pathGeometryMode = PathGeometryMode.NavMeshRoute;

    [Header("Navigation References")]
    public Transform xrOrigin;
    public Transform targetNode;
    [Tooltip("AR Camera (has TrackedPoseDriver). Path starts from camera position so it follows " +
             "physical movement in real-time instead of waiting for slow GPS updates.")]
    public Camera arCamera;

    [Header("GPS gate (SetNavigation-style)")]
    [SerializeField] private SimpleGPSTracker navigationGpsTracker;
    [Tooltip("When true, clear route line while SimpleGPSTracker reports unhealthy. When false (default), path follows AR camera / NavMesh whenever geometry allows — avoids hybrid/device builds looking 'broken' vs Editor bypass. Enable for strict SetNavigation-style gating.")]
    [SerializeField] private bool gateLineUntilNavigationGpsHealthy = false;
    [Tooltip("When true, Editor always draws path even if GPS gate would block (no real GPS/compass in Play Mode).")]
    [SerializeField] private bool bypassNavigationGpsGateInEditor = true;
    [Tooltip("On device the gate waits for compass north alignment before IsNavigationGpsHealthy. Path start uses AR camera world position, so you can show the ribbon while north is still aligning (Editor felt smooth because bypass hides this). Turn off for strict alignment before path.")]
    [SerializeField] private bool allowPathWhileNorthAlignmentPending = true;

    [Header("Path visibility (NavMesh mode)")]
    [Tooltip("When true and path uses NavMesh: never block drawing for GPS gate, try a larger sample radius, and can fall back to a straight line if routing fails.")]
    [SerializeField] private bool prioritizePathVisibility = true;
    [Tooltip("Second pass NavMesh.SamplePosition radius when prioritizePathVisibility is on (meters). Use 0 to disable second pass.")]
    [SerializeField] private float navMeshSampleRadiusExpanded = 24f;
    [Tooltip("When using NavMeshRoute: if routing fails, draw a straight world line instead.")]
    [SerializeField] private bool showStraightLineFallbackWhenNavMeshFails = true;

    [Header("Path Settings (NavMesh only)")]
    [SerializeField] private float navMeshSampleRadius = 8f;

    [Header("Render — mesh ribbon (recommended for AR)")]
    [Tooltip("Use MeshRenderer ribbon + arrow head instead of LineRenderer (better ground hugging, no billboard strip).")]
    [SerializeField] private bool useMeshPath = true;
    [SerializeField] private float pathWidth = 0.5f;
    [Tooltip("White side border width in meters (center strip uses chevron texture / color).")]
    [SerializeField] private float pathBorderWidthMeters = 0.06f;
    [Tooltip("Lift path above each NavMesh corner Y (meters).")]
    [SerializeField] private float pathHeightOffset = 0.04f;
    [Tooltip("Pin path Y to camera-foot level (cameraY - cameraEyeToFootMeters). Fixes 'path floats in air' when GPS Y drifts or NavMesh sits above visible ground.")]
    [SerializeField] private bool clampPathYToCameraFoot = true;
    [Tooltip("Eye-to-foot distance assumed for the user when clampPathYToCameraFoot is on (meters).")]
    [SerializeField] private float cameraEyeToFootMeters = 1.6f;
    [SerializeField] private bool pathAlwaysOnTop = true;
    [SerializeField] private Material pathCenterMaterial;
    [SerializeField] private Material pathBorderMaterial;
    [Tooltip("Optional. If null, a runtime chevron texture is generated.")]
    [SerializeField] private Texture2D pathChevronTexture;
    [SerializeField] private Color pathCenterTint = Color.white;
    [SerializeField] private float pathMetersPerTile = 2.5f;
    [Tooltip("Scales U across the strip for texture detail.")]
    [SerializeField] private float pathUvRepeatAcrossWidth = 1f;
    [SerializeField] private bool drawArrowHead = true;
    [SerializeField] private float arrowHeadLengthMeters = 0.55f;
    [SerializeField] private float arrowHeadWidthMeters = 0.38f;

    [Header("Corner smoothing")]
    [Tooltip("Làm tròn góc gấp của path bằng Chaikin corner-cutting → đường cong mềm mại. " +
             "Đường cong nằm trong polyline gốc nên không đâm vào obstacle.")]
    [SerializeField] private bool smoothCorners = true;
    [Tooltip("Số lần lặp Chaikin. 1 = nhẹ, 2 = mượt (khuyến nghị), 3 = rất mượt nhưng nhiều vertex hơn.")]
    [Range(0, 4)]
    [SerializeField] private int smoothIterations = 2;

    [Header("Minimap mirror")]
    [Tooltip("Hiện 1 bản sao path NÂNG CAO chỉ cho minimap camera thấy, trong khi path AR vẫn sát đất. " +
             "Giải quyết: path AR thấp (đúng tầm mắt) nhưng top-down minimap camera vẫn thấy (không bị plane che).")]
    [SerializeField] private bool showPathOnMinimap = true;
    [Tooltip("Nâng path mirror lên cao bao nhiêu (m) để minimap camera nhìn từ trên thấy rõ, không bị MapPlane che.")]
    [SerializeField] private float minimapPathLiftMeters = 5f;
    [Tooltip("Layer chỉ minimap camera render (AR camera phải loại layer này khỏi cullingMask).")]
    [SerializeField] private string minimapLayerName = "MinimapOnly";

    [Header("Update Throttle")]
    [Tooltip("Minimum seconds between path recalculations.")]
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [Tooltip("Minimum distance (meters) either endpoint must move before a new path is calculated.")]
    [SerializeField] private float minMoveDistanceMeters = 0.15f;
    [Tooltip("Mesh chỉ rebuild khi corners mới lệch corners cũ > giá trị này (mét). Cao = ít rebuild = mượt nhưng path có thể không update kịp khi rẽ. Thấp = update nhạy hơn nhưng dễ flicker. Recommended 0.3-0.7m.")]
    [SerializeField] [Range(0.05f, 2f)] private float cornerSimilarityThresholdMeters = 0.5f;

    private LineRenderer line;
    private Transform _pathMeshRoot;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh pathMesh;
    private NavMeshPath path;

    private Material _runtimeBorderMat;
    private Material _runtimeCenterMat;
    private Texture2D _runtimeChevronTex;

    private float nextPathUpdateTime;
    private Vector3 lastStartPos = Vector3.positiveInfinity;
    private Vector3 lastEndPos = Vector3.positiveInfinity;
    private bool forcePathRecalcAfterTargetChange;

    // Cache corners đã build để skip rebuild khi path mới gần giống path cũ (chống flicker do GPS noise).
    private Vector3[] _lastBuiltCorners;
    private float _harmonyAlpha = 1f;
    private bool _harmonyVisible = true;
    private Color _harmonyTint = Color.white;
    private MaterialPropertyBlock _harmonyPropertyBlock;

#if DEVELOPMENT_BUILD
    private string _lastLoggedPathHudLine;
#endif

    public Transform TargetNode => targetNode;
    public bool HasPath { get; private set; }
    public float CurrentPathDistanceMeters { get; private set; }

    /// <summary>One-line build status for HUD (device / debug).</summary>
    public string PathHudDebugLine { get; private set; } = string.Empty;

    public bool IsNavigationPathBlockedByGpsGate
    {
        get
        {
            if (prioritizePathVisibility || pathGeometryMode == PathGeometryMode.StraightWorldLine)
            {
                return false;
            }

            if (!NavigationGpsGateRuns || navigationGpsTracker == null)
                return false;

            if (!navigationGpsTracker.TryGetPathNavigationBlock(out string reason))
                return false;

            if (allowPathWhileNorthAlignmentPending &&
                string.Equals(reason, "north_pending", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }

    private bool NavigationGpsGateRuns =>
        gateLineUntilNavigationGpsHealthy && navigationGpsTracker != null
#if UNITY_EDITOR
        && !bypassNavigationGpsGateInEditor
#endif
        ;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        path = new NavMeshPath();
        EnsurePathMeshRoot();
        EnsureMeshComponents();
    }

    void OnEnable()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (path == null) path = new NavMeshPath();
        EnsurePathMeshRoot();
        EnsureMeshComponents();

        // lastStartPos/lastEndPos survive SetActive(false) — reset so the position-delta
        // guard does not skip the first Update after reactivation.
        lastStartPos = Vector3.positiveInfinity;
        lastEndPos   = Vector3.positiveInfinity;
        forcePathRecalcAfterTargetChange = true;
        nextPathUpdateTime = 0f;
    }

    private void EnsurePathMeshRoot()
    {
        if (_pathMeshRoot != null) return;

        Transform existing = transform.Find(PathMeshRootName);
        if (existing != null)
            _pathMeshRoot = existing;
        else
        {
            var go = new GameObject(PathMeshRootName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _pathMeshRoot = go.transform;
        }

        if (Application.isPlaying)
        {
            var pmf = GetComponent<MeshFilter>();
            var pmr = GetComponent<MeshRenderer>();
            if (pmf != null) Destroy(pmf);
            if (pmr != null) Destroy(pmr);
        }

        meshFilter = _pathMeshRoot.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = _pathMeshRoot.gameObject.AddComponent<MeshFilter>();

        meshRenderer = _pathMeshRoot.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = _pathMeshRoot.gameObject.AddComponent<MeshRenderer>();
    }

    private MeshRenderer _minimapMirrorRenderer;
    private MeshFilter _minimapMirrorFilter;

    private void EnsureMeshComponents()
    {
        EnsurePathMeshRoot();

        if (pathMesh == null)
        {
            pathMesh = new Mesh { name = "ARPathRibbon" };
            meshFilter.mesh = pathMesh;
        }

        EnsurePathMaterials();
        EnsureMinimapMirror();
    }

    private void EnsureMinimapMirror()
    {
        if (!showPathOnMinimap || _minimapMirrorRenderer != null || _pathMeshRoot == null) return;

        Transform existing = _pathMeshRoot.Find("MinimapPathMirror");
        GameObject go = existing != null ? existing.gameObject : new GameObject("MinimapPathMirror");
        go.transform.SetParent(_pathMeshRoot, false);
        // Nâng theo WORLD +Y (hướng lên trời = về phía minimap camera top-down) bất kể _pathMeshRoot xoay.
        // Nếu hardcode (0,lift,0) local thì khi root bị xoay, offset đi sai trục (phải chỉnh Z thủ công).
        go.transform.localPosition = _pathMeshRoot.InverseTransformVector(Vector3.up * minimapPathLiftMeters);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        int layer = LayerMask.NameToLayer(minimapLayerName);
        if (layer >= 0) go.layer = layer;

        _minimapMirrorFilter = go.GetComponent<MeshFilter>();
        if (_minimapMirrorFilter == null) _minimapMirrorFilter = go.AddComponent<MeshFilter>();
        _minimapMirrorFilter.sharedMesh = pathMesh; // CHIA SẺ mesh → tự cập nhật khi path rebuild

        _minimapMirrorRenderer = go.GetComponent<MeshRenderer>();
        if (_minimapMirrorRenderer == null) _minimapMirrorRenderer = go.AddComponent<MeshRenderer>();
        _minimapMirrorRenderer.enabled = false;
    }

    private void EnsurePathMaterials()
    {
        if (_runtimeBorderMat == null)
        {
            _runtimeBorderMat = pathBorderMaterial != null
                ? new Material(pathBorderMaterial)
                : NavigationPathMaterialHelper.CreateDefaultPathMaterial(Color.white);
        }

        if (_runtimeCenterMat == null)
        {
            _runtimeCenterMat = pathCenterMaterial != null
                ? new Material(pathCenterMaterial)
                : NavigationPathMaterialHelper.CreateDefaultPathMaterial(pathCenterTint);

            if (pathChevronTexture != null)
                NavigationPathMaterialHelper.SetMaterialMainTexture(_runtimeCenterMat, pathChevronTexture);
            else if (pathCenterMaterial == null)
            {
                if (_runtimeChevronTex == null)
                    _runtimeChevronTex = NavigationPathMaterialHelper.CreateChevronStripTexture();
                NavigationPathMaterialHelper.SetMaterialMainTexture(_runtimeCenterMat, _runtimeChevronTex);
            }

            if (pathCenterMaterial == null)
                _runtimeCenterMat.color = pathCenterTint;
        }

        NavigationPathMaterialHelper.Configure(_runtimeBorderMat, pathAlwaysOnTop);
        NavigationPathMaterialHelper.Configure(_runtimeCenterMat, pathAlwaysOnTop);
    }

    private void ApplyMaterialsForCurrentMesh()
    {
        if (meshRenderer == null || pathMesh == null) return;
        if (pathMesh.subMeshCount <= 1)
            meshRenderer.sharedMaterial = _runtimeCenterMat;
        else
            meshRenderer.sharedMaterials = new[] { _runtimeBorderMat, _runtimeCenterMat, _runtimeBorderMat };
    }

    private Vector3 StartPosition =>
        arCamera != null ? arCamera.transform.position :
        xrOrigin != null ? xrOrigin.position : Vector3.zero;

    void Start()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (navigationGpsTracker == null)
            navigationGpsTracker = FindFirstObjectByType<SimpleGPSTracker>();
        EnsureMeshComponents();

        // Inspector-placed target or startup order vs MobileNavigationHUD: force first build once refs exist.
        if (targetNode != null)
        {
            forcePathRecalcAfterTargetChange = true;
            nextPathUpdateTime = 0f;
        }
    }

    /// <summary>After hybrid mode retags MainCamera on the detached outdoor rig, call this so the path uses the same feed as GPSMapPlane.</summary>
    public void RebindToDisplayCamera(Camera cam)
    {
        if (cam != null)
            arCamera = cam;
    }

    private void EnsureLiveArCamera()
    {
        if (arCamera != null && arCamera.isActiveAndEnabled)
            return;
        arCamera = Camera.main;
    }

    void Update()
    {
        if (targetNode == null)
        {
            PathHudDebugLine = "Path: chua gan dich";
            return;
        }

        EnsureLiveArCamera();

        if (arCamera == null && xrOrigin == null)
        {
            PathHudDebugLine = "Path: thieu AR Camera va XR Origin";
            return;
        }

        if (pathGeometryMode == PathGeometryMode.NavMeshRoute &&
            !prioritizePathVisibility &&
            NavigationGpsGateRuns)
        {
            if (navigationGpsTracker == null)
            {
                PathHudDebugLine = "Path: GPS gate [no_tracker]";
                ClearPath(resetThrottle: true);
                return;
            }

            if (navigationGpsTracker.TryGetPathNavigationBlock(out string reason))
            {
                bool ignoreNorthPending = allowPathWhileNorthAlignmentPending &&
                    string.Equals(reason, "north_pending", StringComparison.Ordinal);
                if (!ignoreNorthPending)
                {
                    PathHudDebugLine = $"Path: GPS gate [{reason}]";
                    ClearPath(resetThrottle: true);
                    return;
                }
            }
        }

        if (Time.time < nextPathUpdateTime)
        {
            if (string.IsNullOrEmpty(PathHudDebugLine) ||
                PathHudDebugLine.StartsWith("Path: da dat dich", StringComparison.Ordinal) ||
                PathHudDebugLine.StartsWith("Path: da gan dich", StringComparison.Ordinal))
            {
                PathHudDebugLine = "Path: doi pathUpdateInterval";
            }

            return;
        }

        Vector3 startPos = StartPosition;

        bool startMoved = Vector3.Distance(startPos, lastStartPos) >= minMoveDistanceMeters;
        bool endMoved = Vector3.Distance(targetNode.position, lastEndPos) >= minMoveDistanceMeters;
        if (!forcePathRecalcAfterTargetChange && !startMoved && !endMoved)
        {
            PathHudDebugLine = "Path: doi di chuyen (minMove) hoac doi dich";
            return;
        }

        forcePathRecalcAfterTargetChange = false;

        nextPathUpdateTime = Time.time + pathUpdateInterval;

        if (!TryUpdatePath())
        {
            // Do not commit last start/end on failure — otherwise minMove stays false and we never retry until the user walks.
            lastStartPos = Vector3.positiveInfinity;
            lastEndPos = Vector3.positiveInfinity;
        }
        else
        {
            lastStartPos = startPos;
            lastEndPos = targetNode.position;
        }
    }

    public void SetHarmonyGuidance(float alpha, bool visible, Color tint)
    {
        _harmonyAlpha = Mathf.Clamp01(alpha);
        _harmonyVisible = visible;
        _harmonyTint = tint;
    }

    private void LateUpdate()
    {
        bool show = _harmonyVisible && _harmonyAlpha > 0.001f && HasPath;
        if (meshRenderer != null)
        {
            meshRenderer.enabled = show && useMeshPath;
            ApplyHarmonyColor(meshRenderer);
        }
        if (_minimapMirrorRenderer != null)
        {
            _minimapMirrorRenderer.enabled = show && useMeshPath && showPathOnMinimap;
            ApplyHarmonyColor(_minimapMirrorRenderer);
        }
        if (line != null)
        {
            line.enabled = show && !useMeshPath;
            Color color = _harmonyTint;
            color.a *= _harmonyAlpha;
            line.startColor = color;
            line.endColor = color;
        }
#if DEVELOPMENT_BUILD
        LogPathHudChanges();
#endif
    }

    private void ApplyHarmonyColor(Renderer targetRenderer)
    {
        if (targetRenderer == null) return;
        _harmonyPropertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_harmonyPropertyBlock);
        Color color = _harmonyTint;
        color.a *= _harmonyAlpha;
        _harmonyPropertyBlock.SetColor("_Color", color);
        _harmonyPropertyBlock.SetColor("_BaseColor", color);
        targetRenderer.SetPropertyBlock(_harmonyPropertyBlock);
    }

#if DEVELOPMENT_BUILD
    private void LogPathHudChanges()
    {
        string line = PathHudDebugLine;
        if (string.Equals(line, _lastLoggedPathHudLine, StringComparison.Ordinal))
            return;

        _lastLoggedPathHudLine = line;

        if (string.IsNullOrEmpty(line) || line.StartsWith("Path: OK", StringComparison.Ordinal))
            return;

        Debug.Log("[ARPathFinder] " + line);
    }
#endif

    public void SetTarget(Transform newTarget)
    {
        targetNode = newTarget;
        lastEndPos = Vector3.positiveInfinity;
        lastStartPos = Vector3.positiveInfinity;
        nextPathUpdateTime = 0f;
        forcePathRecalcAfterTargetChange = true;
        _lastBuiltCorners = null;
        PathHudDebugLine = "Path: da dat dich, doi cap nhat...";
    }

    /// <summary>
    /// Material ribbon center (chevron xanh) — dùng để đồng bộ Multiset LineRenderer / shared style.
    /// </summary>
    public Material GetOrCreateSharedPathMaterial()
    {
        EnsurePathMaterials();
        return _runtimeCenterMat;
    }

    /// <returns>True if a route was drawn.</returns>
    private bool TryUpdatePath()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (path == null) path = new NavMeshPath();

        if ((!useMeshPath && line == null) || targetNode == null)
        {
            PathHudDebugLine = "Path: thieu LineRenderer hoac dich";
            ClearPath(resetThrottle: false);
            return false;
        }

        if (pathGeometryMode == PathGeometryMode.StraightWorldLine)
        {
            return TryApplyStraightWorldPath(StartPosition, targetNode.position);
        }

        if (TryBuildAndApplyNavMeshPath())
        {
            return true;
        }

        if (showStraightLineFallbackWhenNavMeshFails &&
            TryApplyStraightWorldPath(StartPosition, targetNode.position))
        {
            return true;
        }

        PathHudDebugLine = prioritizePathVisibility
            ? "Path: khong ve duoc (NavMesh + fallback tat)"
            : PathHudDebugLine;
        ClearPath(resetThrottle: false);
        return false;
    }

    private bool TryBuildAndApplyNavMeshPath()
    {
        Vector3 startWorld = StartPosition;
        Vector3 endWorld = targetNode.position;

        float[] radii = GetNavMeshSampleRadiiInOrder();
        foreach (float radius in radii)
        {
            bool startOnMesh = NavMesh.SamplePosition(startWorld, out NavMeshHit startHit, radius, NavMesh.AllAreas);
            bool endOnMesh = NavMesh.SamplePosition(endWorld, out NavMeshHit endHit, radius, NavMesh.AllAreas);
            if (!startOnMesh || !endOnMesh)
            {
                continue;
            }

            bool foundPath = NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);
            int cornerCount = path.corners != null ? path.corners.Length : 0;
            if (!foundPath || path.status == NavMeshPathStatus.PathInvalid || cornerCount < 2)
            {
                continue;
            }

            HasPath = path.status == NavMeshPathStatus.PathComplete;
            CurrentPathDistanceMeters = CalculatePathDistance(path);
            Vector3[] corners = path.corners;
            ApplyCornersToRenderers(corners, radius);
            return true;
        }

        string detail = $"r={navMeshSampleRadius:F1}m";
        if (prioritizePathVisibility && navMeshSampleRadiusExpanded > navMeshSampleRadius + 0.01f)
        {
            detail += $"+{navMeshSampleRadiusExpanded:F0}m";
        }

        PathHudDebugLine = $"Path: khong len NavMesh / Nav loi ({detail})";
        return false;
    }

    private float[] GetNavMeshSampleRadiiInOrder()
    {
        if (!prioritizePathVisibility || navMeshSampleRadiusExpanded <= navMeshSampleRadius + 0.01f)
        {
            return new[] { navMeshSampleRadius };
        }

        return new[] { navMeshSampleRadius, navMeshSampleRadiusExpanded };
    }

    private void ApplyCornersToRenderers(Vector3[] corners, float usedRadius)
    {
        // Làm tròn góc gấp trước khi dựng mesh/line (chỉ khi >= 3 điểm; đoạn thẳng 2 điểm bỏ qua).
        if (smoothCorners && corners != null && corners.Length >= 3)
            corners = NavMeshPathRibbon.SmoothCornersChaikin(corners, smoothIterations);

        // SKIP rebuild khi corners gần như giống lần trước (GPS noise → corner xê dịch vài cm).
        // Tránh flicker mesh khi user đứng yên hoặc đi chậm.
        if (CornersSimilarToCache(corners))
        {
            PathHudDebugLine = "Path: OK (cached)";
            return;
        }
        CacheCorners(corners);

        if (useMeshPath)
        {
            EnsureMeshComponents();
            Transform meshSpace = _pathMeshRoot != null ? _pathMeshRoot : transform;
            Func<Vector3, float> yOffsetFn;
            if (clampPathYToCameraFoot && arCamera != null)
            {
                float footY = arCamera.transform.position.y - cameraEyeToFootMeters;
                yOffsetFn = corner => (footY - corner.y) + pathHeightOffset;
            }
            else
            {
                yOffsetFn = _ => pathHeightOffset;
            }
            NavMeshPathRibbon.BuildMesh(
                pathMesh,
                meshSpace,
                corners,
                yOffsetFn,
                pathWidth,
                pathWidth,
                pathMetersPerTile,
                drawArrowHead,
                arrowHeadLengthMeters,
                arrowHeadWidthMeters,
                pathBorderWidthMeters,
                pathUvRepeatAcrossWidth);
            ApplyMaterialsForCurrentMesh();
            meshRenderer.enabled = true;

            // Mirror cho minimap: dùng chung mesh + material, chỉ khác độ cao (world +Y) + layer
            if (showPathOnMinimap && _minimapMirrorRenderer != null)
            {
                _minimapMirrorFilter.sharedMesh = pathMesh;
                _minimapMirrorRenderer.sharedMaterials = meshRenderer.sharedMaterials;
                // Re-apply offset world +Y (phòng _pathMeshRoot xoay lúc runtime)
                _minimapMirrorRenderer.transform.localPosition =
                    _pathMeshRoot.InverseTransformVector(Vector3.up * minimapPathLiftMeters);
                SetMinimapMirrorActive(true);
            }

            if (line != null)
            {
                line.positionCount = 0;
                line.enabled = false;
            }

            string completeFlag = HasPath ? "complete" : "partial";
            string radiusHint = usedRadius > 0.01f ? $" · sample≤{usedRadius:F0}m" : string.Empty;
            PathHudDebugLine = $"Path: OK mesh · ~{CurrentPathDistanceMeters:F0}m · {corners.Length} pts · {completeFlag}{radiusHint}";
        }
        else
        {
            if (pathMesh != null)
                pathMesh.Clear();
            if (meshRenderer != null)
                meshRenderer.enabled = false;

            line.enabled = true;
            line.positionCount = corners.Length;
            line.SetPositions(corners);

            PathHudDebugLine = $"Path: OK line · ~{CurrentPathDistanceMeters:F0}m · {corners.Length} pts" +
                (usedRadius > 0.01f ? $" · sample≤{usedRadius:F0}m" : string.Empty);
        }
    }

    /// <summary>Exact world-space segment from start to target (plus mesh height offset in ribbon builder).</summary>
    private bool TryApplyStraightWorldPath(Vector3 startWorld, Vector3 endWorld)
    {
        Vector3[] corners = { startWorld, endWorld };

        HasPath = true;
        CurrentPathDistanceMeters = Vector3.Distance(startWorld, endWorld);

        ApplyCornersToRenderers(corners, 0f);
        PathHudDebugLine = $"Path: OK thang · {CurrentPathDistanceMeters:F0}m";
        return true;
    }

    private void ClearPath(bool resetThrottle)
    {
        HasPath = false;
        CurrentPathDistanceMeters = 0f;
        _lastBuiltCorners = null;
        if (line != null)
        {
            line.positionCount = 0;
            if (useMeshPath)
                line.enabled = false;
        }

        if (pathMesh != null)
            pathMesh.Clear();
        if (meshRenderer != null && useMeshPath)
            meshRenderer.enabled = false;

        // Tắt hẳn MinimapPathMirror GO (không chỉ disable renderer) — đến nơi / clear dest
        // để minimap không còn thấy đường kẻ.
        SetMinimapMirrorActive(false);

        if (resetThrottle)
        {
            lastStartPos = Vector3.positiveInfinity;
            lastEndPos = Vector3.positiveInfinity;
            nextPathUpdateTime = 0f;
        }
    }

    /// <summary>
    /// Bật/tắt GameObject MinimapPathMirror + MeshRenderer.
    /// Clear dest / đến nơi → false; có path mới → true.
    /// </summary>
    private void SetMinimapMirrorActive(bool active)
    {
        // Resolve GO kể cả khi field renderer chưa gán (scene đã có child sẵn).
        Transform mirrorTf = null;
        if (_minimapMirrorRenderer != null)
            mirrorTf = _minimapMirrorRenderer.transform;
        else if (_pathMeshRoot != null)
            mirrorTf = _pathMeshRoot.Find("MinimapPathMirror");

        if (mirrorTf == null && pathMesh != null)
        {
            // Fallback: tìm theo tên dưới component
            var found = transform.Find(PathMeshRootName + "/MinimapPathMirror");
            if (found != null) mirrorTf = found;
        }

        if (mirrorTf != null && mirrorTf.gameObject.activeSelf != active)
            mirrorTf.gameObject.SetActive(active);

        if (_minimapMirrorRenderer != null)
            _minimapMirrorRenderer.enabled = active;

        // Nếu bật lại nhưng renderer ref mất — re-ensure khi showPathOnMinimap.
        if (active && showPathOnMinimap && _minimapMirrorRenderer == null)
        {
            EnsureMinimapMirror();
            if (_minimapMirrorRenderer != null)
            {
                _minimapMirrorRenderer.gameObject.SetActive(true);
                _minimapMirrorRenderer.enabled = true;
            }
        }
    }

    /// <summary>Public: tắt path + MinimapPathMirror (gọi từ ArrivalWatcher khi đến nơi).</summary>
    public void ClearNavigationVisuals()
    {
        SetTarget(null);
        ClearPath(resetThrottle: true);
        // Force-disable mọi MinimapPathMirror con (phòng instance lạ).
        if (_pathMeshRoot != null)
        {
            for (int i = 0; i < _pathMeshRoot.childCount; i++)
            {
                var c = _pathMeshRoot.GetChild(i);
                if (c != null && c.name == "MinimapPathMirror")
                    c.gameObject.SetActive(false);
            }
        }
    }

    private static float CalculatePathDistance(NavMeshPath navPath)
    {
        float distance = 0f;
        Vector3[] corners = navPath.corners;
        for (int i = 1; i < corners.Length; i++)
            distance += Vector3.Distance(corners[i - 1], corners[i]);
        return distance;
    }

    /// <summary>
    /// True nếu corners mới gần như trùng corners đã build (≤ CornerSimilarityThresholdMeters mỗi điểm).
    /// Tránh rebuild mesh khi GPS noise làm path xê dịch vài cm → hết flicker.
    /// </summary>
    private bool CornersSimilarToCache(Vector3[] newCorners)
    {
        if (_lastBuiltCorners == null || newCorners == null) return false;
        if (_lastBuiltCorners.Length != newCorners.Length) return false;
        for (int i = 0; i < newCorners.Length; i++)
        {
            if (Vector3.Distance(_lastBuiltCorners[i], newCorners[i]) > cornerSimilarityThresholdMeters)
                return false;
        }
        return true;
    }

    private void CacheCorners(Vector3[] corners)
    {
        if (corners == null) { _lastBuiltCorners = null; return; }
        if (_lastBuiltCorners == null || _lastBuiltCorners.Length != corners.Length)
            _lastBuiltCorners = new Vector3[corners.Length];
        System.Array.Copy(corners, _lastBuiltCorners, corners.Length);
    }
}
