using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// The three tabs shared by the top-level pages: AR guidance, history and settings. The pages put one on
    /// their own bottom edge; the router treats the AR page as the root, so a tab never grows the back stack.
    /// </summary>
    public class UiBottomNav : MonoBehaviour
    {
        public const int ArTab = 0;
        public const int HistoryTab = 1;
        public const int SettingsTab = 2;

        public static readonly PageID[] Routes = { PageID.ARPage, PageID.HistoryPage, PageID.MainSettings };

        [SerializeField] private Button[] tabs;
        [SerializeField] private Image[] icons;
        [SerializeField] private TMP_Text[] labels;

        public event Action<int> TabClicked;

        public int Selected { get; private set; } = -1;

        public static int IndexOf(PageID page)
        {
            return Array.IndexOf(Routes, page);
        }

        /// <summary>Switches to a tab's page, keeping the AR page as the bottom of the history.</summary>
        public static void Navigate(NavigationManager navigation, int tab)
        {
            if (navigation == null || tab < 0 || tab >= Routes.Length)
            {
                return;
            }

            PageID target = Routes[tab];
            if (!navigation.HasRoute(target))
            {
                Debug.Log($"[UiBottomNav] {target} screen is not available.");
                return;
            }

            if (tab == ArTab)
            {
                if (!navigation.ReturnTo(PageID.ARPage, PageTransition.Fade))
                {
                    navigation.Navigate(PageID.ARPage, true, PageTransition.Fade);
                }

                return;
            }

            navigation.NavigateFrom(PageID.ARPage, target);
        }

        private void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].onClick.AddListener(() => TabClicked?.Invoke(index));
            }
        }

        public void Select(int index)
        {
            Selected = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                Color color = i == index ? UiTheme.NavActive : UiTheme.TextPrimary;
                icons[i].color = color;
                labels[i].color = color;
            }
        }
    }
}
