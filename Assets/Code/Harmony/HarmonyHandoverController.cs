using ARNav.Hybrid;

namespace ARNav.Harmony
{
    public sealed class HarmonyHandoverController
    {
        private readonly IndoorMapSwitcher _mapSwitcher;
        private readonly MultisetPoseProvider _vpsProvider;

        public HarmonyHandoverController(
            IndoorMapSwitcher mapSwitcher,
            MultisetPoseProvider vpsProvider)
        {
            _mapSwitcher = mapSwitcher;
            _vpsProvider = vpsProvider;
        }

        public bool BeginVpsScan(BuildingId building, string floorId, out string failureReason)
        {
            failureReason = string.Empty;
            if (_mapSwitcher == null)
            {
                failureReason = "IndoorMapSwitcher missing";
                return false;
            }
            if (_vpsProvider == null)
            {
                failureReason = "MultisetPoseProvider missing";
                return false;
            }

            _vpsProvider.SetCurrentBuilding(building, floorId);
            if (!_mapSwitcher.EnterIndoor(building))
            {
                _vpsProvider.SetCurrentBuilding(BuildingId.None, string.Empty);
                failureReason = $"Indoor map unavailable for {building}";
                return false;
            }

            _mapSwitcher.RequestLocalization();
            return true;
        }

        public void RetryVps()
        {
            _mapSwitcher?.RequestLocalization();
        }

        public void ReturnToOutdoor()
        {
            _mapSwitcher?.ExitToOutdoor();
            _vpsProvider?.SetCurrentBuilding(BuildingId.None, string.Empty);
        }

        public void AbortVpsScan()
        {
            ReturnToOutdoor();
        }
    }
}
