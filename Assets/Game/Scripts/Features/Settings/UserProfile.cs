using System;
using System.Globalization;
using UnityEngine;

namespace Game.Features.Settings
{
    /// <summary>The signed-in user's profile as shown on the profile screen, read from the copy cached at login.</summary>
    public sealed class UserProfile
    {
        public string Name = string.Empty;
        public string Phone = string.Empty;
        public string Email = string.Empty;
        public string Gender = string.Empty;

        /// <summary>dd/MM/yyyy when the backend date parses, otherwise the raw text.</summary>
        public string Birthday = string.Empty;

        public static UserProfile Load()
        {
            return FromJson(PlayerPrefs.GetString(AppConst.KEY_CACHE, string.Empty));
        }

        public static UserProfile FromJson(string json)
        {
            var profile = new UserProfile();
            if (string.IsNullOrEmpty(json))
            {
                return profile;
            }

            RegisterRes cached;
            try
            {
                cached = JsonUtility.FromJson<RegisterRes>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[UserProfile] The cached profile is unreadable: " + exception.Message);
                return profile;
            }

            if (cached == null)
            {
                return profile;
            }

            profile.Name = cached.name ?? string.Empty;
            profile.Phone = cached.phone ?? string.Empty;
            profile.Email = cached.email ?? string.Empty;
            profile.Gender = cached.gender ?? string.Empty;
            profile.Birthday = DisplayBirthday(cached.birthday);
            return profile;
        }

        public static string DisplayBirthday(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime date)
                ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : raw;
        }
    }
}
