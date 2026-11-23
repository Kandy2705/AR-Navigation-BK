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
        [Tooltip("The sheet that rises from the bottom edge.")]
        [SerializeField] private RectTransform panel;

        private UiOverlayMotion motion;

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
            Motion.Show();
        }

        /// <summary>Colors the title (red for "Đăng xuất"); reset to the theme title color by passing null.</summary>
        public void SetTitleColor(Color? color)
        {
            if (titleLabel != null)
            {
                titleLabel.color = color ?? UiTheme.TextTitle;
            }
        }

        public bool IsVisible => gameObject.activeSelf && (motion == null || motion.Visible);

        public void Hide()
        {
            Motion.Hide(() => gameObject.SetActive(false));
        }

        private UiOverlayMotion Motion => motion ??= new UiOverlayMotion(
            this, OverlayStyle.SlideUp, panel, backdropButton != null ? backdropButton.targetGraphic : null);

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
