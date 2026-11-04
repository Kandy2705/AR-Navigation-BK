using System.Collections.Generic;
using Game.Foundation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Settings
{
    /// <summary>"Trung tâm hỗ trợ": FAQ accordion filtered by category chips.</summary>
    public class SupportCenterUI : UiPage
    {
        [SerializeField] private Button backButton;
        [SerializeField] private UiChipRow categoryChips;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UiAccordionItem itemTemplate;

        private readonly List<UiAccordionItem> items = new List<UiAccordionItem>();

        public IReadOnlyList<UiAccordionItem> Items => items;

        private void Awake()
        {
            itemTemplate.gameObject.SetActive(false);
            backButton.onClick.AddListener(GoBack);
            categoryChips.Changed += _ => Render();
        }

        protected override void OnShow()
        {
            categoryChips.SetOptions(SupportContent.Categories, 0);
            Render();
        }

        private void Render()
        {
            foreach (UiAccordionItem item in items)
            {
                if (item != null)
                {
                    UiGameObjects.Discard(item.gameObject);
                }
            }

            items.Clear();
            foreach (SupportContent.Entry entry in SupportContent.For(Mathf.Max(categoryChips.Selected, 0)))
            {
                UiAccordionItem item = Instantiate(itemTemplate, listContent);
                item.name = "Faq" + (items.Count + 1);
                item.gameObject.SetActive(true);
                item.Bind(entry.Question, entry.Answer);
                items.Add(item);
            }
        }
    }
}
