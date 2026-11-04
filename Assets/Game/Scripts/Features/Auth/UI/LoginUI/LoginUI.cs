using System;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Auth
{
    /// <summary>Login ("Chào mừng trở lại"): email, password, remember me, forgot password, and the success card.</summary>
    public class LoginUI : UiPage
    {
        public const string EmptyMessage = "Vui lòng nhập email và mật khẩu";
        public const string FailedMessage = "Email hoặc mật khẩu không đúng";
        public const string SuccessTitle = "Đăng nhập thành công!";
        public const string SuccessMessage = "Vui lòng chờ....\nBạn sẽ được chuyển qua trang chủ.";

        [SerializeField] private Button backButton;
        [SerializeField] private UiTextField emailField;
        [SerializeField] private UiTextField passwordField;
        [SerializeField] private Toggle rememberToggle;
        [SerializeField] private Button forgotButton;
        [SerializeField] private Button loginButton;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private UiStatusOverlay successOverlay;
        [SerializeField, Min(0f)] private float successSeconds = 2f;

        private ILoginApi api;
        private Action<LoginRes, string> startSession = LoginSession.Start;
        private SavedCredentials saved = new SavedCredentials();
        private bool busy;

        /// <summary>Replaces the backend pieces (used by tests and by anything that wants a different sign-in).</summary>
        public void Configure(ILoginApi loginApi, Action<LoginRes, string> sessionStarter, SavedCredentials credentials)
        {
            api = loginApi;
            startSession = sessionStarter;
            saved = credentials;
        }

        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            forgotButton.onClick.AddListener(OpenForgotPassword);
            loginButton.onClick.AddListener(HandleLogin);
        }

        protected override void OnShow()
        {
            busy = false;
            loginButton.interactable = true;
            errorLabel.ShowMessage(null);
            successOverlay.Hide();

            if (saved.IsRemembered)
            {
                emailField.Text = saved.Email;
                passwordField.Text = saved.Password;
                rememberToggle.isOn = true;
            }
        }

        private void HandleLogin()
        {
            if (busy)
            {
                return;
            }

            string email = emailField.Text.Trim();
            string password = passwordField.Text;
            if (email.Length == 0 || password.Length == 0)
            {
                errorLabel.ShowMessage(EmptyMessage);
                return;
            }

            errorLabel.ShowMessage(null);
            SetBusy(true);
            (api ?? AuthServices.Login).Login(email, password, login => HandleSuccess(login, email, password), HandleFailure);
        }

        private void HandleSuccess(LoginRes login, string email, string password)
        {
            SetBusy(false);
            startSession(login, password);

            if (rememberToggle.isOn)
            {
                saved.Remember(email, password);
            }
            else
            {
                saved.Forget();
            }

            successOverlay.ShowThen(SuccessTitle, SuccessMessage, successSeconds, OpenHome);
        }

        private void HandleFailure(string reason)
        {
            SetBusy(false);
            Debug.LogWarning("[LoginUI] Login failed: " + reason);
            errorLabel.ShowMessage(FailedMessage);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            loginButton.interactable = !value;
        }

        private void OpenForgotPassword()
        {
            Open(PageID.EmailChangeForm);
        }

        private void OpenHome()
        {
            if (!Open(PageID.MainSettings))
            {
                successOverlay.Hide();
            }
        }

        private bool Open(PageID target)
        {
            if (Navigation != null && Navigation.HasRoute(target))
            {
                Navigation.Navigate(target);
                return true;
            }

            // The target screen has not been migrated to Canvas yet.
            Debug.Log($"[LoginUI] {target} screen is not available yet.");
            return false;
        }
    }
}
