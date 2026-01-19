using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26; // Thư viện API

public class LoginController : MonoBehaviour
{
    // Biến để hứng các ô nhập liệu
    private string BASE_API = "https://arnavbk-avbcg7hecgacc5bg.malaysiawest-01.azurewebsites.net/users/login";
    private TextField _emailInput;
    private TextField _passswordInput;
    private Button _btnLogin;

    private void OnEnable()
    {
        Debug.Log("Hàm đang hoạt động bình thường");
       
        var uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

       
        _emailInput = root.Q<TextField>("LoginEmailField");
        _passswordInput = root.Q<TextField>("LoginPasswordField");

        _btnLogin = root.Q<Button>("LoginSubmitButton");

        if (_btnLogin != null)
        {
            _btnLogin.clicked += HandleLogin;
        }
    }

    // Hàm xử lý logic khi bấm nút
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
        myData.password = _passswordInput.value;

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
                PlayerPrefs.Save();
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
    }

}