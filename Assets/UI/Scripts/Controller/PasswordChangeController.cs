using UnityEngine;
using UnityEngine.UIElements;

public class PasswordChangeController : IPageController
{
    private TextField _oldpasswordInput;
    private TextField _newpasswordInput;
    private TextField _confirmNewPasswordInput;
    private Button _btnSubmit;
    private bool isOldPasswordVisible = false;
    private bool isNewPasswordVisible = false;
    private bool isConfirmNewPasswordVisible = false;
    private VisualElement oldToggleEyeIcon;
    private VisualElement newToggleEyeIcon;
    private VisualElement confirmToggleEyeIcon;
    private NavigationManager navigationManager;
    private Label errorText;
    private string cacheKey = AppConst.KEY_CACHE;
    private ChangePasswordService _service = new ChangePasswordService();

    private Label _statusLabel; 

    private IVisualElementScheduledItem _timerTask;

    private void Start(VisualElement root)
    {
        _oldpasswordInput = root.Q<TextField>("OldPasswordInput");
        _newpasswordInput = root.Q<TextField>("NewPasswordInput");
        _confirmNewPasswordInput = root.Q<TextField>("ConfirmNewPasswordInput");

        _btnSubmit = root.Q<Button>("Btn-Confirm");

        errorText = root.Q<Label>("ErrorLabel");
        oldToggleEyeIcon = root.Q<VisualElement>("OldToggleEyeIcon");
        newToggleEyeIcon = root.Q<VisualElement>("NewToggleEyeIcon");
        confirmToggleEyeIcon = root.Q<VisualElement>("ConfirmToggleEyeIcon");

        if (oldToggleEyeIcon != null)
        {
            oldToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _oldpasswordInput, oldToggleEyeIcon, ref isOldPasswordVisible));
        }
        if(newToggleEyeIcon != null)
        {
            newToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _newpasswordInput, newToggleEyeIcon, ref isNewPasswordVisible));
        }
        if(confirmToggleEyeIcon != null)
        {
            confirmToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _confirmNewPasswordInput, confirmToggleEyeIcon, ref isConfirmNewPasswordVisible));
        }

        if(_btnSubmit != null)
        {
            _btnSubmit.clicked += OTPRequest;
        }

         if(errorText != null) errorText.style.display = DisplayStyle.None;
    }

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;

        Start(root);
        navigator.BindButton(root, "Btn-Back", navigator.PreviousPage(), true);
    }

    private void OTPRequest()
    {
        if(PlayerPrefs.HasKey(cacheKey))
        {
            string jsonString = PlayerPrefs.GetString(cacheKey);
            
            bool hasError = false;
            Debug.Log("Dữ liệu cache đã được tải: " + jsonString);
            RegisterRes cachedRes = JsonUtility.FromJson<RegisterRes>(jsonString);
            Debug.Log("Dữ liệu email: " + cachedRes.email);
            ChangePasswordService.cacheChangePasswordData.email = cachedRes.email;

           
             if(string.IsNullOrEmpty(_oldpasswordInput.value) || string.IsNullOrEmpty(_newpasswordInput.value) || string.IsNullOrEmpty(_confirmNewPasswordInput.value))
            {
                errorText.text = "Vui lòng điền đầy đủ thông tin mật khẩu!";
                hasError = true;
            }

            if (_oldpasswordInput.value != PlayerPrefs.GetString("PASSWORD", "no pass"))
            {
                errorText.text = "Mật khẩu cũ không chính xác, vui lòng nhập lại!";
                hasError = true;
            }

            if(_newpasswordInput.value != _confirmNewPasswordInput.value)
            {
                errorText.text = "Mật khẩu mới và xác nhận mật khẩu không khớp!";
                hasError = true;
            }

            if(_oldpasswordInput.value == _newpasswordInput.value)
            {
                errorText.text = "Mật khẩu mới không được trùng với mật khẩu cũ!";
                hasError = true;
            }

            if(hasError == true)
            {
                Debug.LogWarning(errorText.text);
                errorText.style.display = DisplayStyle.Flex;
                return;
            }

            ChangePasswordService.cacheChangePasswordData.oldPassword = _oldpasswordInput.value;
            ChangePasswordService.cacheChangePasswordData.newPassword = _newpasswordInput.value;

             int remaining = ChangePasswordService.GetRemainingCooldown();
            if (remaining > 0)
            {
                Debug.LogWarning($"Chặn gọi API! Còn {remaining} giây.");
                return;
            }

            OnClickSendOTP(ChangePasswordService.cacheChangePasswordData.email);
        }
        else
        {
            Debug.Log("Không có dữ liệu cache để tải.");
        }
       
    }

    // public void OnClickSendOTP(string userEmail)
    // {
    //     _service.RequestOTP(userEmail)
    //     .Then(res => {
    //         Debug.Log("Mã OTP đã được gửi vào Email của bạn!");
    //     })
    //     .Catch(err => {
    //         Debug.LogError("Gửi OTP thất bại: " + err.Message);
    //     });
    // }

    public void OnClickSendOTP(string userEmail)
    {
        _service.RequestOTP(userEmail)
        .Then(res => {
            Debug.Log("Mã OTP đã được gửi!");
            ChangePasswordService.SaveCooldown(); 
            
            //StartCountdown(_btnSubmit); 
            
            navigationManager.Navigate(PageID.OTPPage);
        })
        .Catch(err => {
            Debug.LogError("Gửi OTP thất bại: " + err.Message);
        });
    }

    // private void CheckAndStartTimer(VisualElement root)
    // {
    //     int remaining = ChangePasswordService.GetRemainingCooldown();
    //     if (remaining > 0)
    //     {
    //         StartCountdown(root);
    //     }
    

    // private void StartCountdown(VisualElement root)
    // {
    //     _btnSubmit.SetEnabled(false); 

    //     _timerTask = root.schedule.Execute(() => {
    //         int remaining = ChangePasswordService.GetRemainingCooldown();
            
    //         if (remaining > 0)
    //         {
    //             _statusLabel.text = $"Gửi lại sau {remaining}s";
    //             _statusLabel.style.display = DisplayStyle.Flex;
    //         }
    //         else
    //         {
    //             _btnSubmit.SetEnabled(true);
    //             _statusLabel.style.display = DisplayStyle.None;
    //             _timerTask.Pause(); // Dừng đếm ngược khi hết thời gian
    //         }
    //     }).Every(1000);
    // }
    
}