using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Helper add một stylesheet chung (caret trắng) cho mọi UIDocument
/// và tắt elastic-bounce trên mọi ScrollView để layout không bị "kéo xuống" khi rỗng.
/// Stylesheet được lưu ở Assets/UI/Resources/InputCaretStyle.uss để load qua Resources.
/// </summary>
public static class CaretStyleApplier
{
    private const string ResourcePath = "InputCaretStyle";
    private static StyleSheet _cached;
    private static bool _missingLogged;

    /// <summary>
    /// Lazy load stylesheet và add vào root nếu chưa có.
    /// Đồng thời tắt elastic scroll trên mọi ScrollView trong cây để chỉ scroll
    /// được khi nội dung thực sự vượt vùng hiển thị.
    /// </summary>
    public static void Apply(VisualElement root)
    {
        if (root == null) return;

        if (_cached == null)
        {
            _cached = Resources.Load<StyleSheet>(ResourcePath);
            if (_cached == null)
            {
                if (!_missingLogged)
                {
                    Debug.LogWarning($"[CaretStyleApplier] Không tìm thấy Resources/{ResourcePath}.uss");
                    _missingLogged = true;
                }
            }
        }

        if (_cached != null && !root.styleSheets.Contains(_cached))
        {
            root.styleSheets.Add(_cached);
        }

        ClampScrollViews(root);
    }

    /// <summary>
    /// Set touchScrollBehavior = Clamped (mặc định Elastic) cho mọi ScrollView,
    /// và tắt scroller (bar) hiển thị mặc định ở phía dọc của list view ngắn.
    /// Gọi schedule để bắt cả những ScrollView được instantiate sau frame đầu.
    /// </summary>
    private static void ClampScrollViews(VisualElement root)
    {
        ApplyClampNow(root);
        // Apply lại 1 lần ở frame kế tiếp để bắt các ScrollView build dynamic
        // (Routing/PageFactory instantiate sau Awake).
        root.schedule.Execute(() => ApplyClampNow(root)).ExecuteLater(50);
    }

    private static void ApplyClampNow(VisualElement root)
    {
        root.Query<ScrollView>().ForEach(sv =>
        {
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            sv.elasticity = 0f;
        });
    }
}
