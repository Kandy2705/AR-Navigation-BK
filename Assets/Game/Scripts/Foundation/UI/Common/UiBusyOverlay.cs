using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>Dim layer with a spinner shown while a page loads; it blocks taps on the page below.</summary>
    public class UiBusyOverlay : MonoBehaviour
    {
        public bool IsVisible => gameObject.activeSelf;

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
