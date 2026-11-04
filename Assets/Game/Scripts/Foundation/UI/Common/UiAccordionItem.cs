using System;
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
            // The chevron points right when folded and up when open.
            chevron.localRotation = Quaternion.Euler(0f, 0f, open ? 90f : 0f);
            if (isActiveAndEnabled)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            }
        }
    }
}
