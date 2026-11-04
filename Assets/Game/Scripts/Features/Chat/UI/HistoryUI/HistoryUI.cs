using System;
using System.Collections.Generic;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Chat
{
    /// <summary>
    /// "Lịch sử": the user's conversations, newest first, with time-range chips, swipe-to-delete rows, a
    /// confirmed "delete all", search, and the bottom tab bar.
    /// </summary>
    public class HistoryUI : UiPage
    {
        public const string DeleteAllTitle = "Xóa tất cả lịch sử";
        public const string DeleteAllMessage = "Bạn có chắc chắn muốn xóa tất cả lịch sử không ?";
        public const string EmptyMessage = "Chưa có lịch sử nào";

        [SerializeField] private Button backButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button clearAllButton;
        [SerializeField] private UiChipRow rangeChips;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UiHistoryRow rowTemplate;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private UiBottomNav bottomNav;
        [SerializeField] private UiModal confirmModal;
        [SerializeField] private UiBusyOverlay busy;

        private HistoryRowList list;
        private bool opening;

        private void Awake()
        {
            list = new HistoryRowList(listContent, rowTemplate);
            backButton.onClick.AddListener(GoBack);
            searchButton.onClick.AddListener(() => OpenPage(PageID.SearchPage));
            clearAllButton.onClick.AddListener(AskDeleteAll);
            rangeChips.Changed += _ => Render();
            bottomNav.TabClicked += tab => UiBottomNav.Navigate(Navigation, tab);
            confirmModal.Confirmed += DeleteAll;
        }

        protected override void OnShow()
        {
            bottomNav.Select(UiBottomNav.HistoryTab);
            rangeChips.SetOptions(HistoryFilter.RangeLabels);
            opening = false;
            confirmModal.Hide();
            Render();
            Load();
        }

        private void Load()
        {
            busy.Show();
            ChatServices.Api.GetHistories(
                items =>
                {
                    ChatState.Histories = items;
                    busy.Hide();
                    Render();
                },
                (code, message) =>
                {
                    Debug.LogWarning($"[HistoryUI] Could not load the history ({code}): {message}");
                    busy.Hide();
                    if (code == 401 && Navigation != null)
                    {
                        Navigation.Navigate(PageID.Login, true);
                    }
                });
        }

        private void Render()
        {
            HistoryRange? range = rangeChips.Selected < 0 ? (HistoryRange?)null : (HistoryRange)rangeChips.Selected;
            List<ChatHistoryItem> shown = HistoryFilter.Apply(ChatState.Histories, range, null, DateTime.Now);
            list.Show(shown, Open, Delete);
            emptyLabel.gameObject.SetActive(shown.Count == 0 && !busy.IsVisible);
            emptyLabel.text = EmptyMessage;
        }

        private void Open(ChatHistoryItem item)
        {
            if (opening)
            {
                return;
            }

            opening = true;
            busy.Show();
            ChatSessionStarter.OpenExisting(ChatServices.Api, item, () =>
            {
                busy.Hide();
                opening = false;
                OpenPage(PageID.Chatbox);
            });
        }

        private void Delete(ChatHistoryItem item)
        {
            if (string.IsNullOrEmpty(item.id))
            {
                return;
            }

            ChatServices.Api.DeleteHistory(
                item.id,
                () =>
                {
                    ChatState.Histories.RemoveAll(x => x != null && x.id == item.id);
                    Render();
                },
                (code, message) => Debug.LogWarning($"[HistoryUI] Could not delete {item.id} ({code}): {message}"));
        }

        private void AskDeleteAll()
        {
            if (ChatState.Histories.Count == 0)
            {
                return;
            }

            confirmModal.SetTitleColor(null);
            confirmModal.Show(DeleteAllTitle, DeleteAllMessage, "Xóa", "Hủy");
        }

        /// <summary>The backend deletes one conversation at a time, so "delete all" sends one request per entry.</summary>
        private void DeleteAll()
        {
            List<ChatHistoryItem> targets = ChatState.Histories.FindAll(x => x != null && !string.IsNullOrEmpty(x.id));
            if (targets.Count == 0)
            {
                ChatState.Histories.Clear();
                Render();
                return;
            }

            int pending = targets.Count;
            busy.Show();
            foreach (ChatHistoryItem target in targets)
            {
                ChatServices.Api.DeleteHistory(
                    target.id,
                    () =>
                    {
                        ChatState.Histories.Remove(target);
                        Finish(ref pending);
                    },
                    (code, message) =>
                    {
                        Debug.LogWarning($"[HistoryUI] Could not delete {target.id} ({code}): {message}");
                        Finish(ref pending);
                    });
            }
        }

        private void Finish(ref int pending)
        {
            pending--;
            if (pending > 0)
            {
                return;
            }

            busy.Hide();
            Render();
        }
    }
}
