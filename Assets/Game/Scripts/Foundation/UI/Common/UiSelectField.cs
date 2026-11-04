using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// A form row that looks like a dropdown ("Giới tính ▾"): gray hint until a value is chosen, white after.
    /// It only reports taps; the screen decides what to open (see <see cref="UiPickerSheet"/>).
    /// </summary>
    public class UiSelectField : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string hint;

        public event Action Clicked;

        public string Value { get; private set; } = string.Empty;

        public bool HasValue => !string.IsNullOrEmpty(Value);

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke());
            Render();
        }

        public void SetValue(string value)
        {
            Value = value ?? string.Empty;
            Render();
        }

        private void Render()
        {
            label.text = HasValue ? Value : hint;
            label.color = HasValue ? UiTheme.TextPrimary : UiTheme.Placeholder;
        }
    }
}
