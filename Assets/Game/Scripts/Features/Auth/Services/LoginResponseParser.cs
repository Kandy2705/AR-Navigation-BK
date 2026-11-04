using UnityEngine;

namespace Game.Features.Auth
{
    /// <summary>Reads the login response. The backend wraps it as {success, data:{tokens}}; a flat {accessToken,...} is accepted too.</summary>
    public static class LoginResponseParser
    {
        public static LoginRes Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            LoginRes wrapped = TryParse(() => JsonUtility.FromJson<LoginResponseWrapper>(json)?.data);
            if (wrapped != null)
            {
                return wrapped;
            }

            return TryParse(() => JsonUtility.FromJson<LoginRes>(json));
        }

        private static LoginRes TryParse(System.Func<LoginRes> read)
        {
            try
            {
                LoginRes result = read();
                return result != null && !string.IsNullOrEmpty(result.accessToken) ? result : null;
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }
    }
}
