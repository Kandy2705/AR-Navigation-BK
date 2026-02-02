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

    private string cacheKey = AppConst.KEY_CACHE;

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
                Debug.Log("Đã đăng xuất");
                // navigator.Navigate(PageID.Login); // Ví dụ
            };
        }
        
        LoadProfileData();
        navigator.BindButton(root, "BtnBack", PageID.MainSettings, true);
        navigator.BindButton(root, "BtnEmailChange", PageID.EmailChange, false);
        navigator.BindButton(root, "BtnPasswordChange", PageID.PasswordChange, false);
    }

    private void UpdateUI(RegisterRes data){
        if (_userName != null) _userName.value = data.name;
        if (_userPhone != null) _userPhone.value = data.phone;
        if (_userGender != null) _userGender.value = data.gender;
        if (_userBirthday != null) _userBirthday.value = data.birthday;
    }

    private void LoadProfileData()
    {

        if (PlayerPrefs.HasKey(cacheKey))
        {
            string jsonString = PlayerPrefs.GetString(cacheKey); // 1. Lấy chuỗi JSON
            try 
            {
                RegisterRes cachedRes = JsonUtility.FromJson<RegisterRes>(jsonString);
                Debug.Log($"Cache Res có dạng: {cachedRes}");
                UpdateUI(cachedRes); 
                Debug.Log("Controller: Đã load dữ liệu từ Cache (Offline)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Lỗi đọc cache cũ: " + ex.Message);
            }
        }


        Debug.Log("Controller: Đang yêu cầu Service lấy dữ liệu...");

        Service.GetUserProfile()
            .Then(res => 
            {
                Debug.Log("Controller: Đã nhận dữ liệu, đang update UI");
                UpdateUI(res);
                string jsonToSave = JsonUtility.ToJson(res); 
                
                PlayerPrefs.SetString(cacheKey, jsonToSave);
                PlayerPrefs.Save();
            })
            .Catch(err => 
            {
                Debug.LogError("Controller: Lỗi khi lấy profile: " + err.Message);
                
                var reqErr = err as RequestException;
                if (reqErr != null && reqErr.StatusCode == 401)
                {
                    Service.Logout();
                }
            });
    }
}