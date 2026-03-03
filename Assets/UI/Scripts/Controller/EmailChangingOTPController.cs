using UnityEngine;
using UnityEngine.UIElements;

public class EmailChangingOTPController : IPageController
{
    private Button ConfirmBtn;
    private TextField OTP1;
    private TextField OTP2;
    private TextField OTP3;
    private TextField OTP4;

    private NavigationManager navigationManager;

    private ChangePasswordService service = new ChangePasswordService();
    private ProfileService logout = new ProfileService();

    private void Start(VisualElement root)
    {
        ConfirmBtn = root.Q<Button>("btn-confirm");
        OTP1 = root.Q<TextField>("OTP1");
        OTP2 = root.Q<TextField>("OTP2");
        OTP3 = root.Q<TextField>("OTP3");
        OTP4 = root.Q<TextField>("OTP4");

        if (ConfirmBtn != null)
        {
            ConfirmBtn.clicked += () => OnClickConfirmChange(
                ChangePasswordService.cacheChangePasswordData.email,
                ChangePasswordService.cacheChangePasswordData.oldPassword,
                ChangePasswordService.cacheChangePasswordData.newPassword,
                OTP1.value + OTP2.value + OTP3.value + OTP4.value
            );
        }
    }
    
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;

        Start(root);
        PageID prevPage = navigator.PreviousPage();
       
        navigator.BindButton(root, "Btn-Back", prevPage, true);

    }

    public void OnClickConfirmChange(string email, string oldP, string newP, string otpCode)
    {
        service.VerifyAndChangePassword(email, oldP, newP, otpCode)
        .Then(res => {
            Debug.Log("Đổi mật khẩu thành công");
            navigationManager.Navigate(PageID.Login);
            logout.Logout();
        })

        .Catch(err => {
            Debug.LogError("Lỗi xác nhận: " + err.Message);
        });
    }
}