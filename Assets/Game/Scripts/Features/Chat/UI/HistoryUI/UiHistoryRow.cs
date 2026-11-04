using System;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Chat
{
    /// <summary>One conversation in the history and search lists: title, date, and swipe-to-delete.</summary>
    public class UiHistoryRow : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private UiSwipeRow swipe;

        public event Action<ChatHistoryItem> Opened;
        public event Action<ChatHistoryItem> DeleteRequested;

        public ChatHistoryItem Item { get; private set; }

        public string Title => titleLabel.text;

        private void Awake()
        {
            openButton.onClick.AddListener(HandleTap);
            swipe.DeleteRequested += () => DeleteRequested?.Invoke(Item);
        }

        public void Bind(ChatHistoryItem item)
        {
            Item = item;
            titleLabel.text = HistoryFilter.TitleOf(item);
            timeLabel.text = HistoryFilter.DisplayDate(item.create_date);
            swipe.Close();
        }

        private void HandleTap()
        {
            // A tap on a row that is showing its delete button only closes it.
            if (swipe.IsOpen)
            {
                swipe.Close();
                return;
            }

            Opened?.Invoke(Item);
        }
    }
}
