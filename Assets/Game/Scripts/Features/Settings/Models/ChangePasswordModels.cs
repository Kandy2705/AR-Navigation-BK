[System.Serializable]
public class RequestOtpBody {
    public string email;
}

[System.Serializable]
public class ChangePasswordBody {
    public string email;
    public string newPassword;
    public string oldPassword;
    public string otpCode;
}