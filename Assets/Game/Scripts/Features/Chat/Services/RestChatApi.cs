using System;
using System.Collections.Generic;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Chat
{
    /// <summary>The chat endpoints (chat/histories, chat/chatboxes) through the shared <see cref="ChatService"/>.</summary>
    public sealed class RestChatApi : IChatApi
    {
        private readonly ChatService service = new ChatService();

        public void GetHistories(Action<List<ChatHistoryItem>> onSuccess, Action<long, string> onError)
        {
            service.GetHistory()
                .Then(res =>
                {
                    ChatHistoryListResponse parsed = TryParse<ChatHistoryListResponse>(res.Text);
                    onSuccess(parsed?.data ?? new List<ChatHistoryItem>());
                })
                .Catch(error => Fail(error, onError));
        }

        public void CreateHistory(string header, Action<ChatHistoryItem> onSuccess, Action<long, string> onError)
        {
            service.CreateHistory(header)
                .Then(res => onSuccess(ParseCreated(res.Text, header)))
                .Catch(error => Fail(error, onError));
        }

        public void DeleteHistory(string historyId, Action onSuccess, Action<long, string> onError)
        {
            service.DeleteHistory(historyId)
                .Then(_ => onSuccess())
                .Catch(error => Fail(error, onError));
        }

        public void GetMessages(string historyId, Action<List<ChatMessage>> onSuccess, Action<long, string> onError)
        {
            service.GetChatboxMessages(historyId)
                .Then(res => onSuccess(ParseMessages(res.Text)))
                .Catch(error => Fail(error, onError));
        }

        public void Send(string content, string historyId, Action<ChatMessage> onSuccess, Action<long, string> onError)
        {
            service.SendMessage(content, historyId)
                .Then(res => onSuccess(ParseReply(res.Text)))
                .Catch(error => Fail(error, onError));
        }

        /// <summary>POST chat/histories answers { success, data: history }; some deployments answer the bare history.</summary>
        public static ChatHistoryItem ParseCreated(string json, string fallbackHeader)
        {
            // JsonUtility fills nested objects with defaults, so "data" is never null: look for an id instead.
            ChatHistoryResponse wrapper = TryParse<ChatHistoryResponse>(json);
            if (wrapper?.data != null && !string.IsNullOrEmpty(wrapper.data.id))
            {
                return wrapper.data;
            }

            ChatHistoryItem direct = TryParse<ChatHistoryItem>(json);
            if (direct != null && !string.IsNullOrEmpty(direct.id))
            {
                return direct;
            }

            return new ChatHistoryItem { header = fallbackHeader };
        }

        /// <summary>GET chat/chatboxes/history/{id} answers { data: [...] } or a bare array.</summary>
        public static List<ChatMessage> ParseMessages(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new List<ChatMessage>();
            }

            ChatHistoryMessagesResponse wrapper = TryParse<ChatHistoryMessagesResponse>(json);
            if (wrapper?.data != null)
            {
                return wrapper.data;
            }

            ChatHistoryMessagesResponse bare = TryParse<ChatHistoryMessagesResponse>("{\"data\":" + json + "}");
            return bare?.data ?? new List<ChatMessage>();
        }

        /// <summary>POST chat/chatboxes answers the user's message and the assistant's reply; the last non-user entry is the reply.</summary>
        public static ChatMessage ParseReply(string json)
        {
            SendMessageResponse wrapper = TryParse<SendMessageResponse>(json);
            if (wrapper?.data == null || wrapper.data.Count == 0)
            {
                return null;
            }

            for (int i = wrapper.data.Count - 1; i >= 0; i--)
            {
                ChatMessage message = wrapper.data[i];
                if (message != null && !ChatMessages.IsFromUser(message) && !string.IsNullOrEmpty(message.GetContent()))
                {
                    return message;
                }
            }

            return wrapper.data[wrapper.data.Count - 1];
        }

        private static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RestChatApi] Could not parse {typeof(T).Name}: {exception.Message}");
                return null;
            }
        }

        private static void Fail(Exception error, Action<long, string> onError)
        {
            var request = error as RequestException;
            onError(request != null ? request.StatusCode : -1, error.Message);
        }
    }
}
