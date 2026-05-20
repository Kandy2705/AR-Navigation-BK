using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using System.Globalization;

public class HistoryManager
{
    private VisualElement _root;

    // Callback dạng cũ — chỉ truyền chatTitle (giữ để không phá Routing.cs / EmailChangeController)
    private Action<string> _onOpenChat;

    // Callback dạng mới — truyền cả ChatHistoryItem (dùng khi populate từ API)
    private Action<ChatHistoryItem> _onOpenChatItem;

    // Callback xoá 1 session — đi kèm với mode API.
    private Action<ChatHistoryItem> _onDeleteItem;

    // Các biến lưu tham chiếu UI
    private VisualElement _deleteModal;
    private VisualElement _itemPendingDelete;
    private ChatHistoryItem _itemPendingDeleteData;
    private VisualElement _searchOverlay;
    private TextField _inputSearch;
    private bool _hasFonts = false;
    private StyleFontDefinition _cachedTitleFont;
    private StyleFontDefinition _cachedDateFont;

    private VisualElement _listContainer;     // parent của các history-card (ScrollView.body-list)
    private ScrollView _searchResultsContainer;

    // ------------------------------------------------------------------
    // CONSTRUCTORS
    // ------------------------------------------------------------------

    /// <summary>
    /// Constructor cũ — giữ tương thích với code đang dùng (Routing.cs, EmailChangeController...).
    /// Vẫn dùng card mock có sẵn trong UXML.
    /// </summary>
    public HistoryManager(VisualElement rootElement, Action<string> onOpenChat)
    {
        Debug.Log("HistoryManager initialized (legacy mock mode).");
        _root = rootElement;
        _onOpenChat = onOpenChat;

        InitCommon();
        SetupListLogic();
    }

    /// <summary>
    /// Constructor mới — render danh sách động từ API.
    /// </summary>
    public HistoryManager(VisualElement rootElement, List<ChatHistoryItem> items,
        Action<ChatHistoryItem> onOpenChatItem,
        Action<ChatHistoryItem> onDeleteItem = null)
    {
        Debug.Log($"HistoryManager initialized (API mode) — {items?.Count ?? 0} items");
        _root = rootElement;
        _onOpenChatItem = onOpenChatItem;
        _onDeleteItem = onDeleteItem;

        InitCommon();
        PopulateFromData(items);
    }

    private void InitCommon()
    {
        // Fix lỗi chiều cao (full màn hình)
        _root.style.height = Length.Percent(100);
        _root.style.width = Length.Percent(100);

        SetupModalLogic();
        SetupSearchLogic();
    }

    // ------------------------------------------------------------------
    // 1A. MOCK MODE — gắn logic vào card có sẵn trong UXML
    // ------------------------------------------------------------------
    private void SetupListLogic()
    {
        List<VisualElement> containers = _root.Query<VisualElement>(className: "history-card").ToList();

        if (containers.Count > 0)
        {
            CacheFontFromSample(containers[0]);
            _listContainer = containers[0].parent;
        }

        foreach (var container in containers)
        {
            BindCardLogic(container, chatTitle: container.Q<Label>(className: "card-title")?.text ?? "Đoạn chat", item: null);
        }
    }

    // ------------------------------------------------------------------
    // 1B. API MODE — clear card mock và build lại từ List<ChatHistoryItem>
    // ------------------------------------------------------------------
    private void PopulateFromData(List<ChatHistoryItem> items)
    {
        var existing = _root.Query<VisualElement>(className: "history-card").ToList();
        if (existing.Count > 0)
        {
            CacheFontFromSample(existing[0]);
            _listContainer = existing[0].parent;
            foreach (var card in existing) card.RemoveFromHierarchy();
        }

        if (_listContainer == null)
        {
            // fallback: tìm ScrollView body-list
            _listContainer = _root.Q<ScrollView>(className: "body-list");
            if (_listContainer is ScrollView sv) _listContainer = sv.contentContainer;
        }

        if (_listContainer == null)
        {
            Debug.LogError("[HistoryManager] Không xác định được container chứa history-card.");
            return;
        }

        if (items == null || items.Count == 0)
        {
            Debug.Log("[HistoryManager] Danh sách lịch sử rỗng.");
            return;
        }

        foreach (var item in items)
        {
            string title = string.IsNullOrEmpty(item.header) ? "Đoạn chat" : item.header;
            string date = FormatDate(item.create_date);

            VisualElement card = BuildHistoryCard(title, date);
            _listContainer.Add(card);

            BindCardLogic(card, title, item);
        }
    }

    /// <summary>
    /// Tái sử dụng cho cả mock-mode và API-mode.
    /// item == null nghĩa là đang dùng card mock (legacy).
    /// </summary>
    private void BindCardLogic(VisualElement container, string chatTitle, ChatHistoryItem item)
    {
        var slider = container.Q<VisualElement>(className: "card-content");
        var arrow = slider?.Q<VisualElement>(className: "card-arrow");
        var btnDeleteRed = container.Q<Button>(className: "btn-delete-hidden");

        if (slider != null)
        {
            // A. Mũi tên trượt
            if (arrow != null)
            {
                arrow.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    CloseAllOtherCards(slider);
                    slider.ToggleInClassList("card-swiped");
                });
            }

            // B. Click vào thẻ để mở Chat
            slider.RegisterCallback<ClickEvent>(evt =>
            {
                if (slider.ClassListContains("card-swiped"))
                {
                    slider.RemoveFromClassList("card-swiped");
                    return;
                }

                if (_onOpenChatItem != null && item != null)
                {
                    _onOpenChatItem.Invoke(item);
                }
                else
                {
                    _onOpenChat?.Invoke(chatTitle);
                }
            });
        }

        // C. Nút Thùng Rác
        if (btnDeleteRed != null)
        {
            btnDeleteRed.RegisterCallback<ClickEvent>(evt =>
            {
                _itemPendingDelete = container;
                _itemPendingDeleteData = item;
                if (slider != null) slider.RemoveFromClassList("card-swiped");
                if (_deleteModal != null) _deleteModal.RemoveFromClassList("hidden");
            });
        }
    }

    // ------------------------------------------------------------------
    // 2. MODAL XÓA
    // ------------------------------------------------------------------
    private void SetupModalLogic()
    {
        _deleteModal = _root.Q<VisualElement>("DeleteModal");
        var btnCancel = _root.Q<Button>("BtnCancel");
        var btnConfirm = _root.Q<Button>("BtnConfirm");

        if (_deleteModal == null) return;

        btnCancel?.RegisterCallback<ClickEvent>(evt =>
        {
            _deleteModal.AddToClassList("hidden");
            _itemPendingDelete = null;
            _itemPendingDeleteData = null;
        });

        btnConfirm?.RegisterCallback<ClickEvent>(evt =>
        {
            if (_itemPendingDelete != null)
            {
                _itemPendingDelete.RemoveFromHierarchy();
            }

            // Nếu API mode: gọi callback để xoá trên server.
            if (_itemPendingDeleteData != null && _onDeleteItem != null)
            {
                _onDeleteItem.Invoke(_itemPendingDeleteData);
            }

            _itemPendingDelete = null;
            _itemPendingDeleteData = null;
            _deleteModal.AddToClassList("hidden");
        });
    }

    // ------------------------------------------------------------------
    // 3. TÌM KIẾM
    // ------------------------------------------------------------------
    private void SetupSearchLogic()
    {
        _searchOverlay = _root.Q<VisualElement>("SearchOverlay");
        _inputSearch = _root.Q<TextField>("InputSearch");
        _searchResultsContainer = _root.Q<ScrollView>(className: "search-results-body");
        var notFoundState = _root.Q<VisualElement>("NotFoundState");

        var btnBackSearch = _root.Q<Button>("BtnBackSearch");
        var btnOpenSearch = _root.Q<VisualElement>("Header")?.Q<VisualElement>(className: "icon-search");

        if (_searchOverlay == null) return;

        btnOpenSearch?.RegisterCallback<ClickEvent>(evt =>
        {
            _searchOverlay.RemoveFromClassList("hidden");
            _inputSearch.value = "";
            _searchResultsContainer?.Clear();
            notFoundState?.AddToClassList("hidden");

            _inputSearch.schedule.Execute(() => _inputSearch.Focus()).StartingIn(50);
        });

        btnBackSearch?.RegisterCallback<ClickEvent>(evt =>
        {
            _searchOverlay.AddToClassList("hidden");
            _inputSearch.Blur();
        });

        if (_inputSearch != null)
        {
            _inputSearch.RegisterValueChangedCallback(evt =>
            {
                PerformSearchAndPopulate(evt.newValue, notFoundState);
            });
        }
    }

    private void PerformSearchAndPopulate(string searchText, VisualElement notFoundState)
    {
        _searchResultsContainer?.Clear();

        string token = string.IsNullOrEmpty(searchText) ? "" : searchText.ToLower().Trim();
        if (string.IsNullOrEmpty(token))
        {
            notFoundState?.AddToClassList("hidden");
            return;
        }

        var originalCards = _root.Query<VisualElement>(className: "history-card").ToList();
        int foundCount = 0;

        foreach (var originalCard in originalCards)
        {
            // Bỏ qua các card mới được clone vào search container
            if (_searchResultsContainer != null && originalCard.parent == _searchResultsContainer.contentContainer)
                continue;

            var titleLbl = originalCard.Q<Label>(className: "card-title");
            var dateLbl = originalCard.Q<Label>(className: "card-date");

            string title = titleLbl != null ? titleLbl.text : "";
            string date = dateLbl != null ? dateLbl.text : "";

            if (title.ToLower().Contains(token))
            {
                foundCount++;
                _searchResultsContainer?.Add(BuildHistoryCard(title, date, isSearchResult: true));
            }
        }

        if (notFoundState != null)
        {
            if (foundCount == 0) notFoundState.RemoveFromClassList("hidden");
            else notFoundState.AddToClassList("hidden");
        }
    }

    // ------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------
    private VisualElement BuildHistoryCard(string title, string date, bool isSearchResult = false)
    {
        var card = new VisualElement();
        card.AddToClassList("history-card");
        card.style.display = DisplayStyle.Flex;

        var btnDelete = new Button();
        btnDelete.AddToClassList("btn-delete-hidden");
        card.Add(btnDelete);

        var content = new VisualElement();
        content.AddToClassList("card-content");
        card.Add(content);

        var textGroup = new VisualElement();
        textGroup.AddToClassList("card-text-group");
        content.Add(textGroup);

        var lblTitle = new Label(title);
        lblTitle.AddToClassList("card-title");
        if (_hasFonts) lblTitle.style.unityFontDefinition = _cachedTitleFont;
        textGroup.Add(lblTitle);

        var lblDate = new Label(date);
        lblDate.AddToClassList("card-date");
        if (_hasFonts) lblDate.style.unityFontDefinition = _cachedDateFont;
        textGroup.Add(lblDate);

        var arrow = new Label("›");
        arrow.AddToClassList("card-arrow");
        content.Add(arrow);

        if (isSearchResult)
        {
            // Search result chỉ cần click → mở chat theo title
            content.RegisterCallback<ClickEvent>(evt =>
            {
                _onOpenChat?.Invoke(title);
                _onOpenChatItem?.Invoke(new ChatHistoryItem { header = title });
            });
        }

        return card;
    }

    private void CacheFontFromSample(VisualElement sampleCard)
    {
        var sampleTitle = sampleCard.Q<Label>(className: "card-title");
        var sampleDate = sampleCard.Q<Label>(className: "card-date");

        if (sampleTitle != null) _cachedTitleFont = sampleTitle.style.unityFontDefinition;
        if (sampleDate != null) _cachedDateFont = sampleDate.style.unityFontDefinition;
        _hasFonts = true;
    }

    private void CloseAllOtherCards(VisualElement currentSlider)
    {
        var allSliders = _root.Query<VisualElement>(className: "card-content").ToList();
        foreach (var s in allSliders)
        {
            if (s != currentSlider) s.RemoveFromClassList("card-swiped");
        }
    }

    /// <summary>
    /// Format ISO date "2024-03-08T03:23:00Z" → "08-03-2024 | 03:23 AM".
    /// Nếu parse fail thì trả về raw.
    /// </summary>
    private static string FormatDate(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime dt))
        {
            return dt.ToLocalTime().ToString("dd-MM-yyyy | hh:mm tt", CultureInfo.InvariantCulture);
        }
        return raw;
    }
}
