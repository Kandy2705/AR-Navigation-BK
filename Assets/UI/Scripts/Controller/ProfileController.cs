using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26; // Cần import để catch lỗi Exception của thư viện này

public class ProfileController : IPageController
{
    // Inject Service vào
    private readonly ProfileService Service = new ProfileService();
    
    // UI Elements
    private Label _labelUserName;
    private TextField _userName;
    private TextField _userPhone;
    private TextField _userGender;
    private TextField _userBirthday;

    private string cacheKey = AppConst.KEY_CACHE;

    // Hàm Initialize từ interface IPageController
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        // 1. Ánh xạ UI (Binding)
        _labelUserName = root.Q<Label>("LabelUserName");
        _userName = root.Q<TextField>("input-name");   
        _userPhone = root.Q<TextField>("input-phone"); 
        _userGender = root.Q<TextField>("input-gender"); 
        _userBirthday = root.Q<TextField>("input-birthday");

        var btnLogout = root.Q<Button>("LogoutButton");
        var btnBack = root.Q<Button>("BtnBack");
        
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
        if (_labelUserName != null) _labelUserName.text = data.name ?? "";
        if (_userName != null) _userName.value = data.name;
        if (_userPhone != null) _userPhone.value = data.phone;
        if (_userGender != null) _userGender.value = data.gender;
        if (_userBirthday != null) _userBirthday.value = data.birthday;
    }

    private void LoadProfileData()
    {

        if (PlayerPrefs.HasKey(cacheKey))
        {
            string jsonString = PlayerPrefs.GetString(cacheKey); 
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
        else
        {
            Debug.LogWarning("[Profile] Chưa có cache user trong PlayerPrefs. Cần đăng nhập thật để lấy dữ liệu.");
        }

        // TODO: gọi Service.GetUserProfile(token) để refresh khi đã có token thật.
    }


    // public string getCache()
    // {
        
    // }
}