using System.Collections.Generic;

namespace Game.Features.Chat
{
    /// <summary>The history list and the conversation that is open, shared by the history, search and chat screens.</summary>
    public static class ChatState
    {
        public static List<ChatHistoryItem> Histories { get; set; } = new List<ChatHistoryItem>();

        public static ChatHistoryItem Current { get; set; }

        public static void Clear()
        {
            Histories = new List<ChatHistoryItem>();
            Current = null;
        }
    }
}
