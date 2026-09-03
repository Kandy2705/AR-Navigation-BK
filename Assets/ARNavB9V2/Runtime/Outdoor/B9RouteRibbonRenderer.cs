using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ARNavB9V2.Outdoor
{
    /// <summary>
    /// Draws one continuous outdoor route mesh: textured blue center plus white side borders.
    /// The implementation is self-contained in V2; only the legacy chevron artwork is reused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9RouteRibbonRenderer : MonoBehaviour
    {
        [SerializeField] private MeshFilter ribbonFilter;
        [SerializeField] private MeshRenderer ribbonRenderer;
        [SerializeField] private MeshFilter minimapFilter;
        [SerializeField] private MeshRenderer minimapRenderer;
        [SerializeField] private Material centerMaterial;
        [SerializeField] private Material borderMaterial;
        [SerializeField] private Camera groundReferenceCamera;
        [SerializeField] private float pathWidthMeters = 0.5f;
        [SerializeField] private float borderWidthMeters = 0.06f;
        [SerializeField] private float heightOffsetMeters = 0.04f;
        [SerializeField] private bool lockToEstimatedCameraGround = true;
        [SerializeField] private float assumedPhoneHeightMeters = 1.45f;
        [SerializeField] private bool useCameraGroundLockInEditor;
        [SerializeField] private float metersPerChevronTile = 2.5f;
        [SerializeField] private float pathStartTrimMeters = 1.2f;
        [SerializeField, Range(0, 4)] private int cornerSmoothingIterations = 2;
        [SerializeField] private bool addDestinationArrow = true;
        [SerializeField] private float destinationArrowLengthMeters = 0.55f;
        [SerializeField] private float destinationArrowWidthMeters = 0.38f;
        [SerializeField] private float minimapWidthMultiplier = 3.2f;
        [SerializeField] private float minimapLiftMeters = 3f;

        private Mesh ribbonMesh;
        private Mesh minimapMesh;
        private bool hasEstimatedGroundHeight;
        private float estimatedGroundHeight;
        private MaterialPropertyBlock reliabilityPropertyBlock;
        private bool adaptiveReliabilityPresentation;
        private float presentedReliability = 1f;

        public bool HasVisiblePath { get; private set; }
        public Mesh RouteMesh => ribbonMesh;
        public MeshRenderer RouteMeshRenderer => ribbonRenderer;

        public void Configure(Material border, Material center, Camera referenceCamera = null)
        {
            borderMaterial = border;
            centerMaterial = center;
            groundReferenceCamera = referenceCamera;
            hasEstimatedGroundHeight = false;
            EnsureVisualObject();
            ApplyMaterials();
            ClearPath();
        }

        private void Awake()
        {
            ResolveGroundReferenceCamera();
            EnsureVisualObject();
            ApplyMaterials();
        }

        public void ConfigureGroundReference(Camera referenceCamera)
        {
            groundReferenceCamera = referenceCamera;
            hasEstimatedGroundHeight = false;
        }

        public void ConfigureRouteStyle(
            float widthMeters,
            float sideBorderMeters,
            float verticalOffsetMeters,
            bool useEstimatedCameraGround)
        {
            pathWidthMeters = Mathf.Max(0.1f, widthMeters);
            borderWidthMeters = Mathf.Clamp(
                sideBorderMeters,
                0.01f,
                pathWidthMeters * 0.45f);
            heightOffsetMeters = Mathf.Max(0.005f, verticalOffsetMeters);
            lockToEstimatedCameraGround = useEstimatedCameraGround;
            hasEstimatedGroundHeight = false;
        }

        public void ConfigureMinimapPresentation(float widthMultiplier, float liftMeters)
        {
            minimapWidthMultiplier = Mathf.Max(1f, widthMultiplier);
            minimapLiftMeters = Mathf.Max(0.1f, liftMeters);
            EnsureVisualObject();
            ApplyMaterials();
        }

        public void SetReliabilityPresentation(bool adaptive, float reliability)
        {
            adaptiveReliabilityPresentation = adaptive;
            presentedReliability = Mathf.Clamp01(reliability);
            ApplyReliabilityPresentation(ribbonRenderer);
            ApplyReliabilityPresentation(minimapRenderer);
        }

        public void SetPath(IReadOnlyList<Vector3> points)
        {
            ResolveGroundReferenceCamera();
            CaptureEstimatedGroundHeightIfNeeded();
            EnsureVisualObject();
            List<Vector3> cleaned = RemoveDuplicatePoints(points);
            if (cleaned.Count < 2)
            {
                ClearPath();
                return;
            }

            cleaned = TrimPathStart(cleaned, pathStartTrimMeters);
            cleaned = SmoothCornersChaikin(cleaned, cornerSmoothingIterations);
            if (cleaned.Count < 2)
            {
                ClearPath();
                return;
            }

            Mesh replacement = BuildRibbonMesh(
                cleaned,
                pathWidthMeters,
                0f,
                "B9 V2 Continuous Chevron Route");
            ReplaceMesh(ref ribbonMesh, replacement);
            ribbonFilter.sharedMesh = ribbonMesh;

            Mesh minimapReplacement = BuildRibbonMesh(
                cleaned,
                pathWidthMeters * minimapWidthMultiplier,
                minimapLiftMeters,
                "B9 V2 Minimap Chevron Route");
            ReplaceMesh(ref minimapMesh, minimapReplacement);
            minimapFilter.sharedMesh = minimapMesh;
            ApplyMaterials();
            ribbonRenderer.enabled = true;
            minimapRenderer.enabled = true;
            HasVisiblePath = true;
            SetReliabilityPresentation(adaptiveReliabilityPresentation, presentedReliability);
        }

        public void ClearPath()
        {
            EnsureVisualObject();
            ribbonRenderer.enabled = false;
            minimapRenderer.enabled = false;
            HasVisiblePath = false;
        }

        private Mesh BuildRibbonMesh(
            IReadOnlyList<Vector3> points,
            float widthMeters,
            float verticalLiftMeters,
            string meshName)
        {
            var vertices = new List<Vector3>(points.Count * 6 + 3);
            var uvs = new List<Vector2>(points.Count * 6 + 3);
            var centerTriangles = new List<int>((points.Count - 1) * 6 + 3);
            var borderTriangles = new List<int>((points.Count - 1) * 12);
            float distance = 0f;
            float halfWidth = widthMeters * 0.5f;
            float widthScale = widthMeters / Mathf.Max(0.01f, pathWidthMeters);
            float safeBorderWidth = Mathf.Clamp(
                borderWidthMeters * widthScale,
                0.001f,
                Mathf.Max(0.001f, halfWidth * 0.45f));
            float innerHalfWidth = halfWidth - safeBorderWidth;

            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                    distance += Vector3.Distance(points[i - 1], points[i]);

                Vector3 position = points[i];
                position.y = ResolveRouteHeight(points[i].y) + verticalLiftMeters;
                Vector3 forward = GetJoinedForward(points, i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                Vector3 outerLeft = position - right * halfWidth;
                Vector3 innerLeft = position - right * innerHalfWidth;
                Vector3 innerRight = position + right * innerHalfWidth;
                Vector3 outerRight = position + right * halfWidth;

                int row = vertices.Count;
                AddVertex(vertices, uvs, outerLeft, 0f, distance);
                AddVertex(vertices, uvs, innerLeft, 0.25f, distance);
                AddVertex(vertices, uvs, innerLeft, 0.26f, distance);
                AddVertex(vertices, uvs, innerRight, 0.74f, distance);
                AddVertex(vertices, uvs, innerRight, 0.75f, distance);
                AddVertex(vertices, uvs, outerRight, 1f, distance);

                if (i == 0)
                    continue;

                int previousRow = row - 6;
                AppendQuad(borderTriangles, previousRow, previousRow + 1, row, row + 1);
                AppendQuad(centerTriangles, previousRow + 2, previousRow + 3, row + 2, row + 3);
                AppendQuad(borderTriangles, previousRow + 4, previousRow + 5, row + 4, row + 5);
            }

            if (addDestinationArrow)
            {
                AppendDestinationArrow(
                    points,
                    distance,
                    vertices,
                    uvs,
                    centerTriangles,
                    widthScale,
                    verticalLiftMeters);
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(centerTriangles, 0);
            mesh.SetTriangles(borderTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            return mesh;
        }

        private void AddVertex(
            List<Vector3> vertices,
            List<Vector2> uvs,
            Vector3 worldPosition,
            float u,
            float distance)
        {
            vertices.Add(transform.InverseTransformPoint(worldPosition));

            // The legacy artwork points down in texture space. Negative V makes every
            // chevron point from the user's start toward the B9 entrance.
            float v = -distance / Mathf.Max(0.01f, metersPerChevronTile);
            uvs.Add(new Vector2(u, v));
        }

        private void AppendDestinationArrow(
            IReadOnlyList<Vector3> points,
            float pathDistance,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            float widthScale,
            float verticalLiftMeters)
        {
            if (destinationArrowLengthMeters <= 0.01f || destinationArrowWidthMeters <= 0.01f)
                return;

            Vector3 end = points[points.Count - 1];
            Vector3 direction = end - points[points.Count - 2];
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            direction.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float arrowLength = destinationArrowLengthMeters * widthScale;
            float arrowWidth = destinationArrowWidthMeters * widthScale;
            Vector3 tip = end + direction * (arrowLength * 0.12f);
            tip.y = ResolveRouteHeight(end.y) + verticalLiftMeters;
            Vector3 baseCenter = tip - direction * arrowLength;
            Vector3 baseLeft = baseCenter - right * (arrowWidth * 0.5f);
            Vector3 baseRight = baseCenter + right * (arrowWidth * 0.5f);
            int start = vertices.Count;
            float v = -pathDistance / Mathf.Max(0.01f, metersPerChevronTile);

            vertices.Add(transform.InverseTransformPoint(tip));
            uvs.Add(new Vector2(0.5f, v));
            vertices.Add(transform.InverseTransformPoint(baseRight));
            uvs.Add(new Vector2(0.74f, v));
            vertices.Add(transform.InverseTransformPoint(baseLeft));
            uvs.Add(new Vector2(0.26f, v));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static void AppendQuad(
            List<int> triangles,
            int previousLeft,
            int previousRight,
            int currentLeft,
            int currentRight)
        {
            triangles.Add(previousLeft);
            triangles.Add(currentLeft);
            triangles.Add(previousRight);
            triangles.Add(currentLeft);
            triangles.Add(currentRight);
            triangles.Add(previousRight);
        }

        private float ResolveRouteHeight(float navMeshHeight)
        {
            return ShouldUseCameraGroundLock() && hasEstimatedGroundHeight
                ? estimatedGroundHeight + heightOffsetMeters
                : navMeshHeight + heightOffsetMeters;
        }

        private bool ShouldUseCameraGroundLock()
        {
            if (!lockToEstimatedCameraGround || groundReferenceCamera == null)
                return false;
#if UNITY_EDITOR
            return useCameraGroundLockInEditor;
#else
            return true;
#endif
        }

        private void ResolveGroundReferenceCamera()
        {
            if (groundReferenceCamera == null)
                groundReferenceCamera = Camera.main;
        }

        private void CaptureEstimatedGroundHeightIfNeeded()
        {
            if (hasEstimatedGroundHeight || !ShouldUseCameraGroundLock())
                return;
            estimatedGroundHeight = groundReferenceCamera.transform.position.y
                                    - Mathf.Max(0.5f, assumedPhoneHeightMeters);
            hasEstimatedGroundHeight = true;
        }

        private void ApplyReliabilityPresentation(MeshRenderer target)
        {
            if (target == null)
                return;
            reliabilityPropertyBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(reliabilityPropertyBlock);
            Color tint = Color.white;
            if (adaptiveReliabilityPresentation)
            {
                tint = presentedReliability >= 0.72f
                    ? Color.white
                    : presentedReliability >= 0.4f
                        ? new Color(1f, 0.78f, 0.2f, 0.78f)
                        : new Color(1f, 0.28f, 0.2f, 0.48f);
            }
            reliabilityPropertyBlock.SetColor("_BaseColor", tint);
            reliabilityPropertyBlock.SetColor("_Color", tint);
            target.SetPropertyBlock(reliabilityPropertyBlock);
        }

        private static Vector3 GetJoinedForward(IReadOnlyList<Vector3> points, int index)
        {
            Vector3 forward;
            if (index == 0)
            {
                forward = points[1] - points[0];
            }
            else if (index == points.Count - 1)
            {
                forward = points[index] - points[index - 1];
            }
            else
            {
                Vector3 incoming = points[index] - points[index - 1];
                Vector3 outgoing = points[index + 1] - points[index];
                incoming.y = 0f;
                outgoing.y = 0f;
                incoming.Normalize();
                outgoing.Normalize();
                forward = incoming + outgoing;
                if (forward.sqrMagnitude < 0.0001f)
                    forward = outgoing;
            }

            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : forward.normalized;
        }

        private static List<Vector3> RemoveDuplicatePoints(IReadOnlyList<Vector3> points)
        {
            var result = new List<Vector3>();
            if (points == null)
                return result;

            for (int i = 0; i < points.Count; i++)
            {
                if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], points[i]) > 0.01f)
                    result.Add(points[i]);
            }
            return result;
        }

        private static List<Vector3> TrimPathStart(List<Vector3> points, float trimMeters)
        {
            if (points.Count < 2 || trimMeters <= 0.01f)
                return points;

            float remaining = trimMeters;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 segment = points[i + 1] - points[i];
                float length = segment.magnitude;
                if (length <= 0.01f)
                    continue;

                if (remaining < length)
                {
                    var trimmed = new List<Vector3>(points.Count - i);
                    trimmed.Add(points[i] + segment * (remaining / length));
                    for (int j = i + 1; j < points.Count; j++)
                        trimmed.Add(points[j]);
                    return trimmed;
                }
                remaining -= length;
            }

            // Keep short routes visible instead of trimming the whole mesh away.
            return points;
        }

        private static List<Vector3> SmoothCornersChaikin(List<Vector3> points, int iterations)
        {
            if (points.Count < 3 || iterations <= 0)
                return points;

            List<Vector3> current = points;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var next = new List<Vector3>(current.Count * 2);
                next.Add(current[0]);
                for (int i = 0; i < current.Count - 1; i++)
                {
                    next.Add(Vector3.Lerp(current[i], current[i + 1], 0.25f));
                    next.Add(Vector3.Lerp(current[i], current[i + 1], 0.75f));
                }
                next.Add(current[current.Count - 1]);
                current = next;
            }
            return current;
        }

        private void EnsureVisualObject()
        {
            EnsureVisualPart(
                "Ribbon Mesh",
                LayerMask.NameToLayer("Default"),
                ref ribbonFilter,
                ref ribbonRenderer);
            EnsureVisualPart(
                "Minimap Ribbon Mesh",
                LayerMask.NameToLayer("MinimapOnly"),
                ref minimapFilter,
                ref minimapRenderer);
        }

        private void EnsureVisualPart(
            string objectName,
            int layer,
            ref MeshFilter meshFilter,
            ref MeshRenderer meshRenderer)
        {
            if (meshFilter != null && meshRenderer != null)
                return;

            Transform child = transform.Find(objectName);
            GameObject visual = child != null ? child.gameObject : new GameObject(objectName);
            visual.transform.SetParent(transform, false);
            if (layer >= 0)
                visual.layer = layer;
            meshFilter = visual.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = visual.AddComponent<MeshFilter>();
            meshRenderer = visual.GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void ApplyMaterials()
        {
            Material[] materials = { centerMaterial, borderMaterial };
            if (ribbonRenderer != null)
                ribbonRenderer.sharedMaterials = materials;
            if (minimapRenderer != null)
                minimapRenderer.sharedMaterials = materials;
        }

        private static void ReplaceMesh(ref Mesh current, Mesh replacement)
        {
            if (current != null)
            {
                if (Application.isPlaying) Destroy(current);
                else DestroyImmediate(current);
            }
            current = replacement;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && ribbonMesh != null)
                Destroy(ribbonMesh);
            if (Application.isPlaying && minimapMesh != null)
                Destroy(minimapMesh);
        }
    }
}
