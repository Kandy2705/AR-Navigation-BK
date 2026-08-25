using ARNavB9V2.Outdoor;
using UnityEngine;

namespace ARNavB9V2.Indoor
{
    [DisallowMultipleComponent]
    public sealed class B9IndoorSceneContext : MonoBehaviour
    {
        [SerializeField] private B9IndoorRouteController routeController;
        [SerializeField] private B9RouteRibbonRenderer ribbonRenderer;
        [SerializeField] private B9IndoorMinimapController minimapController;
        [SerializeField] private Transform userMarker;
        [SerializeField] private Transform destinationMarker;

        public B9IndoorRouteController RouteController => routeController;
        public B9RouteRibbonRenderer RibbonRenderer => ribbonRenderer;
        public B9IndoorMinimapController MinimapController => minimapController;
        public Transform UserMarker => userMarker;
        public Transform DestinationMarker => destinationMarker;

        public void Configure(
            B9IndoorRouteController route,
            B9RouteRibbonRenderer ribbon,
            B9IndoorMinimapController minimap,
            Transform user,
            Transform destination)
        {
            routeController = route;
            ribbonRenderer = ribbon;
            minimapController = minimap;
            userMarker = user;
            destinationMarker = destination;
        }

        public void PrepareForLocalization()
        {
            minimapController?.Deactivate();
            routeController?.PrepareForLocalization();
            ribbonRenderer?.ClearPath();
        }

        public bool BeginNavigation(string roomId)
        {
            minimapController?.Activate();
            return routeController != null && routeController.BeginNavigation(roomId);
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (routeController == null)
                return Fail("Indoor route controller missing", out reason);
            if (ribbonRenderer == null)
                return Fail("Indoor route renderer missing", out reason);
            if (minimapController == null || minimapController.RenderedTexture == null)
                return Fail("Indoor minimap controller missing", out reason);
            if (userMarker == null || destinationMarker == null)
                return Fail("Indoor minimap markers missing", out reason);

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
