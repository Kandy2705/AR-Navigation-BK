namespace Game.Features.Chat
{
    /// <summary>Where the chat screens get their backend; swap <see cref="Api"/> in tests.</summary>
    public static class ChatServices
    {
        public static IChatApi Api { get; set; } = new RestChatApi();
    }
}
