using System;
using System.Runtime.InteropServices;

public static class IosCameraPermissionBridge
{
    public enum AuthorizationStatus
    {
        NotDetermined = 0,
        Restricted = 1,
        Denied = 2,
        Authorized = 3,
        Unavailable = -1,
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int ARNavCameraAuthorizationStatus();

    [DllImport("__Internal")]
    private static extern void ARNavRequestCameraAuthorization();
#endif

    public static AuthorizationStatus GetAuthorizationStatus()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            return (AuthorizationStatus)ARNavCameraAuthorizationStatus();
        }
        catch (Exception)
        {
            return AuthorizationStatus.Unavailable;
        }
#else
        return AuthorizationStatus.Authorized;
#endif
    }

    public static bool RequestAuthorization()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            ARNavRequestCameraAuthorization();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
#else
        return true;
#endif
    }
}
