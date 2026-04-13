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
    private Label errorLabel;
    private Label loadingTitleLabel;
    private Label loadingMessageLabel;
    private VisualElement toggleEyeIcon;     
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
        Debug.Log("Du lieu hien tai cua Cache: " + PlayerPrefs.GetString(cacheKey, "Không có dữ liệu"));

        _emailInput = root.Q<TextField>("EmailInput");
        _passwordInput = root.Q<TextField>("PasswordInput");
        _rememberToggle = root.Q<Toggle>("RememberToggle");
        toggleEyeIcon = root.Q<VisualElement>("ToggleEyeIcon");

        loginLoading = root.Q<VisualElement>("LoginLoading");
        errorLabel = root.Q<Label>("ErrorLabel");
        loadingTitleLabel = root.Q<Label>("LoadingTitleLabel");
        loadingMessageLabel = root.Q<Label>("LoadingMessageLabel");
        iconSuccess = root.Q<VisualElement>("IconSuccess") ?? root.Q<VisualElement>("IconSucces");

        _btnLogin = root.Q<Button>("LoginSubmitButton");
        if (_btnLogin != null)
        {
            _btnLogin.clicked += HandleLogin;
        }

        if(errorLabel != null)
        {
            errorLabel.style.display = DisplayStyle.None;
        }

        if (toggleEyeIcon != null)
        {
            toggleEyeIcon.RegisterCallback<ClickEvent>(OnTogglePasswordClick);
        }
    }

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Debug.Log("hehe khởi tạo được Login Page Controller rồi");
        Start(root);
        navigatorManager = navigator;
        navigator.BindButton(root, "BtnBack", PageID.WelcomePage, true);
        navigator.BindButton(root, "ForgotPasswordButton", PageID.EmailChangeForm, false);
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
        //string access_token = "";
    
        RestClient.Post(BASE_API, myData)
        .Then(response => 
        {
            PlayerPrefs.SetString("PASSWORD", myData.password);
            if(errorLabel != null)
            {
                errorLabel.style.display = DisplayStyle.None;
            }

            var resData = JsonUtility.FromJson<LoginRes>(response.Text);

            if(resData != null && !string.IsNullOrEmpty(resData.accessToken))
            {
                Debug.Log("Đăng nhập thành công! Token: " + resData.accessToken);
                RestClient.DefaultRequestHeaders["Authorization"] = "Bearer " + resData.accessToken;
                PlayerPrefs.SetString("REFRESH_TOKEN", resData.refreshToken);
                PlayerPrefs.SetString("ACCESS_TOKEN", resData.accessToken);

                PlayerPrefs.Save();
                
                Debug.Log($"Chuẩn bị đăng nhap, dữ liệu ACCESS_TOKEN: {PlayerPrefs.GetString("ACCESS_TOKEN", "Không có dữ liệu")}");
                getUserProfileData(resData.accessToken);
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
            if(errorLabel != null)
            {
                errorLabel.style.display = DisplayStyle.Flex;
            }
            Debug.LogError("Lỗi: " + error.Message);
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

    private void getUserProfileData(string access_token)
    {
        Service.GetUserProfile(access_token)
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

    private void ShowLoadingOverlay(VisualElement loginLoading, string title, string message)
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
        ShowLoadingOverlay(
            loginLoading,
            "Đăng nhập thành công!",
            "Vui lòng chờ...\nBạn sẽ được chuyển qua trang chủ."
        );
        await LoginProcess();
    }

    private async Task LoginProcess()
    {
        float waitTime = 2f; 
        float timer = 0f;

        while (timer < waitTime)
        {
            loginLogoRotate();
            timer += Time.deltaTime; 
            await Task.Yield();
        }

        HideLoadingOverlay();
        navigatorManager.Navigate(PageID.MainSettings, false);
    }

    private void loginLogoRotate()
    {
        if (iconSuccess != null && iconSuccess.resolvedStyle.display != DisplayStyle.None)
        {
            float speed = 180f;
            iconSuccessAngle += speed * Time.deltaTime;
            if (iconSuccessAngle >= 360f) iconSuccessAngle -= 360f;
            iconSuccess.style.rotate = new Rotate(Angle.Degrees(iconSuccessAngle));
        }
    }

    private void OnTogglePasswordClick(ClickEvent evt)
    {
        isPasswordVisible = !isPasswordVisible;
        _passwordInput.isPasswordField = !isPasswordVisible;
        UpdateEyeIcon();
    }

    private void UpdateEyeIcon()
    {
        if (isPasswordVisible)
        {
            toggleEyeIcon.RemoveFromClassList("icon-eye-closed");
            toggleEyeIcon.AddToClassList("icon-eye-open");
        }
        else
        {
            toggleEyeIcon.RemoveFromClassList("icon-eye-open");
            toggleEyeIcon.AddToClassList("icon-eye-closed");
        }
    }
}