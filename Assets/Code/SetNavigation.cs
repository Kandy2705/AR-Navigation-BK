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

    private NavMeshPath path;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Vector3[] lastCorners;

    void Start()
    {
        path = new NavMeshPath();

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        mesh = new Mesh();
        meshFilter.mesh = mesh;

        // dùng Unlit shader để không bị ảnh hưởng ánh sáng và không xoay
        if (pathMaterial != null)
            meshRenderer.material = pathMaterial;
        else
            meshRenderer.material = new Material(Shader.Find("Unlit/Color")) { color = fallbackColor };
    }

    void Update()
    {
        if (!GlobalProperties.Instance.IsShowNavigation)
        {
            mesh.Clear();
            meshRenderer.enabled = false;
            return;
        }

        meshRenderer.enabled = true;

        if (navTargetObject == null || markerObject == null)
        {
            mesh.Clear();
            return;
        }

        // Ensure both start and end are on the NavMesh by sampling nearby points
        const float sampleDistance = 2.0f;
        NavMeshHit startHit, endHit;
        bool haveStart = NavMesh.SamplePosition(markerObject.transform.position, out startHit, sampleDistance, NavMesh.AllAreas);
        bool haveEnd = NavMesh.SamplePosition(navTargetObject.transform.position, out endHit, sampleDistance, NavMesh.AllAreas);

        if (!haveStart || !haveEnd)
        {
            // one or both points are off the NavMesh -> no path
            mesh.Clear();
            return;
        }

        // Calculate path using sampled positions on the NavMesh
        NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);

        // If path isn't complete or has too few corners, clear and return
        if (path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length < 2)
        {
            mesh.Clear();
            lastCorners = null;
            return;
        }

        // Only rebuild mesh when path corners actually changed
        if (!CornersEqual(path.corners, lastCorners))
        {
            lastCorners = (Vector3[])path.corners.Clone();
            BuildPathMesh(path.corners);
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

        // 2 vertex cho mỗi corner: left, right
        Vector3[] verts = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        List<int> tris = new List<int>((n - 1) * 6);
        float dist = 0f;

        // Tạo các cặp left/right cho từng corner (world space)
        for (int i = 0; i < n; i++)
        {
            Vector3 worldPos = corners[i];
            // dính nhẹ lên mặt đất
            worldPos.y += lineHeightOffset;

            // hướng: nếu last corner, lấy hướng từ prev->cur; nếu first, từ cur->next; else trung bình
            Vector3 forward;
            if (i == 0) forward = (corners[1] - corners[0]).normalized;
            else if (i == n - 1) forward = (corners[n - 1] - corners[n - 2]).normalized;
            else
            {
                Vector3 a = (corners[i] - corners[i - 1]).normalized;
                Vector3 b = (corners[i + 1] - corners[i]).normalized;
                forward = (a + b).normalized;
                if (forward.sqrMagnitude < 0.0001f) forward = b; // fallback
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // width interpolation along whole path
            float t = (float)i / (n - 1);
            float width = Mathf.Lerp(startWidth, endWidth, t) * 0.5f;

            Vector3 leftWorld = worldPos - right * width;
            Vector3 rightWorld = worldPos + right * width;

      
            verts[i * 2 + 0] = transform.InverseTransformPoint(leftWorld);
            verts[i * 2 + 1] = transform.InverseTransformPoint(rightWorld);

            if (i > 0) dist += Vector3.Distance(corners[i - 1], corners[i]);
            float v = dist / metersPerTile;     // v > 1 => tự repeat
            uvs[i * 2 + 0] = new Vector2(0, v);
            uvs[i * 2 + 1] = new Vector2(1, v);
        }

        // Build triangles between consecutive corner pairs
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

}

