using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// A horizontally scrolling row of single-select chips (history filters, support categories). It clones a
    /// hidden chip template per label. With <c>allowDeselect</c> tapping the chosen chip clears the choice.
    /// </summary>
    public class UiChipRow : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Button chipTemplate;
        [SerializeField] private bool allowDeselect;
        [SerializeField] private Color normalFill = Color.clear;
        [SerializeField] private Color selectedFill = new Color(0.231f, 0.192f, 0.537f, 1f);
        [SerializeField] private Color normalText = Color.gray;
        [SerializeField] private Color selectedText = Color.white;

        private readonly List<Button> chips = new List<Button>();

        public event Action<int> Changed;

        /// <summary>Index of the chosen chip, or -1 when none is chosen.</summary>
        public int Selected { get; private set; } = -1;

        public int Count => chips.Count;

        public void SetOptions(IList<string> labels, int selected = -1)
        {
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                UiGameObjects.Discard(chips[i].gameObject);
            }

            chips.Clear();
            chipTemplate.gameObject.SetActive(false);
            for (int i = 0; i < labels.Count; i++)
            {
                int index = i;
                Button chip = Instantiate(chipTemplate, content);
                chip.name = "Chip" + (i + 1);
                chip.GetComponentInChildren<TMP_Text>(true).text = labels[i];
                chip.onClick.AddListener(() => Select(index, true));
                chip.gameObject.SetActive(true);
                chips.Add(chip);
            }

            Selected = selected;
            Render();
        }

        public void Select(int index, bool notify)
        {
            if (index == Selected && allowDeselect)
            {
                index = -1;
            }

            if (index == Selected)
            {
                return;
            }

            Selected = index;
            Render();
            if (notify)
            {
                Changed?.Invoke(Selected);
            }
        }

        private void Render()
        {
            for (int i = 0; i < chips.Count; i++)
            {
                bool on = i == Selected;
                Image fill = chips[i].targetGraphic as Image;
                if (fill != null)
                {
                    fill.color = on ? selectedFill : normalFill;
                }

                chips[i].GetComponentInChildren<TMP_Text>(true).color = on ? selectedText : normalText;
            }
        }
    }
}
