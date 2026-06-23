using UnityEngine;
using UnityEngine.UIElements;

public class PasswordScreenController : MonoBehaviour
{
    private static bool IsForceIndoorTestMode()
    {
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        return hybrid != null && hybrid.ForceIndoorTestModeEnabled;
    }

    private void OnEnable()
    {
        // Trong indoor test mode, không cần bind password toggle.
        if (IsForceIndoorTestMode()) return;

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
            Debug.LogWarning($"PasswordScreenController: Không tìm thấy '{inputName}' hoặc '{btnName}' trên page hiện tại — skip.");
            return;
        }

        // 2. Mặc định set input là password (hiện dấu *)
        inputField.isPasswordField = true;

        // 3. Gán sự kiện Click
        toggleBtn.clicked += () => 
        {
            inputField.isPasswordField = !inputField.isPasswordField;
            toggleBtn.ToggleInClassList("eye-open"); 
            if (inputField.isPasswordField) inputField.Focus();
        };
    }
}