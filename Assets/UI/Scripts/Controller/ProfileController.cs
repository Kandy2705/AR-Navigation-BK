using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26; // Cần import để catch lỗi Exception của thư viện này

public class ProfileController : IPageController
{
    // Inject Service vào
    private readonly ProfileService Service = new ProfileService();
    
    // UI Elements
    private TextField _userName;
    private TextField _userPhone;
    private TextField _userGender;
    private TextField _userBirthday;

    // Hàm Initialize từ interface IPageController
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        // 1. Ánh xạ UI (Binding)
        _userName = root.Q<TextField>("input-name");   
        _userPhone = root.Q<TextField>("input-phone"); 
        _userGender = root.Q<TextField>("input-gender"); 
        _userBirthday = root.Q<TextField>("input-birthday");

        var btnLogout = root.Q<Button>("LogoutButton");
        var btnBack = root.Q<Button>("BtnBack");

        // 2. Gán sự kiện nút bấm
        if (btnLogout != null) 
        {
            btnLogout.clicked += () => 
            {
                Service.Logout();
                // Điều hướng về trang Login hoặc Home sau khi logout
                Debug.Log("Đã đăng xuất");
                // navigator.Navigate(PageID.Login); // Ví dụ
            };
        }
        
        // if (btnBack != null)
        // {
        //      btnBack.clicked += () => navigator.GoBack();
        // }

        // 3. Gọi Service lấy dữ liệu (Controller chỉ ra lệnh)
        LoadProfileData();
        navigator.BindButton(root, "BtnBack", PageID.MainSettings, true);
        navigator.BindButton(root, "BtnEmailChange", PageID.EmailChange, false);
        navigator.BindButton(root, "BtnPasswordChange", PageID.PasswordChange, false);
    }

    private void LoadProfileData()
    {
        Debug.Log("Controller: Đang yêu cầu Service lấy dữ liệu...");

        Service.GetUserProfile()
            .Then(res => 
            {
                Debug.Log("Controller: Đã nhận dữ liệu, đang update UI");
                if (_userName != null) _userName.value = res.name;
                if (_userPhone != null) _userPhone.value = res.phone;
                if (_userGender != null) _userGender.value = res.gender;
                if (_userBirthday != null) _userBirthday.value = res.birthday;
            })
            .Catch(err => 
            {
                // 5. Xử lý lỗi (Thất bại)
                Debug.LogError("Controller: Lỗi khi lấy profile: " + err.Message);
                
                var reqErr = err as RequestException;
                if (reqErr != null && reqErr.StatusCode == 401)
                {
                    Service.Logout();
                }
            });
    }
}