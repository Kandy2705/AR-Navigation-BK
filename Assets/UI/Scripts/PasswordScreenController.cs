using UnityEngine;
using UnityEngine.UIElements;

public class PasswordScreenController : MonoBehaviour
{
    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // --- Cặp 1: Mật khẩu cũ ---
        SetupPasswordToggle(root, "old-password", "btn-toggle-old");

        // --- Cặp 2: Mật khẩu mới ---
        SetupPasswordToggle(root, "new-password", "btn-toggle-new");

        // --- Cặp 3: Nhập lại mật khẩu ---
        SetupPasswordToggle(root, "confirm-password", "btn-toggle-re");
    }

    // Hàm tiện ích để nối nút với input
    private void SetupPasswordToggle(VisualElement root, string inputName, string btnName)
    {
        // 1. Tìm Input và Nút bằng tên đã đặt trong UXML
        var inputField = root.Q<PlaceHolder>(inputName);
        var toggleBtn = root.Q<Button>(btnName);

        if (inputField == null || toggleBtn == null) 
        {
            Debug.LogError($"Không tìm thấy {inputName} hoặc {btnName}");
            return;
        }

        // 2. Mặc định set input là password (hiện dấu *)
        inputField.isPasswordField = true;

        // 3. Gán sự kiện Click
        toggleBtn.clicked += () => 
        {
            // Đảo ngược trạng thái mật khẩu
            inputField.isPasswordField = !inputField.isPasswordField;
            
            // (Tùy chọn) Đổi hình con mắt bằng cách đổi class
            // Bạn cần tạo class .eye-open trong USS có ảnh mắt mở
            toggleBtn.ToggleInClassList("eye-open"); 
            
            // Focus lại vào input để gõ tiếp
            inputField.Q("unity-text-input").Focus();
        };
    }
}