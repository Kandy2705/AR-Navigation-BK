using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Scrolling list of options with one selected row (used by <see cref="UiPickerSheet"/>). Rows are cloned
    /// from a hidden template button and reused when the options change.
    /// </summary>
    public class UiOptionColumn : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private Button itemTemplate;
        [SerializeField] private float itemHeight = 44f;

        private readonly List<Button> rows = new List<Button>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private readonly List<Image> highlights = new List<Image>();
        private int optionCount;

        public int SelectedIndex { get; private set; } = -1;

        public void SetOptions(IList<string> options, int selected)
        {
            optionCount = options.Count;
            for (int i = 0; i < optionCount; i++)
            {
                if (i >= rows.Count)
                {
                    CreateRow();
                }

                rows[i].gameObject.SetActive(true);
                labels[i].text = options[i];
            }

            for (int i = optionCount; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(false);
            }

            Select(Mathf.Clamp(selected, 0, Mathf.Max(0, optionCount - 1)), true);
        }

        public void Select(int index, bool scrollToIt)
        {
            SelectedIndex = index;
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i == index;
                highlights[i].color = on ? UiTheme.Surface : Color.clear;
                labels[i].color = on ? UiTheme.TextPrimary : UiTheme.Placeholder;
            }

            if (scrollToIt)
            {
                ScrollTo(index);
            }
        }

        private void CreateRow()
        {
            Button row = Instantiate(itemTemplate, content, false);
            row.gameObject.SetActive(true);
            int index = rows.Count;
            row.onClick.AddListener(() => Select(index, false));
            rows.Add(row);
            labels.Add(row.GetComponentInChildren<TMP_Text>(true));
            highlights.Add(row.GetComponent<Image>());
        }

        /// <summary>Puts the selected row in the middle of the visible area when there is room to scroll.</summary>
        private void ScrollTo(int index)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float viewport = ((RectTransform)scroll.viewport).rect.height;
            float total = optionCount * itemHeight;
            float target = index * itemHeight - viewport * 0.5f + itemHeight * 0.5f;
            float y = Mathf.Clamp(target, 0f, Mathf.Max(0f, total - viewport));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
        }
    }
}
