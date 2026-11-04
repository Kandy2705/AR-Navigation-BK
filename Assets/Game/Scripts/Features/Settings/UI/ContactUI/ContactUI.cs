using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Settings
{
    /// <summary>"Thông tin liên hệ": address, phone and email, and a button that opens the mail app.</summary>
    public class ContactUI : UiPage
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text addressLabel;
        [SerializeField] private TMP_Text phoneLabel;
        [SerializeField] private TMP_Text emailLabel;
        [SerializeField] private Button sendEmailButton;

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            sendEmailButton.onClick.AddListener(() => ContactInfo.OpenUrl(ContactInfo.MailUrl));
        }

        protected override void OnShow()
        {
            addressLabel.text = ContactInfo.Address;
            phoneLabel.text = ContactInfo.Phone;
            emailLabel.text = ContactInfo.Email;
        }
    }
}
