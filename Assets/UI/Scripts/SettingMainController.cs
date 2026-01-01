using UnityEngine;
using UnityEngine.UIElements;

public class SettingMainController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _bottomSheet;
    private Button _logoutBtnOpen;
    private Button _cancelBtn;
    private Button _confirmBtn;

    // Tên class dùng để kích hoạt animation trong USS
    private const string CLASS_OVERLAY_SHOW = "logout-overlay--show";
    private const string CLASS_SHEET_UP = "bottom-sheet--up";

    void OnEnable()
    {
        Debug.Log("Hàm trên đã hoạt động");
        _doc = GetComponent<UIDocument>();
        var root = _doc.rootVisualElement;

        // 1. Tìm các thành phần UI theo Name hoặc Class
        // Tìm nút Logout ở màn hình chính (Dựa trên UXML cũ của bạn, nút này có class 'logout-item')
        _logoutBtnOpen = root.Q<Button>(className: "logout-item"); 
        
        // Tìm các thành phần Modal vừa thêm
        _overlay = root.Q<VisualElement>("logout-overlay");
        _bottomSheet = root.Q<VisualElement>("bottom-sheet");
        _cancelBtn = root.Q<Button>("btn-cancel");
        _confirmBtn = root.Q<Button>("btn-confirm");

        // 2. Đăng ký sự kiện Click
        if (_logoutBtnOpen != null)
        {
            _logoutBtnOpen.clicked += ShowLogoutModal;
        } 
        
        if (_cancelBtn != null) _cancelBtn.clicked += HideLogoutModal;
        
        // Xử lý khi bấm vào vùng đen (Overlay) thì cũng đóng modal
        if (_overlay != null) _overlay.RegisterCallback<ClickEvent>(OnOverlayClick);

        if (_confirmBtn != null) _confirmBtn.clicked += OnLogoutConfirmed;
    }

    private void ShowLogoutModal()
    {
        _overlay.AddToClassList(CLASS_OVERLAY_SHOW);
        _bottomSheet.AddToClassList(CLASS_SHEET_UP);
    }

    private void HideLogoutModal()
    {
        // Gỡ class để nó tự động chạy transition ngược lại (mờ đi và trượt xuống)
        _overlay.RemoveFromClassList(CLASS_OVERLAY_SHOW);
        _bottomSheet.RemoveFromClassList(CLASS_SHEET_UP);
    }

    private void OnOverlayClick(ClickEvent evt)
    {
        // Chỉ đóng nếu click chính xác vào vùng đen (không phải click vào bảng trắng bên trong)
        if (evt.target == _overlay)
        {
            HideLogoutModal();
        }
    }

    private void OnLogoutConfirmed()
    {
        Debug.Log("Đang thực hiện đăng xuất...");
    }

    void OnDisable()
    {
        if (_logoutBtnOpen != null) _logoutBtnOpen.clicked -= ShowLogoutModal;
        if (_cancelBtn != null) _cancelBtn.clicked -= HideLogoutModal;
        if (_confirmBtn != null) _confirmBtn.clicked -= OnLogoutConfirmed;
        if (_overlay != null) _overlay.UnregisterCallback<ClickEvent>(OnOverlayClick);
    }
}