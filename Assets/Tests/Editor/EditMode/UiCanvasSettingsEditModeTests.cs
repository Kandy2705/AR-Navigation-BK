#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Features.Auth;
using Game.Features.Chat;
using Game.Features.Settings;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Phase 7 of the UI Toolkit → Canvas migration: settings, profile, change email, change password, support center
    /// and contact. A fake account backend answers at once, and the profile cache and tokens are restored after every test.
    /// When an asset is missing run Tools ▸ UI Canvas ▸ Run Phase 4-7 Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasSettingsEditModeTests
    {
        private const string KeyPrefix = "UiCanvasSettingsTest_";
        private const string ProfileJson = "{\"email\":\"kandy@example.com\",\"name\":\"Kandy\",\"phone\":\"04384983483\",\"gender\":\"Nữ\",\"birthday\":\"2004-05-27T00:00:00.000Z\"}";
        private static readonly string[] SavedKeys = { AppConst.KEY_CACHE, "ACCESS_TOKEN", "REFRESH_TOKEN", "PASSWORD" };

        private readonly Dictionary<string, string> savedPrefs = new Dictionary<string, string>();
        private IAccountApi originalApi;
        private Action<string> originalOpenUrl;
        private FakeAccountApi api;

        [SetUp]
        public void SetUp()
        {
            foreach (string key in SavedKeys)
            {
                savedPrefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                PlayerPrefs.DeleteKey(key);
            }

            originalApi = AccountServices.Api;
            originalOpenUrl = ContactInfo.OpenUrl;
            api = new FakeAccountApi();
            AccountServices.Api = api;
            AuthFlow.Clear();
            ChatState.Clear();
            NavigationManager.pageHistory.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string key in SavedKeys)
            {
                if (savedPrefs[key] == null)
                {
                    PlayerPrefs.DeleteKey(key);
                }
                else
                {
                    PlayerPrefs.SetString(key, savedPrefs[key]);
                }
            }

            PlayerPrefs.Save();
            AccountServices.Api = originalApi;
            ContactInfo.OpenUrl = originalOpenUrl;
            AuthFlow.Clear();
            ChatState.Clear();
            NavigationManager.pageHistory.Clear();
        }

        // ------------------------------------------------------------------ models and content

        [Test]
        public void UserProfile_WhenTheCacheIsComplete_ReadsEveryField()
        {
            UserProfile profile = UserProfile.FromJson(ProfileJson);

            Assert.AreEqual("Kandy", profile.Name);
            Assert.AreEqual("04384983483", profile.Phone);
            Assert.AreEqual("kandy@example.com", profile.Email);
            Assert.AreEqual("Nữ", profile.Gender);
            Assert.AreEqual("27/05/2004", profile.Birthday);
        }

        [Test]
        public void UserProfile_WhenTheCacheIsMissingOrBroken_IsEmpty()
        {
            Assert.AreEqual(string.Empty, UserProfile.FromJson(string.Empty).Name);
            Assert.AreEqual(string.Empty, UserProfile.Load().Email, "nothing is cached");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[UserProfile\] The cached profile is unreadable"));
            Assert.AreEqual(string.Empty, UserProfile.FromJson("{broken").Email);
        }

        [Test]
        public void UserProfile_WhenTheBirthdayIsNotADate_KeepsTheRawText()
        {
            Assert.AreEqual("27/05/2004", UserProfile.DisplayBirthday("2004-05-27T00:00:00.000Z"));
            Assert.AreEqual("sometime", UserProfile.DisplayBirthday("sometime"));
            Assert.AreEqual(string.Empty, UserProfile.DisplayBirthday(null));
        }

        [Test]
        public void SupportContent_WhenFilteringByCategory_ReturnsThatCategoryOnly()
        {
            Assert.AreEqual(6, SupportContent.For(0).Count, "All");
            Assert.AreEqual(4, SupportContent.For(1).Count, "Account");
            Assert.AreEqual(1, SupportContent.For(2).Count, "Billing");
            Assert.AreEqual(1, SupportContent.For(3).Count, "Prompting");
            Assert.AreEqual(SupportContent.Entries.Length, SupportContent.For(-1).Count);
        }

        // ------------------------------------------------------------------ prefabs

        [TestCase(UiCanvasSettingsBuilder.SettingsName, PageID.MainSettings)]
        [TestCase(UiCanvasSettingsBuilder.ProfileName, PageID.Profile)]
        [TestCase(UiCanvasSettingsBuilder.ChangeEmailName, PageID.EmailChange)]
        [TestCase(UiCanvasSettingsBuilder.ChangePasswordName, PageID.PasswordChange)]
        [TestCase(UiCanvasSettingsBuilder.SupportCenterName, PageID.SupportCenter)]
        [TestCase(UiCanvasSettingsBuilder.ContactName, PageID.Contact)]
        public void SettingsPrefabs_WhenBuilt_HaveTheirPageIdAndEveryReferenceWired(string screen, PageID expected)
        {
            UiPage page = PrefabAsserts.LoadPage(screen);

            Assert.AreEqual(expected, page.Id);
            PrefabAsserts.AssertAllReferencesWired(page.gameObject, screen);
        }

        [Test]
        public void Shell_WhenBuilt_RoutesEveryScreenOnce()
        {
            var seen = new HashSet<PageID>();
            foreach (UiCanvasShellRoutes.Route route in UiCanvasShellRoutes.All)
            {
                Assert.IsTrue(seen.Add(route.Id), "duplicate route for " + route.Id);
                Assert.AreEqual(route.Id, PrefabAsserts.LoadPage(route.Screen).Id, route.Screen + " answers to its route's PageID");
            }

            foreach (PageID required in new[] { PageID.ARPage, PageID.HistoryPage, PageID.Chatbox, PageID.SearchPage, PageID.MainSettings, PageID.Profile, PageID.EmailChange, PageID.PasswordChange, PageID.SupportCenter, PageID.Contact })
            {
                Assert.Contains(required, new List<PageID>(seen));
            }
        }

        // ------------------------------------------------------------------ settings

        [Test]
        public void Settings_WhenShown_HighlightsTheSettingsTab()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SettingsName, KeyPrefix))
            {
                harness.Open<SettingsUI>();

                Assert.AreEqual(UiBottomNav.SettingsTab, harness.Find<UiBottomNav>("Content/BottomNav").Selected);
                Assert.AreEqual(UiTheme.NavActive, harness.Find<TMP_Text>("Content/BottomNav/Tab3/Label").color);
                Assert.IsFalse(harness.Find<UiModal>("ConfirmSheet").IsVisible);
            }
        }

        [Test]
        public void Settings_WhenLogoutIsTapped_AsksFirstAndKeepsTheSessionOnCancel()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SettingsName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                harness.Open<SettingsUI>();

                harness.Click("Content/LogoutButton");
                var modal = harness.Find<UiModal>("ConfirmSheet");
                Assert.IsTrue(modal.IsVisible);
                Assert.AreEqual(SettingsUI.LogoutTitle, harness.Find<TMP_Text>("ConfirmSheet/Panel/Title").text);
                Assert.AreEqual(UiTheme.DangerText, harness.Find<TMP_Text>("ConfirmSheet/Panel/Title").color, "the title is red");
                Assert.AreEqual(SettingsUI.LogoutMessage, harness.Find<TMP_Text>("ConfirmSheet/Panel/Message").text);

                harness.Click("ConfirmSheet/Panel/CancelButton");

                Assert.IsFalse(modal.IsVisible);
                Assert.IsTrue(PlayerPrefs.HasKey(AppConst.KEY_CACHE), "still signed in");
            }
        }

        [Test]
        public void Settings_WhenLogoutIsConfirmed_ForgetsTheUser()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SettingsName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                PlayerPrefs.SetString("ACCESS_TOKEN", "token");
                PlayerPrefs.SetString("REFRESH_TOKEN", "refresh");
                PlayerPrefs.SetString("PASSWORD", "secret123");
                ChatState.Current = new ChatHistoryItem { id = "c1" };
                AuthFlow.BeginOtp("kandy@example.com", _ => { });
                harness.Open<SettingsUI>();

                harness.Click("Content/LogoutButton");
                harness.Click("ConfirmSheet/Panel/ConfirmButton");

                foreach (string key in SavedKeys)
                {
                    Assert.IsFalse(PlayerPrefs.HasKey(key), key + " was removed");
                }

                Assert.IsNull(ChatState.Current);
                Assert.AreEqual(string.Empty, AuthFlow.Email);
            }
        }

        [Test]
        public void Settings_WhenAnEntryIsTapped_OpensItsScreen()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SettingsName, KeyPrefix))
            {
                harness.Open<SettingsUI>();

                // No router here: each tap only logs that the screen is not available, and must not throw.
                LogAssert.Expect(LogType.Log, "[SettingsUI] Profile screen is not available.");
                harness.Click("Content/ProfileButton");
                LogAssert.Expect(LogType.Log, "[SettingsUI] SupportCenter screen is not available.");
                harness.Click("Content/SupportButton");
                LogAssert.Expect(LogType.Log, "[SettingsUI] Contact screen is not available.");
                harness.Click("Content/ContactButton");
            }
        }

        // ------------------------------------------------------------------ profile

        [Test]
        public void Profile_WhenShown_FillsTheFieldsFromTheCacheAndLocksThem()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ProfileName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);

                harness.Open<ProfileUI>();

                string card = "Content/Form/Viewport/Content/Card/";
                Assert.AreEqual("Kandy", harness.Find<UiTextField>(card + "NameField").Text);
                Assert.AreEqual("04384983483", harness.Find<UiTextField>(card + "PhoneField").Text);
                Assert.AreEqual("Nữ", harness.Find<UiSelectField>(card + "GenderField").Value);
                Assert.AreEqual("27/05/2004", harness.Find<UiSelectField>(card + "BirthdayField").Value);
                Assert.IsTrue(harness.Find<TMP_InputField>(card + "NameField/Input").readOnly, "no endpoint to save edits yet");
                Assert.IsTrue(harness.Find<TMP_InputField>(card + "PhoneField/Input").readOnly);
            }
        }

        [Test]
        public void Profile_WhenNothingIsCached_ShowsEmptyFields()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ProfileName, KeyPrefix))
            {
                harness.Open<ProfileUI>();

                Assert.AreEqual(string.Empty, harness.Find<UiTextField>("Content/Form/Viewport/Content/Card/NameField").Text);
                Assert.IsFalse(harness.Find<UiSelectField>("Content/Form/Viewport/Content/Card/GenderField").HasValue);
            }
        }

        // ------------------------------------------------------------------ change password

        [Test]
        public void ChangePassword_WhenTheInputIsNotAcceptable_ShowsTheFirstProblem()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                AccountSession.SavedPassword = "oldpass1";
                harness.Open<ChangePasswordUI>();

                harness.Click("Content/ChangeButton");
                Assert.AreEqual(ChangePasswordUI.EmptyMessage, harness.ErrorText);

                harness.Type("Content/Card/OldField", "wrongpass1");
                harness.Type("Content/Card/NewField", "newpass12");
                harness.Type("Content/Card/ConfirmField", "newpass12");
                harness.Click("Content/ChangeButton");
                Assert.AreEqual(ChangePasswordUI.WrongOldMessage, harness.ErrorText);

                harness.Type("Content/Card/OldField", "oldpass1");
                harness.Type("Content/Card/ConfirmField", "different1");
                harness.Click("Content/ChangeButton");
                Assert.AreEqual(ChangePasswordUI.MismatchMessage, harness.ErrorText);

                harness.Type("Content/Card/NewField", "oldpass1");
                harness.Type("Content/Card/ConfirmField", "oldpass1");
                harness.Click("Content/ChangeButton");
                Assert.AreEqual(ChangePasswordUI.SameMessage, harness.ErrorText);

                harness.Type("Content/Card/NewField", "short");
                harness.Type("Content/Card/ConfirmField", "short");
                harness.Click("Content/ChangeButton");
                Assert.AreEqual(ChangePasswordUI.WeakMessage, harness.ErrorText);

                Assert.IsEmpty(api.PasswordOtpRequests, "no code is requested for a bad form");
            }
        }

        [Test]
        public void ChangePassword_WhenNoAccountIsCached_AsksToSignInAgain()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                harness.Open<ChangePasswordUI>();
                FillPasswordForm(harness, "oldpass1", "newpass12");

                harness.Click("Content/ChangeButton");

                Assert.AreEqual(ChangePasswordUI.NoAccountMessage, harness.ErrorText);
            }
        }

        [Test]
        public void ChangePassword_WhenACodeWasSentRecently_ShowsHowLongToWait()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                api.Cooldown = 42;
                harness.Open<ChangePasswordUI>();
                FillPasswordForm(harness, "oldpass1", "newpass12");

                harness.Click("Content/ChangeButton");

                Assert.AreEqual(ChangePasswordUI.WaitMessage(42), harness.ErrorText);
                Assert.IsEmpty(api.PasswordOtpRequests);
            }
        }

        [Test]
        public void ChangePassword_WhenTheFormIsValid_SendsACodeAndHandsOverToTheOtpScreen()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                harness.Open<ChangePasswordUI>();
                FillPasswordForm(harness, "oldpass1", "newpass12");

                harness.Click("Content/ChangeButton");

                CollectionAssert.AreEqual(new[] { "kandy@example.com" }, api.PasswordOtpRequests);
                Assert.AreEqual("kandy@example.com", AuthFlow.Email);
                Assert.IsNotNull(AuthFlow.OnOtpConfirmed, "the OTP screen knows what to do with the code");
                Assert.IsEmpty(api.PasswordChanges, "nothing changes before the code is entered");
            }
        }

        [Test]
        public void ChangePassword_WhenTheCodeComesBack_ChangesThePasswordAndShowsTheSuccessCard()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                var page = harness.Open<ChangePasswordUI>();
                FillPasswordForm(harness, "oldpass1", "newpass12");
                harness.Click("Content/ChangeButton");

                AuthFlow.ConfirmOtp("1234");
                page.Show(); // the router shows the page again when the OTP screen goes back

                CollectionAssert.AreEqual(new[] { "kandy@example.com|oldpass1|newpass12|1234" }, api.PasswordChanges);
                Assert.IsTrue(harness.Overlay.IsVisible);
                Assert.AreEqual(ChangePasswordUI.SuccessTitle, harness.Find<TMP_Text>("StatusOverlay/Card/Title").text);
                Assert.AreEqual("newpass12", AccountSession.SavedPassword, "the next change checks against the new password");
                Assert.AreEqual(string.Empty, AuthFlow.Email, "the flow is finished");
            }
        }

        [Test]
        public void ChangePassword_WhenTheBackendRejects_ShowsTheErrorAndAllowsRetrying()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                api.ChangeError = "boom";
                var page = harness.Open<ChangePasswordUI>();
                FillPasswordForm(harness, "oldpass1", "newpass12");
                harness.Click("Content/ChangeButton");
                AuthFlow.ConfirmOtp("1234");
                LogAssert.Expect(LogType.Warning, "[ChangePasswordUI] Could not change the password: boom");

                page.Show();

                Assert.AreEqual(ChangePasswordUI.FailedMessage, harness.ErrorText);
                Assert.IsTrue(harness.Find<Button>("Content/ChangeButton").interactable);
                Assert.IsFalse(harness.Overlay.IsVisible);
            }
        }

        [Test]
        public void ChangePassword_WhenShownAgainFromScratch_ClearsTheFields()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangePasswordName, KeyPrefix))
            {
                var page = harness.Open<ChangePasswordUI>();
                FillPasswordForm(harness, "oldpass1", "newpass12");

                page.Show();

                Assert.IsTrue(harness.Find<UiTextField>("Content/Card/OldField").IsEmpty);
                Assert.IsTrue(harness.Find<UiTextField>("Content/Card/NewField").IsEmpty);
            }
        }

        // ------------------------------------------------------------------ change email

        [Test]
        public void ChangeEmail_WhenTheAddressIsNotAcceptable_ShowsTheMatchingMessage()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangeEmailName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                harness.Open<ChangeEmailUI>();

                harness.Type("Content/Card/EmailField", "not-an-email");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(ChangeEmailUI.InvalidEmailMessage, harness.ErrorText);

                harness.Type("Content/Card/EmailField", "KANDY@example.com");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(ChangeEmailUI.SameEmailMessage, harness.ErrorText);

                Assert.IsEmpty(api.EmailOtpRequests);
            }
        }

        [Test]
        public void ChangeEmail_WhenTheCodeComesBack_UpdatesTheCachedEmailAndShowsTheSuccessCard()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ChangeEmailName, KeyPrefix))
            {
                PlayerPrefs.SetString(AppConst.KEY_CACHE, ProfileJson);
                var page = harness.Open<ChangeEmailUI>();
                harness.Type("Content/Card/EmailField", " new@example.com ");

                harness.Click("Content/ContinueButton");

                CollectionAssert.AreEqual(new[] { "new@example.com" }, api.EmailOtpRequests);
                Assert.AreEqual("new@example.com", AuthFlow.Email);

                AuthFlow.ConfirmOtp("4321");
                page.Show();

                CollectionAssert.AreEqual(new[] { "new@example.com|4321" }, api.EmailChanges);
                Assert.IsTrue(harness.Overlay.IsVisible);
                Assert.AreEqual(ChangeEmailUI.SuccessTitle, harness.Find<TMP_Text>("StatusOverlay/Card/Title").text);
                Assert.AreEqual("new@example.com", UserProfile.Load().Email);
                Assert.AreEqual("Kandy", UserProfile.Load().Name, "the rest of the profile is untouched");
            }
        }

        // ------------------------------------------------------------------ support center

        [Test]
        public void SupportCenter_WhenShown_ListsEveryQuestionFolded()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SupportCenterName, KeyPrefix))
            {
                var page = harness.Open<SupportCenterUI>();

                Assert.AreEqual(SupportContent.Entries.Length, page.Items.Count);
                Assert.AreEqual(0, harness.Find<UiChipRow>("Content/CategoryChips").Selected, "All is chosen");
                Assert.AreEqual(SupportContent.Entries[0].Question, page.Items[0].Question);
                foreach (UiAccordionItem item in page.Items)
                {
                    Assert.IsFalse(item.IsOpen);
                }
            }
        }

        [Test]
        public void SupportCenter_WhenACategoryIsChosen_ShowsOnlyItsQuestions()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SupportCenterName, KeyPrefix))
            {
                var page = harness.Open<SupportCenterUI>();
                var chips = harness.Find<UiChipRow>("Content/CategoryChips");

                chips.Select(1, true);
                Assert.AreEqual(4, page.Items.Count, "Account");

                chips.Select(2, true);
                Assert.AreEqual(1, page.Items.Count, "Billing");

                chips.Select(2, true);
                Assert.AreEqual(0, chips.Selected - 2, "the chosen chip stays chosen: one category is always active");
                Assert.AreEqual(1, page.Items.Count);

                chips.Select(0, true);
                Assert.AreEqual(SupportContent.Entries.Length, page.Items.Count, "back to All");
            }
        }

        [Test]
        public void SupportCenter_WhenAQuestionIsTapped_UnfoldsItsAnswer()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.SupportCenterName, KeyPrefix))
            {
                var page = harness.Open<SupportCenterUI>();
                UiAccordionItem second = page.Items[1];
                PageHarness.Wake(second.gameObject);
                Transform answer = second.transform.Find("Answer");
                Assert.IsFalse(answer.gameObject.activeSelf);

                second.transform.Find("Header").GetComponent<Button>().onClick.Invoke();

                Assert.IsTrue(second.IsOpen);
                Assert.IsTrue(answer.gameObject.activeSelf);
                Assert.AreEqual(SupportContent.Entries[1].Answer, answer.GetComponent<TMP_Text>().text);
                Assert.AreEqual(90f, second.transform.Find("Header/Chevron").localEulerAngles.z, 0.01f, "the chevron points up when open");

                second.transform.Find("Header").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(second.IsOpen);
                Assert.IsFalse(answer.gameObject.activeSelf);
            }
        }

        // ------------------------------------------------------------------ contact

        [Test]
        public void Contact_WhenShown_ShowsTheContactDetails()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ContactName, KeyPrefix))
            {
                harness.Open<ContactUI>();

                Assert.AreEqual(ContactInfo.Address, harness.Find<TMP_Text>("Content/AddressValue").text);
                Assert.AreEqual(ContactInfo.Phone, harness.Find<TMP_Text>("Content/PhoneValue").text);
                Assert.AreEqual(ContactInfo.Email, harness.Find<TMP_Text>("Content/EmailValue").text);
            }
        }

        [Test]
        public void Contact_WhenSendEmailIsTapped_OpensTheMailApp()
        {
            using (var harness = new PageHarness(UiCanvasSettingsBuilder.ContactName, KeyPrefix))
            {
                var opened = new List<string>();
                ContactInfo.OpenUrl = opened.Add;
                harness.Open<ContactUI>();

                harness.Click("Content/SendEmailButton");

                CollectionAssert.AreEqual(new[] { "mailto:" + ContactInfo.Email }, opened);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void FillPasswordForm(PageHarness harness, string oldPassword, string newPassword)
        {
            harness.Type("Content/Card/OldField", oldPassword);
            harness.Type("Content/Card/NewField", newPassword);
            harness.Type("Content/Card/ConfirmField", newPassword);
        }

        private sealed class FakeAccountApi : IAccountApi
        {
            public readonly List<string> PasswordOtpRequests = new List<string>();
            public readonly List<string> PasswordChanges = new List<string>();
            public readonly List<string> EmailOtpRequests = new List<string>();
            public readonly List<string> EmailChanges = new List<string>();
            public int Cooldown;
            public string ChangeError;

            public int OtpCooldownSeconds => Cooldown;

            public void RequestPasswordOtp(string email, Action onSuccess, Action<string> onError)
            {
                PasswordOtpRequests.Add(email);
                onSuccess();
            }

            public void ChangePassword(string email, string oldPassword, string newPassword, string otp, Action onSuccess, Action<string> onError)
            {
                if (ChangeError != null)
                {
                    onError(ChangeError);
                    return;
                }

                PasswordChanges.Add($"{email}|{oldPassword}|{newPassword}|{otp}");
                onSuccess();
            }

            public void RequestEmailOtp(string newEmail, Action onSuccess, Action<string> onError)
            {
                EmailOtpRequests.Add(newEmail);
                onSuccess();
            }

            public void ChangeEmail(string newEmail, string otp, Action onSuccess, Action<string> onError)
            {
                EmailChanges.Add(newEmail + "|" + otp);
                onSuccess();
            }
        }
    }
}
#endif
