using System.Collections;
using Game.Foundation.UI;
using UnityEngine;

namespace Game.Features.Onboarding
{
    /// <summary>Brand splash (logo, "Agentic AR", loading spinner). Moves on to the intro after a short delay.</summary>
    public class SplashUI : UiPage
    {
        [SerializeField, Min(0f)] private float displaySeconds = 2f;

        private Coroutine advance;

        protected override void OnShow()
        {
            StopAdvance();
            if (Application.isPlaying)
            {
                advance = StartCoroutine(AdvanceAfterDelay());
            }
        }

        protected override void OnHide()
        {
            StopAdvance();
        }

        private IEnumerator AdvanceAfterDelay()
        {
            yield return new WaitForSecondsRealtime(displaySeconds);
            advance = null;
            if (Navigation != null)
            {
                Navigation.Navigate(PageID.Onboarding);
            }
        }

        private void StopAdvance()
        {
            if (advance == null)
            {
                return;
            }

            StopCoroutine(advance);
            advance = null;
        }
    }
}
