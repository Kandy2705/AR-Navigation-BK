using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class ControllerFactory
{
    // Hàm tĩnh: Nhận ID trang -> Trả về Controller tương ứng
    public static IDisposable CreateController(PageID pageId, VisualElement root)
    {
        switch (pageId)
        {

            // case "Login": 
            //     return new LoginController(root);

            // case "Register": 
            //     return new RegisterController(root);

            // case PageID.Profile: 
            //     Debug.Log("Đang truyền controller script vào trang Profile");
            //     //return new ProfileController(root);

            // --- CÁC TRANG PHỤ (Dựa trên hình Inspector cũ của bạn) ---
            // case "Email Change": 
            //    return new EmailChangeController(root);
            
            // case "Password Change": 
            //    return new PasswordChangeController(root);

            default:
                Debug.LogWarning($"[Factory] Chưa code logic cho trang: '{pageId}'. Chỉ hiện UI.");
                return null;
        }
    }
}