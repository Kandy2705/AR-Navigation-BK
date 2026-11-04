using System;
using UnityEngine;

namespace Game.Features.Settings
{
    /// <summary>Contact details of the support team. The values are the mock-up's placeholders until real ones are supplied.</summary>
    public static class ContactInfo
    {
        public const string Address = "Trường đại học Bách Khoa TP.HCM";
        public const string Phone = "0123 456 789";
        public const string Email = "contact@example.com";

        /// <summary>Opens a URL; replaced in tests so no mail app is launched.</summary>
        public static Action<string> OpenUrl { get; set; } = Application.OpenURL;

        public static string MailUrl => "mailto:" + Email;
    }
}
