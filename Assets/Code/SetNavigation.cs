using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SetNavigation : MonoBehaviour
{
    [SerializeField]
    private Camera topDownCamera;

    [SerializeField]
    private GameObject navTargetObject;

    [SerializeField]
    private GameObject markerObject;

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
    private float lineHeightOffset = -0.2f;

    [SerializeField]
    private bool showLineHeightSlider = true;

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

    private NavMeshPath path;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Vector3[] lastCorners;
    private string lastDebugState;
    private float lastDebugLogTime;

    void Start()
    {
        path = new NavMeshPath();

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;

        if (pathMaterial != null)
            meshRenderer.material = pathMaterial;
        else
            meshRenderer.material = new Material(Shader.Find("Unlit/Color")) { color = fallbackColor };

        LogState(
            "START_CONFIG",
            () => $"topDownCamera={(topDownCamera != null ? topDownCamera.name : "null")}, " +
                  $"markerRef={(markerObject != null ? markerObject.name : "null")}, " +
                  $"targetRef={(navTargetObject != null ? navTargetObject.name : "null")}, " +
                  $"startWidth={startWidth:0.###}, endWidth={endWidth:0.###}, " +
                  $"lineHeightOffset={lineHeightOffset:0.###}, metersPerTile={metersPerTile:0.###}, sampleDistance=2.00");
    }

    void Update()
    {
        if (GlobalProperties.Instance == null)
        {
            mesh.Clear();
            meshRenderer.enabled = false;
            LogState("MISSING_GLOBAL_PROPERTIES", "GlobalProperties.Instance is null");
            return;
        }

        if (!GlobalProperties.Instance.IsShowNavigation)
        {
            mesh.Clear();
            meshRenderer.enabled = false;
            LogState("NAVIGATION_HIDDEN", "GlobalProperties.IsShowNavigation == false");
            return;
        }

        meshRenderer.enabled = true;

        if (navTargetObject == null || markerObject == null)
        {
            mesh.Clear();
            LogState(
                "MISSING_REFERENCE",
                () => $"markerObject null={markerObject == null}, navTargetObject null={navTargetObject == null}, " +
                      $"markerRef={(markerObject != null ? markerObject.name : "null")}, " +
                      $"targetRef={(navTargetObject != null ? navTargetObject.name : "null")}");
            return;
        }

        const float sampleDistance = 2.0f;
        NavMeshHit startHit, endHit;
        bool haveStart = NavMesh.SamplePosition(markerObject.transform.position, out startHit, sampleDistance, NavMesh.AllAreas);
        bool haveEnd = NavMesh.SamplePosition(navTargetObject.transform.position, out endHit, sampleDistance, NavMesh.AllAreas);

        if (!haveStart || !haveEnd)
        {
            mesh.Clear();
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
            LogState(
                "PATH_RENDERED",
                () => $"status={path.status}, corners={path.corners.Length}, lineHeightOffset={lineHeightOffset:0.00}, " +
                      $"start={path.corners[0]}, end={path.corners[path.corners.Length - 1]}, " +
                      $"markerToStart={Vector3.Distance(markerObject.transform.position, startHit.position):0.###}, " +
                      $"targetToEnd={Vector3.Distance(navTargetObject.transform.position, endHit.position):0.###}");
        }
    }

    private void OnGUI()
    {
        if (!showLineHeightSlider)
        {
            return;
        }

        float clampedWidthPercent = Mathf.Clamp(lineHeightSliderWidthPercent, 0.1f, 1f);
        float sliderWidth = Screen.width * clampedWidthPercent;
        float sliderX = (Screen.width - sliderWidth) * 0.5f;
        float sliderY = lineHeightSliderYOffset;
        Rect labelRect = new Rect(sliderX - 10f, sliderY - 28f, sliderWidth + 20f, 24f);
        GUI.Label(labelRect, $"Line Height: {lineHeightOffset:0.00}m");
        lineHeightOffset = GUI.HorizontalSlider(
            new Rect(sliderX, sliderY, sliderWidth, lineHeightSliderHeight),
            lineHeightOffset,
            lineHeightMin,
            lineHeightMax);
    }

    void BuildPathMesh(Vector3[] corners)
    {
        mesh.Clear();

        int n = corners.Length;
        if (n < 2) return;

        Vector3[] verts = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        List<int> tris = new List<int>((n - 1) * 6);
        float dist = 0f;

        for (int i = 0; i < n; i++)
        {
            Vector3 worldPos = corners[i];
            worldPos.y += lineHeightOffset;

            Vector3 forward;
            if (i == 0) forward = (corners[1] - corners[0]).normalized;
            else if (i == n - 1) forward = (corners[n - 1] - corners[n - 2]).normalized;
            else
            {
                Vector3 a = (corners[i] - corners[i - 1]).normalized;
                Vector3 b = (corners[i + 1] - corners[i]).normalized;
                forward = (a + b).normalized;
                if (forward.sqrMagnitude < 0.0001f) forward = b;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float t = (float)i / (n - 1);
            float width = Mathf.Lerp(startWidth, endWidth, t) * 0.5f;

            Vector3 leftWorld = worldPos - right * width;
            Vector3 rightWorld = worldPos + right * width;

            verts[i * 2 + 0] = transform.InverseTransformPoint(leftWorld);
            verts[i * 2 + 1] = transform.InverseTransformPoint(rightWorld);

            if (i > 0) dist += Vector3.Distance(corners[i - 1], corners[i]);
            float v = dist / metersPerTile;
            uvs[i * 2 + 0] = new Vector2(0, v);
            uvs[i * 2 + 1] = new Vector2(1, v);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = (i + 1) * 2;
            int i3 = i2 + 1;

            tris.Add(i0);
            tris.Add(i2);
            tris.Add(i1);

            tris.Add(i2);
            tris.Add(i3);
            tris.Add(i1);
        }

        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.MarkDynamic();
    }

    private bool CornersEqual(Vector3[] a, Vector3[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
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
