#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Features.Chat;
using Game.Features.Home;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// Phases 4-6 of the UI Toolkit → Canvas migration: the AR page overlay, history, search and chat screens. A fake
    /// backend answers at once, so no request leaves the editor. When an asset is missing run Tools ▸ UI Canvas ▸ Run Phase 4-7 Setup.
    /// </summary>
    [Category("TestAR")]
    public sealed class UiCanvasHomeEditModeTests
    {
        private const string KeyPrefix = "UiCanvasHomeTest_";
        private const string RowsPath = "Content/List/Viewport/Content";
        private static readonly DateTime Now = new DateTime(2026, 11, 5, 12, 0, 0); // a Thursday

        private IChatApi originalApi;
        private FakeChatApi api;

        [SetUp]
        public void SetUp()
        {
            originalApi = ChatServices.Api;
            api = new FakeChatApi();
            ChatServices.Api = api;
            ChatState.Clear();
            NavigationManager.pageHistory.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ChatServices.Api = originalApi;
            ChatState.Clear();
            NavigationManager.pageHistory.Clear();
        }

        // ------------------------------------------------------------------ history filter

        [Test]
        public void HistoryFilter_WhenFormattingTheDate_UsesTheMockupShape()
        {
            Assert.AreEqual("08-03-2024 | 03:23 AM", HistoryFilter.DisplayDate("2024-03-08T03:23:00"));
            Assert.AreEqual("08-03-2024 | 03:23 PM", HistoryFilter.DisplayDate("2024-03-08T15:23:00"));
            Assert.AreEqual(string.Empty, HistoryFilter.DisplayDate(""));
            Assert.AreEqual(string.Empty, HistoryFilter.DisplayDate("not a date"));
        }

        [Test]
        public void HistoryFilter_WhenTheTitleIsBlank_UsesTheDefaultTitle()
        {
            Assert.AreEqual(HistoryFilter.UntitledChat, HistoryFilter.TitleOf(new ChatHistoryItem { header = "  " }));
            Assert.AreEqual(HistoryFilter.UntitledChat, HistoryFilter.TitleOf(null));
            Assert.AreEqual("Tòa A4", HistoryFilter.TitleOf(new ChatHistoryItem { header = " Tòa A4 " }));
        }

        [Test]
        public void HistoryFilter_WhenFilteringByRange_KeepsOnlyTheEntriesInside()
        {
            var items = new List<ChatHistoryItem>
            {
                FakeChatApi.Item("today", "A", Now.AddHours(-2)),
                FakeChatApi.Item("tuesday", "B", Now.AddDays(-2)),   // Nov 3: this week and this month
                FakeChatApi.Item("saturday", "C", Now.AddDays(-5)),  // Oct 31: last week, last month
                FakeChatApi.Item("old", "D", Now.AddDays(-20)),
            };

            CollectionAssert.AreEqual(new[] { "today" }, Ids(HistoryFilter.Apply(items, HistoryRange.Last24Hours, null, Now)));
            CollectionAssert.AreEqual(new[] { "today", "tuesday" }, Ids(HistoryFilter.Apply(items, HistoryRange.Last3Days, null, Now)));
            CollectionAssert.AreEqual(new[] { "today", "tuesday" }, Ids(HistoryFilter.Apply(items, HistoryRange.ThisWeek, null, Now)));
            CollectionAssert.AreEqual(new[] { "today", "tuesday" }, Ids(HistoryFilter.Apply(items, HistoryRange.ThisMonth, null, Now)));
            CollectionAssert.AreEqual(new[] { "today", "tuesday", "saturday", "old" }, Ids(HistoryFilter.Apply(items, null, null, Now)), "no range keeps all, newest first");
        }

        [Test]
        public void HistoryFilter_WhenSearching_IgnoresCaseAndAccents()
        {
            var items = new List<ChatHistoryItem>
            {
                FakeChatApi.Item("a4", "Tòa A4", Now),
                FakeChatApi.Item("oisp", "Thông tin về tòa OISP", Now.AddHours(-1)),
                FakeChatApi.Item("lib", "Thư viện", Now.AddHours(-2)),
            };

            CollectionAssert.AreEqual(new[] { "a4", "oisp" }, Ids(HistoryFilter.Apply(items, null, "TOA", Now)));
            CollectionAssert.AreEqual(new[] { "lib" }, Ids(HistoryFilter.Apply(items, null, "thu vien", Now)));
            CollectionAssert.AreEqual(new[] { "oisp" }, Ids(HistoryFilter.Apply(items, null, "thông tin", Now)));
            Assert.IsEmpty(HistoryFilter.Apply(items, null, "burger", Now));
        }

        [Test]
        public void HistoryFilter_WhenAnEntryHasNoDate_IsLeftOutOfTimeRanges()
        {
            var items = new List<ChatHistoryItem> { new ChatHistoryItem { id = "x", header = "No date" } };

            Assert.IsEmpty(HistoryFilter.Apply(items, HistoryRange.ThisMonth, null, Now));
            Assert.AreEqual(1, HistoryFilter.Apply(items, null, null, Now).Count);
        }

        // ------------------------------------------------------------------ response parsing

        [Test]
        public void RestChatApi_WhenParsingMessages_AcceptsAWrapperAndABareArray()
        {
            const string wrapper = "{\"success\":true,\"data\":[{\"Content\":\"hi\",\"Contact_person\":\"user\"},{\"content\":\"hello\",\"contact_person\":\"assistant\"}]}";
            const string bare = "[{\"Content\":\"hi\",\"Contact_person\":\"user\"}]";

            List<ChatMessage> fromWrapper = RestChatApi.ParseMessages(wrapper);
            Assert.AreEqual(2, fromWrapper.Count);
            Assert.IsTrue(ChatMessages.IsFromUser(fromWrapper[0]));
            Assert.IsFalse(ChatMessages.IsFromUser(fromWrapper[1]));
            Assert.AreEqual("hello", fromWrapper[1].GetContent());

            Assert.AreEqual(1, RestChatApi.ParseMessages(bare).Count);
            Assert.IsEmpty(RestChatApi.ParseMessages(string.Empty));
        }

        [Test]
        public void RestChatApi_WhenParsingTheReply_TakesTheLastAnswerNotTheEcho()
        {
            const string json = "{\"success\":true,\"data\":[{\"content\":\"question\",\"contact_person\":\"user\"},{\"content\":\"answer\",\"contact_person\":\"assistant\"}]}";

            Assert.AreEqual("answer", RestChatApi.ParseReply(json).GetContent());
            Assert.IsNull(RestChatApi.ParseReply("{\"success\":true,\"data\":[]}"));
            Assert.IsNull(RestChatApi.ParseReply(string.Empty));
        }

        [Test]
        public void RestChatApi_WhenParsingACreatedConversation_FallsBackToTheTitleWeSent()
        {
            ChatHistoryItem wrapped = RestChatApi.ParseCreated("{\"success\":true,\"data\":{\"id\":\"7\",\"header\":\"x\"}}", "fallback");
            Assert.AreEqual("7", wrapped.id);

            ChatHistoryItem bare = RestChatApi.ParseCreated("{\"id\":\"8\",\"header\":\"y\"}", "fallback");
            Assert.AreEqual("8", bare.id);

            ChatHistoryItem broken = RestChatApi.ParseCreated("<html>", "fallback");
            Assert.IsNull(broken.id);
            Assert.AreEqual("fallback", broken.header);
        }

        // ------------------------------------------------------------------ session starter

        [Test]
        public void ChatSessionStarter_WhenCreatingAConversation_MakesItCurrentAndFirstInTheList()
        {
            ChatState.Histories.Add(FakeChatApi.Item("old", "Old", Now));
            bool ready = false;

            ChatSessionStarter.StartNew(api, Now, () => ready = true, () => Assert.Fail("not unauthorized"));

            Assert.IsTrue(ready);
            Assert.AreEqual("new-1", ChatState.Current.id);
            Assert.AreEqual("new-1", ChatState.Histories[0].id);
            Assert.AreEqual(ChatSessionStarter.NewChatTitle(Now), api.Created[0]);
        }

        [Test]
        public void ChatSessionStarter_WhenTheBackendIsUnreachable_OpensALocalConversation()
        {
            api.CreateError = -1;
            bool ready = false;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[ChatSessionStarter] Could not create a conversation (-1): create failed");

            ChatSessionStarter.StartNew(api, Now, () => ready = true, () => Assert.Fail("not unauthorized"));

            Assert.IsTrue(ready);
            Assert.IsNull(ChatState.Current.id);
            Assert.AreEqual(ChatSessionStarter.NewChatTitle(Now), ChatState.Current.header);
        }

        [Test]
        public void ChatSessionStarter_WhenTheSessionExpired_ReportsUnauthorized()
        {
            api.CreateError = 401;
            bool unauthorized = false;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[ChatSessionStarter] Could not create a conversation (401): create failed");

            ChatSessionStarter.StartNew(api, Now, () => Assert.Fail("not ready"), () => unauthorized = true);

            Assert.IsTrue(unauthorized);
            Assert.IsNull(ChatState.Current);
        }

        [Test]
        public void ChatSessionStarter_WhenOpeningAnExistingConversation_LoadsItsMessages()
        {
            var item = FakeChatApi.Item("c1", "Tòa A4", Now);
            api.Messages["c1"] = new List<ChatMessage> { ChatMessages.FromUser("hi") };
            bool ready = false;

            ChatSessionStarter.OpenExisting(api, item, () => ready = true);

            Assert.IsTrue(ready);
            Assert.AreSame(item, ChatState.Current);
            Assert.AreEqual(1, item.messages.Count);
        }

        // ------------------------------------------------------------------ prefabs

        [TestCase(UiCanvasHomeBuilder.ArHudName, PageID.ARPage)]
        [TestCase(UiCanvasHomeBuilder.HistoryName, PageID.HistoryPage)]
        [TestCase(UiCanvasHomeBuilder.ChatName, PageID.Chatbox)]
        [TestCase(UiCanvasHomeBuilder.SearchName, PageID.SearchPage)]
        public void HomePrefabs_WhenBuilt_HaveTheirPageIdAndEveryReferenceWired(string screen, PageID expected)
        {
            UiPage page = PrefabAsserts.LoadPage(screen);

            Assert.AreEqual(expected, page.Id);
            PrefabAsserts.AssertAllReferencesWired(page.gameObject, screen);
        }

        [Test]
        public void SharedPrefabs_WhenBuilt_HaveEveryReferenceWired()
        {
            foreach (string path in new[] { UiCanvasListKit.BottomNavPrefab, UiCanvasListKit.ConfirmSheetPrefab, UiCanvasListKit.BusyOverlayPrefab })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, path + " — run Tools/UI Canvas/Run Phase 4-7 Setup");
                PrefabAsserts.AssertAllReferencesWired(prefab, path);
            }
        }

        [Test]
        public void ArHudPrefab_WhenBuilt_IsTransparentSoTheCameraShowsThrough()
        {
            UiPage page = PrefabAsserts.LoadPage(UiCanvasHomeBuilder.ArHudName);

            Assert.IsNull(page.transform.Find("Background"), "no opaque layer behind the HUD");
        }

        // ------------------------------------------------------------------ AR page

        [Test]
        public void ArHud_WhenBuildingThePrompt_ShortensTheBuildingName()
        {
            Assert.AreEqual(ArHudUI.AskPrompt, ArHudUI.PromptFor(string.Empty));
            Assert.AreEqual("Đến B4", ArHudUI.PromptFor("Tòa B4"));
            Assert.AreEqual("Đến A4", ArHudUI.PromptFor("tòa A4"));
            Assert.AreEqual("Đến Thư viện", ArHudUI.PromptFor("Thư viện"));
        }

        [Test]
        public void ArHud_WhenThereIsNoRoute_HidesClearAndInvitesAQuestion()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            {
                var page = harness.Open<ArHudUI>();

                Assert.IsFalse(page.HasRoute);
                Assert.IsFalse(harness.Find<Button>("Content/ClearRouteButton").gameObject.activeSelf);
                Assert.AreEqual(ArHudUI.AskPrompt, harness.Find<TMP_Text>("Content/PromptBubble/Label").text);
                Assert.AreEqual(ArHudUI.SearchPlaceholder, harness.Find<TMP_Text>("Content/SearchBar/Label").text);
            }
        }

        [Test]
        public void ArHud_WhenADestinationIsSet_ShowsItAndOffersToClearTheRoute()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            {
                var page = harness.Open<ArHudUI>();
                int cleared = 0;
                page.RouteCleared += () => cleared++;

                page.SetDestination("Tòa A4");

                Assert.IsTrue(page.HasRoute);
                Assert.IsTrue(harness.Find<Button>("Content/ClearRouteButton").gameObject.activeSelf);
                Assert.AreEqual("Tòa A4", harness.Find<TMP_Text>("Content/SearchBar/Label").text);
                Assert.AreEqual("Đến A4", harness.Find<TMP_Text>("Content/PromptBubble/Label").text);

                harness.Click("Content/ClearRouteButton");

                Assert.AreEqual(1, cleared);
                Assert.IsFalse(page.HasRoute);
                Assert.AreEqual(ArHudUI.AskPrompt, harness.Find<TMP_Text>("Content/PromptBubble/Label").text);
            }
        }

        [Test]
        public void ArHud_WhenTheSearchBarIsTapped_StartsANewConversation()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ArHudName, KeyPrefix))
            {
                harness.Open<ArHudUI>();

                harness.Click("Content/SearchBar");

                Assert.AreEqual(1, api.Created.Count);
                Assert.AreEqual("new-1", ChatState.Current.id);
                Assert.IsFalse(harness.Find<UiBusyOverlay>("BusyOverlay").IsVisible, "the spinner is gone once the chat is ready");
            }
        }

        // ------------------------------------------------------------------ history

        [Test]
        public void History_WhenLoaded_ListsTheConversationsNewestFirst()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = ThreeConversations();

                harness.Open<HistoryUI>();

                UiHistoryRow[] rows = harness.Root.GetComponentsInChildren<UiHistoryRow>(false);
                CollectionAssert.AreEqual(new[] { "Mới nhất", "Hôm kia", "Cũ" }, Array.ConvertAll(rows, row => row.Title));
                Assert.AreEqual("05-11-2026 | 10:00 AM", rows[0].transform.Find("Content/Text/Time").GetComponent<TMP_Text>().text);
                Assert.IsFalse(harness.Find<TMP_Text>("Content/EmptyLabel").gameObject.activeSelf);
                Assert.IsFalse(harness.Find<UiBusyOverlay>("BusyOverlay").IsVisible);
            }
        }

        [Test]
        public void History_WhenThereIsNothing_ShowsTheEmptyMessage()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                harness.Open<HistoryUI>();

                Assert.IsEmpty(harness.Root.GetComponentsInChildren<UiHistoryRow>(false));
                TMP_Text empty = harness.Find<TMP_Text>("Content/EmptyLabel");
                Assert.IsTrue(empty.gameObject.activeSelf);
                Assert.AreEqual(HistoryUI.EmptyMessage, empty.text);
            }
        }

        [Test]
        public void History_WhenAChipIsChosen_ShowsOnlyThatRange()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = new List<ChatHistoryItem>
                {
                    FakeChatApi.Item("a", "Vừa xong", DateTime.Now.AddMinutes(-5)),
                    FakeChatApi.Item("b", "Hai ngày", DateTime.Now.AddDays(-2)),
                    FakeChatApi.Item("c", "Cũ", DateTime.Now.AddDays(-90)),
                };
                harness.Open<HistoryUI>();
                var chips = harness.Find<UiChipRow>("Content/RangeChips");
                Assert.AreEqual(4, chips.Count);
                Assert.AreEqual(-1, chips.Selected, "no filter at first");
                Assert.AreEqual(3, ActiveRows(harness));

                chips.Select(0, true);
                Assert.AreEqual(1, ActiveRows(harness), "24 giờ qua");

                chips.Select(1, true);
                Assert.AreEqual(2, ActiveRows(harness), "3 ngày qua");

                chips.Select(1, true);
                Assert.AreEqual(-1, chips.Selected, "tapping the chosen chip clears it");
                Assert.AreEqual(3, ActiveRows(harness));
            }
        }

        [Test]
        public void History_WhenARowIsDeleted_AsksTheBackendAndDropsIt()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = ThreeConversations();
                harness.Open<HistoryUI>();
                UiHistoryRow first = harness.Root.GetComponentsInChildren<UiHistoryRow>(false)[0];
                PageHarness.Wake(first.gameObject);

                first.transform.Find("DeleteButton").GetComponent<Button>().onClick.Invoke();

                CollectionAssert.AreEqual(new[] { "new" }, api.Deleted);
                Assert.AreEqual(2, ChatState.Histories.Count);
                Assert.AreEqual(2, ActiveRows(harness));
            }
        }

        [Test]
        public void History_WhenDeleteAllIsConfirmed_DeletesEveryConversation()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = ThreeConversations();
                harness.Open<HistoryUI>();

                harness.Click("Content/ClearAllButton");
                var modal = harness.Find<UiModal>("ConfirmSheet");
                Assert.IsTrue(modal.IsVisible, "the confirmation comes first");
                Assert.AreEqual(HistoryUI.DeleteAllTitle, harness.Find<TMP_Text>("ConfirmSheet/Panel/Title").text);
                Assert.AreEqual(HistoryUI.DeleteAllMessage, harness.Find<TMP_Text>("ConfirmSheet/Panel/Message").text);
                Assert.IsEmpty(api.Deleted, "nothing is deleted before the user confirms");

                harness.Click("ConfirmSheet/Panel/ConfirmButton");

                CollectionAssert.AreEquivalent(new[] { "new", "mid", "old" }, api.Deleted);
                Assert.IsEmpty(ChatState.Histories);
                Assert.AreEqual(0, ActiveRows(harness));
                Assert.IsTrue(harness.Find<TMP_Text>("Content/EmptyLabel").gameObject.activeSelf);
                Assert.IsFalse(modal.IsVisible);
            }
        }

        [Test]
        public void History_WhenDeleteAllIsCancelled_KeepsEverything()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = ThreeConversations();
                harness.Open<HistoryUI>();

                harness.Click("Content/ClearAllButton");
                harness.Click("ConfirmSheet/Panel/CancelButton");

                Assert.IsEmpty(api.Deleted);
                Assert.AreEqual(3, ActiveRows(harness));
                Assert.IsFalse(harness.Find<UiModal>("ConfirmSheet").IsVisible);
            }
        }

        [Test]
        public void History_WhenARowIsTapped_LoadsItsMessagesAndMakesItCurrent()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = ThreeConversations();
                api.Messages["new"] = new List<ChatMessage> { ChatMessages.FromUser("hi") };
                harness.Open<HistoryUI>();
                UiHistoryRow first = harness.Root.GetComponentsInChildren<UiHistoryRow>(false)[0];
                PageHarness.Wake(first.gameObject);

                first.transform.Find("Content").GetComponent<Button>().onClick.Invoke();

                Assert.AreEqual("new", ChatState.Current.id);
                Assert.AreEqual(1, ChatState.Current.messages.Count);
            }
        }

        [Test]
        public void History_WhenARowShowsItsDeleteButton_ATapOnlyClosesIt()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                api.Histories = ThreeConversations();
                harness.Open<HistoryUI>();
                UiHistoryRow first = harness.Root.GetComponentsInChildren<UiHistoryRow>(false)[0];
                PageHarness.Wake(first.gameObject);
                var swipe = first.GetComponentInChildren<UiSwipeRow>();
                swipe.Open();
                Assert.IsTrue(swipe.IsOpen);

                first.transform.Find("Content").GetComponent<Button>().onClick.Invoke();

                Assert.IsFalse(swipe.IsOpen);
                Assert.IsNull(ChatState.Current, "the conversation did not open");
            }
        }

        [Test]
        public void History_WhenTheTabBarIsShown_HighlightsHistory()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.HistoryName, KeyPrefix))
            {
                harness.Open<HistoryUI>();

                Assert.AreEqual(UiBottomNav.HistoryTab, harness.Find<UiBottomNav>("Content/BottomNav").Selected);
                Assert.AreEqual(UiTheme.NavActive, harness.Find<TMP_Text>("Content/BottomNav/Tab2/Label").color);
                Assert.AreEqual(Color.white, harness.Find<TMP_Text>("Content/BottomNav/Tab1/Label").color);
                Assert.AreEqual(UiTheme.NavActive, harness.Find<Image>("Content/BottomNav/Tab2/Icon").color);
            }
        }

        [Test]
        public void BottomNav_WhenMappingPages_FollowsTheTabOrder()
        {
            Assert.AreEqual(UiBottomNav.ArTab, UiBottomNav.IndexOf(PageID.ARPage));
            Assert.AreEqual(UiBottomNav.HistoryTab, UiBottomNav.IndexOf(PageID.HistoryPage));
            Assert.AreEqual(UiBottomNav.SettingsTab, UiBottomNav.IndexOf(PageID.MainSettings));
            Assert.AreEqual(-1, UiBottomNav.IndexOf(PageID.Login));
        }

        // ------------------------------------------------------------------ search

        [Test]
        public void Search_WhenNothingIsTyped_ListsNothingAndHidesTheEmptyState()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.SearchName, KeyPrefix))
            {
                ChatState.Histories = ThreeConversations();

                var page = harness.Open<SearchUI>();

                Assert.AreEqual(0, page.ResultCount);
                Assert.IsFalse(page.ShowsEmptyState);
            }
        }

        [Test]
        public void Search_WhenTheQueryMatches_ListsTheConversations()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.SearchName, KeyPrefix))
            {
                ChatState.Histories = ThreeConversations();
                var page = harness.Open<SearchUI>();

                harness.Find<TMP_InputField>("Content/QueryBox/Input").text = "hom";

                Assert.AreEqual(1, page.ResultCount);
                Assert.AreEqual("Hôm kia", harness.Root.GetComponentsInChildren<UiHistoryRow>(false)[0].Title);
                Assert.IsFalse(page.ShowsEmptyState);
            }
        }

        [Test]
        public void Search_WhenNothingMatches_ShowsKhongTimThay()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.SearchName, KeyPrefix))
            {
                ChatState.Histories = ThreeConversations();
                var page = harness.Open<SearchUI>();

                harness.Find<TMP_InputField>("Content/QueryBox/Input").text = "burger";

                Assert.AreEqual(0, page.ResultCount);
                Assert.IsTrue(page.ShowsEmptyState);
                Assert.AreEqual("Không tìm thấy", harness.Find<TMP_Text>("Content/EmptyState/Title").text);

                harness.Find<TMP_InputField>("Content/QueryBox/Input").text = string.Empty;
                Assert.IsFalse(page.ShowsEmptyState, "clearing the query clears the message");
            }
        }

        // ------------------------------------------------------------------ chat

        [Test]
        public void Chat_WhenOpened_ShowsTheTitleAndThePastMessages()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ChatName, KeyPrefix))
            {
                ChatState.Current = new ChatHistoryItem
                {
                    id = "c1",
                    header = "Tòa A4",
                    messages = new List<ChatMessage>
                    {
                        ChatMessages.FromUser("Lịch sử hình thành của tòa B4 đi."),
                        new ChatMessage { contact_person = "assistant", content = "Đây là câu trả lời" },
                        new ChatMessage { contact_person = "assistant", content = string.Empty },
                    },
                };

                var page = harness.Open<ChatUI>();

                Assert.AreEqual("Tòa A4", harness.Find<TMP_Text>("Content/Title").text);
                Assert.AreEqual(2, page.Bubbles.Count, "an empty message is not drawn");
                Assert.AreEqual("Lịch sử hình thành của tòa B4 đi.", page.Bubbles[0].Text);
                Assert.AreEqual("User1", page.Bubbles[0].name);
                Assert.AreEqual("Bot2", page.Bubbles[1].name);
            }
        }

        [Test]
        public void Chat_WhenThereIsNoConversation_UsesTheDefaultTitle()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ChatName, KeyPrefix))
            {
                harness.Open<ChatUI>();

                Assert.AreEqual(ChatUI.DefaultTitle, harness.Find<TMP_Text>("Content/Title").text);
            }
        }

        [Test]
        public void Chat_WhenAQuestionIsSent_ShowsItAndTheAnswer()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ChatName, KeyPrefix))
            {
                ChatState.Current = new ChatHistoryItem { id = "c1", header = "Chat" };
                var page = harness.Open<ChatUI>();

                harness.Find<TMP_InputField>("Content/InputBox/Input").text = "  Tòa A4 ở đâu?  ";
                harness.Click("Content/SendButton");

                CollectionAssert.AreEqual(new[] { "Tòa A4 ở đâu?" }, api.Sent);
                Assert.AreEqual(2, page.Bubbles.Count);
                Assert.AreEqual("Tòa A4 ở đâu?", page.Bubbles[0].Text);
                Assert.AreEqual(api.Reply, page.Bubbles[1].Text);
                Assert.AreEqual(string.Empty, harness.Find<TMP_InputField>("Content/InputBox/Input").text, "the box is cleared");
                Assert.AreEqual(2, ChatState.Current.messages.Count, "the exchange is kept for when the user comes back");
                Assert.IsTrue(harness.Find<Button>("Content/SendButton").interactable);
            }
        }

        [Test]
        public void Chat_WhenTheInputIsBlank_SendsNothing()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ChatName, KeyPrefix))
            {
                var page = harness.Open<ChatUI>();

                harness.Find<TMP_InputField>("Content/InputBox/Input").text = "   ";
                harness.Click("Content/SendButton");

                Assert.IsEmpty(api.Sent);
                Assert.AreEqual(0, page.Bubbles.Count);
            }
        }

        [Test]
        public void Chat_WhenTheBackendFails_ApologisesAndAllowsRetrying()
        {
            using (var harness = new PageHarness(UiCanvasHomeBuilder.ChatName, KeyPrefix))
            {
                api.SendError = 500;
                var page = harness.Open<ChatUI>();
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[ChatUI] Could not send the question (500): send failed");

                harness.Find<TMP_InputField>("Content/InputBox/Input").text = "hello";
                harness.Click("Content/SendButton");

                Assert.AreEqual(2, page.Bubbles.Count);
                Assert.AreEqual(ChatUI.FailedReply, page.Bubbles[1].Text);
                Assert.IsTrue(harness.Find<Button>("Content/SendButton").interactable, "can try again");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static List<ChatHistoryItem> ThreeConversations()
        {
            return new List<ChatHistoryItem>
            {
                FakeChatApi.Item("old", "Cũ", new DateTime(2026, 9, 1, 8, 0, 0)),
                FakeChatApi.Item("new", "Mới nhất", new DateTime(2026, 11, 5, 10, 0, 0)),
                FakeChatApi.Item("mid", "Hôm kia", new DateTime(2026, 11, 3, 9, 0, 0)),
            };
        }

        private static int ActiveRows(PageHarness harness)
        {
            return harness.Root.GetComponentsInChildren<UiHistoryRow>(false).Length;
        }

        private static string[] Ids(List<ChatHistoryItem> items)
        {
            return items.ConvertAll(item => item.id).ToArray();
        }
    }
}
#endif
