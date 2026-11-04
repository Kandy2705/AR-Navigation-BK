using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Chat
{
    /// <summary>Fills a list container with <see cref="UiHistoryRow"/> clones; shared by the history and search screens.</summary>
    public sealed class HistoryRowList
    {
        private readonly RectTransform content;
        private readonly UiHistoryRow template;
        private readonly List<UiHistoryRow> rows = new List<UiHistoryRow>();

        public HistoryRowList(RectTransform content, UiHistoryRow template)
        {
            this.content = content;
            this.template = template;
            template.gameObject.SetActive(false);
        }

        public IReadOnlyList<UiHistoryRow> Rows => rows;

        public void Show(IList<ChatHistoryItem> items, Action<ChatHistoryItem> onOpen, Action<ChatHistoryItem> onDelete)
        {
            Clear();
            foreach (ChatHistoryItem item in items)
            {
                UiHistoryRow row = UnityEngine.Object.Instantiate(template, content);
                row.name = "Row" + (rows.Count + 1);
                row.gameObject.SetActive(true);
                row.Bind(item);
                row.Opened += onOpen;
                row.DeleteRequested += onDelete;
                rows.Add(row);
            }
        }

        public void Clear()
        {
            foreach (UiHistoryRow row in rows)
            {
                if (row != null)
                {
                    Game.Foundation.UI.UiGameObjects.Discard(row.gameObject);
                }
            }

            rows.Clear();
        }
    }
}
