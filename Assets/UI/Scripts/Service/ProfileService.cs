using Proyecto26;
using RSG;
using UnityEngine;

public class ProfileService
{
    private static readonly string BASE_URL = AppConst.BASE_API + "/users/me";

    /// <summary>
    /// Lấy hồ sơ user. BE trả wrapper {success, data: RegisterRes, message}
    /// nên cần parse 2 lớp; fallback parse flat nếu BE đổi format.
    /// </summary>
    public IPromise<RegisterRes> GetUserProfile(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Promise<RegisterRes>.Rejected(new System.Exception("No Token"));
        }

        RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + token;

        var promise = new Promise<RegisterRes>();

        RestClient.Get(BASE_URL)
            .Then(res =>
            {
                Debug.Log($"[ProfileService] Server trả về: {res.Text}");
                RegisterRes parsed = ParseProfileResponse(res.Text);
                if (parsed == null)
                {
                    promise.Reject(new System.Exception("Parse profile fail: " + res.Text));
                    return;
                }
                promise.Resolve(parsed);
            })
            .Catch(err => promise.Reject(err));

        return promise;
    }

    public void Logout()
    {
        PlayerPrefs.DeleteKey("ACCESS_TOKEN");
        PlayerPrefs.DeleteKey("REFRESH_TOKEN");
        PlayerPrefs.Save();
    }

    private static RegisterRes ParseProfileResponse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        // 1) Wrapper {success, data: {...}}
        try
        {
            var wrapper = JsonUtility.FromJson<ProfileResponseWrapper>(json);
            if (wrapper != null && wrapper.data != null && !string.IsNullOrEmpty(wrapper.data.email))
            {
                return wrapper.data;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ProfileService] Parse wrapper fail: {ex.Message}");
        }

        // 2) Fallback: BE trả flat
        try
        {
            var flat = JsonUtility.FromJson<RegisterRes>(json);
            if (flat != null && !string.IsNullOrEmpty(flat.email)) return flat;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ProfileService] Parse flat fail: {ex.Message}");
        }

        return null;
    }
}

[System.Serializable]
public class ProfileResponseWrapper
{
    public bool success;
    public RegisterRes data;
    public string message;
    public string errorCode;
}
