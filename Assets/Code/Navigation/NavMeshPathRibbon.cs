using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a horizontal strip mesh along NavMesh path corners + optional flat arrow head at the destination.
/// Vertices are stored in <paramref name="localSpace"/> local coordinates (same convention as SetNavigation).
/// With positive <paramref name="borderWidthMeters"/>, builds three parallel strips (white | center | white) for submeshes 0–2.
/// </summary>
public static class NavMeshPathRibbon
{
    /// <param name="worldYOffset">Extra Y added in world space after corner position (e.g. lift above NavMesh).</param>
    /// <param name="borderWidthMeters">If &lt;= 0, single full-width strip (one submesh). If &gt; 0, left/right border strips in meters.</param>
    public static void BuildMesh(
        Mesh mesh,
        Transform localSpace,
        Vector3[] corners,
        Func<Vector3, float> worldYOffset,
        float startWidth,
        float endWidth,
        float metersPerTile,
        bool addArrowHead,
        float arrowLengthMeters,
        float arrowWidthMeters,
        float borderWidthMeters = 0f,
        float uvScaleAcrossStrip = 1f)
    {
        mesh.Clear();

        int n = corners != null ? corners.Length : 0;
        if (n < 2)
            return;

        if (borderWidthMeters <= 1e-4f)
        {
            BuildMeshSingleStrip(mesh, localSpace, corners, worldYOffset, startWidth, endWidth, metersPerTile,
                addArrowHead, arrowLengthMeters, arrowWidthMeters, uvScaleAcrossStrip);
            return;
        }

        BuildMeshThreeStrip(mesh, localSpace, corners, worldYOffset, startWidth, endWidth, metersPerTile,
            addArrowHead, arrowLengthMeters, arrowWidthMeters, borderWidthMeters, uvScaleAcrossStrip);
    }

    private static void BuildMeshSingleStrip(
        Mesh mesh,
        Transform localSpace,
        Vector3[] corners,
        Func<Vector3, float> worldYOffset,
        float startWidth,
        float endWidth,
        float metersPerTile,
        bool addArrowHead,
        float arrowLengthMeters,
        float arrowWidthMeters,
        float uvScaleAcrossStrip)
    {
        int n = corners.Length;
        var vertsList = new List<Vector3>(n * 2 + 3);
        var uvsList = new List<Vector2>(n * 2 + 3);
        var trisList = new List<int>((n - 1) * 6 + 3);

        float dist = AppendRibbonStripRows(localSpace, corners, worldYOffset, startWidth, endWidth, metersPerTile,
            vertsList, uvsList, trisList, uvScaleAcrossStrip);

        AppendArrowHeadIfNeeded(localSpace, corners, worldYOffset, addArrowHead, arrowLengthMeters, arrowWidthMeters,
            metersPerTile, dist, vertsList, uvsList, trisList);

        FinalizeMesh(mesh, vertsList, uvsList, trisList, subMeshCount: 1);
    }

    private static void BuildMeshThreeStrip(
        Mesh mesh,
        Transform localSpace,
        Vector3[] corners,
        Func<Vector3, float> worldYOffset,
        float startWidth,
        float endWidth,
        float metersPerTile,
        bool addArrowHead,
        float arrowLengthMeters,
        float arrowWidthMeters,
        float borderWidthMeters,
        float uvScaleAcrossStrip)
    {
        int n = corners.Length;
        var vertsList = new List<Vector3>(n * 6 + 9);
        var uvsList = new List<Vector2>(n * 6 + 9);
        var trisLeft = new List<int>((n - 1) * 6);
        var trisCenter = new List<int>((n - 1) * 6);
        var trisRight = new List<int>((n - 1) * 6);

        float dist = 0f;

        for (int i = 0; i < n; i++)
        {
            if (i > 0)
                dist += Vector3.Distance(corners[i - 1], corners[i]);

            Vector3 worldPos = corners[i];
            worldPos.y += worldYOffset(worldPos);

            Vector3 forward = GetSmoothedForward(corners, i, n);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float t = (float)i / (n - 1);
            float halfW = Mathf.Lerp(startWidth, endWidth, t) * 0.5f;
            float b = Mathf.Clamp(borderWidthMeters, 1e-4f, Mathf.Max(1e-4f, halfW - 1e-3f));
            if (b >= halfW * 0.49f) b = halfW * 0.2f;

            float innerHalf = halfW - b;

            Vector3 p0 = worldPos - right * halfW;
            Vector3 p1 = worldPos - right * innerHalf;
            Vector3 p2 = worldPos - right * innerHalf;
            Vector3 p3 = worldPos + right * innerHalf;
            Vector3 p4 = worldPos + right * innerHalf;
            Vector3 p5 = worldPos + right * halfW;

            int row = i * 6;
            AddVert(localSpace, vertsList, uvsList, p0, 0f, dist, metersPerTile, uvScaleAcrossStrip);
            AddVert(localSpace, vertsList, uvsList, p1, 0.25f, dist, metersPerTile, uvScaleAcrossStrip);
            AddVert(localSpace, vertsList, uvsList, p2, 0.26f, dist, metersPerTile, uvScaleAcrossStrip);
            AddVert(localSpace, vertsList, uvsList, p3, 0.74f, dist, metersPerTile, uvScaleAcrossStrip);
            AddVert(localSpace, vertsList, uvsList, p4, 0.75f, dist, metersPerTile, uvScaleAcrossStrip);
            AddVert(localSpace, vertsList, uvsList, p5, 1f, dist, metersPerTile, uvScaleAcrossStrip);

            if (i > 0)
            {
                int prev = (i - 1) * 6;
                Quad(trisLeft, prev + 0, prev + 1, row + 0, row + 1);
                Quad(trisCenter, prev + 2, prev + 3, row + 2, row + 3);
                Quad(trisRight, prev + 4, prev + 5, row + 4, row + 5);
            }
        }

        if (addArrowHead && n >= 2 && arrowLengthMeters > 0.01f && arrowWidthMeters > 0.01f)
            AppendCenterArrowHeadThreeStrip(localSpace, corners, worldYOffset, arrowHeadLengthMeters: arrowLengthMeters,
                arrowHeadWidthMeters: arrowWidthMeters, metersPerTile, dist, vertsList, uvsList, trisCenter);

        mesh.SetVertices(vertsList);
        mesh.SetUVs(0, uvsList);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(trisLeft, 0);
        mesh.SetTriangles(trisCenter, 1);
        mesh.SetTriangles(trisRight, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.MarkDynamic();
    }

    private static void AddVert(Transform localSpace, List<Vector3> verts, List<Vector2> uvs, Vector3 world,
        float u, float distAlong, float metersPerTile, float uvScaleAcrossStrip)
    {
        verts.Add(localSpace.InverseTransformPoint(world));
        float v = distAlong / Mathf.Max(1e-4f, metersPerTile);
        uvs.Add(new Vector2(u * Mathf.Max(0.01f, uvScaleAcrossStrip), v));
    }

    private static void Quad(List<int> tris, int a0, int a1, int b0, int b1)
    {
        tris.Add(a0);
        tris.Add(b0);
        tris.Add(a1);
        tris.Add(b0);
        tris.Add(b1);
        tris.Add(a1);
    }

    private static float AppendRibbonStripRows(
        Transform localSpace,
        Vector3[] corners,
        Func<Vector3, float> worldYOffset,
        float startWidth,
        float endWidth,
        float metersPerTile,
        List<Vector3> vertsList,
        List<Vector2> uvsList,
        List<int> trisList,
        float uvScaleAcrossFullWidth)
    {
        int n = corners.Length;
        float dist = 0f;

        for (int i = 0; i < n; i++)
        {
            Vector3 worldPos = corners[i];
            worldPos.y += worldYOffset(worldPos);

            Vector3 forward = GetSmoothedForward(corners, i, n);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float t = (float)i / (n - 1);
            float halfW = Mathf.Lerp(startWidth, endWidth, t) * 0.5f;

            Vector3 leftWorld = worldPos - right * halfW;
            Vector3 rightWorld = worldPos + right * halfW;

            vertsList.Add(localSpace.InverseTransformPoint(leftWorld));
            vertsList.Add(localSpace.InverseTransformPoint(rightWorld));

            if (i > 0) dist += Vector3.Distance(corners[i - 1], corners[i]);
            float v = dist / Mathf.Max(1e-4f, metersPerTile);
            float uScale = Mathf.Max(0.01f, uvScaleAcrossFullWidth);
            uvsList.Add(new Vector2(0f, v));
            uvsList.Add(new Vector2(1f * uScale, v));
        }

        for (int i = 0; i < n - 1; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = (i + 1) * 2;
            int i3 = i2 + 1;

            trisList.Add(i0);
            trisList.Add(i2);
            trisList.Add(i1);
            trisList.Add(i2);
            trisList.Add(i3);
            trisList.Add(i1);
        }

        return dist;
    }

    private static Vector3 GetSmoothedForward(Vector3[] corners, int i, int n)
    {
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

        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            return Vector3.forward;
        return forward.normalized;
    }

    private static void AppendArrowHeadIfNeeded(
        Transform localSpace,
        Vector3[] corners,
        Func<Vector3, float> worldYOffset,
        bool addArrowHead,
        float arrowLengthMeters,
        float arrowWidthMeters,
        float metersPerTile,
        float dist,
        List<Vector3> vertsList,
        List<Vector2> uvsList,
        List<int> trisList)
    {
        int n = corners.Length;
        if (!addArrowHead || n < 2 || arrowLengthMeters <= 0.01f || arrowWidthMeters <= 0.01f)
            return;

        Vector3 endCorner = corners[n - 1];
        Vector3 prevCorner = corners[n - 2];
        Vector3 dir = endCorner - prevCorner;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f)
            dir = Vector3.forward;
        else
            dir.Normalize();

        Vector3 rightA = Vector3.Cross(Vector3.up, dir).normalized;
        float yTip = worldYOffset(endCorner);
        Vector3 tipW = endCorner + Vector3.up * yTip;
        tipW += dir * (arrowLengthMeters * 0.15f);

        Vector3 baseCenter = tipW - dir * arrowLengthMeters;
        Vector3 baseL = baseCenter - rightA * (arrowWidthMeters * 0.5f);
        Vector3 baseR = baseCenter + rightA * (arrowWidthMeters * 0.5f);

        int v0 = vertsList.Count;
        vertsList.Add(localSpace.InverseTransformPoint(tipW));
        vertsList.Add(localSpace.InverseTransformPoint(baseR));
        vertsList.Add(localSpace.InverseTransformPoint(baseL));

        float uvTail = dist / Mathf.Max(1e-4f, metersPerTile);
        uvsList.Add(new Vector2(0.5f, uvTail));
        uvsList.Add(new Vector2(1f, uvTail));
        uvsList.Add(new Vector2(0f, uvTail));

        trisList.Add(v0);
        trisList.Add(v0 + 1);
        trisList.Add(v0 + 2);
    }

    private static void AppendCenterArrowHeadThreeStrip(
        Transform localSpace,
        Vector3[] corners,
        Func<Vector3, float> worldYOffset,
        float arrowHeadLengthMeters,
        float arrowHeadWidthMeters,
        float metersPerTile,
        float dist,
        List<Vector3> vertsList,
        List<Vector2> uvsList,
        List<int> trisCenter)
    {
        int n = corners.Length;
        Vector3 endCorner = corners[n - 1];
        Vector3 prevCorner = corners[n - 2];
        Vector3 dir = endCorner - prevCorner;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
        else dir.Normalize();

        Vector3 rightA = Vector3.Cross(Vector3.up, dir).normalized;
        float yTip = worldYOffset(endCorner);
        Vector3 tipW = endCorner + Vector3.up * yTip + dir * (arrowHeadLengthMeters * 0.12f);
        Vector3 baseCenter = tipW - dir * arrowHeadLengthMeters;
        Vector3 baseL = baseCenter - rightA * (arrowHeadWidthMeters * 0.5f);
        Vector3 baseR = baseCenter + rightA * (arrowHeadWidthMeters * 0.5f);

        float uvTail = dist / Mathf.Max(1e-4f, metersPerTile);
        int v0 = vertsList.Count;
        vertsList.Add(localSpace.InverseTransformPoint(tipW));
        uvsList.Add(new Vector2(0.5f, uvTail));
        vertsList.Add(localSpace.InverseTransformPoint(baseR));
        uvsList.Add(new Vector2(0.74f, uvTail));
        vertsList.Add(localSpace.InverseTransformPoint(baseL));
        uvsList.Add(new Vector2(0.26f, uvTail));

        trisCenter.Add(v0);
        trisCenter.Add(v0 + 1);
        trisCenter.Add(v0 + 2);
    }

    private static void FinalizeMesh(Mesh mesh, List<Vector3> verts, List<Vector2> uvs, List<int> tris, int subMeshCount)
    {
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = subMeshCount;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.MarkDynamic();
    }
}

/// <summary>Shared URP/material setup for navigation path ribbons (SetNavigation + ARPathFinder).</summary>
public static class NavigationPathMaterialHelper
{
    public static void Configure(Material material, bool alwaysOnTop)
    {
        if (material == null) return;

        if (alwaysOnTop)
        {
            material.renderQueue = 4000;
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_ZTest"))
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
        }
        else
        {
            material.renderQueue = 2450;
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_ZTest"))
                material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
    }

    public static Material CreateDefaultPathMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("[NavMeshPathRibbon] No usable shader found (URP/Unlit, URP/Lit, Unlit/Color, Standard all null). " +
                           "Add 'Universal Render Pipeline/Unlit' to Graphics Settings > Always Included Shaders.");
            return null;
        }
        var m = new Material(shader) { color = color };
        return m;
    }

    /// <summary>Repeating chevron pattern along V (along path). RGBA for transparency on AR.</summary>
    public static Texture2D CreateChevronStripTexture(int width = 128, int height = 256)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapModeU = TextureWrapMode.Clamp;
        tex.wrapModeV = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        Color blue = new Color(0.05f, 0.45f, 1f, 0.96f);
        Color white = new Color(1f, 1f, 1f, 0.95f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            tex.SetPixel(x, y, blue);

        int step = Mathf.Max(16, height / 6);
        for (int cy = step / 2; cy < height - step / 2; cy += step)
            DrawChevronBand(tex, width, cy, white);

        tex.Apply(false, true);
        return tex;
    }

    private static void DrawChevronBand(Texture2D tex, int w, int yCenter, Color fg)
    {
        int thick = Mathf.Max(2, w / 28);
        int halfW = w / 2;
        for (int y = yCenter - 18; y <= yCenter + 18; y++)
        {
            if (y < 0 || y >= tex.height) continue;
            for (int x = 0; x < w; x++)
            {
                int dx = x - halfW;
                int dy = (yCenter + 5) - y;
                if (x < halfW && Mathf.Abs(dx + dy) <= thick) tex.SetPixel(x, y, fg);
                else if (x >= halfW && Mathf.Abs(dx - dy) <= thick) tex.SetPixel(x, y, fg);
            }
        }
    }

    public static void SetMaterialMainTexture(Material m, Texture2D tex)
    {
        if (m == null || tex == null) return;
        if (m.HasProperty("_BaseMap"))
            m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
    }
}
