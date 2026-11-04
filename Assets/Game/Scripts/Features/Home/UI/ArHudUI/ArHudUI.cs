using System;
using Game.Features.Chat;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Home
{
    /// <summary>
    /// The overlay on the camera view: "Lịch sử/Cài đặt" at the top, "Xóa chỉ đường" while a route is active,
    /// and the assistant with its search bar at the bottom. The page background is transparent so the AR scene
    /// shows through; the navigation scene drives the route through <see cref="SetDestination"/>.
    /// </summary>
    public class ArHudUI : UiPage
    {
        public const string AskPrompt = "Hãy hỏi tôi";
        public const string SearchPlaceholder = "Nhập địa chỉ nội dung về tôi";
        private const string BuildingPrefix = "Tòa ";

        [SerializeField] private Button menuButton;
        [SerializeField] private Button clearRouteButton;
        [SerializeField] private Button searchBar;
        [SerializeField] private TMP_Text searchLabel;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private UiBusyOverlay busy;

        private bool opening;

        public string Destination { get; private set; } = string.Empty;

        public bool HasRoute => !string.IsNullOrEmpty(Destination);

        /// <summary>Raised when the user taps "Xóa chỉ đường"; the navigation scene stops guiding.</summary>
        public event Action RouteCleared;

        private void Awake()
        {
            menuButton.onClick.AddListener(OpenHistory);
            clearRouteButton.onClick.AddListener(ClearRoute);
            searchBar.onClick.AddListener(OpenChat);
            Render();
        }

        protected override void OnShow()
        {
            opening = false;
            busy.Hide();
            Render();
        }

        /// <summary>Shows the destination in the bar and the prompt ("Đến B4"), and offers "Xóa chỉ đường".</summary>
        public void SetDestination(string name)
        {
            Destination = name?.Trim() ?? string.Empty;
            Render();
        }

        public void ClearRoute()
        {
            Destination = string.Empty;
            Render();
            RouteCleared?.Invoke();
        }

        public static string PromptFor(string destination)
        {
            if (string.IsNullOrEmpty(destination))
            {
                return AskPrompt;
            }

            string shortName = destination.StartsWith(BuildingPrefix, StringComparison.OrdinalIgnoreCase)
                ? destination.Substring(BuildingPrefix.Length)
                : destination;
            return "Đến " + shortName.Trim();
        }

        private void Render()
        {
            clearRouteButton.gameObject.SetActive(HasRoute);
            promptLabel.text = PromptFor(Destination);
            searchLabel.text = HasRoute ? Destination : SearchPlaceholder;
            searchLabel.color = HasRoute ? UiTheme.TextPrimary : UiTheme.Placeholder;
        }

        private void OpenHistory()
        {
            if (Navigation != null && Navigation.HasRoute(PageID.HistoryPage))
            {
                Navigation.NavigateFrom(PageID.ARPage, PageID.HistoryPage);
            }
            else
            {
                Debug.Log("[ArHudUI] The history screen is not available.");
            }
        }

        private void OpenChat()
        {
            if (opening)
            {
                return;
            }

            opening = true;
            busy.Show();
            ChatSessionStarter.StartNew(
                ChatServices.Api,
                DateTime.Now,
                () =>
                {
                    busy.Hide();
                    opening = false;
                    OpenPage(PageID.Chatbox);
                },
                () =>
                {
                    busy.Hide();
                    opening = false;
                    OpenPage(PageID.Login);
                });
        }
    }
}
