using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>One FAQ entry: the question is always shown, the answer unfolds when it is tapped.</summary>
    public class UiAccordionItem : MonoBehaviour
    {
        [SerializeField] private Button header;
        [SerializeField] private TMP_Text questionLabel;
        [SerializeField] private TMP_Text answerLabel;
        [SerializeField] private RectTransform chevron;

        private const float OpenAngle = 90f;
        private const float ChevronSeconds = 0.22f;
        private const float AnswerFadeSeconds = 0.28f;

        private Coroutine running;

        public event Action<UiAccordionItem> Toggled;

        public bool IsOpen { get; private set; }

        public string Question => questionLabel.text;

        private void Awake()
        {
            header.onClick.AddListener(() =>
            {
                SetOpen(!IsOpen);
                Toggled?.Invoke(this);
            });
        }

        public void Bind(string question, string answer)
        {
            questionLabel.text = question;
            answerLabel.text = answer;
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            answerLabel.gameObject.SetActive(open);
            if (isActiveAndEnabled)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            }

            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            // The chevron points right when folded and up when open; the answer fades in as it unfolds.
            float target = open ? OpenAngle : 0f;
            if (!UiMotion.CanAnimate(this))
            {
                chevron.localRotation = Quaternion.Euler(0f, 0f, target);
                answerLabel.alpha = 1f;
                return;
            }

            running = StartCoroutine(Animate(target, open));
        }

        private void OnDisable()
        {
            running = null;
            chevron.localRotation = Quaternion.Euler(0f, 0f, IsOpen ? OpenAngle : 0f);
            answerLabel.alpha = 1f;
        }

        private IEnumerator Animate(float targetAngle, bool fadeAnswer)
        {
            float fromAngle = chevron.localEulerAngles.z;
            if (fromAngle > 180f)
            {
                fromAngle -= 360f;
            }

            float seconds = Mathf.Max(ChevronSeconds, fadeAnswer ? AnswerFadeSeconds : 0f);
            yield return UiMotion.Run(seconds, t =>
            {
                float elapsed = t * seconds;
                float turn = UiMotion.OutCubic(elapsed / ChevronSeconds);
                chevron.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, targetAngle, turn));
                if (fadeAnswer)
                {
                    answerLabel.alpha = UiMotion.OutCubic(elapsed / AnswerFadeSeconds);
                }
            });
            running = null;
        }
    }
}
