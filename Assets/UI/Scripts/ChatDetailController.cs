using UnityEngine;
using UnityEngine.UIElements;

// [SỬA] Bỏ MonoBehaviour, biến thành class thường
public class ChatDetailController
{
    private VisualElement _mainRoot;
    private VisualElement _chatRoot;
    private ScrollView _messageList;

    // [SỬA] Dùng Constructor để nhận Root và Template từ Routing
    public ChatDetailController(VisualElement mainRoot, VisualTreeAsset chatTemplate, string chatTitle)
    {
        _mainRoot = mainRoot;

        if (chatTemplate == null) {
            Debug.LogError("Chat Template bị null!");
            return;
        }

        // 1. Tạo UI Chat
        TemplateContainer chatInstance = chatTemplate.Instantiate();
        _chatRoot = chatInstance.Q<VisualElement>("ChatScreen"); // Đảm bảo UXML Chat có tên này

        // Fix full màn hình
        _chatRoot.style.height = Length.Percent(100);
        _chatRoot.style.width = Length.Percent(100);
        
        // Setup vị trí ban đầu (ẩn bên phải để chờ trượt vào)
        _chatRoot.AddToClassList("screen-overlay");
        
        // Add thẳng vào Main Root (đè lên tất cả)
        _mainRoot.Add(_chatRoot);

        // 2. Setup Logic & Event
        SetupLogic(chatTitle);

        // 3. Animation trượt vào (Thay Coroutine bằng Schedule)
        _chatRoot.schedule.Execute(() => {
            _chatRoot.AddToClassList("screen-in");
        }).StartingIn(10); 
    }

    private void SetupLogic(string title)
    {
        var header = _chatRoot.Q<Label>("HeaderTitle");
        var btnBack = _chatRoot.Q<Button>("BtnBack");
        _messageList = _chatRoot.Q<ScrollView>("MessageList");
        var input = _chatRoot.Q<TextField>("InputChat");
        var btnSend = _chatRoot.Q<Button>("BtnSend");

        if (header != null) header.text = title;
        if (_messageList != null) _messageList.contentContainer.Clear();

        // Nút Back
        if (btnBack != null) btnBack.RegisterCallback<ClickEvent>(evt => CloseChat());

        // Nút Gửi
        if (btnSend != null && input != null)
        {
            btnSend.RegisterCallback<ClickEvent>(evt => HandleSendMessage(input));
        }
    }

    private void HandleSendMessage(TextField input)
    {
        string text = input.value;
        if (string.IsNullOrWhiteSpace(text)) return;

        AddMessage(text, true);
        input.value = "";

        // Giả lập bot trả lời
        _chatRoot.schedule.Execute(() => {
            AddMessage("Bot: Đã nhận " + text, false);
        }).StartingIn(1000);
    }

    private void AddMessage(string text, bool isUser)
    {
        if (_messageList == null) return;

        var container = new VisualElement();
        container.AddToClassList("msg-container");
        container.AddToClassList(isUser ? "msg-right" : "msg-left");

        var lbl = new Label(text);
        lbl.AddToClassList("msg-text");

        container.Add(lbl);
        _messageList.contentContainer.Add(container);

        // Scroll xuống đáy
        _messageList.schedule.Execute(() => {
            if(_messageList.verticalScroller != null)
                _messageList.scrollOffset = new Vector2(0, _messageList.verticalScroller.highValue);
        }).StartingIn(50);
    }

    public void CloseChat()
    {
        if (_chatRoot == null) return;

        // Animation trượt ra
        _chatRoot.RemoveFromClassList("screen-in");

        // Đợi animation xong (0.35s) rồi xóa UI
        _chatRoot.schedule.Execute(() => {
            _chatRoot.RemoveFromHierarchy();
            _chatRoot = null; 
        }).StartingIn(350);
    }
}