using System;
using System.Collections.Generic;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Auth
{
    /// <summary>
    /// Register, step 1 ("Hoàn thiện hồ sơ của bạn!"): name, phone, email, gender and birthday. Gender and
    /// birthday are chosen in a bottom sheet. Continuing sends the one-time code and opens the OTP screen.
    /// </summary>
    public class RegisterUI : UiPage
    {
        public const string NameMessage = "Vui lòng nhập đầy đủ họ tên";
        public const string PhoneMessage = "Vui lòng nhập số điện thoại";
        public const string EmailMessage = "Vui lòng nhập email";
        public const string InvalidEmailMessage = "Email không hợp lệ";
        public const string GenderMessage = "Vui lòng chọn giới tính";
        public const string BirthdayMessage = "Vui lòng chọn ngày sinh";

        public static readonly string[] Genders = { "Nam", "Nữ", "Khác" };

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField nameField;
        [SerializeField] private UiTextField phoneField;
        [SerializeField] private UiTextField emailField;
        [SerializeField] private UiSelectField genderField;
        [SerializeField] private UiSelectField birthdayField;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private UiPickerSheet pickerSheet;

        private DateTime? birthday;
        private bool busy;

        private void Awake()
        {
            backButton.onClick.AddListener(HandleBack);
            continueButton.onClick.AddListener(HandleContinue);
            genderField.Clicked += OpenGenderPicker;
            birthdayField.Clicked += OpenBirthdayPicker;
        }

        protected override void OnShow()
        {
            busy = false;
            continueButton.interactable = true;
            errorLabel.ShowMessage(null);
            pickerSheet.Hide();
            ShowDraft(AuthFlow.Registration);
        }

        /// <summary>Fills the form from a draft (coming back from the OTP screen) or empties it when there is none.</summary>
        private void ShowDraft(RegistrationDraft draft)
        {
            nameField.Text = draft?.Name ?? string.Empty;
            phoneField.Text = draft?.Phone ?? string.Empty;
            emailField.Text = draft?.Email ?? string.Empty;
            genderField.SetValue(draft?.Gender);
            birthday = draft?.Birthday;
            birthdayField.SetValue(birthday.HasValue ? BirthdayFormat.Display(birthday.Value) : string.Empty);
        }

        private void HandleBack()
        {
            AuthFlow.ClearRegistration();
            GoBack();
        }

        private void HandleContinue()
        {
            if (busy)
            {
                return;
            }

            string error = Validate(out RegistrationDraft draft);
            if (error != null)
            {
                errorLabel.ShowMessage(error);
                return;
            }

            errorLabel.ShowMessage(null);
            AuthFlow.BeginRegistration(draft);
            SetBusy(true);
            AuthServices.Register.RequestOtp(draft.Email, () => OtpSent(draft.Email), HandleFailure);
        }

        private string Validate(out RegistrationDraft draft)
        {
            draft = new RegistrationDraft
            {
                Name = nameField.Text.Trim(),
                Phone = phoneField.Text.Trim(),
                Email = emailField.Text.Trim(),
                Gender = genderField.Value,
                Birthday = birthday,
            };

            if (draft.Name.Length == 0) return NameMessage;
            if (draft.Phone.Length == 0) return PhoneMessage;
            if (draft.Email.Length == 0) return EmailMessage;
            if (!AuthValidation.IsValidEmail(draft.Email)) return InvalidEmailMessage;
            if (draft.Gender.Length == 0) return GenderMessage;
            if (!draft.Birthday.HasValue) return BirthdayMessage;
            return null;
        }

        private void OtpSent(string email)
        {
            SetBusy(false);
            AuthFlow.BeginOtp(email, _ => Open(PageID.PasswordConfirm));
            Open(PageID.OTPPage);
        }

        private void HandleFailure(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[RegisterUI] Could not send the code: " + reason);
            errorLabel.ShowMessage(reason);
        }

        private void OpenGenderPicker()
        {
            int selected = Array.IndexOf(Genders, genderField.Value);
            var layout = new List<UiPickerSheet.Column>
            {
                new UiPickerSheet.Column { Options = Genders, Selected = Math.Max(0, selected) },
            };

            pickerSheet.Show("Chọn giới tính", layout, picked => genderField.SetValue(Genders[picked[0]]));
        }

        private void OpenBirthdayPicker()
        {
            DateTime current = birthday ?? new DateTime(2000, 1, 1);
            List<int> years = BirthdayFormat.Years(DateTime.Today);
            int yearIndex = years.IndexOf(current.Year);

            var layout = new List<UiPickerSheet.Column>
            {
                new UiPickerSheet.Column { Options = BirthdayFormat.Days(), Selected = current.Day - 1 },
                new UiPickerSheet.Column { Options = BirthdayFormat.Months(), Selected = current.Month - 1 },
                new UiPickerSheet.Column { Options = years.ConvertAll(year => year.ToString()), Selected = Math.Max(0, yearIndex) },
            };

            pickerSheet.Show("Chọn ngày sinh", layout, picked =>
            {
                birthday = BirthdayFormat.Clamp(years[picked[2]], picked[1] + 1, picked[0] + 1);
                birthdayField.SetValue(BirthdayFormat.Display(birthday.Value));
            });
        }

        private void SetBusy(bool value)
        {
            busy = value;
            continueButton.interactable = !value;
        }

        private void Open(PageID target)
        {
            if (Navigation != null && Navigation.HasRoute(target))
            {
                Navigation.Navigate(target);
            }
            else
            {
                Debug.Log($"[RegisterUI] {target} screen is not available.");
            }
        }
    }
}
