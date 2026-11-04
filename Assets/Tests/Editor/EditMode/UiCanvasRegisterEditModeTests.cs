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
using UnityEngine;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Phase 3 of the UI Toolkit → Canvas migration: sign-up. The profile form (with its gender and birthday
    /// pickers) and the password screen are driven in a preview scene with a fake backend, so no account is created.
    /// When an asset is missing run Tools ▸ UI Canvas ▸ Run Phase 3 Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasRegisterEditModeTests
    {
        private const string KeyPrefix = "UiCanvasTest_";
        private const string Card = "Content/Form/Viewport/Content/Card/";

        private IRegisterApi originalRegisterApi;

        [SetUp]
        public void SetUp()
        {
            originalRegisterApi = AuthServices.Register;
            AuthFlow.Clear();
            NavigationManager.pageHistory.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            AuthServices.Register = originalRegisterApi;
            AuthFlow.Clear();
            NavigationManager.pageHistory.Clear();
        }

        // ------------------------------------------------------------------ logic

        [Test]
        public void BirthdayFormat_WhenFormatting_UsesTheFigmaAndBackendShapes()
        {
            var date = new DateTime(2004, 5, 27);
            Assert.AreEqual("27/05/2004", BirthdayFormat.Display(date));
            Assert.AreEqual("2004-05-27T00:00:00.000Z", BirthdayFormat.Backend(date));
        }

        [TestCase(2001, 2, 31, 28)]
        [TestCase(2004, 2, 31, 29)]
        [TestCase(2004, 4, 31, 30)]
        [TestCase(2004, 5, 31, 31)]
        public void BirthdayFormat_WhenTheMonthIsShorter_PullsTheDayBack(int year, int month, int day, int expectedDay)
        {
            DateTime result = BirthdayFormat.Clamp(year, month, day);
            Assert.AreEqual(expectedDay, result.Day);
            Assert.AreEqual(month, result.Month);
        }

        [Test]
        public void BirthdayFormat_WhenBuildingTheLists_GivesTwoDigitDaysAndNewestYearsFirst()
        {
            List<string> days = BirthdayFormat.Days();
            Assert.AreEqual(31, days.Count);
            Assert.AreEqual("01", days[0]);
            Assert.AreEqual("31", days[30]);

            List<string> months = BirthdayFormat.Months();
            Assert.AreEqual(12, months.Count);
            Assert.AreEqual("Tháng 1", months[0]);

            List<int> years = BirthdayFormat.Years(new DateTime(2026, 11, 4));
            Assert.AreEqual(2021, years[0], "newest first, MinAge years back");
            Assert.AreEqual(BirthdayFormat.YearSpan + 1, years.Count);
            Assert.Greater(years[0], years[years.Count - 1]);
        }

        [TestCase("abc12345", true)]
        [TestCase("Passw0rd", true)]
        [TestCase("abcdefgh", false)]
        [TestCase("12345678", false)]
        [TestCase("abc1234", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsStrongPassword_WhenChecking_NeedsEightCharsWithLettersAndDigits(string password, bool expected)
        {
            Assert.AreEqual(expected, AuthValidation.IsStrongPassword(password));
        }

        [Test]
        public void AuthFlow_WhenClearing_DropsTheRegistrationToo()
        {
            AuthFlow.BeginRegistration(new RegistrationDraft { Name = "A" });
            Assert.NotNull(AuthFlow.Registration);

            AuthFlow.ClearRegistration();
            Assert.IsNull(AuthFlow.Registration);

            AuthFlow.BeginRegistration(new RegistrationDraft { Name = "A" });
            AuthFlow.Clear();
            Assert.IsNull(AuthFlow.Registration);
        }

        // ------------------------------------------------------------------ prefabs

        [Test]
        public void RegisterPrefabs_WhenBuilt_HaveTheirPageIds()
        {
            Assert.AreEqual(PageID.Register, LoadPage(UiCanvasRegisterBuilder.RegisterName).Id);
            Assert.AreEqual(PageID.PasswordConfirm, LoadPage(UiCanvasRegisterBuilder.CreatePasswordName).Id);
        }

        [Test]
        public void PickerSheet_WhenBuilt_HasEveryColumnWired()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasRegisterBuilder.PickerSheetPrefab);
            Assert.NotNull(sheet, "PickerSheet.prefab — run Tools/UI Canvas/Run Phase 3 Setup");

            AssertReferencesWired(sheet.GetComponent<UiPickerSheet>(), "PickerSheet");
            UiOptionColumn[] columns = sheet.GetComponentsInChildren<UiOptionColumn>(true);
            Assert.AreEqual(3, columns.Length, "day, month and year");
            foreach (UiOptionColumn column in columns)
            {
                AssertReferencesWired(column, column.name);
            }
        }

        // ------------------------------------------------------------------ profile form

        [Test]
        public void Register_WhenAFieldIsMissing_AsksForTheFirstOneInOrder()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                AuthServices.Register = new FakeRegisterApi();
                harness.Open<RegisterUI>();

                harness.Click("Content/ContinueButton");
                Assert.AreEqual(RegisterUI.NameMessage, harness.ErrorText);

                harness.Type(Card + "NameField", "Nguyen Van A");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(RegisterUI.PhoneMessage, harness.ErrorText);

                harness.Type(Card + "PhoneField", "0901234567");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(RegisterUI.EmailMessage, harness.ErrorText);

                harness.Type(Card + "EmailField", "not-an-email");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(RegisterUI.InvalidEmailMessage, harness.ErrorText);

                harness.Type(Card + "EmailField", "a@b.com");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(RegisterUI.GenderMessage, harness.ErrorText);

                harness.Find<UiSelectField>(Card + "GenderField").SetValue("Nữ");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(RegisterUI.BirthdayMessage, harness.ErrorText);
            }
        }

        [Test]
        public void Register_WhenAGenderIsPicked_ShowsItOnTheField()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                harness.Open<RegisterUI>();
                Assert.IsFalse(harness.Sheet.IsVisible);

                harness.Click(Card + "GenderField");
                Assert.IsTrue(harness.Sheet.IsVisible, "sheet opens");
                Assert.AreEqual("Chọn giới tính", harness.Find<TMP_Text>("PickerSheet/Panel/Title").text);

                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column1").Select(2, false);
                harness.Click("PickerSheet/Panel/DoneButton");

                Assert.IsFalse(harness.Sheet.IsVisible, "sheet closes");
                Assert.AreEqual("Khác", harness.Find<UiSelectField>(Card + "GenderField").Value);
            }
        }

        [Test]
        public void Register_WhenABirthdayIsPicked_ShowsItAsDayMonthYear()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                harness.Open<RegisterUI>();

                harness.Click(Card + "BirthdayField");
                List<int> years = BirthdayFormat.Years(DateTime.Today);
                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column1").Select(26, false); // 27
                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column2").Select(4, false);  // Tháng 5
                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column3").Select(years.IndexOf(2004), false);
                harness.Click("PickerSheet/Panel/DoneButton");

                Assert.AreEqual("27/05/2004", harness.Find<UiSelectField>(Card + "BirthdayField").Value);
            }
        }

        [Test]
        public void Register_WhenTheDayDoesNotExistInTheMonth_UsesTheLastDay()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                harness.Open<RegisterUI>();

                harness.Click(Card + "BirthdayField");
                List<int> years = BirthdayFormat.Years(DateTime.Today);
                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column1").Select(30, false); // 31
                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column2").Select(1, false);  // Tháng 2
                harness.Find<UiOptionColumn>("PickerSheet/Panel/Columns/Column3").Select(years.IndexOf(2001), false);
                harness.Click("PickerSheet/Panel/DoneButton");

                Assert.AreEqual("28/02/2001", harness.Find<UiSelectField>(Card + "BirthdayField").Value);
            }
        }

        [Test]
        public void Register_WhenTheFormIsComplete_SendsTheCodeAndStartsTheOtpFlow()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                var api = new FakeRegisterApi();
                AuthServices.Register = api;
                harness.Open<RegisterUI>();

                FillForm(harness);
                harness.Click("Content/ContinueButton");

                CollectionAssert.AreEqual(new[] { "a@b.com" }, api.OtpRequests);
                RegistrationDraft draft = AuthFlow.Registration;
                Assert.NotNull(draft);
                Assert.AreEqual("Nguyen Van A", draft.Name);
                Assert.AreEqual("0901234567", draft.Phone);
                Assert.AreEqual("a@b.com", draft.Email);
                Assert.AreEqual("Nữ", draft.Gender);
                Assert.AreEqual(new DateTime(2000, 1, 1), draft.Birthday);
                Assert.AreEqual("a@b.com", AuthFlow.Email);
                Assert.IsNotNull(AuthFlow.OnOtpConfirmed, "the OTP screen knows where to go next");
            }
        }

        [Test]
        public void Register_WhenReopenedWithADraft_RefillsTheForm()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                AuthFlow.BeginRegistration(new RegistrationDraft
                {
                    Name = "Nguyen Van A",
                    Phone = "0901234567",
                    Email = "a@b.com",
                    Gender = "Nam",
                    Birthday = new DateTime(2004, 5, 27),
                });

                harness.Open<RegisterUI>();

                Assert.AreEqual("Nguyen Van A", harness.Find<UiTextField>(Card + "NameField").Text);
                Assert.AreEqual("a@b.com", harness.Find<UiTextField>(Card + "EmailField").Text);
                Assert.AreEqual("Nam", harness.Find<UiSelectField>(Card + "GenderField").Value);
                Assert.AreEqual("27/05/2004", harness.Find<UiSelectField>(Card + "BirthdayField").Value);
            }
        }

        [Test]
        public void Register_WhenGoingBack_DropsTheDraft()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.RegisterName, KeyPrefix))
            {
                harness.Open<RegisterUI>();
                AuthFlow.BeginRegistration(new RegistrationDraft { Name = "A" });

                harness.Click("Content/Form/Viewport/Content/BackButton");

                Assert.IsNull(AuthFlow.Registration);
            }
        }

        // ------------------------------------------------------------------ password

        [Test]
        public void CreatePassword_WhenTheInputIsNotAcceptable_ShowsTheMatchingMessage()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.CreatePasswordName, KeyPrefix))
            {
                var api = new FakeRegisterApi();
                AuthServices.Register = api;
                harness.Open<CreatePasswordUI>();

                harness.Click("Content/ContinueButton");
                Assert.AreEqual(CreatePasswordUI.MissingProfileMessage, harness.ErrorText, "no profile was entered");

                AuthFlow.BeginRegistration(new RegistrationDraft { Name = "A", Email = "a@b.com" });
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(CreatePasswordUI.EmptyMessage, harness.ErrorText);

                harness.Type("Content/Card/PasswordField", "abc");
                harness.Type("Content/Card/ConfirmField", "abc");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(CreatePasswordUI.WeakMessage, harness.ErrorText);

                harness.Type("Content/Card/PasswordField", "abc12345");
                harness.Type("Content/Card/ConfirmField", "abc12346");
                harness.Click("Content/ContinueButton");
                Assert.AreEqual(CreatePasswordUI.MismatchMessage, harness.ErrorText);
                Assert.AreEqual(0, api.Registrations.Count);
            }
        }

        [Test]
        public void CreatePassword_WhenTheBackendAccepts_CreatesTheAccountAndShowsTheSuccessCard()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.CreatePasswordName, KeyPrefix))
            {
                var api = new FakeRegisterApi();
                AuthServices.Register = api;
                AuthFlow.BeginRegistration(new RegistrationDraft { Name = "Nguyen Van A", Email = "a@b.com" });
                harness.Open<CreatePasswordUI>();

                harness.Type("Content/Card/PasswordField", "abc12345");
                harness.Type("Content/Card/ConfirmField", "abc12345");
                harness.Click("Content/ContinueButton");

                CollectionAssert.AreEqual(new[] { "Nguyen Van A|a@b.com|abc12345" }, api.Registrations);
                Assert.IsTrue(harness.Overlay.IsVisible, "success card");
                Assert.AreEqual(CreatePasswordUI.SuccessTitle, harness.Find<TMP_Text>("StatusOverlay/Card/Title").text);
                Assert.AreEqual(string.Empty, harness.ErrorText);
            }
        }

        [Test]
        public void CreatePassword_WhenTheBackendRejects_ShowsTheErrorAndAllowsRetrying()
        {
            using (var harness = new PageHarness(UiCanvasRegisterBuilder.CreatePasswordName, KeyPrefix))
            {
                AuthServices.Register = new FakeRegisterApi { Error = "409" };
                AuthFlow.BeginRegistration(new RegistrationDraft { Name = "A", Email = "a@b.com" });
                harness.Open<CreatePasswordUI>();

                harness.Type("Content/Card/PasswordField", "abc12345");
                harness.Type("Content/Card/ConfirmField", "abc12345");
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[CreatePasswordUI] Registration failed: 409");
                harness.Click("Content/ContinueButton");

                Assert.AreEqual(CreatePasswordUI.FailedMessage, harness.ErrorText);
                Assert.IsTrue(harness.Find<UnityEngine.UI.Button>("Content/ContinueButton").interactable, "can retry");
                Assert.IsFalse(harness.Overlay.IsVisible);
            }
        }

        // ------------------------------------------------------------------ router

        [Test]
        public void NavigationManager_WhenNavigatingFromAnAnchor_DropsTheHistoryAfterIt()
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

                navigation.NavigateFrom(PageID.SplashPage, PageID.Onboarding);

                Assert.IsTrue(IsActive(container, UiCanvasOnboardingBuilder.IntroName), "target is shown");
                Assert.IsFalse(IsActive(container, UiCanvasOnboardingBuilder.WelcomeName));
                Assert.AreEqual(2, NavigationManager.pageHistory.Count, "anchor plus target");
                Assert.AreEqual(PageID.Onboarding, NavigationManager.pageHistory.Peek());
                Assert.AreEqual(PageID.SplashPage, navigation.PreviousPage(), "Back goes to the anchor");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(navigationGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void FillForm(PageHarness harness)
        {
            harness.Type(Card + "NameField", " Nguyen Van A ");
            harness.Type(Card + "PhoneField", "0901234567");
            harness.Type(Card + "EmailField", " a@b.com ");
            harness.Find<UiSelectField>(Card + "GenderField").SetValue("Nữ");

            // The birthday goes through the real picker; the default selection is 01/01/2000.
            harness.Click(Card + "BirthdayField");
            harness.Click("PickerSheet/Panel/DoneButton");
        }

        private static UiPage LoadPage(string screenName)
        {
            var page = AssetDatabase.LoadAssetAtPath<UiPage>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(page, screenName + ".prefab — run Tools/UI Canvas/Run Phase 3 Setup");
            return page;
        }

        private static void AssertReferencesWired(UnityEngine.Object target, string label)
        {
            SerializedProperty property = new SerializedObject(target).GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script")
                {
                    Assert.NotNull(property.objectReferenceValue, label + "." + property.name + " is not wired");
                }

                if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                {
                    for (int i = 0; i < property.arraySize; i++)
                    {
                        SerializedProperty element = property.GetArrayElementAtIndex(i);
                        if (element.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            Assert.NotNull(element.objectReferenceValue, label + "." + property.name + "[" + i + "] is not wired");
                        }
                    }
                }
            }
        }

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

        private sealed class FakeRegisterApi : IRegisterApi
        {
            public readonly List<string> OtpRequests = new List<string>();
            public readonly List<string> Registrations = new List<string>();
            public string Error;

            public void RequestOtp(string email, Action onSuccess, Action<string> onError)
            {
                OtpRequests.Add(email);
                onSuccess();
            }

            public void Register(RegistrationDraft draft, string password, Action onSuccess, Action<string> onError)
            {
                if (Error != null)
                {
                    onError(Error);
                    return;
                }

                Registrations.Add(draft.Name + "|" + draft.Email + "|" + password);
                onSuccess();
            }
        }
    }
}
#endif
