using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class HistoryController : MonoBehaviour
{
    private UIDocument _uiDoc;
    private VisualElement _root;

    // [THAY ĐỔI 1] Thay vì giữ template, ta tham chiếu đến script Chat đã gắn trên Scene
    [Header("Kết Nối")]
    public ChatDetailController chatController;

    // --- CÁC BIẾN CŨ (GIỮ NGUYÊN) ---
    private VisualElement _deleteModal;
    private VisualElement _itemPendingDelete;
    private VisualElement _searchOverlay;
    private TextField _inputSearch;

    void OnEnable()
    {
        _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;

        // Fix lỗi chiều cao (Giữ nguyên)
        _root.style.height = Length.Percent(100);
        _root.style.width = Length.Percent(100);

        SetupModalLogic();
        SetupSearchLogic();
        SetupListLogic();
    }
    void SetupListLogic()
    {
        List<VisualElement> containers = _root.Query<VisualElement>(className: "history-card").ToList();

        foreach (var container in containers)
        {
            var slider = container.Q<VisualElement>(className: "card-content");
            var arrow = slider?.Q<VisualElement>(className: "card-arrow");
            var btnDeleteRed = container.Q<Button>(className: "btn-delete-hidden");

            // Lấy tên tòa nhà
            var titleLabel = slider?.Q<Label>(className: "card-title"); 
            string chatTitle = titleLabel != null ? titleLabel.text : "Đoạn chat";

            if (slider != null)
            {
                // Logic Trượt (GIỮ NGUYÊN)
                if (arrow != null)
                {
                    arrow.RegisterCallback<ClickEvent>(evt => {
                        evt.StopPropagation(); 
                        CloseAllOtherCards(slider);
                        slider.ToggleInClassList("card-swiped");
                    });
                }

                // Logic Click vào thẻ (THAY ĐỔI NHỎ Ở ĐÂY)
                slider.RegisterCallback<ClickEvent>(evt => {
                    if (slider.ClassListContains("card-swiped"))
                    {
                        slider.RemoveFromClassList("card-swiped");
                    }
                    else 
                    {
                        // [SỬA] Gọi sang ChatManager để mở chat
                        if (chatController != null) {
                            //chatController.OpenChat(chatTitle);
                        } else {
                            Debug.LogError("Chưa gắn ChatController vào Inspector!");
                        }
                    }
                });
            }

            // Logic Nút Thùng Rác (GIỮ NGUYÊN)
            if (btnDeleteRed != null)
            {
                btnDeleteRed.RegisterCallback<ClickEvent>(evt => {
                    _itemPendingDelete = container;
                    if (slider != null) slider.RemoveFromClassList("card-swiped");
                    _deleteModal.RemoveFromClassList("hidden");
                });
            }
        }
    }

    // --- CÁC HÀM CŨ GIỮ NGUYÊN 100% ---

    void SetupModalLogic()
    {
        _deleteModal = _root.Q<VisualElement>("DeleteModal");
        var btnCancel = _root.Q<Button>("BtnCancel");
        var btnConfirm = _root.Q<Button>("BtnConfirm");

        if (_deleteModal == null) return;

        btnCancel?.RegisterCallback<ClickEvent>(evt => {
            _deleteModal.AddToClassList("hidden");
            _itemPendingDelete = null;
        });

        btnConfirm?.RegisterCallback<ClickEvent>(evt => {
            if (_itemPendingDelete != null)
            {
                _itemPendingDelete.RemoveFromHierarchy();
                _itemPendingDelete = null;
            }
            _deleteModal.AddToClassList("hidden");
        });
    }

    void SetupSearchLogic()
    {
        _searchOverlay = _root.Q<VisualElement>("SearchOverlay");
        _inputSearch = _root.Q<TextField>("InputSearch");
        var btnBackSearch = _root.Q<Button>("BtnBackSearch");
        var btnOpenSearch = _root.Q<VisualElement>("Header")?.Q<VisualElement>(className: "icon-search"); 

        if (_searchOverlay == null) return;

        btnOpenSearch?.RegisterCallback<ClickEvent>(evt => {
            _searchOverlay.RemoveFromClassList("hidden");
            _inputSearch.schedule.Execute(() => _inputSearch.Focus()).StartingIn(50);
        });

        btnBackSearch?.RegisterCallback<ClickEvent>(evt => {
            _searchOverlay.AddToClassList("hidden");
            _inputSearch.Blur();
        });
    }

    void CloseAllOtherCards(VisualElement currentSlider)
    {
        var allSliders = _root.Query<VisualElement>(className: "card-content").ToList();
        foreach (var s in allSliders)
        {
            if (s != currentSlider) s.RemoveFromClassList("card-swiped");
        }
    }
}