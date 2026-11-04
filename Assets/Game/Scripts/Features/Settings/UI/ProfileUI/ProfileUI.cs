using Game.Foundation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Settings
{
    /// <summary>
    /// "Thông tin cá nhân": the cached profile and the entries to the email and password changes. The fields are
    /// display-only because the backend has no profile update endpoint yet.
    /// </summary>
    public class ProfileUI : UiPage
    {
        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField nameField;
        [SerializeField] private UiTextField phoneField;
        [SerializeField] private UiSelectField genderField;
        [SerializeField] private UiSelectField birthdayField;
        [SerializeField] private Button changeEmailButton;
        [SerializeField] private Button changePasswordButton;

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            changeEmailButton.onClick.AddListener(() => OpenPage(PageID.EmailChange));
            changePasswordButton.onClick.AddListener(() => OpenPage(PageID.PasswordChange));
            nameField.SetReadOnly(true);
            phoneField.SetReadOnly(true);
        }

        protected override void OnShow()
        {
            UserProfile profile = UserProfile.Load();
            nameField.Text = profile.Name;
            phoneField.Text = profile.Phone;
            genderField.SetValue(profile.Gender);
            birthdayField.SetValue(profile.Birthday);
        }
    }
}
