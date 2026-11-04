using System;

namespace Game.Features.Chat
{
    public static class ChatMessages
    {
        public const string UserSender = "user";

        public static bool IsFromUser(ChatMessage message)
        {
            string sender = message?.GetSender();
            return !string.IsNullOrEmpty(sender) && sender.Equals(UserSender, StringComparison.OrdinalIgnoreCase);
        }

        public static ChatMessage FromUser(string text)
        {
            return new ChatMessage { contact_person = UserSender, content = text };
        }
    }
}
