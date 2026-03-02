using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26;
public class RegisterPageController : IPageController
{
    private NavigationManager navigationManager;
    private TextField _emailInput;
    private TextField _phoneNumberInput;
    private TextField _genderInput;
    private TextField _birthdayInput;
    //private TextField _passInput;
    private TextField _nameInput;
    private Button _basicInfoCompleteButton;
    private Button _OTPConfirmButton;
    private Button _btnRegister;
    private readonly ProfileService Service = new ProfileService();
    //private GameObject loginGameObject;
    private const string cacheKey = AppConst.KEY_CACHE;
    private const string BASE_API = AppConst.BASE_API + "/users/create-customer";

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;
        Start(root);
        navigator.BindButton(root, "BtnBack", PageID.WelcomePage, true);
    }

    public void Start(VisualElement root)
    {
        Debug.Log("Hàm Register đang bắt đầu hoạt động bình thường");

        _emailInput = root.Q<TextField>("EmailInput");
        _phoneNumberInput = root.Q<TextField>("PhoneInput");
        _genderInput = root.Q<TextField>("GenderInput");
        _birthdayInput = root.Q<TextField>("BirthdayInput");
        _nameInput = root.Q<TextField>("UsernameInput");
        //_passInput = root.Q<TextField>("NewPasswordField");

        _basicInfoCompleteButton = root.Q<Button>("ContinueButton");
        //_btnRegister = root.Q<Button>("NewPassChangeButton");

        if (_basicInfoCompleteButton != null)
        {
            _basicInfoCompleteButton.clicked += HandleRegister;
            navigationManager.BindButton(root, "ContinueButton", PageID.OTPPage, false);
        }
    }

    private void HandleRegister()
    {
        // A. Gom dữ liệu
        var myData = new RegisterReq();
        myData.gender = _genderInput.value;
        myData.birthday = ConvertToBackendDate(_birthdayInput.value);
        myData.phone = _phoneNumberInput.value;
        myData.email = _emailInput.value;
        //myData.password = _passInput.value;
        myData.name = _nameInput.value;
        // Các trường khác lấy tương tự...

        // Debug.Log("Đang gửi: " + myData.email);
        // Debug.Log("Dữ liệu hiện tại " + myData.Data);

        // // B. Gọi API
        // RestClient.Post(BASE_API, myData)
        // .Then(response => 
        // {
        //     Debug.Log("Thành công rồi! Server trả về: " + response.Text);
        // })
        // .Catch(error => 
        // {
        //     Debug.LogError("Lỗi: " + error.Message);
        // });
    }

    // Hàm biến hình: Từ "01/01/2000" -> "2000-01-01T00:00:00.000Z"
    private string ConvertToBackendDate(string inputDate)
    {
        
        if (System.DateTime.TryParse(inputDate, out System.DateTime dt))
        {
            return dt.ToString("yyyy-MM-ddT00:00:00.000Z");
        }
        return System.DateTime.Now.ToString("yyyy-MM-ddT00:00:00.000Z");
    }

    
}
