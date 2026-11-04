using System;
using System.Collections.Generic;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Chat
{
    /// <summary>One conversation: bubbles for the user's questions (purple, right) and the assistant's answers (gray, left).</summary>
    public class ChatUI : UiPage
    {
        public const string DefaultTitle = "Chatbot";
        public const string FailedReply = "Xin lỗi, hệ thống đang gặp lỗi. Vui lòng thử lại sau.";
        public const string EmptyReply = "(không có nội dung)";

        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform messageContent;
        [SerializeField] private UiChatBubble userBubble;
        [SerializeField] private UiChatBubble botBubble;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button sendButton;

        private readonly List<UiChatBubble> bubbles = new List<UiChatBubble>();
        private bool sending;

        public IReadOnlyList<UiChatBubble> Bubbles => bubbles;

        private void Awake()
        {
            userBubble.gameObject.SetActive(false);
            botBubble.gameObject.SetActive(false);
            backButton.onClick.AddListener(GoBack);
            sendButton.onClick.AddListener(Send);
            input.onSubmit.AddListener(_ => Send());
        }

        protected override void OnShow()
        {
            sending = false;
            sendButton.interactable = true;
            input.text = string.Empty;
            ClearBubbles();

            ChatHistoryItem session = ChatState.Current;
            titleLabel.text = string.IsNullOrWhiteSpace(session?.header) ? DefaultTitle : session.header;
            if (session?.messages != null)
            {
                foreach (ChatMessage message in session.messages)
                {
                    string text = message?.GetContent();
                    if (!string.IsNullOrEmpty(text))
                    {
                        AddBubble(text, ChatMessages.IsFromUser(message));
                    }
                }
            }
        }

        private void Send()
        {
            string text = input.text.Trim();
            if (sending || text.Length == 0)
            {
                return;
            }

            AddBubble(text, true);
            input.text = string.Empty;
            Remember(ChatMessages.FromUser(text));

            sending = true;
            sendButton.interactable = false;
            ChatServices.Api.Send(
                text,
                ChatState.Current?.id,
                reply =>
                {
                    string answer = reply?.GetContent();
                    AddBubble(string.IsNullOrEmpty(answer) ? EmptyReply : answer, false);
                    if (reply != null)
                    {
                        Remember(reply);
                    }

                    Done();
                },
                (code, message) =>
                {
                    Debug.LogWarning($"[ChatUI] Could not send the question ({code}): {message}");
                    AddBubble(FailedReply, false);
                    Done();
                    if (code == 401 && Navigation != null)
                    {
                        Navigation.Navigate(PageID.Login, true);
                    }
                });
        }

        private void Done()
        {
            sending = false;
            sendButton.interactable = true;
        }

        private static void Remember(ChatMessage message)
        {
            ChatHistoryItem session = ChatState.Current;
            if (session == null)
            {
                return;
            }

            if (session.messages == null)
            {
                session.messages = new List<ChatMessage>();
            }

            session.messages.Add(message);
        }

        private void AddBubble(string text, bool fromUser)
        {
            UiChatBubble bubble = Instantiate(fromUser ? userBubble : botBubble, messageContent);
            bubble.gameObject.SetActive(true);
            bubble.name = (fromUser ? "User" : "Bot") + (bubbles.Count + 1);
            bubble.SetText(text);
            bubbles.Add(bubble);

            LayoutRebuilder.ForceRebuildLayoutImmediate(messageContent);
            scroll.verticalNormalizedPosition = 0f;
        }

        private void ClearBubbles()
        {
            foreach (UiChatBubble bubble in bubbles)
            {
                if (bubble != null)
                {
                    UiGameObjects.Discard(bubble.gameObject);
                }
            }

            bubbles.Clear();
        }
    }
}
