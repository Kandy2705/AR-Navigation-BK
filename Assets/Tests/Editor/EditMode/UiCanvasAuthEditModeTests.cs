#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Features.Auth;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Phase 2 of the UI Toolkit → Canvas migration: login and forgot-password screens. Logic is tested on its
    /// own; each screen is then driven in a preview scene with fake backends, so no request leaves the editor.
    /// When an asset is missing run Tools ▸ UI Canvas ▸ Run Phase 2 Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasAuthEditModeTests
    {
        private const string SetupHint = " — run Tools/UI Canvas/Run Phase 2 Setup";
        private const string KeyPrefix = "UiCanvasTest_";

        private IForgotPasswordApi originalForgotApi;

        [SetUp]
        public void SetUp()
        {
            originalForgotApi = AuthServices.ForgotPassword;
            AuthFlow.Clear();
            NavigationManager.pageHistory.Clear();
            new SavedCredentials(KeyPrefix).Forget();
        }

        [TearDown]
        public void TearDown()
        {
            AuthServices.ForgotPassword = originalForgotApi;
            AuthFlow.Clear();
            new SavedCredentials(KeyPrefix).Forget();
            PlayerPrefs.DeleteKey(KeyPrefix + "KEY_IS_REMEMBER");
            NavigationManager.pageHistory.Clear();
        }

        // ------------------------------------------------------------------ logic

        [TestCase("quyen@x.com", "qu*****@x.com")]
        [TestCase("ab@x.com", "*****@x.com")]
        [TestCase("a@x.com", "*****@x.com")]
        [TestCase("", EmailMasker.Unknown)]
        [TestCase("not-an-email", EmailMasker.Unknown)]
        public void EmailMasker_WhenMasking_KeepsTwoLettersAndTheDomain(string email, string expected)
        {
            Assert.AreEqual(expected, EmailMasker.Mask(email));
        }

        [TestCase("a@b.com", true)]
        [TestCase("  a.b+c@mail.example.vn ", true)]
        [TestCase("a@b", false)]
        [TestCase("a b@c.com", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValidEmail_WhenGivenInput_OnlyAcceptsWellFormedEmails(string email, bool expected)
        {
            Assert.AreEqual(expected, AuthValidation.IsValidEmail(email));
        }

        [TestCase("abc", "abc", true)]
        [TestCase("abc", "abd", false)]
        [TestCase("", "", false)]
        [TestCase(null, null, false)]
        public void PasswordsMatch_WhenComparing_RequiresNonEmptyEqualValues(string a, string b, bool expected)
        {
            Assert.AreEqual(expected, AuthValidation.PasswordsMatch(a, b));
        }

        [Test]
        public void LoginResponseParser_WhenBackendWrapsTheTokens_ReadsThem()
        {
            LoginRes res = LoginResponseParser.Parse("{\"success\":true,\"data\":{\"accessToken\":\"AAA\",\"refreshToken\":\"RRR\"}}");
            Assert.NotNull(res);
            Assert.AreEqual("AAA", res.accessToken);
            Assert.AreEqual("RRR", res.refreshToken);
        }

        [Test]
        public void LoginResponseParser_WhenTokensAreFlat_ReadsThem()
        {
            LoginRes res = LoginResponseParser.Parse("{\"accessToken\":\"AAA\",\"refreshToken\":\"RRR\"}");
            Assert.NotNull(res);
            Assert.AreEqual("AAA", res.accessToken);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"success\":false,\"message\":\"nope\"}")]
        [TestCase("{\"success\":true,\"data\":{\"refreshToken\":\"RRR\"}}")]
        public void LoginResponseParser_WhenThereIsNoAccessToken_ReturnsNull(string json)
        {
            Assert.IsNull(LoginResponseParser.Parse(json));
        }

        [Test]
        public void SavedCredentials_WhenRememberedThenForgotten_ClearsTheLogin()
        {
            var saved = new SavedCredentials(KeyPrefix);
            Assert.IsFalse(saved.IsRemembered);

            saved.Remember("a@b.com", "secret");
            Assert.IsTrue(saved.IsRemembered);
            Assert.AreEqual("a@b.com", saved.Email);
            Assert.AreEqual("secret", saved.Password);

            saved.Forget();
            Assert.IsFalse(saved.IsRemembered);
            Assert.AreEqual(string.Empty, saved.Email);
            Assert.AreEqual(string.Empty, saved.Password);
        }

        [Test]
        public void AuthFlow_WhenOtpConfirmed_KeepsTheCodeAndCallsTheFlow()
        {
            string received = null;
            AuthFlow.BeginOtp("a@b.com", code => received = code);

            AuthFlow.ConfirmOtp("1234");

            Assert.AreEqual("1234", received);
            Assert.AreEqual("a@b.com", AuthFlow.Email);
            Assert.AreEqual("1234", AuthFlow.Otp);

            AuthFlow.Clear();
            Assert.AreEqual(string.Empty, AuthFlow.Email);
            Assert.AreEqual(string.Empty, AuthFlow.Otp);
        }

        // ------------------------------------------------------------------ prefabs

        [TestCase(UiCanvasAuthBuilder.LoginName, PageID.Login)]
        [TestCase(UiCanvasAuthBuilder.ForgotEmailName, PageID.EmailChangeForm)]
        [TestCase(UiCanvasAuthBuilder.OtpName, PageID.OTPPage)]
        [TestCase(UiCanvasAuthBuilder.NewPasswordName, PageID.PasswordChangeForm)]
        public void AuthPrefab_WhenBuilt_IsAUiPageWithItsPageId(string screenName, PageID expected)
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(page, screenName + ".prefab" + SetupHint);
            Assert.AreEqual(expected, page.Id);
        }

        public static IEnumerable<string> AllScreens()
        {
            foreach (UiCanvasShellRoutes.Route route in UiCanvasShellRoutes.All)
            {
                yield return route.Screen;
            }
        }

        [TestCaseSource(nameof(AllScreens))]
        public void ScreenPrefab_WhenBuilt_HasEveryReferenceWired(string screenName)
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(page, screenName + ".prefab" + SetupHint);

            SerializedProperty property = new SerializedObject(page).GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                {
                    Assert.NotNull(property.objectReferenceValue, screenName + "." + property.name + " is not wired");
                }
            }
        }

        [Test]
        public void StatusOverlay_WhenNestedInAPage_StartsHidden()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.LoginName, KeyPrefix))
            {
                Assert.IsFalse(harness.Overlay.IsVisible);
            }
        }

        // ------------------------------------------------------------------ login

        [Test]
        public void Login_WhenFieldsAreEmpty_ShowsTheMessageAndDoesNotCallTheApi()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.LoginName, KeyPrefix))
            {
                var api = new FakeLoginApi();
                LoginUI page = harness.Open<LoginUI>(api: api);

                harness.Click("Content/LoginButton");

                Assert.AreEqual(0, api.Calls);
                Assert.AreEqual(LoginUI.EmptyMessage, harness.ErrorText);
            }
        }

        [Test]
        public void Login_WhenTheApiSucceeds_StartsTheSessionRemembersAndShowsTheSuccessCard()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.LoginName, KeyPrefix))
            {
                var api = new FakeLoginApi { Result = new LoginRes { accessToken = "AAA", refreshToken = "RRR" } };
                LoginRes started = null;
                string startedPassword = null;
                LoginUI page = harness.Open<LoginUI>(api: api, session: (res, password) =>
                {
                    started = res;
                    startedPassword = password;
                });

                harness.Type("Content/Card/EmailField", " a@b.com ");
                harness.Type("Content/Card/PasswordField", "secret");
                harness.Find<Toggle>("Content/RememberToggle").isOn = true;
                harness.Click("Content/LoginButton");

                Assert.AreEqual("a@b.com", api.Email, "email is trimmed");
                Assert.AreEqual("secret", api.Password);
                Assert.AreEqual("AAA", started.accessToken);
                Assert.AreEqual("secret", startedPassword);
                Assert.IsTrue(harness.Saved.IsRemembered);
                Assert.AreEqual("a@b.com", harness.Saved.Email);
                Assert.IsTrue(harness.Overlay.IsVisible, "success card");
                Assert.AreEqual(LoginUI.SuccessTitle, harness.Find<TMP_Text>("StatusOverlay/Card/Title").text);
                Assert.AreEqual(string.Empty, harness.ErrorText);
            }
        }

        [Test]
        public void Login_WhenTheApiSucceedsWithoutRememberMe_ForgetsTheSavedLogin()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.LoginName, KeyPrefix))
            {
                harness.Saved.Remember("old@b.com", "old");
                var api = new FakeLoginApi { Result = new LoginRes { accessToken = "AAA" } };
                harness.Open<LoginUI>(api: api, session: (res, password) => { });

                harness.Type("Content/Card/EmailField", "a@b.com");
                harness.Type("Content/Card/PasswordField", "secret");
                harness.Find<Toggle>("Content/RememberToggle").isOn = false;
                harness.Click("Content/LoginButton");

                Assert.IsFalse(harness.Saved.IsRemembered);
            }
        }

        [Test]
        public void Login_WhenTheApiFails_ShowsTheErrorAndAllowsRetrying()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.LoginName, KeyPrefix))
            {
                var api = new FakeLoginApi { Error = "401" };
                bool sessionStarted = false;
                harness.Open<LoginUI>(api: api, session: (res, password) => sessionStarted = true);

                harness.Type("Content/Card/EmailField", "a@b.com");
                harness.Type("Content/Card/PasswordField", "wrong");
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[LoginUI] Login failed: 401");
                harness.Click("Content/LoginButton");

                Assert.AreEqual(LoginUI.FailedMessage, harness.ErrorText);
                Assert.IsTrue(harness.Find<Button>("Content/LoginButton").interactable, "can retry");
                Assert.IsFalse(sessionStarted);
                Assert.IsFalse(harness.Overlay.IsVisible);
            }
        }

        [Test]
        public void Login_WhenOpenedWithARememberedLogin_FillsTheForm()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.LoginName, KeyPrefix))
            {
                harness.Saved.Remember("a@b.com", "secret");
                harness.Open<LoginUI>(api: new FakeLoginApi());

                Assert.AreEqual("a@b.com", harness.Find<UiTextField>("Content/Card/EmailField").Text);
                Assert.AreEqual("secret", harness.Find<UiTextField>("Content/Card/PasswordField").Text);
                Assert.IsTrue(harness.Find<Toggle>("Content/RememberToggle").isOn);
            }
        }

        // ------------------------------------------------------------------ forgot password

        [Test]
        public void ForgotEmail_WhenTheEmailIsInvalid_ShowsTheMessageAndSendsNothing()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.ForgotEmailName, KeyPrefix))
            {
                var forgot = new FakeForgotApi();
                AuthServices.ForgotPassword = forgot;
                harness.Open<ForgotEmailUI>();

                harness.Type("Content/Card/EmailField", "not-an-email");
                harness.Click("Content/ContinueButton");

                Assert.AreEqual(0, forgot.OtpRequests.Count);
                Assert.AreEqual(ForgotEmailUI.InvalidEmailMessage, harness.ErrorText);
            }
        }

        [Test]
        public void ForgotEmail_WhenTheEmailIsValid_RequestsTheCodeAndStartsTheOtpFlow()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.ForgotEmailName, KeyPrefix))
            {
                var forgot = new FakeForgotApi();
                AuthServices.ForgotPassword = forgot;
                harness.Open<ForgotEmailUI>();

                harness.Type("Content/Card/EmailField", " a@b.com ");
                harness.Click("Content/ContinueButton");

                CollectionAssert.AreEqual(new[] { "a@b.com" }, forgot.OtpRequests);
                Assert.AreEqual("a@b.com", AuthFlow.Email);
                Assert.IsNotNull(AuthFlow.OnOtpConfirmed);
            }
        }

        [Test]
        public void Otp_WhenOpened_ShowsTheMaskedEmail()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.OtpName, KeyPrefix))
            {
                AuthFlow.BeginOtp("quyen@x.com", _ => { });
                harness.Open<OtpUI>();

                string subtitle = harness.Find<TMP_Text>("Content/Subtitle").text;
                StringAssert.Contains("qu*****@x.com", subtitle);
            }
        }

        [Test]
        public void Otp_WhenTheCodeIsIncomplete_ShowsTheMessageAndDoesNotContinue()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.OtpName, KeyPrefix))
            {
                string received = null;
                AuthFlow.BeginOtp("a@b.com", code => received = code);
                harness.Open<OtpUI>();

                harness.SetDigits("12");
                harness.Click("Content/ConfirmButton");

                Assert.IsNull(received);
                Assert.AreEqual(OtpUI.IncompleteMessage, harness.ErrorText);
            }
        }

        [Test]
        public void Otp_WhenTheCodeIsComplete_HandsItToTheFlow()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.OtpName, KeyPrefix))
            {
                string received = null;
                AuthFlow.BeginOtp("a@b.com", code => received = code);
                harness.Open<OtpUI>();

                harness.SetDigits("1234");
                harness.Click("Content/ConfirmButton");

                Assert.AreEqual("1234", received);
                Assert.AreEqual("1234", AuthFlow.Otp);
            }
        }

        [Test]
        public void NewPassword_WhenTheFieldsAreEmptyOrDifferent_ShowsTheMatchingMessage()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.NewPasswordName, KeyPrefix))
            {
                var forgot = new FakeForgotApi();
                AuthServices.ForgotPassword = forgot;
                harness.Open<NewPasswordUI>();

                harness.Click("Content/SubmitButton");
                Assert.AreEqual(NewPasswordUI.EmptyMessage, harness.ErrorText);

                harness.Type("Content/Card/PasswordField", "abc12345");
                harness.Type("Content/Card/ConfirmField", "different");
                harness.Click("Content/SubmitButton");
                Assert.AreEqual(NewPasswordUI.MismatchMessage, harness.ErrorText);
                Assert.AreEqual(0, forgot.Resets.Count);
            }
        }

        [Test]
        public void NewPassword_WhenThePasswordsMatch_ResetsItAndShowsTheSuccessCard()
        {
            using (var harness = new PageHarness(UiCanvasAuthBuilder.NewPasswordName, KeyPrefix))
            {
                var forgot = new FakeForgotApi();
                AuthServices.ForgotPassword = forgot;
                AuthFlow.BeginOtp("a@b.com", _ => { });
                AuthFlow.ConfirmOtp("1234");
                harness.Open<NewPasswordUI>();

                harness.Type("Content/Card/PasswordField", "abc12345");
                harness.Type("Content/Card/ConfirmField", "abc12345");
                harness.Click("Content/SubmitButton");

                CollectionAssert.AreEqual(new[] { "a@b.com|1234|abc12345" }, forgot.Resets);
                Assert.IsTrue(harness.Overlay.IsVisible, "success card");
                Assert.AreEqual(string.Empty, harness.ErrorText);
            }
        }

        // ------------------------------------------------------------------ router

        [Test]
        public void NavigationManager_WhenGoingBackAndReturning_FollowsTheHistory()
        {
            var canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            var container = new GameObject("Pages", typeof(RectTransform)).GetComponent<RectTransform>();
            container.SetParent(canvasGo.transform, false);
            var navigationGo = new GameObject("TestNavigation");
            navigationGo.SetActive(false);
            try
            {
                var navigation = navigationGo.AddComponent<NavigationManager>();
                var serialized = new SerializedObject(navigation);
                SerializedProperty routes = serialized.FindProperty("pages");
                routes.arraySize = 3;
                SetRoute(routes.GetArrayElementAtIndex(0), PageID.SplashPage, UiCanvasOnboardingBuilder.SplashName);
                SetRoute(routes.GetArrayElementAtIndex(1), PageID.Onboarding, UiCanvasOnboardingBuilder.IntroName);
                SetRoute(routes.GetArrayElementAtIndex(2), PageID.WelcomePage, UiCanvasOnboardingBuilder.WelcomeName);
                serialized.FindProperty("firstPage").enumValueIndex = (int)PageID.SplashPage;
                serialized.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
                serialized.FindProperty("canvasPageContainer").objectReferenceValue = container;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Invoke(navigation, "Awake");
                Invoke(navigation, "OnEnable");
                navigation.Navigate(PageID.Onboarding);
                navigation.Navigate(PageID.WelcomePage);

                Assert.IsTrue(navigation.GoBack());
                Assert.IsTrue(IsActive(container, UiCanvasOnboardingBuilder.IntroName), "back goes to the intro");
                Assert.IsFalse(IsActive(container, UiCanvasOnboardingBuilder.WelcomeName));

                navigation.Navigate(PageID.WelcomePage);
                Assert.IsTrue(navigation.ReturnTo(PageID.SplashPage));
                Assert.IsTrue(IsActive(container, UiCanvasOnboardingBuilder.SplashName), "returned to the first page");
                Assert.IsFalse(IsActive(container, UiCanvasOnboardingBuilder.WelcomeName));
                Assert.AreEqual(1, NavigationManager.pageHistory.Count, "everything after it was dropped");

                Assert.IsFalse(navigation.GoBack(), "nothing before the first page");
                Assert.IsFalse(navigation.ReturnTo(PageID.Login), "not in the history");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(navigationGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void SetRoute(SerializedProperty route, PageID id, string screenName)
        {
            route.FindPropertyRelative("id").enumValueIndex = (int)id;
            route.FindPropertyRelative("canvasPage").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
        }

        private static bool IsActive(Transform container, string pageName)
        {
            Transform page = container.Find(pageName + "(Clone)");
            return page != null && page.gameObject.activeSelf;
        }

        private static void Invoke(object target, string method)
        {
            target.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(target, null);
        }

        private sealed class FakeLoginApi : ILoginApi
        {
            public int Calls;
            public string Email;
            public string Password;
            public LoginRes Result;
            public string Error;

            public void Login(string email, string password, Action<LoginRes> onSuccess, Action<string> onError)
            {
                Calls++;
                Email = email;
                Password = password;
                if (Result != null)
                {
                    onSuccess(Result);
                }
                else
                {
                    onError(Error ?? "failed");
                }
            }
        }

        private sealed class FakeForgotApi : IForgotPasswordApi
        {
            public readonly List<string> OtpRequests = new List<string>();
            public readonly List<string> Resets = new List<string>();

            public void RequestOtp(string email, Action onSuccess, Action<string> onError)
            {
                OtpRequests.Add(email);
                onSuccess();
            }

            public void ResetPassword(string email, string otp, string newPassword, Action onSuccess, Action<string> onError)
            {
                Resets.Add(email + "|" + otp + "|" + newPassword);
                onSuccess();
            }
        }
    }
}
#endif
