using System;

[Serializable]
public class RegisterRequest
{
    public string email;
    public string password;
    public string name;
    public string phone;
    public string birthday; 
    public string cccd;
    public string role;     
    public string gender;   
}

[Serializable]
public class RegisterResponse
{
    public string email;
    public string name;
    public string phone;
    public string birthday;
    public string role;
    public string gender;
}