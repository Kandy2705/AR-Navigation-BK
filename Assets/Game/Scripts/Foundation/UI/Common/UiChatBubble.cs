using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>A chat bubble that is as wide as its text up to a maximum, then wraps.</summary>
    public class UiChatBubble : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private LayoutElement labelLayout;
        [SerializeField, Min(40f)] private float maxTextWidth = 230f;

        public string Text => label.text;

        public void SetText(string text)
        {
            label.text = text ?? string.Empty;
            Vector2 wanted = label.GetPreferredValues(label.text, maxTextWidth, 0f);
            labelLayout.preferredWidth = Mathf.Min(Mathf.Ceil(wanted.x), maxTextWidth);
        }
    }
}
