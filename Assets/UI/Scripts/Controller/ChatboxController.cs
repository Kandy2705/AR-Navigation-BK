using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26;

public class ChatboxController : IPageController
{
    private readonly ChatService _chatService = new ChatService();

    private VisualElement _root;
    private NavigationManager _navigator;

    private Label _headerTitle;
    private ScrollView _messageList;
    private TextField _input;
    private Button _btnSend;

    private bool _isSending;

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        _root = root;
        _navigator = navigator;

        _headerTitle = root.Q<Label>("HeaderTitle");
        _messageList = root.Q<ScrollView>("MessageList");
        // Trong UI Chat.uxml ô input đặt name="PlaceHolder" và là custom TextField (PlaceHolder.cs).
        _input = root.Q<TextField>("PlaceHolder") ?? root.Q<TextField>("InputChat");
        _btnSend = root.Q<Button>("BtnSend");

        navigator.BindButton(root, "BtnBack", PageID.HistoryPage, true);

        InitHeader();
        ResetMessageList();
        RenderInitialMessages();
        BindSend();
    }

    private void InitHeader()
    {
        if (_headerTitle == null) return;

        string title = ChatSession.Current?.header;
        if (string.IsNullOrEmpty(title)) title = Routing.CurrentChatTitle;
        if (string.IsNullOrEmpty(title)) title = "Chatbot";

        _headerTitle.text = title;
    }

    private void ResetMessageList()
    {
        if (_messageList == null) return;
        // UXML có sẵn một số message demo — clear hết để render từ data.
        _messageList.contentContainer.Clear();
    }

    private void RenderInitialMessages()
    {
        var messages = ChatSession.Current?.messages;
        if (messages == null || messages.Count == 0) return;

        foreach (var msg in messages)
        {
            if (msg == null) continue;
            string content = msg.GetContent();
            if (string.IsNullOrEmpty(content)) continue;

            string sender = msg.GetSender();
            bool isUser = !string.IsNullOrEmpty(sender)
                          && sender.Equals("user", System.StringComparison.OrdinalIgnoreCase);
            AddMessage(content, isUser);
        }
    }

    private void BindSend()
    {
        if (_btnSend == null) return;

        _btnSend.clicked += HandleSend;

        if (_input != null)
        {
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    HandleSend();
                    evt.StopPropagation();
                }
            });
        }
    }

    private void HandleSend()
    {
        if (_isSending) return;

        string text = _input != null ? _input.value : "";
        if (string.IsNullOrWhiteSpace(text)) return;

        // 1. Echo tin nhắn của user lên ngay
        AddMessage(text, isUser: true);
        if (_input != null) _input.value = "";

        // 2. Cập nhật vào ChatSession.Current để giữ trạng thái khi quay lại
        if (ChatSession.Current != null)
        {
            if (ChatSession.Current.messages == null)
                ChatSession.Current.messages = new List<ChatMessage>();
            ChatSession.Current.messages.Add(new ChatMessage { contact_person = "user", content = text });
        }

        // 3. Gọi API
        _isSending = true;
        _btnSend.SetEnabled(false);

        string historyId = ChatSession.Current != null ? ChatSession.Current.id : null;

        _chatService.SendMessage(text, historyId)
            .Then(res =>
            {
                Debug.Log($"[Chatbox] Server trả về: {res.Text}");

                ChatMessage botMsg = ParseBotReply(res.Text);
                string botContent = botMsg != null ? botMsg.GetContent() : null;
                if (string.IsNullOrEmpty(botContent)) botContent = "(không có nội dung)";

                AddMessage(botContent, isUser: false);

                if (ChatSession.Current != null && botMsg != null)
                {
                    if (ChatSession.Current.messages == null)
                        ChatSession.Current.messages = new List<ChatMessage>();
                    ChatSession.Current.messages.Add(botMsg);
                }
            })
            .Catch(err =>
            {
                Debug.LogError($"[Chatbox] Lỗi POST /chat/chatboxes: {err.Message}");
                AddMessage("Xin lỗi, hệ thống đang gặp lỗi. Vui lòng thử lại sau.", isUser: false);

                var reqErr = err as RequestException;
                if (reqErr != null && reqErr.StatusCode == 401)
                {
                    _navigator.Navigate(PageID.Login, true);
                }
            })
            .Finally(() =>
            {
                _isSending = false;
                _btnSend.SetEnabled(true);
            });
    }

    /// <summary>
    /// POST /chat/chatboxes trả wrapper { success, data: List&lt;ChatMessage&gt;, message }.
    /// data thường có 2 phần tử: tin nhắn user vừa gửi + tin trả lời của assistant.
    /// Lấy phần tử cuối cùng có contact_person khác "user" (assistant/bot).
    /// </summary>
    private static ChatMessage ParseBotReply(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var wrapper = JsonUtility.FromJson<SendMessageResponse>(json);
            if (wrapper == null || wrapper.data == null || wrapper.data.Count == 0) return null;

            for (int i = wrapper.data.Count - 1; i >= 0; i--)
            {
                var m = wrapper.data[i];
                if (m == null) continue;
                string sender = m.GetSender();
                bool isUser = !string.IsNullOrEmpty(sender)
                              && sender.Equals("user", System.StringComparison.OrdinalIgnoreCase);
                if (!isUser && !string.IsNullOrEmpty(m.GetContent())) return m;
            }

            return wrapper.data[wrapper.data.Count - 1];
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Chatbox] Parse reply fail: {ex.Message}");
            return null;
        }
    }

    private void AddMessage(string text, bool isUser)
    {
        if (_messageList == null) return;

        var container = new VisualElement();
        container.AddToClassList("msg-container");
        container.AddToClassList(isUser ? "msg-right" : "msg-left");

        if (isUser)
        {
            // UXML mock đang dùng inline style cho msg-right: nền tím #483C9B.
            container.style.backgroundColor = new StyleColor(new Color(72f / 255f, 60f / 255f, 155f / 255f));
        }

        var lbl = new Label(text);
        lbl.AddToClassList("msg-text");

        container.Add(lbl);
        _messageList.contentContainer.Add(container);

        // Scroll xuống đáy ở frame kế tiếp
        _messageList.schedule.Execute(() =>
        {
            if (_messageList.verticalScroller != null)
                _messageList.scrollOffset = new Vector2(0, _messageList.verticalScroller.highValue);
        }).StartingIn(50);
    }
}
