using UnityEngine;

namespace ARNavB9V2.Vps
{
    /// <summary>
    /// Presents the official MultiSet localization loader while the real SDK is
    /// capturing/requesting VPS frames. Failure and retry remain owned by the V2 HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9MultisetLocalizationUx : MonoBehaviour
    {
        [SerializeField] private B9VpsTransitionController transition;
        [SerializeField] private GameObject loaderPanel;

        public GameObject LoaderPanel => loaderPanel;
        public bool IsVisible => loaderPanel != null && loaderPanel.activeSelf;

        public void Configure(
            B9VpsTransitionController transitionController,
            GameObject multisetLoaderPanel)
        {
            Detach();
            transition = transitionController;
            loaderPanel = multisetLoaderPanel;
            Attach();
            RefreshVisibility();
        }

        private void OnEnable()
        {
            Attach();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            Detach();
            if (loaderPanel != null)
                loaderPanel.SetActive(false);
        }

        private void Attach()
        {
            if (transition == null)
                return;
            transition.StateChanged -= HandleTransitionStateChanged;
            transition.StateChanged += HandleTransitionStateChanged;
        }

        private void Detach()
        {
            if (transition != null)
                transition.StateChanged -= HandleTransitionStateChanged;
        }

        private void HandleTransitionStateChanged(
            B9VpsTransitionController.TransitionState _)
        {
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (loaderPanel == null)
                return;

            bool shouldShow = transition != null
                              && (transition.State
                                  == B9VpsTransitionController.TransitionState.StartingVps
                                  || transition.State
                                  == B9VpsTransitionController.TransitionState.Scanning);
            if (loaderPanel.activeSelf != shouldShow)
                loaderPanel.SetActive(shouldShow);
        }
    }
}
