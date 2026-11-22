using System;
using UnityEngine;

namespace Game.Features.Settings
{
    /// <summary>Contact details of the support team. Shown on the "Liên hệ" page.</summary>
    public static class ContactInfo
    {
        public const string Address = "Trường đại học Bách Khoa TP.HCM";
        public const string Phone = "0865 127 514";
        public const string Email = "man.ngoman2705@gmail.com";

        /// <summary>Opens a URL; replaced in tests so no mail app is launched.</summary>
        public static Action<string> OpenUrl { get; set; } = Application.OpenURL;

        public static string MailUrl => "mailto:" + Email;
    }
}
