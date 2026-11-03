using Game.Foundation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Onboarding
{
    /// <summary>Welcome screen with the login / register call to action.</summary>
    public class WelcomeUI : UiPage
    {
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;

        private void Awake()
        {
            loginButton.onClick.AddListener(() => Open(PageID.Login));
            registerButton.onClick.AddListener(() => Open(PageID.Register));
        }

        private void Open(PageID target)
        {
            if (Navigation != null && Navigation.HasRoute(target))
            {
                Navigation.Navigate(target);
                return;
            }

            // The login and register screens move to Canvas in the next phases.
            Debug.Log($"[WelcomeUI] {target} screen is not available yet.");
        }
    }
}
