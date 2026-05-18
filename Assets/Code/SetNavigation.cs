using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SetNavigation : MonoBehaviour
{
    [SerializeField]
    private Camera topDownCamera;

    [SerializeField]
    private GameObject navTargetObject;

    [SerializeField]
    private GameObject markerObject;

    [Header("GPS Validity Gate")]
    [SerializeField]
    private GPSMarker gpsMarker;

    [Tooltip("Hide navigation if the last good GPS fix is farther than this from the reference/map origin (meters).")]
    [SerializeField]
    private float maxGpsDistanceFromReferenceMeters = 250f;

    [Tooltip("Hide navigation if the last GPS fix was rejected as a jump (prevents unrealistic route when GPS is far/off-map).")]
    [SerializeField]
    private bool hidePathWhenGpsJumpRejected = true;

    [SerializeField]
    private GameObject requiredActiveRoot;

    [SerializeField]
    private bool requireReferencesActive = true;

    [SerializeField]
    private float startWidth = 0.2f;

    [SerializeField]
    private float endWidth = 0.2f;

    [SerializeField]
    private Material pathMaterial;

    [SerializeField]
    private Color fallbackColor = new Color(0f, 0.9f, 1f, 1f);

    [SerializeField]
    private float metersPerTile = 1f;

    [SerializeField]
    private float lineHeightOffset = -1.2f;

    [Header("Visibility (AR camera)")]
    [Tooltip("Lift the navigation line relative to the camera so it stays visible at different phone heights.")]
    [SerializeField]
    private bool useCameraRelativeHeight = false;

    [Tooltip("Camera used for camera-relative height. If null, Camera.main will be used.")]
    [SerializeField]
    private Camera heightReferenceCamera;

    [Tooltip("Target line height relative to the reference camera (meters). Negative = below camera.")]
    [SerializeField]
    private float heightRelativeToCameraMeters = -1.0f;

    [Tooltip("Clamp additional lift (meters) added on top of Line Height Offset.")]
    [SerializeField]
    private Vector2 cameraRelativeLiftClampMeters = new Vector2(0.0f, 2.0f);

    [Header("AR path draw order")]
    [Tooltip("If true, path uses transparent queue + ZTest Always (always visible, but can look 'in your face'). If false, path respects depth so it can sit on the ground better.")]
    [SerializeField]
    private bool pathAlwaysOnTop = false;

    [SerializeField]
    private bool showLineHeightSlider = false;

    [SerializeField]
    private float lineHeightMin = -0.5f;

    [SerializeField]
    private float lineHeightMax = 0.5f;

    [SerializeField]
    private float lineHeightSliderWidthPercent = 0.7f;

    [SerializeField]
    private float lineHeightSliderHeight = 24f;

    [SerializeField]
    private float lineHeightSliderYOffset = 120f;

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugLogs = false;

    [SerializeField]
    private float debugRepeatInterval = 1.5f;

    [Header("Performance")]
    [SerializeField]
    private float pathUpdateInterval = 0.35f;

    [SerializeField]
    private float navMeshSampleDistance = 2.0f;

    [Header("Optional LineRenderer Output")]
    [SerializeField]
    private LineRenderer lineRenderer;

    [Tooltip("When true, also drive the LineRenderer (if assigned) from path corners.")]
    [SerializeField]
    private bool renderWithLineRenderer = false;

    private NavMeshPath path;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Vector3[] lastCorners;
    private string lastDebugState;
    private float lastDebugLogTime;
    private float lastPathUpdateTime = -999f;

    void Start()
    {
        path = new NavMeshPath();

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;

        if (pathMaterial != null)
        {
            meshRenderer.material = pathMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            meshRenderer.material = new Material(shader) { color = fallbackColor };
        }

        NavigationPathMaterialHelper.Configure(meshRenderer.material, pathAlwaysOnTop);

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            ConfigureLineRenderer(lineRenderer);
        }

        LogState(
            "START_CONFIG",
            () => $"topDownCamera={(topDownCamera != null ? topDownCamera.name : "null")}, " +
                  $"markerRef={(markerObject != null ? markerObject.name : "null")}, " +
                  $"targetRef={(navTargetObject != null ? navTargetObject.name : "null")}, " +
                  $"startWidth={startWidth:0.###}, endWidth={endWidth:0.###}, " +
                  $"lineHeightOffset={lineHeightOffset:0.###}, metersPerTile={metersPerTile:0.###}, " +
                  $"sampleDistance={navMeshSampleDistance:0.##}, pathUpdateInterval={pathUpdateInterval:0.##}");

        if (gpsMarker == null)
        {
            gpsMarker = FindFirstObjectByType<GPSMarker>(FindObjectsInactive.Include);
        }
    }

    void Update()
    {
        if (!IsGpsStateValidForNavigation())
        {
            HidePath("GPS_INVALID", "GPS is out-of-range or last fix was rejected as jump");
            return;
        }

        if (!CanRenderNavigation())
        {
            HidePath("INACTIVE_CONTEXT", "Required root, marker, or target is inactive");
            return;
        }

        if (GlobalProperties.Instance == null)
        {
            HidePath("MISSING_GLOBAL_PROPERTIES", "GlobalProperties.Instance is null");
            return;
        }

        if (!GlobalProperties.Instance.IsShowNavigation)
        {
            HidePath("NAVIGATION_HIDDEN", "GlobalProperties.IsShowNavigation == false");
            return;
        }

        meshRenderer.enabled = true;

        if (navTargetObject == null || markerObject == null)
        {
            HidePath(
                "MISSING_REFERENCE",
                $"markerObject null={markerObject == null}, navTargetObject null={navTargetObject == null}, " +
                $"markerRef={(markerObject != null ? markerObject.name : "null")}, " +
                $"targetRef={(navTargetObject != null ? navTargetObject.name : "null")}");
            return;
        }

        float interval = Mathf.Max(0.02f, pathUpdateInterval);
        if (Time.time - lastPathUpdateTime < interval)
        {
            return;
        }

        lastPathUpdateTime = Time.time;

        float sampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        NavMeshHit startHit, endHit;
        bool haveStart = NavMesh.SamplePosition(markerObject.transform.position, out startHit, sampleDistance, NavMesh.AllAreas);
        bool haveEnd = NavMesh.SamplePosition(navTargetObject.transform.position, out endHit, sampleDistance, NavMesh.AllAreas);

        if (!haveStart || !haveEnd)
        {
            mesh.Clear();
            lastCorners = null; // force rebuild when coming back on NavMesh
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
            }
            LogState(
                "OFF_NAVMESH",
                () => $"haveStart={haveStart}, haveEnd={haveEnd}, sampleDistance={sampleDistance:0.00}, " +
                      $"{DescribeTransform(markerObject.transform, "marker")} | " +
                      $"{DescribeTransform(navTargetObject.transform, "target")} | " +
                      $"{NearestNavMeshInfo(markerObject.transform.position, "marker", sampleDistance)} | " +
                      $"{NearestNavMeshInfo(navTargetObject.transform.position, "target", sampleDistance)} | " +
                      $"{NearestNavMeshInfo(markerObject.transform.position, "marker", 20f)} | " +
                      $"{NearestNavMeshInfo(navTargetObject.transform.position, "target", 20f)} | " +
                      $"{NearestNavMeshInfo(markerObject.transform.position, "marker", 1000f)} | " +
                      $"{NearestNavMeshInfo(navTargetObject.transform.position, "target", 1000f)}");
            return;
        }

        NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);

        if (path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length < 2)
        {
            mesh.Clear();
            lastCorners = null;
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
            }
            int corners = path.corners == null ? 0 : path.corners.Length;
            LogState(
                "PATH_NOT_COMPLETE",
                () => $"status={path.status}, corners={corners}, startHit={startHit.position}, endHit={endHit.position}, " +
                      $"{DescribeTransform(markerObject.transform, "marker")} | " +
                      $"{DescribeTransform(navTargetObject.transform, "target")}");
            return;
        }

        if (!CornersEqual(path.corners, lastCorners))
        {
            lastCorners = (Vector3[])path.corners.Clone();
            BuildPathMesh(path.corners);
            UpdateLineRenderer(path.corners);
            LogState(
                "PATH_RENDERED",
                () => $"status={path.status}, corners={path.corners.Length}, lineHeightOffset={lineHeightOffset:0.00}, " +
                      $"start={path.corners[0]}, end={path.corners[path.corners.Length - 1]}, " +
                      $"markerToStart={Vector3.Distance(markerObject.transform.position, startHit.position):0.###}, " +
                      $"targetToEnd={Vector3.Distance(navTargetObject.transform.position, endHit.position):0.###}");
        }
    }

    private float GetEffectiveHeightOffset(float cornerWorldY)
    {
        float baseOffset = lineHeightOffset;

        if (!useCameraRelativeHeight)
        {
            return baseOffset;
        }

        Camera cam = heightReferenceCamera != null ? heightReferenceCamera : Camera.main;
        if (cam == null)
        {
            return baseOffset;
        }

        // We want: (cornerWorldY + offset) ~= (cameraY + heightRelativeToCameraMeters)
        float desiredY = cam.transform.position.y + heightRelativeToCameraMeters;
        float desiredExtraLift = desiredY - (cornerWorldY + baseOffset);
        float extraLift = Mathf.Clamp(desiredExtraLift, cameraRelativeLiftClampMeters.x, cameraRelativeLiftClampMeters.y);
        return baseOffset + extraLift;
    }

    private void OnDisable()
    {
        if (mesh != null)
        {
            mesh.Clear();
        }

        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
    }

    private bool CanRenderNavigation()
    {
        if (requiredActiveRoot != null && !requiredActiveRoot.activeInHierarchy)
        {
            return false;
        }

        if (!requireReferencesActive)
        {
            return true;
        }

        if (markerObject != null && !markerObject.activeInHierarchy)
        {
            return false;
        }

        return navTargetObject == null || navTargetObject.activeInHierarchy;
    }

    private bool IsGpsStateValidForNavigation()
    {
        if (gpsMarker == null)
        {
            return true;
        }

        if (hidePathWhenGpsJumpRejected && gpsMarker.LastFixRejectedAsJump)
        {
            return false;
        }

        float maxRange = Mathf.Max(0f, maxGpsDistanceFromReferenceMeters);
        if (maxRange <= 0f)
        {
            return true;
        }

        float distance = gpsMarker.LastEnuDistanceFromRefMeters;
        if (distance > maxRange)
        {
            return false;
        }

        return true;
    }

    private void HidePath(string state, string detail)
    {
        if (mesh != null)
        {
            mesh.Clear();
        }

        // Important: when we hide/clear the path due to temporary invalid state (OFF_NAVMESH, GPS_INVALID, etc.),
        // reset cached corners so the next valid update rebuilds the mesh even if the path corners match the last one.
        lastCorners = null;

        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        LogState(state, detail);
    }

    private void OnGUI()
    {
        // IMGUI debug slider intentionally disabled.
        return;
    }

    void BuildPathMesh(Vector3[] corners)
    {
        NavMeshPathRibbon.BuildMesh(
            mesh,
            transform,
            corners,
            wp => GetEffectiveHeightOffset(wp.y),
            startWidth,
            endWidth,
            metersPerTile,
            addArrowHead: false,
            arrowLengthMeters: 0f,
            arrowWidthMeters: 0f);
    }

    private bool CornersEqual(Vector3[] a, Vector3[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private void UpdateLineRenderer(Vector3[] corners)
    {
        if (!renderWithLineRenderer || lineRenderer == null || corners == null || corners.Length < 2)
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
            }
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = corners.Length;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 p = corners[i];
            p.y += GetEffectiveHeightOffset(p.y);
            lineRenderer.SetPosition(i, p);
        }
    }

    private static void ConfigureLineRenderer(LineRenderer lr)
    {
        if (lr == null) return;
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View; // always face camera
        lr.textureMode = LineTextureMode.Tile;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 6;
    }


    private void LogState(string state, string detail)
    {
        if (!ShouldLogState(state))
        {
            return;
        }

        Debug.Log($"[SetNavigation:{name}] {state} | {detail}", this);
        lastDebugState = state;
        lastDebugLogTime = Time.time;
    }

    private void LogState(string state, Func<string> detailFactory)
    {
        if (!ShouldLogState(state))
        {
            return;
        }

        string detail = detailFactory != null ? detailFactory() : string.Empty;
        Debug.Log($"[SetNavigation:{name}] {state} | {detail}", this);
        lastDebugState = state;
        lastDebugLogTime = Time.time;
    }

    private bool ShouldLogState(string state)
    {
        if (!enableDebugLogs)
        {
            return false;
        }

        if (string.IsNullOrEmpty(state))
        {
            return false;
        }

        float repeat = Mathf.Max(0.1f, debugRepeatInterval);
        bool shouldLog = state != lastDebugState || (Time.time - lastDebugLogTime) >= repeat;
        if (!shouldLog)
        {
            return false;
        }

        return true;
    }

    private static string DescribeTransform(Transform t, string label)
    {
        if (t == null)
        {
            return $"{label}=null";
        }

        string parentName = t.parent != null ? t.parent.name : "null";
        string path = GetHierarchyPath(t);
        return
            $"{label}[name={t.name}, id={t.gameObject.GetInstanceID()}, " +
            $"local={t.localPosition}, world={t.position}, parent={parentName}, path={path}]";
    }

    private static string NearestNavMeshInfo(Vector3 position, string label, float maxDistance = 1000f)
    {
        if (NavMesh.SamplePosition(position, out var hit, maxDistance, NavMesh.AllAreas))
        {
            float delta = Vector3.Distance(position, hit.position);
            return $"{label}NearestNavMesh[r={maxDistance:0.##}, hit={hit.position}, delta={delta:0.###}]";
        }

        return $"{label}NearestNavMesh[r={maxDistance:0.##}, not-found]";
    }

    private static string GetHierarchyPath(Transform t)
    {
        var names = new List<string>();
        Transform current = t;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
