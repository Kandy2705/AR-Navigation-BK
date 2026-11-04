using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Input row used by the login/register/settings forms: TMP input, optional show/hide-password
    /// toggle and an inline error line.
    /// </summary>
    public class UiTextField : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private Image background;

        [Tooltip("Soft keyboard layout on mobile (email, phone pad, ...).")]
        [SerializeField] private TouchScreenKeyboardType keyboardType = TouchScreenKeyboardType.Default;

        [Header("Password toggle (optional)")]
        [SerializeField] private bool isPasswordField;
        [SerializeField] private Button visibilityToggle;
        [SerializeField] private Image visibilityIcon;
        [SerializeField] private Sprite iconHidden;
        [SerializeField] private Sprite iconShown;

        public event Action<string> ValueChanged;
        public event Action<string> Submitted;

        public string Text
        {
            get => input.text;
            set => input.text = value;
        }

        public bool IsPasswordHidden { get; private set; }

        private void Awake()
        {
            input.keyboardType = keyboardType;
            input.onValueChanged.AddListener(HandleValueChanged);
            input.onSubmit.AddListener(HandleSubmit);

            if (visibilityToggle != null)
            {
                visibilityToggle.gameObject.SetActive(isPasswordField);
                visibilityToggle.onClick.AddListener(ToggleVisibility);
            }

            SetPasswordHidden(isPasswordField);
            ClearError();
        }

        public void SetError(string message)
        {
            bool hasError = !string.IsNullOrEmpty(message);
            if (errorLabel != null)
            {
                errorLabel.text = hasError ? message : string.Empty;
                errorLabel.gameObject.SetActive(hasError);
            }

            if (background != null)
            {
                background.color = hasError ? Color.Lerp(UiTheme.Surface, UiTheme.Danger, 0.25f) : UiTheme.Surface;
            }
        }

        public void ClearError()
        {
            SetError(null);
        }

        public bool IsEmpty => string.IsNullOrEmpty(input.text);

        public void Focus()
        {
            input.ActivateInputField();
        }

        public void ToggleVisibility()
        {
            SetPasswordHidden(!IsPasswordHidden);
        }

        private void SetPasswordHidden(bool hidden)
        {
            IsPasswordHidden = hidden;
            input.contentType = hidden ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.ForceLabelUpdate();

            if (visibilityIcon != null)
            {
                visibilityIcon.sprite = hidden ? iconHidden : iconShown;
            }
        }

        private void HandleValueChanged(string value)
        {
            ValueChanged?.Invoke(value);
        }

        private void HandleSubmit(string value)
        {
            Submitted?.Invoke(value);
        }
    }
}
