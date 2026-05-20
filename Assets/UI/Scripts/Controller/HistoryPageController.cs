using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26;

public class HistoryPageController : IPageController
{
    private readonly ChatService _chatService = new ChatService();
    private VisualElement _root;
    private NavigationManager _navigator;
    private Button _btnNewChat;
    private VisualElement _loadingOverlay;
    private float _loadingAngle;
    private IVisualElementScheduledItem _spinTask;
    private bool _isCreating;

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("Navigating to History Page");
        _root = root;
        _navigator = navigator;

        // BtnBack vẫn bind nội bộ trang.
        navigator.BindButton(root, "BtnBack", PageID.None, true);

        _btnNewChat = root.Q<Button>("BtnNewChat");
        if (_btnNewChat != null) _btnNewChat.clicked += HandleCreateNewChat;

        BuildLoadingOverlay();
        ShowLoading(true);

        LoadHistory();
    }

    private void BuildLoadingOverlay()
    {
        // Overlay phủ toàn page khi đang tải lịch sử.
        _loadingOverlay = new VisualElement { name = "HistoryLoadingOverlay" };
        _loadingOverlay.style.position = Position.Absolute;
        _loadingOverlay.style.left = 0;
        _loadingOverlay.style.right = 0;
        _loadingOverlay.style.top = 0;
        _loadingOverlay.style.bottom = 0;
        _loadingOverlay.style.justifyContent = Justify.Center;
        _loadingOverlay.style.alignItems = Align.Center;
        _loadingOverlay.style.backgroundColor = new StyleColor(new Color(20f / 255f, 20f / 255f, 20f / 255f, 0.85f));
        _loadingOverlay.pickingMode = PickingMode.Position;
        _loadingOverlay.style.display = DisplayStyle.None;

        var spinner = new VisualElement { name = "HistoryLoadingSpinner" };
        spinner.style.width = 48;
        spinner.style.height = 48;
        spinner.style.borderTopLeftRadius = 24;
        spinner.style.borderTopRightRadius = 24;
        spinner.style.borderBottomLeftRadius = 24;
        spinner.style.borderBottomRightRadius = 24;
        spinner.style.borderTopWidth = 3;
        spinner.style.borderRightWidth = 3;
        spinner.style.borderBottomWidth = 3;
        spinner.style.borderLeftWidth = 3;
        var transparent = new Color(0f, 0f, 0f, 0f);
        var accent = new Color(72f / 255f, 60f / 255f, 155f / 255f);
        spinner.style.borderTopColor = accent;
        spinner.style.borderRightColor = transparent;
        spinner.style.borderBottomColor = transparent;
        spinner.style.borderLeftColor = transparent;
        _loadingOverlay.Add(spinner);

        var label = new Label("Đang tải...");
        label.style.color = Color.white;
        label.style.marginTop = 12;
        _loadingOverlay.Add(label);

        _root.Add(_loadingOverlay);

        _spinTask = _root.schedule.Execute(() =>
        {
            if (_loadingOverlay.style.display.value == DisplayStyle.None) return;
            _loadingAngle = (_loadingAngle + 12f) % 360f;
            spinner.style.rotate = new Rotate(Angle.Degrees(_loadingAngle));
        }).Every(16);
    }

    private void ShowLoading(bool show)
    {
        if (_loadingOverlay == null) return;
        _loadingOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        _loadingOverlay.pickingMode = show ? PickingMode.Position : PickingMode.Ignore;
    }

    private void HandleCreateNewChat()
    {
        if (_isCreating) return;
        _isCreating = true;
        _btnNewChat?.SetEnabled(false);

        string defaultTitle = $"Cuộc trò chuyện {System.DateTime.Now:dd-MM HH:mm}";

        _chatService.CreateHistory(defaultTitle)
            .Then(res =>
            {
                Debug.Log($"[HistoryPage] POST /chat/histories → {res.Text}");

                ChatHistoryItem created = ParseCreated(res.Text, defaultTitle);
                ChatSession.Current = created;
                Routing.CurrentChatTitle = created.header;

                if (ChatSession.HistoryList == null)
                    ChatSession.HistoryList = new System.Collections.Generic.List<ChatHistoryItem>();
                ChatSession.HistoryList.Insert(0, created);

                _navigator.Navigate(PageID.Chatbox, false);
            })
            .Catch(err =>
            {
                var reqErr = err as RequestException;
                string body = reqErr != null ? reqErr.Response : "(no body)";
                long code = reqErr != null ? reqErr.StatusCode : -1;
                Debug.LogError($"[HistoryPage] Lỗi tạo cuộc trò chuyện ({code}): {err.Message}\nResponse body: {body}");

                if (code == 401)
                {
                    _navigator.Navigate(PageID.Login, true);
                    return;
                }

                // Fallback: vẫn cho user vào chatbox với session local để thao tác,
                // server sẽ tạo session khi gửi tin nhắn đầu (nếu BE hỗ trợ).
                ChatSession.Current = new ChatHistoryItem { header = defaultTitle };
                Routing.CurrentChatTitle = defaultTitle;
                _navigator.Navigate(PageID.Chatbox, false);
            })
            .Finally(() =>
            {
                _isCreating = false;
                _btnNewChat?.SetEnabled(true);
            });
    }

    private static ChatHistoryItem ParseCreated(string json, string fallbackTitle)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<ChatHistoryResponse>(json);
            if (wrapper != null && wrapper.data != null) return wrapper.data;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HistoryPage] Parse wrapper fail: {ex.Message}");
        }

        try
        {
            var direct = JsonUtility.FromJson<ChatHistoryItem>(json);
            if (direct != null && !string.IsNullOrEmpty(direct.id)) return direct;
        }
        catch { /* ignore */ }

        return new ChatHistoryItem { header = fallbackTitle };
    }

    private void LoadHistory()
    {
        _chatService.GetHistory()
            .Then(res =>
            {
                Debug.Log($"[HistoryPage] Server trả về: {res.Text}");

                var parsed = JsonUtility.FromJson<ChatHistoryListResponse>(res.Text);
                if (parsed == null || parsed.data == null)
                {
                    Debug.LogWarning("[HistoryPage] Response không hợp lệ, dùng danh sách rỗng.");
                    BuildList(null);
                    ShowLoading(false);
                    return;
                }

                ChatSession.HistoryList = parsed.data;
                BuildList(parsed.data);
                ShowLoading(false);
            })
            .Catch(err =>
            {
                var reqErr = err as RequestException;
                string body = reqErr != null ? reqErr.Response : "(no body)";
                long code = reqErr != null ? reqErr.StatusCode : -1;
                Debug.LogError($"[HistoryPage] Lỗi GET /chat/histories ({code}): {err.Message}\nResponse body: {body}");

                // Khi lỗi (vd: chưa có token, network) — vẫn render với data rỗng để UI không kẹt UI mock cũ.
                BuildList(null);
                ShowLoading(false);

                if (code == 401)
                {
                    Debug.LogWarning("[HistoryPage] Token hết hạn — chuyển về Login.");
                    _navigator.Navigate(PageID.Login, true);
                }
            });
    }

    private void BuildList(System.Collections.Generic.List<ChatHistoryItem> items)
    {
        new HistoryManager(_root, items, OnOpenChatItem, OnDeleteHistoryItem);
    }

    private void OnDeleteHistoryItem(ChatHistoryItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
        {
            Debug.LogWarning("[HistoryPage] Không có id để xoá session.");
            return;
        }

        string targetId = item.id;

        _chatService.DeleteHistory(targetId)
            .Then(res =>
            {
                Debug.Log($"[HistoryPage] DELETE /chat/histories/{targetId} → {res.Text}");

                // Chỉ xoá khỏi list local sau khi server đã xác nhận thành công.
                if (ChatSession.HistoryList != null)
                {
                    ChatSession.HistoryList.RemoveAll(x => x != null && x.id == targetId);
                }

                // Re-render list.
                if (ChatSession.HistoryList != null) BuildList(ChatSession.HistoryList);
            })
            .Catch(err =>
            {
                var reqErr = err as RequestException;
                string body = reqErr != null ? reqErr.Response : "(no body)";
                long code = reqErr != null ? reqErr.StatusCode : -1;

                // Trích message từ body nếu có.
                string userMessage = "Không xoá được lịch sử";
                if (!string.IsNullOrEmpty(body) && body.Contains("\"message\""))
                {
                    int idx = body.IndexOf("\"message\":\"") + "\"message\":\"".Length;
                    int end = body.IndexOf("\"", idx);
                    if (idx >= 0 && end > idx) userMessage = body.Substring(idx, end - idx);
                }

                Debug.LogError($"[HistoryPage] Xoá session thất bại ({code}): {userMessage}\nFull body: {body}");
            });
    }

    private void OnOpenChatItem(ChatHistoryItem item)
    {
        if (item == null) return;

        ChatSession.Current = item;
        Routing.CurrentChatTitle = item.header ?? "";

        // Nếu có id thì load message cũ trước rồi mới chuyển sang Chatbox.
        if (!string.IsNullOrEmpty(item.id))
        {
            ShowLoading(true);

            _chatService.GetChatboxMessages(item.id)
                .Then(res =>
                {
                    Debug.Log($"[HistoryPage] Server trả tin nhắn của {item.id}: {res.Text}");
                    item.messages = ParseChatboxMessages(res.Text);
                    ShowLoading(false);
                    _navigator.Navigate(PageID.Chatbox, false);
                })
                .Catch(err =>
                {
                    var reqErr = err as RequestException;
                    string body = reqErr != null ? reqErr.Response : "(no body)";
                    long code = reqErr != null ? reqErr.StatusCode : -1;
                    Debug.LogError($"[HistoryPage] Lỗi GET /chat/chatboxes/history/{{id}} ({code}): {err.Message}\nResponse body: {body}");

                    item.messages = new System.Collections.Generic.List<ChatMessage>();
                    ShowLoading(false);
                    _navigator.Navigate(PageID.Chatbox, false);
                });
        }
        else
        {
            _navigator.Navigate(PageID.Chatbox, false);
        }
    }

    private static System.Collections.Generic.List<ChatMessage> ParseChatboxMessages(string json)
    {
        if (string.IsNullOrEmpty(json)) return new System.Collections.Generic.List<ChatMessage>();

        // 1) Wrapper {success, data: [...]}
        try
        {
            var wrapper = JsonUtility.FromJson<ChatHistoryMessagesResponse>(json);
            if (wrapper != null && wrapper.data != null) return wrapper.data;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HistoryPage] Parse messages wrapper fail: {ex.Message}");
        }

        // 2) BE trả thẳng mảng [...]: cần bọc lại để JsonUtility xử lý.
        try
        {
            string wrapped = "{\"data\":" + json + "}";
            var bareList = JsonUtility.FromJson<ChatHistoryMessagesResponse>(wrapped);
            if (bareList != null && bareList.data != null) return bareList.data;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HistoryPage] Parse messages bare-array fail: {ex.Message}");
        }

        return new System.Collections.Generic.List<ChatMessage>();
    }
}
