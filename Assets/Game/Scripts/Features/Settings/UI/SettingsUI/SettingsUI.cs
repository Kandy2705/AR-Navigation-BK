using Game.Features.Settings;
using Game.Foundation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Settings
{
    /// <summary>"Cài đặt": account, about, logout (with confirmation) and the bottom tab bar.</summary>
    public class SettingsUI : UiPage
    {
        public const string LogoutTitle = "Đăng xuất";
        public const string LogoutMessage = "Bạn có chắc chắn muốn đăng xuất ra khỏi tài khoản này";

        [SerializeField] private Button profileButton;
        [SerializeField] private Button supportButton;
        [SerializeField] private Button contactButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private UiBottomNav bottomNav;
        [SerializeField] private UiModal logoutModal;

        private void Awake()
        {
            profileButton.onClick.AddListener(() => OpenPage(PageID.Profile));
            supportButton.onClick.AddListener(() => OpenPage(PageID.SupportCenter));
            contactButton.onClick.AddListener(() => OpenPage(PageID.Contact));
            logoutButton.onClick.AddListener(AskLogout);
            bottomNav.TabClicked += tab => UiBottomNav.Navigate(Navigation, tab);
            logoutModal.Confirmed += SignOut;
        }

        protected override void OnShow()
        {
            bottomNav.Select(UiBottomNav.SettingsTab);
            logoutModal.Hide();
        }

        private void AskLogout()
        {
            logoutModal.SetTitleColor(UiTheme.DangerText);
            logoutModal.Show(LogoutTitle, LogoutMessage, LogoutTitle, "Hủy");
        }

        private void SignOut()
        {
            AccountSession.SignOut();
            if (Navigation != null)
            {
                Navigation.ResetTo(PageID.Login);
            }
        }
    }
}
