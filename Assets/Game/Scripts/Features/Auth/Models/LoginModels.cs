using System;

[Serializable]
public class LoginReq
{
    public string email;
    public string password;
}


[Serializable]
public class LoginRes
{
    public string accessToken;    
    public string refreshToken;
    public string expiresAt;
  
}

[Serializable]
public class LoginResponseWrapper
{
    public bool success;
    public LoginRes data;
    public string message;
    public string errorCode;
}

