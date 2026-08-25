using ARNavB9V2.Data;
using ARNavB9V2.Outdoor;
using ARNavB9V2.UI;
using Unity.AI.Navigation;
using UnityEngine;

namespace ARNavB9V2.Scene
{
    [DisallowMultipleComponent]
    public sealed class B9OutdoorSceneContext : MonoBehaviour
    {
        [SerializeField] private B9OutdoorMapDefinition outdoorMap;
        [SerializeField] private Transform schoolGround;
        [SerializeField] private NavMeshSurface schoolGroundNavMesh;
        [SerializeField] private B9OutdoorLocationProvider locationProvider;
        [SerializeField] private B9OutdoorPoseController poseController;
        [SerializeField] private B9OutdoorRouteController routeController;
        [SerializeField] private B9RouteRibbonRenderer ribbonRenderer;
        [SerializeField] private B9OutdoorMinimapController minimapController;
        [SerializeField] private B9NavigationHud navigationHud;
        [SerializeField] private Transform userMarker;
        [SerializeField] private Transform entranceMarker;

        public B9OutdoorMapDefinition OutdoorMap => outdoorMap;
        public Transform SchoolGround => schoolGround;
        public NavMeshSurface SchoolGroundNavMesh => schoolGroundNavMesh;
        public B9OutdoorLocationProvider LocationProvider => locationProvider;
        public B9OutdoorPoseController PoseController => poseController;
        public B9OutdoorRouteController RouteController => routeController;
        public B9RouteRibbonRenderer RibbonRenderer => ribbonRenderer;
        public B9OutdoorMinimapController MinimapController => minimapController;
        public B9NavigationHud NavigationHud => navigationHud;
        public Transform UserMarker => userMarker;
        public Transform EntranceMarker => entranceMarker;

        public void Configure(
            B9OutdoorMapDefinition map,
            Transform ground,
            NavMeshSurface surface,
            B9OutdoorLocationProvider location,
            B9OutdoorPoseController pose,
            B9OutdoorRouteController route,
            B9RouteRibbonRenderer ribbon,
            B9OutdoorMinimapController minimap,
            B9NavigationHud hud,
            Transform user,
            Transform entrance)
        {
            outdoorMap = map;
            schoolGround = ground;
            schoolGroundNavMesh = surface;
            locationProvider = location;
            poseController = pose;
            routeController = route;
            ribbonRenderer = ribbon;
            minimapController = minimap;
            navigationHud = hud;
            userMarker = user;
            entranceMarker = entrance;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (outdoorMap == null || outdoorMap.MapId != "SchoolGround")
                return Fail("SchoolGround definition missing", out reason);
            if (schoolGround == null)
                return Fail("SchoolGround visual missing", out reason);
            if (schoolGroundNavMesh == null || schoolGroundNavMesh.navMeshData == null)
                return Fail("SchoolGround NavMesh missing", out reason);
            if (locationProvider == null || poseController == null)
                return Fail("Outdoor GPS pose stack missing", out reason);
            if (routeController == null || ribbonRenderer == null)
                return Fail("Outdoor route stack missing", out reason);
            if (minimapController == null || minimapController.RenderedTexture == null)
                return Fail("Outdoor minimap missing", out reason);
            if (navigationHud == null)
                return Fail("Outdoor navigation HUD missing", out reason);
            if (userMarker == null || entranceMarker == null)
                return Fail("Outdoor minimap markers missing", out reason);

            reason = string.Empty;
            return true;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
