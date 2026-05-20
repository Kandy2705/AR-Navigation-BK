using System;
using System.Collections.Generic;

// =====================================================
// DTO cho hệ thống Chat (khớp với API CO4029_BE)
//   - GET  /chat/history
//   - POST /chat/history
//   - POST /chat/send
// =====================================================

[Serializable]
public class ChatMessage
{
    // BE inconsistent: GET /chat/chatboxes/history dùng PascalCase (Id/Content/Contact_person),
    // POST /chat/chatboxes lại dùng lowercase (id/content/contact_time).
    // Khai báo cả 2 dạng để JsonUtility parse được trong mọi trường hợp.
    public string Id;
    public string id;
    public string Content;
    public string content;
    public string Contact_person;
    public string contact_person;
    public string contact_time;

    public string GetContent() =>
        !string.IsNullOrEmpty(Content) ? Content : content;

    public string GetSender() =>
        !string.IsNullOrEmpty(Contact_person) ? Contact_person :
        !string.IsNullOrEmpty(contact_person) ? contact_person : null;
}

[Serializable]
public class ChatHistoryItem
{
    public string id;
    public string header;        // tên hiển thị của session (BE đặt là "header")
    public string create_date;   // ISO string, vd "2026-03-04T01:28:42"
    public List<ChatMessage> messages;
}

// Wrapper { success, data, message } trả về từ BE.
// JsonUtility không hỗ trợ generic, nên phải tạo wrapper concrete cho từng kiểu.

[Serializable]
public class ChatHistoryListResponse
{
    public bool success;
    public List<ChatHistoryItem> data;
    public string message;
}

[Serializable]
public class ChatHistoryMessagesResponse
{
    public bool success;
    public List<ChatMessage> data;
    public string message;
}

[Serializable]
public class ChatHistoryResponse
{
    public bool success;
    public ChatHistoryItem data;
    public string message;
}

[Serializable]
public class CreateHistoryReq
{
    public string header;
}

[Serializable]
public class SendMessageReq
{
    // Theo Swagger POST /chat/chatboxes — field viết hoa và có dấu gạch dưới.
    public string Content;
    public string History_id;
}

[Serializable]
public class SendMessageResponse
{
    public bool success;
    public List<ChatMessage> data;
    public string message;
}

/// <summary>
/// Lưu trữ tạm thời danh sách lịch sử + session đang mở.
/// Giúp pass dữ liệu giữa HistoryPage và Chatbox mà không cần Singleton phức tạp.
/// </summary>
public static class ChatSession
{
    public static List<ChatHistoryItem> HistoryList = new List<ChatHistoryItem>();
    public static ChatHistoryItem Current;
}
