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
        //CheckAndStartTimer(root);
    }

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;

        Start(root);
        navigator.BindButton(root, "Btn-Back", navigator.PreviousPage(), true);
        //navigator.BindButton(root, "Btn-Confirm", PageID.OTPPage, false);
        //navigator.ShowPasswordButton(root);
    }

    private void OTPRequest()
    {
        if(PlayerPrefs.HasKey(cacheKey))
        {
            string jsonString = PlayerPrefs.GetString(cacheKey);
            Debug.Log("Dữ liệu cache đã được tải: " + jsonString);
            RegisterRes cachedRes = JsonUtility.FromJson<RegisterRes>(jsonString);
            Debug.Log("Dữ liệu email: " + cachedRes.email);
            ChangePasswordService.cacheChangePasswordData.email = cachedRes.email;

            if(_newpasswordInput.value != _confirmNewPasswordInput.value)
            {
                Debug.LogWarning("Mật khẩu mới và xác nhận mật khẩu không khớp!");
                return;
            }

            if(_oldpasswordInput.value == _newpasswordInput.value)
            {
                Debug.LogWarning("Mật khẩu mới không được trùng với mật khẩu cũ!");
                return;
            }

            if(string.IsNullOrEmpty(_oldpasswordInput.value) || string.IsNullOrEmpty(_newpasswordInput.value) || string.IsNullOrEmpty(_confirmNewPasswordInput.value))
            {
                Debug.LogWarning("Vui lòng điền đầy đủ thông tin mật khẩu!");
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