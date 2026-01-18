public static class ApiConfig
{
    // 1. BASE URL (Gốc)
    // Lưu ý: Không có phần /index.html hay ?fbclid...
    public const string BASE_URL = "https://arnavbk-avbcg7hecgacc5bg.malaysiawest-01.azurewebsites.net";

    // 2. CÁC ENDPOINTS (Nhánh con)
    public const string REGISTER = BASE_URL + "/users/register";
    public const string LOGIN = BASE_URL + "/auth/login"; 
    public const string GET_PROFILE = BASE_URL + "/users/me";
    public const string UPLOAD_AVATAR = BASE_URL + "/users/upload";
}