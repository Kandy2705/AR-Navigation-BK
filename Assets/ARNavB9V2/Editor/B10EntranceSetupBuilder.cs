using System;
using System.Collections.Generic;
using System.Linq;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B10EntranceSetupBuilder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";

        [MenuItem("Tools/AR Navigation V2/Maps/Connect 5 B10 Entrances")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open B9NavigationV2 in Edit Mode first.");

            B10BuildingAuthoring authoring = UnityEngine.Object.FindFirstObjectByType<
                B10BuildingAuthoring>(FindObjectsInactive.Include);
            B9OutdoorRouteController route = UnityEngine.Object.FindFirstObjectByType<
                B9OutdoorRouteController>(FindObjectsInactive.Include);
            if (authoring == null || authoring.PortalsRoot == null)
                throw new InvalidOperationException("B10 portal authoring root is missing.");
            if (route == null)
                throw new InvalidOperationException("Outdoor route controller is missing.");

            List<GameObject> markers = scene.GetRootGameObjects()
                .Where(go => TryGetMarkerOrder(go.name, out _))
                .OrderBy(go => GetMarkerOrder(go.name))
                .ToList();
            if (markers.Count != 5)
                throw new InvalidOperationException(
                    $"Expected exactly 5 root markers named GameObject through GameObject (4), found {markers.Count}.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Connect five B10 entrances");
            authoring.PortalsRoot.name = "B10 Entrances (5 MAIN DOORS)";
            var anchors = new List<Transform>(markers.Count);
            for (int i = 0; i < markers.Count; i++)
            {
                GameObject marker = markers[i];
                Vector3 worldPosition = marker.transform.position;
                Quaternion worldRotation = marker.transform.rotation;
                Vector3 worldScale = marker.transform.lossyScale;
                Undo.RecordObject(marker, "Rename B10 entrance");
                marker.name = $"B10 Entrance {i + 1:00}";
                Undo.SetTransformParent(
                    marker.transform,
                    authoring.PortalsRoot,
                    "Group B10 entrance");
                marker.transform.SetPositionAndRotation(worldPosition, worldRotation);
                SetWorldScale(marker.transform, worldScale);
                marker.transform.SetSiblingIndex(i);
                anchors.Add(marker.transform);
            }

            B10EntranceGroup group = authoring.PortalsRoot.GetComponent<B10EntranceGroup>();
            if (group == null)
                group = Undo.AddComponent<B10EntranceGroup>(authoring.PortalsRoot.gameObject);
            Undo.RecordObject(group, "Configure B10 entrances");
            group.Configure(anchors, 6f);
            EditorUtility.SetDirty(group);

            Undo.RecordObject(route, "Connect B10 entrances to outdoor routing");
            route.ConfigureB10Entrances(group);
            EditorUtility.SetDirty(route);

            SerializedObject authored = new SerializedObject(authoring);
            SerializedProperty status = authored.FindProperty("setupStatus");
            if (status != null)
                status.stringValue = "Five outdoor entrances connected. B10 indoor POIs and map-space portal pairs are still required for indoor routing.";
            authored.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = authoring.PortalsRoot.gameObject;
            Debug.Log("[B10 Entrances] DONE count=5, selection=shortest reachable outdoor NavMesh path, arrivalRadius=6m; marker world poses preserved.");
        }

        private static bool TryGetMarkerOrder(string name, out int order)
        {
            if (name == "GameObject")
            {
                order = 0;
                return true;
            }
            for (int i = 1; i <= 4; i++)
            {
                if (name == $"GameObject ({i})")
                {
                    order = i;
                    return true;
                }
            }
            order = -1;
            return false;
        }

        private static int GetMarkerOrder(string name)
        {
            return TryGetMarkerOrder(name, out int order) ? order : int.MaxValue;
        }

        private static void SetWorldScale(Transform target, Vector3 worldScale)
        {
            Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(
                SafeDivide(worldScale.x, parentScale.x),
                SafeDivide(worldScale.y, parentScale.y),
                SafeDivide(worldScale.z, parentScale.z));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.000001f ? value / divisor : value;
        }
    }
}
