using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Row of single-digit boxes for a one-time code. Typing a digit jumps to the next box, deleting one
    /// jumps back, and the focused box is highlighted like in the Figma.
    /// </summary>
    public class UiOtpInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField[] digits;
        [SerializeField] private Image[] boxes;

        public event Action<string> Changed;

        public int Length => digits.Length;

        public string Code
        {
            get
            {
                var builder = new StringBuilder(digits.Length);
                foreach (TMP_InputField digit in digits)
                {
                    builder.Append(digit.text);
                }

                return builder.ToString();
            }
        }

        public bool IsComplete => Code.Length == digits.Length;

        private void Awake()
        {
            for (int i = 0; i < digits.Length; i++)
            {
                int index = i;
                TMP_InputField digit = digits[i];
                digit.characterLimit = 1;
                digit.contentType = TMP_InputField.ContentType.Custom;
                digit.characterValidation = TMP_InputField.CharacterValidation.Digit;
                digit.keyboardType = TouchScreenKeyboardType.NumberPad;
                digit.onValueChanged.AddListener(value => HandleChanged(index, value));
                digit.onSelect.AddListener(_ => SetHighlight(index, true));
                digit.onDeselect.AddListener(_ => SetHighlight(index, false));
                SetHighlight(index, false);
            }
        }

        public void Clear()
        {
            foreach (TMP_InputField digit in digits)
            {
                digit.SetTextWithoutNotify(string.Empty);
            }

            Changed?.Invoke(Code);
        }

        public void FocusFirst()
        {
            if (digits.Length > 0)
            {
                digits[0].ActivateInputField();
            }
        }

        private void HandleChanged(int index, string value)
        {
            if (value.Length == 1 && index < digits.Length - 1)
            {
                digits[index + 1].ActivateInputField();
            }
            else if (value.Length == 0 && index > 0)
            {
                digits[index - 1].ActivateInputField();
            }

            Changed?.Invoke(Code);
        }

        private void SetHighlight(int index, bool active)
        {
            if (boxes != null && index < boxes.Length && boxes[index] != null)
            {
                boxes[index].color = active ? UiTheme.OtpBoxActive : UiTheme.OtpBox;
            }
        }
    }
}
