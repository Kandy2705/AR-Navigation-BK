using System;
using Proyecto26;
using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>Turns a failed REST call into the text shown to the user.</summary>
    public static class ApiError
    {
        public const string NetworkMessage = "Không thể kết nối máy chủ, vui lòng kiểm tra mạng và thử lại";

        [Serializable]
        private class Envelope
        {
            public string message;
        }

        /// <summary>The backend's own message ({success:false, message}) when there is one, otherwise a generic text.</summary>
        public static string Describe(Exception error)
        {
            if (error is RequestException request)
            {
                if (!string.IsNullOrEmpty(request.Response))
                {
                    try
                    {
                        Envelope body = JsonUtility.FromJson<Envelope>(request.Response);
                        if (!string.IsNullOrEmpty(body?.message))
                        {
                            return body.message;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Not JSON: fall through.
                    }
                }

                if (request.IsNetworkError)
                {
                    return NetworkMessage;
                }
            }

            return error?.Message ?? NetworkMessage;
        }
    }

    /// <summary>{success, data:bool, message}: the body of endpoints that answer yes/no.</summary>
    [Serializable]
    public class ApiBoolResponse
    {
        public bool success;
        public bool data;
        public string message;
    }

    [Serializable]
    public class EmailReq
    {
        public string email;
    }

    [Serializable]
    public class VerifyOtpReq
    {
        public string email;
        public string otpCode;
        public string purpose;
    }

    [Serializable]
    public class ResetPasswordReq
    {
        public string email;
        public string otpCode;
        public string newPassword;
    }
}
