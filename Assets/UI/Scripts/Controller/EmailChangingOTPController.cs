using UnityEngine;
using UnityEngine.UIElements;

public enum OTPFlowType
{
    ChangePassword,
    Register
}
public class EmailChangingOTPController : IPageController
{
    private Button ConfirmBtn;
    private TextField OTP1;
    private TextField OTP2;
    private TextField OTP3;
    private TextField OTP4;
    private NavigationManager navigationManager;
    public static OTPFlowType CurrentFlow = OTPFlowType.ChangePassword;
    private ChangePasswordService service = new ChangePasswordService();
    private ProfileService logout = new ProfileService();

    private void Start(VisualElement root)
    {
        ConfirmBtn = root.Q<Button>("btn-confirm");
        OTP1 = root.Q<TextField>("OTP1");
        OTP2 = root.Q<TextField>("OTP2");
        OTP3 = root.Q<TextField>("OTP3");
        OTP4 = root.Q<TextField>("OTP4");

        // Set subtitle với email đã che
        var subtitle = root.Q<Label>("LabelOTPSubtitle");
        if (subtitle != null)
        {
            string maskedEmail = GetMaskedEmail();
            subtitle.text = $"Chúng tôi đã gửi mã OTP đến email của bạn là {maskedEmail}. Nhập mã OTP bên dưới để xác minh.";
        }

        if (ConfirmBtn != null)
        {
            ConfirmBtn.clicked += HandleConfirmOTP;
        }
    }

    private string GetMaskedEmail()
    {
        string email = "";

        if (CurrentFlow == OTPFlowType.ChangePassword)
        {
            email = ChangePasswordService.cacheChangePasswordData?.email ?? "";
        }
        else if (CurrentFlow == OTPFlowType.Register)
        {
            email = RegisterPageController.CurrentData?.email ?? "";
        }

        // Fallback: lấy từ cache profile
        if (string.IsNullOrEmpty(email) && PlayerPrefs.HasKey(AppConst.KEY_CACHE))
        {
            try
            {
                var cached = JsonUtility.FromJson<RegisterRes>(PlayerPrefs.GetString(AppConst.KEY_CACHE));
                email = cached?.email ?? "";
            }
            catch { }
        }

        if (string.IsNullOrEmpty(email)) return "***@***.com";

        // Che email: giữ 2 ký tự đầu + domain
        int atIndex = email.IndexOf('@');
        if (atIndex <= 2) return "*****" + email.Substring(atIndex);
        return email.Substring(0, 2) + "*****" + email.Substring(atIndex);
    }
    
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;

        Start(root);
        PageID prevPage = navigator.PreviousPage();
       
        navigator.BindButton(root, "Btn-Back", prevPage, true);

    }

    private void HandleConfirmOTP()
    {
        string fullOTP = OTP1.value + OTP2.value + OTP3.value + OTP4.value;

        if (CurrentFlow == OTPFlowType.ChangePassword)
        {
            OnClickConfirmChange(
                ChangePasswordService.cacheChangePasswordData.email,
                ChangePasswordService.cacheChangePasswordData.oldPassword,
                ChangePasswordService.cacheChangePasswordData.newPassword,
                fullOTP
            );
        }
        else if (CurrentFlow == OTPFlowType.Register)
        {
            OnClickConfirmRegister(fullOTP);
        }

        
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

    private void OnClickConfirmRegister(string otpCode)
    {
        // TODO: Viết logic gọi API xác nhận OTP cho đăng ký tại đây
        Debug.Log("Đang xử lý OTP cho luồng đăng ký với mã: " + otpCode);

        navigationManager.Navigate(PageID.PasswordConfirm, false);
        
        // Ví dụ:
        // var dataToSubmit = RegistrationSession.CurrentData;
        // RestClient.Post(REGISTER_VERIFY_URL, dataToSubmit, otpCode)...
    }
}