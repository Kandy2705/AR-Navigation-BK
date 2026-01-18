using System;

[Serializable]
public class RegisterReq
{
    public string email;
    public string password;
    public string name;
    public string phone;
    public string birthday; // Định dạng chuỗi "YYYY-MM-DD"
    public string cccd;     // Căn cước công dân
    public string role;     // Mặc định là "Customer"
    public string gender;   // "Male" hoặc "Female"

    // Constructor đặt giá trị mặc định cho những cái UI chưa có
    public RegisterReq()
    {
        this.role = "Customer"; 
        this.cccd = "0123456789"; // Để trống hoặc fake tạm: "0123456789"
    }

    public string Data => $"Email: {email}, Password: {password}, Name: {name}, Phone: {phone}, Birthday: {birthday}, CCCD: {cccd}, Role: {role}, Gender: {gender}";
}


[Serializable]
public class RegisterRes
{
    public string email;    
    public string name;
    public string phone;
    public string birthday;
    public string role;
    public string gender;
}