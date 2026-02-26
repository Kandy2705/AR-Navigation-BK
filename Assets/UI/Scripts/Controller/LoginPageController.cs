using UnityEngine;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using Proyecto26; // Thư viện API

public class LoginPageController : IPageController
{
    private readonly ProfileService Service = new ProfileService();
    private TextField _emailInput;
    private TextField _passwordInput;
    private Toggle _rememberToggle;
    private Button _btnLogin;

    private NavigationManager navigatorManager;

    private VisualElement loginLoading;
    private Label loadingTitleLabel;
    private Label loadingMessageLabel;

    private VisualElement iconSuccess;
    private float iconSuccessAngle = 0f;
    private const string KEY_EMAIL = "KEY_USER_EMAIL";
    private const string KEY_PASS = "KEY_USER_PASS";
    private const string KEY_REMEMBER = "KEY_IS_REMEMBER";
    private const string BASE_API = AppConst.BASE_API + "/users/login";
    private const string cacheKey = AppConst.KEY_CACHE;

    private bool isPasswordVisible = false;
    public void Start(VisualElement root)
    {
        Debug.Log("Hàm Login mới đang bắt đầu hoạt động");

        _emailInput = root.Q<TextField>("EmailInput");
        _passwordInput = root.Q<TextField>("PasswordInput");
        _rememberToggle = root.Q<Toggle>("RememberToggle");

        loginLoading = root.Q<VisualElement>("LoginLoading");
        loadingTitleLabel = root.Q<Label>("LoadingTitleLabel");
        loadingMessageLabel = root.Q<Label>("LoadingMessageLabel");
        iconSuccess = root.Q<VisualElement>("IconSuccess") ?? root.Q<VisualElement>("IconSucces");

        _btnLogin = root.Q<Button>("LoginSubmitButton");
        if (_btnLogin != null)
        {
            _btnLogin.clicked += HandleLogin;
        }
    }

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("hehe khởi tạo được Login Page Controller rồi");
        Start(root);
        navigatorManager = navigator;
        navigator.BindButton(root, "BtnBack", PageID.WelcomePage, true);
    }

    private void HandleLogin()
    {
        if (RestClient.DefaultRequestHeaders.ContainsKey("Authorization"))
        {
            Debug.Log("À anh Thanh, Hóa ra là còn Authorization à!");
            RestClient.DefaultRequestHeaders.Remove("Authorization");
        }
        
        RestClient.DefaultRequestHeaders.Clear();

        var myData = new LoginReq();
        myData.email = _emailInput.value;
        myData.password = _passwordInput.value;

        LoadSavedCredentials();

        Debug.Log("Đang đăng nhập: " + myData.email);
    
        RestClient.Post(BASE_API, myData)
        .Then(response => 
        {
            var resData = JsonUtility.FromJson<LoginRes>(response.Text);

            if(resData != null && !string.IsNullOrEmpty(resData.accessToken))
            {
                Debug.Log("Đăng nhập thành công! Token: " + resData.accessToken);
                RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + resData.accessToken;
                PlayerPrefs.SetString("REFRESH_TOKEN", resData.refreshToken);
                PlayerPrefs.SetString("ACCESS_TOKEN", resData.accessToken);
            }
            else
            {
                Debug.LogError("JSON không khớp hoặc không có token!");
            }
            OnLoginClicked();
            Debug.Log("Thành công rồi! Server trả về: " + response.Text);
        })
        .Catch(error => 
        {
            Debug.LogError("Lỗi: " + error.Message);
        });

        Service.GetUserProfile()
        .Then(res => 
        {
            PlayerPrefs.SetString(cacheKey, JsonUtility.ToJson(res));
            PlayerPrefs.Save();
            string testString = PlayerPrefs.GetString(cacheKey, "Không có dữ liệu");
            Debug.Log($"Đã lưu dữ liệu người dùng vào bộ nhớ đệm {testString}");
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


        if (_rememberToggle.value == true)
        {
            PlayerPrefs.SetString(KEY_EMAIL, myData.email);
            PlayerPrefs.SetString(KEY_PASS, myData.password);
            PlayerPrefs.SetInt(KEY_REMEMBER, 1);
        }
        else
        {
            PlayerPrefs.DeleteKey(KEY_EMAIL);
            PlayerPrefs.DeleteKey(KEY_PASS);
            PlayerPrefs.SetInt(KEY_REMEMBER, 0);
        }
    }

    private void LoadSavedCredentials()
    {
        int isRemember = PlayerPrefs.GetInt(KEY_REMEMBER, 0);

        if (isRemember == 1)
        {
            if (_emailInput != null) 
                _emailInput.value = PlayerPrefs.GetString(KEY_EMAIL);
            
            if (_passwordInput != null) 
                _passwordInput.value = PlayerPrefs.GetString(KEY_PASS);
            
            if (_rememberToggle != null) 
                _rememberToggle.value = true;
        }
    }

    private void ShowLoadingOverlay(string title, string message)
    {
        if (loginLoading == null) return;

        if (loadingTitleLabel != null) loadingTitleLabel.text = title;
        if (loadingMessageLabel != null) loadingMessageLabel.text = message;

        loginLoading.pickingMode = PickingMode.Position;
        loginLoading.style.display = DisplayStyle.Flex;

        if (iconSuccess != null)
        {
            iconSuccess.style.display = DisplayStyle.Flex;
            iconSuccessAngle = 0f;
        }
    }

    private void HideLoadingOverlay()
    {
        if (loginLoading == null) return;

        loginLoading.style.display = DisplayStyle.None;
        loginLoading.pickingMode = PickingMode.Ignore;

        if (iconSuccess != null) iconSuccess.style.display = DisplayStyle.None;
    }


    private async void OnLoginClicked()
    {
        //loginSubmitButton?.SetEnabled(false);

        ShowLoadingOverlay(
            "Đăng nhập thành công!",
            "Vui lòng chờ...\nBạn sẽ được chuyển qua trang chủ."
        );
        await LoginProcess();
    }

    private async Task LoginProcess()
    {
        await Task.Delay(2000); 
        HideLoadingOverlay();
        navigatorManager.Navigate(PageID.MainSettings, false);
        // Logic chuyển scene hoặc xử lý tiếp theo...
    }

    // private void UpdateToggleIcon(VisualElement toggleIcon, bool visible)
    // {
    //     if (toggleIcon == null) return;

    //     var tex = visible ? eyeTexture : eyeSlashTexture;
    //     if (tex == null)
    //     {
    //         toggleIcon.EnableInClassList("eye-visible", visible);
    //         toggleIcon.EnableInClassList("eye-hidden", !visible);
    //         return;
    //     }

    //     toggleIcon.style.backgroundImage = new StyleBackground(tex);
    // }
}