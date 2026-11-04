using System;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Chat
{
    /// <summary>Search over the conversation titles. Nothing is listed until the user types; no match shows "Không tìm thấy".</summary>
    public class SearchUI : UiPage
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_InputField queryInput;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UiHistoryRow rowTemplate;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private UiBusyOverlay busy;

        private HistoryRowList list;
        private bool opening;

        public int ResultCount => list.Rows.Count;

        public bool ShowsEmptyState => emptyState.activeSelf;

        private void Awake()
        {
            list = new HistoryRowList(listContent, rowTemplate);
            backButton.onClick.AddListener(GoBack);
            queryInput.onValueChanged.AddListener(_ => Render());
        }

        protected override void OnShow()
        {
            opening = false;
            queryInput.SetTextWithoutNotify(string.Empty);
            Render();
            queryInput.ActivateInputField();
        }

        private void Render()
        {
            string query = queryInput.text;
            if (string.IsNullOrWhiteSpace(query))
            {
                list.Clear();
                emptyState.SetActive(false);
                return;
            }

            var results = HistoryFilter.Apply(ChatState.Histories, null, query, DateTime.Now);
            list.Show(results, Open, Delete);
            emptyState.SetActive(results.Count == 0);
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
                (code, message) => Debug.LogWarning($"[SearchUI] Could not delete {item.id} ({code}): {message}"));
        }
    }
}
