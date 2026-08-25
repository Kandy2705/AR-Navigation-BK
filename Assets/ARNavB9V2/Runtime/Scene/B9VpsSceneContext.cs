using ARNavB9V2.Vps;
using UnityEngine;

namespace ARNavB9V2.Scene
{
    [DisallowMultipleComponent]
    public sealed class B9VpsSceneContext : MonoBehaviour
    {
        [SerializeField] private GameObject sdkManagerRoot;
        [SerializeField] private MonoBehaviour mapLocalizationManager;
        [SerializeField] private B9VpsTransitionController transitionController;

        public GameObject SdkManagerRoot => sdkManagerRoot;
        public MonoBehaviour MapLocalizationManager => mapLocalizationManager;
        public B9VpsTransitionController TransitionController => transitionController;

        public void Configure(
            GameObject sdkRoot,
            MonoBehaviour localizer,
            B9VpsTransitionController transition)
        {
            sdkManagerRoot = sdkRoot;
            mapLocalizationManager = localizer;
            transitionController = transition;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (sdkManagerRoot == null)
                return Fail("MultiSet SDK manager missing", out reason);
            if (mapLocalizationManager == null
                || mapLocalizationManager.GetType().Name != "MapLocalizationManager")
                return Fail("MultiSet MapLocalizationManager missing", out reason);
            if (transitionController == null)
                return Fail("B9 VPS transition controller missing", out reason);
            if (transitionController.ActiveMapCode != "MAP_9LME2PB7Y3EN")
                return Fail("B9 VPS map code is invalid", out reason);

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
