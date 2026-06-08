using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26;
public class RegisterPageController : IPageController
{
    public static RegisterReq CurrentData;
    private NavigationManager navigationManager;
    private TextField _emailInput;
    private TextField _phoneNumberInput;
    private TextField _genderInput;
    private TextField _birthdayInput;
    private Label errorText;
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

        CurrentData = new RegisterReq();

        _emailInput = root.Q<TextField>("EmailInput");
        _phoneNumberInput = root.Q<TextField>("PhoneInput");
        _genderInput = root.Q<TextField>("GenderInput");
        _birthdayInput = root.Q<TextField>("BirthdayInput");
        _nameInput = root.Q<TextField>("UsernameInput");
        errorText = root.Q<Label>("ErrorLabel");

        _basicInfoCompleteButton = root.Q<Button>("ContinueButton");

        if (_basicInfoCompleteButton != null)
        {
            _basicInfoCompleteButton.clicked += HandleRegister;
        }
    }
    private void HandleRegister()
    {
        // A. Gom dữ liệu
        var myData = CurrentData;
        myData.gender = _genderInput?.value ?? "";
        myData.birthday = ConvertToBackendDate(_birthdayInput?.value ?? "");
        myData.phone = _phoneNumberInput?.value ?? "";
        myData.email = _emailInput?.value ?? "";
        myData.name = _nameInput?.value ?? "";

        Debug.Log("Đang gửi: " + myData.email);
        Debug.Log("Dữ liệu hiện tại " + myData.Data);

        // Validation
        string error = null;
        if (string.IsNullOrEmpty(myData.name)) error = "Vui lòng nhập đầy đủ họ tên";
        else if (string.IsNullOrEmpty(myData.phone)) error = "Vui lòng nhập số điện thoại";
        else if (string.IsNullOrEmpty(myData.email)) error = "Vui lòng nhập email";
        else if (string.IsNullOrEmpty(myData.gender)) error = "Vui lòng nhập giới tính";
        else if (string.IsNullOrEmpty(myData.birthday)) error = "Vui lòng nhập ngày sinh";

        if (error != null)
        {
            if (errorText != null) errorText.text = error;
            return;
        }

        // Clear error nếu có
        if (errorText != null) errorText.text = "";

        EmailChangingOTPController.CurrentFlow = OTPFlowType.Register;
        navigationManager.Navigate(PageID.OTPPage, false);
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
