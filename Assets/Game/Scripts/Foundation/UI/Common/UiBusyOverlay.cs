using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>Dim layer with a spinner shown while a page loads; it blocks taps on the page below.</summary>
    public class UiBusyOverlay : MonoBehaviour
    {
        private UiOverlayMotion motion;

        public bool IsVisible => gameObject.activeSelf && (motion == null || motion.Visible);

        public void Show()
        {
            gameObject.SetActive(true);
            Motion.Show();
        }

        public void Hide()
        {
            Motion.Hide(() => gameObject.SetActive(false));
        }

        private UiOverlayMotion Motion => motion ??= new UiOverlayMotion(this, OverlayStyle.Fade);
    }
}
