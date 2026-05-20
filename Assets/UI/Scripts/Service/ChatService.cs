using Proyecto26;
using RSG;
using UnityEngine;

/// <summary>
/// Service gọi 3 endpoint chat:
///   - GET  /chat/history
///   - POST /chat/history
///   - POST /chat/send
/// Theo cùng pattern với ProfileService / ChangePasswordService:
/// trả về IPromise&lt;ResponseHelper&gt; để Controller tự parse JSON.
/// </summary>
public class ChatService
{
    private readonly string HISTORY_URL = AppConst.BASE_API + "/chat/histories";
    private readonly string CHATBOX_URL = AppConst.BASE_API + "/chat/chatboxes";
    private readonly string CHATBOX_HISTORY_URL = AppConst.BASE_API + "/chat/chatboxes/history";
    private readonly string SEND_URL = AppConst.BASE_API + "/chat/chatboxes";

    private const string TOKEN_KEY = "ACCESS_TOKEN";

    /// <summary>
    /// Đảm bảo header Authorization được gắn trước mỗi request
    /// (tránh trường hợp token còn trong PlayerPrefs nhưng RestClient header bị clear).
    /// </summary>
    private void EnsureAuthHeader()
    {
        string token = PlayerPrefs.GetString(TOKEN_KEY, "");
        if (!string.IsNullOrEmpty(token))
        {
            RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + token;
        }
        else
        {
            Debug.LogWarning("[ChatService] Không tìm thấy ACCESS_TOKEN trong PlayerPrefs.");
        }
    }

    public IPromise<ResponseHelper> GetHistory()
    {
        EnsureAuthHeader();
        Debug.Log($"[ChatService] GET {HISTORY_URL}");
        return RestClient.Get(HISTORY_URL);
    }

    /// <summary>
    /// Lấy danh sách tin nhắn của 1 phiên chat.
    /// GET /chat/chatboxes/history/{historyId}
    /// </summary>
    public IPromise<ResponseHelper> GetChatboxMessages(string historyId)
    {
        EnsureAuthHeader();
        string url = $"{CHATBOX_HISTORY_URL}/{historyId}";
        Debug.Log($"[ChatService] GET {url}");
        return RestClient.Get(url);
    }

    public IPromise<ResponseHelper> CreateHistory(string header)
    {
        EnsureAuthHeader();
        var body = new CreateHistoryReq { header = header };
        Debug.Log($"[ChatService] POST {HISTORY_URL} | header = {header}");
        return RestClient.Post(HISTORY_URL, body);
    }

    /// <summary>
    /// Xoá 1 phiên lịch sử chat theo id (xóa cả session + message bên trong).
    /// DELETE /chat/histories/{historyId}
    /// </summary>
    public IPromise<ResponseHelper> DeleteHistory(string historyId)
    {
        EnsureAuthHeader();
        string url = $"{HISTORY_URL}/{historyId}";
        Debug.Log($"[ChatService] DELETE {url}");
        return RestClient.Delete(url);
    }

    public IPromise<ResponseHelper> SendMessage(string content, string historyId)
    {
        EnsureAuthHeader();
        var body = new SendMessageReq { Content = content, History_id = historyId };
        Debug.Log($"[ChatService] POST {SEND_URL} | Content = {content} | History_id = {historyId}");
        return RestClient.Post(SEND_URL, body);
    }
}
