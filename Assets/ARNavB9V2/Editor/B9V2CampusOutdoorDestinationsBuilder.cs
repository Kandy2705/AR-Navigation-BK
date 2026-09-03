using System;
using System.Collections.Generic;
using ARNavB9V2.Data;
using ARNavB9V2.Outdoor;
using ARNavB9V2.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ARNavB9V2.Editor
{
    public static class B9V2CampusOutdoorDestinationsBuilder
    {
        private const string ScenePath = "Assets/ARNavB9V2/Scenes/B9NavigationV2.unity";
        private const string CatalogPath =
            "Assets/ARNavB9V2/Data/Buildings/B9CampusDestinationCatalog.asset";
        private const string RootName = "[B9 V2] Campus Outdoor Destinations";

        [MenuItem("Tools/AR Navigation V2/Add Campus Outdoor Destinations")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            B9SceneContext foundation = Required<B9SceneContext>("B9 foundation");
            B9OutdoorSceneContext outdoor = Required<B9OutdoorSceneContext>("outdoor navigation");
            B9CampusDestinationCatalog catalog = CreateOrUpdateCatalog(outdoor.OutdoorMap);

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
            GameObject root = new GameObject(RootName);

            var anchors = new List<B9OutdoorDestinationAnchor>();
            foreach (B9CampusDestinationCatalog.Destination destination in catalog.Destinations)
            {
                GameObject anchorObject = new GameObject(destination.Id + " Outdoor Destination");
                anchorObject.transform.SetParent(root.transform, false);
                Vector3 campusPosition = destination.Id == "B9"
                    ? foundation.OutdoorEntranceAnchor.position
                    : outdoor.OutdoorMap.GpsToCampus(destination.Latitude, destination.Longitude);
                if (destination.Id != "B9"
                    && NavMesh.SamplePosition(
                        campusPosition,
                        out NavMeshHit hit,
                        18f,
                        NavMesh.AllAreas))
                {
                    campusPosition = hit.position;
                }
                anchorObject.transform.position = campusPosition;

                B9OutdoorDestinationAnchor anchor =
                    anchorObject.AddComponent<B9OutdoorDestinationAnchor>();
                anchor.Configure(
                    destination.Id,
                    destination.DisplayName,
                    destination.Latitude,
                    destination.Longitude,
                    destination.ArrivalRadiusMeters);
                anchors.Add(anchor);
                EditorUtility.SetDirty(anchor);
            }

            outdoor.RouteController.ConfigureCampusDestinations(catalog, anchors);
            outdoor.MinimapController.AttachRouteController(outdoor.RouteController);
            outdoor.NavigationHud.AttachCampusDestinations(catalog);
            EditorUtility.SetDirty(outdoor.RouteController);
            EditorUtility.SetDirty(outdoor.MinimapController);
            EditorUtility.SetDirty(outdoor.NavigationHud);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save campus destinations.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log(
                "[B9V2 Campus] COMPLETE outdoor destinations A2, A3, A4, A5, "
                + "B6, B8, B10; B9 remains the only indoor-enabled building.");
        }

        private static B9CampusDestinationCatalog CreateOrUpdateCatalog(
            B9OutdoorMapDefinition outdoorMap)
        {
            B9CampusDestinationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<B9CampusDestinationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<B9CampusDestinationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetDestinations(new List<B9CampusDestinationCatalog.Destination>
            {
                new B9CampusDestinationCatalog.Destination(
                    "B9", "Tòa B9", outdoorMap.EntranceLatitude,
                    outdoorMap.EntranceLongitude, true, outdoorMap.ArrivalRadiusMeters),
                new B9CampusDestinationCatalog.Destination(
                    "A2", "Tòa A2", 10.7730139d, 106.659975d, false, 12f),
                new B9CampusDestinationCatalog.Destination(
                    "A3", "Tòa A3", 10.7733139d, 106.6605583d, false, 12f),
                new B9CampusDestinationCatalog.Destination(
                    "A4", "Tòa A4", 10.7732556d, 106.6600972d, false, 12f),
                new B9CampusDestinationCatalog.Destination(
                    "A5", "Tòa A5", 10.7734333d, 106.6590556d, false, 12f),
                new B9CampusDestinationCatalog.Destination(
                    "B6", "Tòa B6", 10.7737806d, 106.6593139d, false, 12f),
                new B9CampusDestinationCatalog.Destination(
                    "B8", "Tòa B8", 10.7737861d, 106.6601667d, false, 12f),
                new B9CampusDestinationCatalog.Destination(
                    "B10", "Tòa B10", 10.773675d, 106.6608861d, false, 12f),
            });
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static T Required<T>(string label) where T : UnityEngine.Object
        {
            T value = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (value == null)
                throw new InvalidOperationException("Missing " + label + ".");
            return value;
        }
    }
}
