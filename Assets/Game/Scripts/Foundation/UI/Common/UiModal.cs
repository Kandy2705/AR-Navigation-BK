using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Confirm / notice dialog (delete history, log out, success). Lives under AppCanvas/Modals so it
    /// covers every page. Pass a null cancel text to show a single-button notice.
    /// </summary>
    public class UiModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;
        [SerializeField] private Button backdropButton;

        public event Action Confirmed;
        public event Action Cancelled;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancel);
            }

            if (backdropButton != null)
            {
                backdropButton.onClick.AddListener(HandleCancel);
            }
        }

        public void Show(string title, string message, string confirmText, string cancelText = null)
        {
            SetText(titleLabel, title);
            SetText(messageLabel, message);
            SetText(confirmLabel, confirmText);
            SetText(cancelLabel, cancelText);

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(!string.IsNullOrEmpty(cancelText));
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleConfirm()
        {
            Hide();
            Confirmed?.Invoke();
        }

        private void HandleCancel()
        {
            Hide();
            Cancelled?.Invoke();
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
