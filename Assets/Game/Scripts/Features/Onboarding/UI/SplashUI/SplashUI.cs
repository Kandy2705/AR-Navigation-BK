using System.Collections;
using Game.Features.Auth;
using Game.Foundation.UI;
using UnityEngine;

namespace Game.Features.Onboarding
{
    /// <summary>
    /// Brand splash (logo, "Agentic AR", loading spinner). While it shows, a session saved at the last login is
    /// restored with its refresh token; the user then lands on the AR home page, otherwise on the intro.
    /// </summary>
    public class SplashUI : UiPage
    {
        [SerializeField, Min(0f)] private float displaySeconds = 2f;
        [SerializeField, Min(1f)] private float restoreTimeoutSeconds = 8f;

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
            bool? restored = null;
            if (SessionRestorer.HasSavedSession)
            {
                SessionRestorer.TryRestore(result => restored = result);
            }
            else
            {
                restored = false;
            }

            yield return new WaitForSecondsRealtime(displaySeconds);
            float waited = 0f;
            while (restored == null && waited < restoreTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            advance = null;
            if (Navigation == null)
            {
                yield break;
            }

            if (restored == true && Navigation.HasRoute(PageID.ARPage))
            {
                Navigation.ResetTo(PageID.ARPage);
            }
            else
            {
                Navigation.Navigate(PageID.Onboarding, false, PageTransition.Fade);
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
