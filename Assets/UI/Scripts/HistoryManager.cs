using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

public class HistoryManager
{
    private VisualElement _root;
    
    // Delegate: Hành động mở chat (Để giảm sự phụ thuộc vào Controller cụ thể)
    private Action<string> _onOpenChat; 

    // Các biến lưu tham chiếu UI
    private VisualElement _deleteModal;
    private VisualElement _itemPendingDelete;
    private VisualElement _searchOverlay;
    private TextField _inputSearch;
    private bool _hasFonts = false;
    private StyleFontDefinition _cachedTitleFont;
    private StyleFontDefinition _cachedDateFont;

    // --- CONSTRUCTOR (Thay cho OnEnable) ---
    // tham số 'onOpenChat': Bạn truyền hàm mở chat vào đây (từ Routing hoặc ChatController)
    public HistoryManager(VisualElement rootElement, Action<string> onOpenChat)
    {
        Debug.Log("HistoryManager initialized.");
        _root = rootElement;
        _onOpenChat = onOpenChat;

        // Fix lỗi chiều cao (để đảm bảo full màn hình)
        _root.style.height = Length.Percent(100);
        _root.style.width = Length.Percent(100);

        // Kích hoạt các logic
        SetupModalLogic();
        SetupSearchLogic();
        SetupListLogic();
    }

    // --- 1. LOGIC DANH SÁCH (SWIPE, CLICK, DELETE) ---
    private void SetupListLogic()
    {
        // Tìm tất cả các thẻ history-card trong Visual Tree
        List<VisualElement> containers = _root.Query<VisualElement>(className: "history-card").ToList();

        // --- ĐOẠN MỚI: LẤY MẪU FONT TỪ THẺ ĐẦU TIÊN TÌM THẤY ---
        if (containers.Count > 0)
        {
            var sampleCard = containers[0];
            var sampleTitle = sampleCard.Q<Label>(className: "card-title");
            var sampleDate = sampleCard.Q<Label>(className: "card-date");

            // Lưu lại Font setting của thẻ mẫu vào biến
            if (sampleTitle != null) _cachedTitleFont = sampleTitle.style.unityFontDefinition;
            if (sampleDate != null) _cachedDateFont = sampleDate.style.unityFontDefinition;
            _hasFonts = true;
        }

        foreach (var container in containers)
        {
            var slider = container.Q<VisualElement>(className: "card-content");
            var arrow = slider?.Q<VisualElement>(className: "card-arrow");
            var btnDeleteRed = container.Q<Button>(className: "btn-delete-hidden");

            // Lấy tên đoạn chat
            var titleLabel = slider?.Q<Label>(className: "card-title");
            string chatTitle = titleLabel != null ? titleLabel.text : "Đoạn chat";

            if (slider != null)
            {
                // Logic A: Mũi tên trượt (Swipe)
                if (arrow != null)
                {
                    arrow.RegisterCallback<ClickEvent>(evt => {
                        evt.StopPropagation(); // Chặn click xuyên xuống dưới
                        CloseAllOtherCards(slider); // Đóng các thẻ khác đang mở
                        slider.ToggleInClassList("card-swiped"); // Bật/Tắt class trượt
                    });
                }

                // Logic B: Click vào thẻ để mở Chat
                slider.RegisterCallback<ClickEvent>(evt => {
                    // Nếu đang ở trạng thái trượt -> Đóng lại
                    if (slider.ClassListContains("card-swiped"))
                    {
                        slider.RemoveFromClassList("card-swiped");
                    }
                    else 
                    {
                        // Gọi hành động mở Chat (được truyền vào từ bên ngoài)
                        _onOpenChat?.Invoke(chatTitle);
                    }
                });
            }

            // Logic C: Nút Thùng Rác (Xóa)
            if (btnDeleteRed != null)
            {
                btnDeleteRed.RegisterCallback<ClickEvent>(evt => {
                    _itemPendingDelete = container; // Lưu lại cái cần xóa
                    
                    // Đóng swipe trước khi hiện modal
                    if (slider != null) slider.RemoveFromClassList("card-swiped");
                    
                    // Hiện Modal
                    if (_deleteModal != null) _deleteModal.RemoveFromClassList("hidden");
                });
            }
        }
    }

    // --- 2. LOGIC MODAL XÓA ---
    private void SetupModalLogic()
    {
        _deleteModal = _root.Q<VisualElement>("DeleteModal");
        var btnCancel = _root.Q<Button>("BtnCancel");
        var btnConfirm = _root.Q<Button>("BtnConfirm");

        if (_deleteModal == null) return;

        // Nút Hủy
        btnCancel?.RegisterCallback<ClickEvent>(evt => {
            _deleteModal.AddToClassList("hidden");
            _itemPendingDelete = null;
        });

        // Nút Đồng ý xóa
        btnConfirm?.RegisterCallback<ClickEvent>(evt => {
            if (_itemPendingDelete != null)
            {
                _itemPendingDelete.RemoveFromHierarchy(); // Xóa khỏi UI
                _itemPendingDelete = null;
            }
            _deleteModal.AddToClassList("hidden");
        });
    }

    // --- 3. LOGIC TÌM KIẾM (SEARCH) ---
    // private void SetupSearchLogic()
    // {
    //     _searchOverlay = _root.Q<VisualElement>("SearchOverlay");
    //     _inputSearch = _root.Q<TextField>("InputSearch");
    //     var btnBackSearch = _root.Q<Button>("BtnBackSearch");
        
    //     // Lấy tham chiếu UI màn hình "Không tìm thấy"
    //     // Đảm bảo trong UI Builder bạn đã đặt tên này cho container chứa icon X
    //     var notFoundState = _root.Q<VisualElement>("NotFoundState"); 
        
    //     // Tìm nút mở search ở màn hình chính
    //     var btnOpenSearch = _root.Q<VisualElement>("Header")?.Q<VisualElement>(className: "icon-search");

    //     if (_searchOverlay == null) return;

    //     // A. Mở tìm kiếm
    //     btnOpenSearch?.RegisterCallback<ClickEvent>(evt => {
    //         _searchOverlay.RemoveFromClassList("hidden");
    //         _inputSearch.value = ""; 
    //         notFoundState?.AddToClassList("hidden"); 
    //         ShowAllItems(); // Hiện lại tất cả item để bắt đầu tìm
            
    //         _inputSearch.schedule.Execute(() => _inputSearch.Focus()).StartingIn(50);
    //     });

    //     // B. Đóng tìm kiếm
    //     btnBackSearch?.RegisterCallback<ClickEvent>(evt => {
    //         _searchOverlay.AddToClassList("hidden");
    //         _inputSearch.Blur();
    //     });

    //     // C. LOGIC SEARCH REALTIME
    //     if (_inputSearch != null)
    //     {
    //         _inputSearch.RegisterValueChangedCallback(evt => {
    //             string searchText = evt.newValue.ToLower().Trim();
    //             PerformSearch(searchText, notFoundState);
    //         });
    //     }
    // }

    // // Hàm thực hiện lọc danh sách
    // private void PerformSearch(string searchText, VisualElement notFoundState)
    // {
    //     // Lấy tất cả các thẻ history-card
    //     var cards = _root.Query<VisualElement>(className: "history-card").ToList();
    //     int visibleCount = 0;

    //     foreach (var card in cards)
    //     {
    //         // Tìm label chứa tên đoạn chat
    //         var titleLabel = card.Q<Label>(className: "card-title");
    //         string titleText = titleLabel != null ? titleLabel.text.ToLower() : "";

    //         if (string.IsNullOrEmpty(searchText))
    //         {
    //             // Nếu không nhập gì -> Hiện tất cả
    //             card.style.display = DisplayStyle.Flex;
    //             visibleCount++;
    //         }
    //         else if (titleText.Contains(searchText))
    //         {
    //             // Nếu khớp từ khóa -> Hiện
    //             card.style.display = DisplayStyle.Flex;
    //             visibleCount++;
    //         }
    //         else
    //         {
    //             // Không khớp -> Ẩn
    //             card.style.display = DisplayStyle.None;
    //         }
    //     }

    //     // Xử lý hiển thị màn hình "Không tìm thấy"
    //     if (notFoundState != null)
    //     {
    //         if (visibleCount == 0)
    //         {
    //             notFoundState.RemoveFromClassList("hidden"); // Hiện màn hình lỗi
    //         }
    //         else
    //         {
    //             notFoundState.AddToClassList("hidden"); // Ẩn màn hình lỗi
    //         }
    //     }
    // }


    private ScrollView _searchResultsContainer;

    private void SetupSearchLogic()
    {
        _searchOverlay = _root.Q<VisualElement>("SearchOverlay");
        _inputSearch = _root.Q<TextField>("InputSearch");
        _searchResultsContainer = _root.Q<ScrollView>(className: "search-results-body"); // Tìm cái ScrollView
        var notFoundState = _root.Q<VisualElement>("NotFoundState");
        
        var btnBackSearch = _root.Q<Button>("BtnBackSearch");
        var btnOpenSearch = _root.Q<VisualElement>("Header")?.Q<VisualElement>(className: "icon-search");

        if (_searchOverlay == null) return;

        // A. Mở tìm kiếm
        btnOpenSearch?.RegisterCallback<ClickEvent>(evt => {
            _searchOverlay.RemoveFromClassList("hidden");
            _inputSearch.value = "";
            
            // Xóa sạch kết quả cũ mỗi khi mở lại
            _searchResultsContainer.Clear(); 
            notFoundState?.AddToClassList("hidden");
            
            _inputSearch.schedule.Execute(() => _inputSearch.Focus()).StartingIn(50);
        });

        // B. Đóng tìm kiếm
        btnBackSearch?.RegisterCallback<ClickEvent>(evt => {
            _searchOverlay.AddToClassList("hidden");
            _inputSearch.Blur();
        });

        // C. Xử lý logic tìm kiếm
        if (_inputSearch != null)
        {
            _inputSearch.RegisterValueChangedCallback(evt => {
                string searchText = evt.newValue;
                PerformSearchAndPopulate(searchText, notFoundState);
            });
        }
    }

    // Hàm lọc và tạo UI mới
    private void PerformSearchAndPopulate(string searchText, VisualElement notFoundState)
    {
        // 1. Xóa kết quả cũ trong container
        _searchResultsContainer.Clear();

        string token = string.IsNullOrEmpty(searchText) ? "" : searchText.ToLower().Trim();

        // Nếu ô trống thì không hiện gì (hoặc hiện tất cả tùy bạn) - Ở đây tôi để trống cho gọn
        if (string.IsNullOrEmpty(token))
        {
            notFoundState?.AddToClassList("hidden");
            return;
        }

        // 2. Quét dữ liệu từ danh sách gốc (Main List)
        // Lưu ý: Trong thực tế bạn nên có 1 List<Data> riêng, nhưng ở đây ta quét từ UI
        var originalCards = _root.Query<VisualElement>(className: "history-card").ToList();
        int foundCount = 0;

        foreach (var originalCard in originalCards)
        {
            // Lấy data từ thẻ gốc
            var titleLbl = originalCard.Q<Label>(className: "card-title");
            var dateLbl = originalCard.Q<Label>(className: "card-date");
            
            string title = titleLbl != null ? titleLbl.text : "";
            string date = dateLbl != null ? dateLbl.text : "";

            // 3. Kiểm tra điều kiện
            if (title.ToLower().Contains(token))
            {
                foundCount++;

                // 4. TẠO THẺ MỚI (Clone UI) và add vào search container
                VisualElement newCard = CreateResultCard(title, date);
                _searchResultsContainer.Add(newCard);
            }
        }

        // 5. Xử lý NotFound
        if (notFoundState != null)
        {
            if (foundCount == 0) notFoundState.RemoveFromClassList("hidden");
            else notFoundState.AddToClassList("hidden");
        }
    }

    // Hàm Helper: Xây dựng giao diện thẻ card bằng code (để hiển thị trong Search)
    private VisualElement CreateResultCard(string title, string date)
    {
        // Tạo container chính
        var card = new VisualElement();
        card.AddToClassList("history-card"); 
        card.style.display = DisplayStyle.Flex; 

        var btnDelete = new Button();
        btnDelete.AddToClassList("btn-delete-hidden");
        card.Add(btnDelete);

        var content = new VisualElement();
        content.AddToClassList("card-content");
        card.Add(content);

        // Group text
        var textGroup = new VisualElement();
        textGroup.AddToClassList("card-text-group");
        content.Add(textGroup);

        // --- TITLE ---
        var lblTitle = new Label(title);
        lblTitle.AddToClassList("card-title");
        
        if (_hasFonts)
        {
            lblTitle.style.unityFontDefinition = _cachedTitleFont;
        }
        textGroup.Add(lblTitle);

        var lblDate = new Label(date);
        lblDate.AddToClassList("card-date");

        if (_hasFonts)
        {
            lblDate.style.unityFontDefinition = _cachedDateFont;
        }
        textGroup.Add(lblDate);

        var arrow = new Label("›");
        arrow.AddToClassList("card-arrow");
        content.Add(arrow);
        
        content.RegisterCallback<ClickEvent>(evt => {
            _onOpenChat?.Invoke(title);
        });

        return card;
    }

    // --- HÀM PHỤ TRỢ ---

     private void ShowAllItems()
    {
        var cards = _root.Query<VisualElement>(className: "history-card").ToList();
        foreach (var card in cards)
        {
            card.style.display = DisplayStyle.Flex;
        }
    }
    private void CloseAllOtherCards(VisualElement currentSlider)
    {
        var allSliders = _root.Query<VisualElement>(className: "card-content").ToList();
        foreach (var s in allSliders)
        {
            if (s != currentSlider) s.RemoveFromClassList("card-swiped");
        }
    }
}