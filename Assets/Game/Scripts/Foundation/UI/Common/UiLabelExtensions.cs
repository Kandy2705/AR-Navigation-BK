using TMPro;

namespace Game.Foundation.UI
{
    public static class UiLabelExtensions
    {
        /// <summary>Shows <paramref name="message"/> in the label, or hides the label when the message is empty.</summary>
        public static void ShowMessage(this TMP_Text label, string message)
        {
            if (label == null)
            {
                return;
            }

            bool visible = !string.IsNullOrEmpty(message);
            label.text = visible ? message : string.Empty;
            label.gameObject.SetActive(visible);
        }
    }
}
