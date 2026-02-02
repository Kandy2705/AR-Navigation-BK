using Proyecto26;
using RSG; // Promise library thường đi kèm Proyecto26
using UnityEngine;

public class ProfileService
{
    private const string BASE_URL = "https://arnavbk-avbcg7hecgacc5bg.malaysiawest-01.azurewebsites.net/users/me";

    // Hàm này trả về Promise (hoặc Task), KHÔNG cập nhật UI
    public IPromise<RegisterRes> GetUserProfile()
    {
        string token = PlayerPrefs.GetString("ACCESS_TOKEN", "");
        
        if (string.IsNullOrEmpty(token))
        {
            return Promise<RegisterRes>.Rejected(new System.Exception("No Token"));
        }

        // Cấu hình Header
        RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + token;

        // Trả về kết quả thô để bên ngoài xử lý
        return RestClient.Get<RegisterRes>(BASE_URL);
    }

    public void Logout()
    {
        PlayerPrefs.DeleteKey("ACCESS_TOKEN");
        PlayerPrefs.DeleteKey("REFRESH_TOKEN");
        PlayerPrefs.Save();
    }
}