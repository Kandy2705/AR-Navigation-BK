#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Features.Chat;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>In-memory chat backend that answers at once, so screens can be driven without a network.</summary>
    internal sealed class FakeChatApi : IChatApi
    {
        public List<ChatHistoryItem> Histories = new List<ChatHistoryItem>();
        public Dictionary<string, List<ChatMessage>> Messages = new Dictionary<string, List<ChatMessage>>();
        public readonly List<string> Deleted = new List<string>();
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Sent = new List<string>();
        public string Reply = "Đây là câu trả lời";
        public long? ListError;
        public long? CreateError;
        public long? SendError;

        public void GetHistories(Action<List<ChatHistoryItem>> onSuccess, Action<long, string> onError)
        {
            if (ListError.HasValue)
            {
                onError(ListError.Value, "list failed");
                return;
            }

            onSuccess(new List<ChatHistoryItem>(Histories));
        }

        public void CreateHistory(string header, Action<ChatHistoryItem> onSuccess, Action<long, string> onError)
        {
            Created.Add(header);
            if (CreateError.HasValue)
            {
                onError(CreateError.Value, "create failed");
                return;
            }

            onSuccess(new ChatHistoryItem { id = "new-" + Created.Count, header = header });
        }

        public void DeleteHistory(string historyId, Action onSuccess, Action<long, string> onError)
        {
            Deleted.Add(historyId);
            onSuccess();
        }

        public void GetMessages(string historyId, Action<List<ChatMessage>> onSuccess, Action<long, string> onError)
        {
            onSuccess(Messages.TryGetValue(historyId, out List<ChatMessage> messages) ? messages : new List<ChatMessage>());
        }

        public void Send(string content, string historyId, Action<ChatMessage> onSuccess, Action<long, string> onError)
        {
            Sent.Add(content);
            if (SendError.HasValue)
            {
                onError(SendError.Value, "send failed");
                return;
            }

            onSuccess(new ChatMessage { contact_person = "assistant", content = Reply });
        }

        public static ChatHistoryItem Item(string id, string header, DateTime date)
        {
            return new ChatHistoryItem { id = id, header = header, create_date = date.ToString("yyyy-MM-ddTHH:mm:ss") };
        }
    }
}
#endif
