using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Nếu dùng Newtonsoft.Json thì bỏ comment dòng dưới
// using Newtonsoft.Json; 

public static class ApiHelper
{
    // Cấu hình Header mặc định (Content-Type)
    private const string CONTENT_TYPE_JSON = "application/json";

    // ==================================================================================
    // 1. NHÓM CRUD CƠ BẢN (GET, POST, PUT, DELETE) - TRẢ VỀ JSON OBJECT
    // ==================================================================================

    public static async Task<T> Get<T>(string url, string token = null)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            AttachHeader(request, token);
            await SendRequest(request);
            return ParseResponse<T>(request);
        }
    }

    public static async Task<T> Post<T>(string url, object body, string token = null)
    {
        using (var request = CreateJsonRequest(url, "POST", body))
        {
            AttachHeader(request, token);
            await SendRequest(request);
            return ParseResponse<T>(request);
        }
    }

    public static async Task<T> Put<T>(string url, object body, string token = null)
    {
        using (var request = CreateJsonRequest(url, "PUT", body))
        {
            AttachHeader(request, token);
            await SendRequest(request);
            return ParseResponse<T>(request);
        }
    }

    public static async Task<T> Delete<T>(string url, string token = null)
    {
        using (var request = UnityWebRequest.Delete(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer(); // Để đọc tin nhắn trả về
            AttachHeader(request, token);
            await SendRequest(request);
            return ParseResponse<T>(request);
        }
    }

    // ==================================================================================
    // 2. NHÓM TẢI ASSET (ẢNH, NHẠC)
    // ==================================================================================

    // Tải ảnh về để gắn vào UI (Avatar, Banner)
    public static async Task<Texture2D> GetTexture(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            await SendRequest(request);
            if (request.result != UnityWebRequest.Result.Success) return null;
            return DownloadHandlerTexture.GetContent(request);
        }
    }

    // Tải nhạc về để phát (Cho dự án Music App của bạn)
    // audioType: Thường là AudioType.MPEG (mp3) hoặc AudioType.WAV
    public static async Task<AudioClip> GetAudio(string url, AudioType audioType = AudioType.MPEG)
    {
        using (var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            await SendRequest(request);
            if (request.result != UnityWebRequest.Result.Success) return null;
            return DownloadHandlerAudioClip.GetContent(request);
        }
    }

    // ==================================================================================
    // 3. NHÓM UPLOAD FILE (MULTIPART/FORM-DATA)
    // ==================================================================================

    // Dùng để upload Avatar hoặc File nhạc
    // formFields: Các trường text đi kèm (ví dụ: "username": "khanh", "type": "avatar")
    // fileData: Mảng byte của file
    // fileName: Tên file (vd: "avatar.png")
    public static async Task<T> UploadFile<T>(string url, byte[] fileData, string fileName, string fieldName = "file", string token = null)
    {
        WWWForm form = new WWWForm();
        // Add file vào form
        form.AddBinaryData(fieldName, fileData, fileName);
        
        // Nếu muốn gửi kèm dữ liệu khác, dùng form.AddField("key", "value");

        using (var request = UnityWebRequest.Post(url, form))
        {
            // Lưu ý: Không set Content-Type là application/json ở đây
            // Unity tự set là multipart/form-data
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }

            await SendRequest(request);
            return ParseResponse<T>(request);
        }
    }

    // ==================================================================================
    // 4. PRIVATE HELPERS (HÀM PHỤ TRỢ)
    // ==================================================================================

    private static UnityWebRequest CreateJsonRequest(string url, string method, object body)
    {
        string jsonString = JsonUtility.ToJson(body);
        // Nếu dùng Newtonsoft: string jsonString = JsonConvert.SerializeObject(body);

        var request = new UnityWebRequest(url, method);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonString);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", CONTENT_TYPE_JSON);
        return request;
    }

    private static void AttachHeader(UnityWebRequest request, string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
        }
    }

    private static async Task SendRequest(UnityWebRequest request)
    {
        var operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
        {
            // Log lỗi chi tiết
            Debug.LogError($"[API ERROR] {request.method} {request.url}\nError: {request.error}\nResponse: {request.downloadHandler?.text}");
        }
    }

    private static T ParseResponse<T>(UnityWebRequest request)
    {
        if (request.result != UnityWebRequest.Result.Success) return default(T);
        
        string json = request.downloadHandler.text;
        
        // Mặc định dùng JsonUtility (Nhanh, có sẵn)
        try {
            return JsonUtility.FromJson<T>(json);
        } catch (Exception) {
            Debug.LogError("Lỗi Parse JSON! Kiểm tra lại Model.");
            return default(T);
        }

        // Nếu bạn cài Newtonsoft.Json thì dùng dòng dưới (Mạnh hơn, xử lý được null/dictionary):
        // return JsonConvert.DeserializeObject<T>(json);
    }
}