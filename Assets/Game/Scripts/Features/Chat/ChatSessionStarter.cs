using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Chat
{
    /// <summary>Opening a conversation: a new one from the AR page, or an existing one from the history lists.</summary>
    public static class ChatSessionStarter
    {
        public static string NewChatTitle(DateTime now)
        {
            return $"Cuộc trò chuyện {now:dd-MM HH:mm}";
        }

        /// <summary>
        /// Creates a conversation and makes it current. When the backend refuses (offline) the chat still opens
        /// with a local session; a 401 reports <paramref name="onUnauthorized"/> instead.
        /// </summary>
        public static void StartNew(IChatApi api, DateTime now, Action onReady, Action onUnauthorized)
        {
            string title = NewChatTitle(now);
            api.CreateHistory(
                title,
                created =>
                {
                    ChatState.Current = created;
                    ChatState.Histories.Insert(0, created);
                    onReady();
                },
                (code, message) =>
                {
                    Debug.LogWarning($"[ChatSessionStarter] Could not create a conversation ({code}): {message}");
                    if (code == 401)
                    {
                        onUnauthorized();
                        return;
                    }

                    ChatState.Current = new ChatHistoryItem { header = title };
                    onReady();
                });
        }

        /// <summary>Makes <paramref name="item"/> current after loading its messages (an empty list when that fails).</summary>
        public static void OpenExisting(IChatApi api, ChatHistoryItem item, Action onReady)
        {
            ChatState.Current = item;
            if (string.IsNullOrEmpty(item.id))
            {
                onReady();
                return;
            }

            api.GetMessages(
                item.id,
                messages =>
                {
                    item.messages = messages;
                    onReady();
                },
                (code, message) =>
                {
                    Debug.LogWarning($"[ChatSessionStarter] Could not load messages ({code}): {message}");
                    item.messages = new List<ChatMessage>();
                    onReady();
                });
        }
    }
}
