using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26; // Thư viện API

public class LoginController : MonoBehaviour
{
    // Biến để hứng các ô nhập liệu
    private string BASE_API = "https://arnavbk-avbcg7hecgacc5bg.malaysiawest-01.azurewebsites.net/users/login";
    private TextField _emailInput;
    private TextField _passwordInput;
    private Toggle _rememberToggle;

    private const string KEY_EMAIL = "KEY_USER_EMAIL";
    private const string KEY_PASS = "KEY_USER_PASS";
    private const string KEY_REMEMBER = "KEY_IS_REMEMBER";

    private readonly ProfileService Service = new ProfileService();
    private const string cacheKey = AppConst.KEY_CACHE;
    
    private Button _btnLogin;

    private void OnEnable()
    {
        Debug.Log("Hàm đang hoạt động bình thường");
       
        var uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

        _emailInput = root.Q<TextField>("LoginEmailField");
        _passwordInput = root.Q<TextField>("LoginPasswordField");
        _rememberToggle = root.Q<Toggle>("RememberToggle");

        _btnLogin = root.Q<Button>("LoginSubmitButton");

        if (_btnLogin != null)
        {
            _btnLogin.clicked += HandleLogin;
        }
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
            Debug.Log("Thành công rồi! Server trả về: " + response.Text);
        })
        .Catch(error => 
        {
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

}