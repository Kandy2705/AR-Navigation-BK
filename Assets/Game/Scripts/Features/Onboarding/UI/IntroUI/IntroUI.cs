using Game.Foundation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Onboarding
{
    /// <summary>Feature intro ("Di chuyển trong khuôn viên dễ dàng"). One button leads to the welcome screen.</summary>
    public class IntroUI : UiPage
    {
        [SerializeField] private Button continueButton;

        private void Awake()
        {
            continueButton.onClick.AddListener(HandleContinue);
        }

        private void HandleContinue()
        {
            if (Navigation != null)
            {
                Navigation.Navigate(PageID.WelcomePage);
            }
        }
    }
}
