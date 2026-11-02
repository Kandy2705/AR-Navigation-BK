using System;
using System.Collections.Generic;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Editor
{
    public static class B9B10RoomAnchorValidator
    {
        [MenuItem("Tools/AR Navigation V2/Maps/Validate B9 + B10 Room Anchors")]
        public static void Validate()
        {
            B9SceneContext b9 = UnityEngine.Object.FindFirstObjectByType<B9SceneContext>(
                FindObjectsInactive.Include);
            B10BuildingAuthoring b10 = UnityEngine.Object.FindFirstObjectByType<B10BuildingAuthoring>(
                FindObjectsInactive.Include);
            if (b9 == null || b10 == null)
                throw new InvalidOperationException("B9 or B10 scene context is missing.");

            int b9Valid = ValidateGroup(
                "B9",
                b9.RoomAnchors,
                b9.MapSpace,
                b9.NavMeshSurface != null ? b9.NavMeshSurface.navMeshData : null,
                out int b9Total);
            int b10Valid = ValidateGroup(
                "B10",
                b10.RoomAnchors,
                b10.IndoorMapSpace,
                b10.IndoorNavMesh != null ? b10.IndoorNavMesh.navMeshData : null,
                out int b10Total);

            string summary = $"[Room Anchors] RESULT B9={b9Valid}/{b9Total}, "
                             + $"B10={b10Valid}/{b10Total} anchors within 3m of NavMesh.";
            if (b9Valid == b9Total && b10Valid == b10Total)
                Debug.Log(summary);
            else
                Debug.LogWarning(summary + " Review the warning lines above.");
        }

        private static int ValidateGroup(
            string prefix,
            IReadOnlyList<B9RoomAnchor> anchors,
            Transform mapSpace,
            NavMeshData navMeshData,
            out int total)
        {
            total = anchors != null ? anchors.Count : 0;
            if (anchors == null || anchors.Count == 0)
                throw new InvalidOperationException($"{prefix} has no room anchors.");
            if (mapSpace == null || navMeshData == null)
                throw new InvalidOperationException($"{prefix} map space or NavMesh is missing.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Vector3 offset = prefix == "B9"
                ? new Vector3(10000f, 10000f, 10000f)
                : new Vector3(20000f, 20000f, 20000f);
            NavMeshDataInstance instance = NavMesh.AddNavMeshData(
                navMeshData,
                offset,
                Quaternion.identity);
            int validCount = 0;
            try
            {
                for (int i = 0; i < anchors.Count; i++)
                {
                    B9RoomAnchor anchor = anchors[i];
                    if (anchor == null)
                        throw new InvalidOperationException($"{prefix} room anchor {i} is null.");
                    if (string.IsNullOrWhiteSpace(anchor.RoomId)
                        || !anchor.RoomId.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"Invalid {prefix} room ID on {anchor.name}: '{anchor.RoomId}'.");
                    if (!ids.Add(anchor.RoomId))
                        throw new InvalidOperationException($"Duplicate room ID: {anchor.RoomId}.");
                    if (string.IsNullOrWhiteSpace(anchor.FloorId))
                        throw new InvalidOperationException($"{anchor.RoomId} has no floor ID.");

                    Vector3 localPosition = mapSpace.InverseTransformPoint(anchor.transform.position);
                    if (!IsFinite(localPosition))
                        throw new InvalidOperationException($"{anchor.RoomId} has an invalid position.");
                    if (!NavMesh.SamplePosition(
                            offset + localPosition,
                            out NavMeshHit hit,
                            3f,
                            NavMesh.AllAreas))
                    {
                        string nearest = NavMesh.SamplePosition(
                            offset + localPosition,
                            out NavMeshHit widerHit,
                            15f,
                            NavMesh.AllAreas)
                            ? $"; nearest={widerHit.distance:0.00}m"
                            : "; no NavMesh within 15m";
                        Debug.LogWarning(
                            $"[Room Anchors] {anchor.RoomId} is farther than 3m from "
                            + $"the {prefix} NavMesh at local={localPosition:F3}{nearest}.");
                        continue;
                    }

                    validCount++;
                    Debug.Log($"[Room Anchors] {anchor.RoomId} floor={anchor.FloorId} "
                              + $"local={localPosition:F3}, navDistance={hit.distance:0.00}m");
                }
            }
            finally
            {
                instance.Remove();
            }

            return validCount;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                   && float.IsFinite(value.y)
                   && float.IsFinite(value.z);
        }
    }
}
