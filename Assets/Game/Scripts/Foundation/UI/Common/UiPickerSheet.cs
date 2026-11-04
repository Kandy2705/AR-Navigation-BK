using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Bottom sheet with one to three option columns and a "Xong" button. One column picks a gender; three
    /// columns (day, month, year) pick a birthday. Tapping the dim area outside closes it without a choice.
    /// </summary>
    public class UiPickerSheet : MonoBehaviour
    {
        public struct Column
        {
            public IList<string> Options;
            public int Selected;
        }

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private UiOptionColumn[] columns;
        [SerializeField] private Button doneButton;
        [SerializeField] private Button scrimButton;

        private Action<int[]> onDone;
        private int activeColumns;

        public bool IsVisible => gameObject.activeSelf;

        public int MaxColumns => columns.Length;

        private void Awake()
        {
            doneButton.onClick.AddListener(Done);
            scrimButton.onClick.AddListener(Hide);
        }

        public void Show(string title, IList<Column> layout, Action<int[]> done)
        {
            titleLabel.text = title;
            onDone = done;
            activeColumns = Mathf.Min(layout.Count, columns.Length);

            // Columns are filled after the sheet is active so their viewports have a size to scroll against.
            gameObject.SetActive(true);
            for (int i = 0; i < columns.Length; i++)
            {
                bool used = i < activeColumns;
                columns[i].gameObject.SetActive(used);
                if (used)
                {
                    columns[i].SetOptions(layout[i].Options, layout[i].Selected);
                }
            }
        }

        public void Hide()
        {
            onDone = null;
            gameObject.SetActive(false);
        }

        private void Done()
        {
            var picked = new int[activeColumns];
            for (int i = 0; i < activeColumns; i++)
            {
                picked[i] = columns[i].SelectedIndex;
            }

            Action<int[]> callback = onDone;
            Hide();
            callback?.Invoke(picked);
        }
    }
}
