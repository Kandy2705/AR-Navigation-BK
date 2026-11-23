using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Full-screen "success" card (check mark, title, message, spinner) shown over a page. A dim scrim
    /// blocks input behind it. Used by login ("Đăng nhập thành công!") and password reset.
    /// </summary>
    public class UiStatusOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("The centered card that pops in.")]
        [SerializeField] private RectTransform card;

        private Coroutine pending;
        private UiOverlayMotion motion;

        public bool IsVisible => gameObject.activeSelf && (motion == null || motion.Visible);

        public void Show(string title, string message)
        {
            StopPending();
            if (titleLabel != null)
            {
                titleLabel.text = title ?? string.Empty;
            }

            if (messageLabel != null)
            {
                messageLabel.text = message ?? string.Empty;
            }

            gameObject.SetActive(true);
            Motion.Show();
        }

        /// <summary>Shows the card, then calls <paramref name="onDone"/> after <paramref name="seconds"/> (real time).</summary>
        public void ShowThen(string title, string message, float seconds, Action onDone)
        {
            Show(title, message);
            if (Application.isPlaying)
            {
                pending = StartCoroutine(Wait(seconds, onDone));
            }
        }

        public void Hide()
        {
            StopPending();
            Motion.Hide(() => gameObject.SetActive(false));
        }

        private UiOverlayMotion Motion => motion ??= new UiOverlayMotion(this, OverlayStyle.Pop, card);

        private IEnumerator Wait(float seconds, Action onDone)
        {
            yield return new WaitForSecondsRealtime(seconds);
            pending = null;
            onDone?.Invoke();
        }

        private void StopPending()
        {
            if (pending == null)
            {
                return;
            }

            StopCoroutine(pending);
            pending = null;
        }
    }
}
