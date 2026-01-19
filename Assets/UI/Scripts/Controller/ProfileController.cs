using System;
using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26; // Thư viện API

public class ProfileController : IDisposable
{
    // URL API lấy profile (Thay bằng link thật của bạn)
    private const string BASE_URL = "https://arnavbk-avbcg7hecgacc5bg.malaysiawest-01.azurewebsites.net/users/me";

    private VisualElement _root;
    
    // Các UI Element để hiển thị thông tin
    private TextField _userName;
    private TextField _userPhone;
    private TextField _userGender;
    private TextField _userBirthday;
    private Button _btnLogout;

    // --- 1. KHỞI TẠO ---
    public ProfileController(VisualElement root)
    {
        _root = root;
        InitUI();
        FetchProfileData(); // Tự động gọi API ngay khi vào màn hình
    }

    private void InitUI()
    {
        _userName = _root.Q<TextField>("input-name");   
        _userPhone = _root.Q<TextField>("input-phone"); 
        _userGender = _root.Q<TextField>("input-gender"); 
        _userBirthday = _root.Q<TextField>("input-birthday");

        _btnLogout = _root.Q<Button>("LogoutButton");
        if (_btnLogout != null) _btnLogout.clicked += OnLogout;
    }

    // --- 2. GỌI API ---
    private void FetchProfileData()
    {
        string token = PlayerPrefs.GetString("ACCESS_TOKEN", "");

        // if (string.IsNullOrEmpty(token))
        // {
        //     Debug.LogWarning("Không tìm thấy Token! Về trang Login.");
        //     ControllerRouting.Instance.GoToPage(PageID.Login);
        //     return;
        // }

        RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + token;

        Debug.Log("Đang gọi API lấy Profile...");

        // D. Gọi GET
        RestClient.Get<RegisterRes>(BASE_URL)
        .Then(res => 
        {
            Debug.Log("Lấy data thành công: " + res.email);
            UpdateUI(res);
        })
        .Catch(err => 
        {
            var reqErr = err as RequestException;
            Debug.LogError("Lỗi API Profile: " + err.Message);

            // Nếu lỗi 401 (Unauthorized) -> Token hết hạn -> Bắt đăng nhập lại
            if (reqErr != null && reqErr.StatusCode == 401)
            {
                OnLogout();
            }
        });
    }

    // --- 3. CẬP NHẬT GIAO DIỆN ---
    private void UpdateUI(RegisterRes data)
    {
        if (_userName != null) _userName.value = data.name;
        if (_userPhone != null) _userPhone.value = data.phone;
        if (_userGender != null) _userGender.value = data.gender;
        if (_userBirthday != null) _userBirthday.value = data.birthday;
    }

    // --- 4. CHỨC NĂNG ĐĂNG XUẤT ---
    private void OnLogout()
    {
        // Xóa token đi
        PlayerPrefs.DeleteKey("ACCESS_TOKEN");
        PlayerPrefs.DeleteKey("REFRESH_TOKEN"); // Nếu có dùng
        PlayerPrefs.Save();
    }

    private void OnDelete()
    {
        // Xóa token đi
        Debug.Log($"Kiểm tra accessToken hiện tại");
        PlayerPrefs.DeleteKey("ACCESS_TOKEN");
        PlayerPrefs.DeleteKey("REFRESH_TOKEN"); // Nếu có dùng
        PlayerPrefs.Save();
    }

    // --- 5. DỌN DẸP ---
    public void Dispose()
    {
        if (_btnLogout != null) _btnLogout.clicked -= OnLogout;
        Debug.Log("Đóng trang Profile -> Dọn dẹp bộ nhớ.");
    }
}