using System;

[Serializable]
public class RegisterReq
{
    public string email;
    public string password;
    public string name;
    public string phone;
    public string birthday; 
    public string cccd;    
    public string role;     
    public string gender; 

    public RegisterReq()
    {
        this.role = "Customer"; 
        this.cccd = "0123456789"; 
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