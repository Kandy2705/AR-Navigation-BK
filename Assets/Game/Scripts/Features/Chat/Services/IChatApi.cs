using System;
using System.Collections.Generic;

namespace Game.Features.Chat
{
    /// <summary>
    /// Backend access of the chat and history screens. Errors carry the HTTP status (401 means the session
    /// expired; -1 means the request never completed) and a message fit for logging.
    /// </summary>
    public interface IChatApi
    {
        void GetHistories(Action<List<ChatHistoryItem>> onSuccess, Action<long, string> onError);

        void CreateHistory(string header, Action<ChatHistoryItem> onSuccess, Action<long, string> onError);

        void DeleteHistory(string historyId, Action onSuccess, Action<long, string> onError);

        void GetMessages(string historyId, Action<List<ChatMessage>> onSuccess, Action<long, string> onError);

        /// <summary>Sends a question; the callback receives the assistant's reply.</summary>
        void Send(string content, string historyId, Action<ChatMessage> onSuccess, Action<long, string> onError);
    }
}
