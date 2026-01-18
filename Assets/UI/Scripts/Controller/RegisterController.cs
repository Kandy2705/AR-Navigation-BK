using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26; // Thư viện API

public class RegisterController : MonoBehaviour
{
    // Biến để hứng các ô nhập liệu
    private string BASE_API = "https://arnavbk-avbcg7hecgacc5bg.malaysiawest-01.azurewebsites.net/users/register";
    private TextField _emailInput;
    private TextField _phoneNumberInput;
    private TextField _genderInput;
    private TextField _birthdayInput;
    private TextField _passInput;
    private TextField _nameInput;
    private Button _btnRegister;

    private void OnEnable()
    {
        Debug.Log("Hàm đang hoạt động bình thường");
       
        var uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

       
        _emailInput = root.Q<TextField>("EmailField");
        _phoneNumberInput = root.Q<TextField>("PhoneNumberField");
        _genderInput = root.Q<TextField>("GenderField");
        _birthdayInput = root.Q<TextField>("InputBirthField");
        _nameInput = root.Q<TextField>("UserNameField");
        _passInput = root.Q<TextField>("NewPasswordField");

        _btnRegister = root.Q<Button>("NewPassChangeButton");

        if (_btnRegister != null)
        {
            _btnRegister.clicked += HandleRegister;
        }
    }

    // Hàm xử lý logic khi bấm nút
    private void HandleRegister()
    {
        // A. Gom dữ liệu
        var myData = new RegisterReq();
        myData.gender = _genderInput.value;
        myData.birthday = ConvertToBackendDate(_birthdayInput.value);
        myData.phone = _phoneNumberInput.value;
        myData.email = _emailInput.value;
        myData.password = _passInput.value;
        myData.name = _nameInput.value;
        // Các trường khác lấy tương tự...

        Debug.Log("Đang gửi: " + myData.email);
        Debug.Log("Dữ liệu hiện tại " + myData.Data);

        // B. Gọi API
        RestClient.Post(BASE_API, myData)
        .Then(response => 
        {
            Debug.Log("Thành công rồi! Server trả về: " + response.Text);
        })
        .Catch(error => 
        {
            Debug.LogError("Lỗi: " + error.Message);
        });
    }

    // Hàm biến hình: Từ "01/01/2000" -> "2000-01-01T00:00:00.000Z"
    private string ConvertToBackendDate(string inputDate)
    {
        
        if (System.DateTime.TryParse(inputDate, out System.DateTime dt))
        {
            return dt.ToString("yyyy-MM-ddT00:00:00.000Z");
        }
        //Debug.LogWarning("Ngày sinh nhập sai format, lấy tạm ngày hôm nay!");
        return System.DateTime.Now.ToString("yyyy-MM-ddT00:00:00.000Z");
    }
}