using Proyecto26;
using RSG;
using UnityEngine;
using System;

public class ChangePasswordService
{
    // Các Endpoint từ AppConst của bạn
    public static ChangePasswordBody cacheChangePasswordData = new ChangePasswordBody();

    private const string OTP_COOLDOWN_KEY = "OTP_LAST_REQUEST_TIME";
    private const int COOLDOWN_MINUTES = 5;

    private readonly string REQUEST_OTP_URL = AppConst.BASE_API + "/users/request-password-change";
    private readonly string VERIFY_CHANGE_URL = AppConst.BASE_API + "/users/change-password";

    // Hàm 1: Gửi yêu cầu mã OTP qua Email
    public IPromise<ResponseHelper> RequestOTP(string email)
    {
        var body = new RequestOtpBody { email = email };

        Debug.Log($"[Service] Đang yêu cầu OTP cho: {email}");
        
        return RestClient.Post(REQUEST_OTP_URL, body);
    }

    // Hàm 2: Gửi thông tin xác nhận đổi mật khẩu
    public IPromise<ResponseHelper> VerifyAndChangePassword(string email, string oldPass, string newPass, string otp)
    {
        var body = new ChangePasswordBody 
        { 
            email = email, 
            newPassword = newPass, 
            oldPassword = oldPass, 
            otpCode = otp 
        };

        Debug.Log($"[Service] Đang xác nhận đổi mật khẩu cho: {email}");
        Debug.Log($"mật khẩu cũ: {oldPass}, mật khẩu mới: {newPass}, OTP: {otp}");

        return RestClient.Post(VERIFY_CHANGE_URL, body);
    }

    public static int GetRemainingCooldown()
    {
        string lastTimeStr = PlayerPrefs.GetString("OTP_COOLDOWN_TIME", "");
        if (string.IsNullOrEmpty(lastTimeStr)) return 0;

        long lastTicks = long.Parse(lastTimeStr);
        DateTime lastRequest = DateTime.FromBinary(lastTicks);
        double elapsedSeconds = (DateTime.Now - lastRequest).TotalSeconds;

        int remaining = 300 - (int)elapsedSeconds; // 5 phút = 300 giây
        return remaining > 0 ? remaining : 0;
    }

    public static void SaveCooldown()
    {
        PlayerPrefs.SetString("OTP_COOLDOWN_TIME", DateTime.Now.ToBinary().ToString());
        PlayerPrefs.Save();
    }
}