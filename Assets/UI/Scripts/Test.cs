using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Test : MonoBehaviour
{
    [Header("Dependencies")]
    public UIDocument uiDocument;
    
    [Header("Page Assets (Lazy Load)")]
    public VisualTreeAsset pageA_Asset; // Kéo file PageA.uxml vào đây
    public VisualTreeAsset pageB_Asset; // Kéo file PageB.uxml vào đây
    public VisualTreeAsset pageC_Asset; // Kéo file PageC.uxml vào đây

    private VisualElement _rootContainer;
    private VisualElement _currentPage;

    void OnEnable()
    {
        // 1. Lấy reference tới Container chính
        var root = uiDocument.rootVisualElement;
        _rootContainer = root.Q<VisualElement>("RootContainer");

        // 2. Load trang A đầu tiên khi game chạy
        LoadPageA_Initial();
    }

    // --- Khởi tạo trang đầu tiên ---
    void LoadPageA_Initial()
    {
        // Tạo trang A từ Asset
        VisualElement pageA = pageA_Asset.Instantiate();
        
        // Gắn class cơ bản và vị trí Center
        pageA.AddToClassList("page");
        pageA.AddToClassList("page-center");

        // Gắn sự kiện nút bấm bên trong Page A
        pageA.Q<Button>("PageA").clicked += () => NavigateTo(pageB_Asset);

        // Thêm vào màn hình và lưu lại biến theo dõi
        _rootContainer.Add(pageA);
        _currentPage = pageA;
    }

    // --- Hàm chuyển trang tổng quát (Slide Effect) ---
    public void NavigateTo(VisualTreeAsset nextTemplate)
    {
        if (_currentPage == null) return;

        // BƯỚC 1: Tạo trang mới (Lazy Load)
        VisualElement nextPage = nextTemplate.Instantiate();
        
        // BƯỚC 2: Setup vị trí ban đầu (Nằm chờ bên phải)
        nextPage.AddToClassList("page");
        nextPage.AddToClassList("page-right"); // Quan trọng: Đặt nó ở bên phải trước

        // Gắn sự kiện cho trang mới (Ví dụ nút Back ở trang B)
        // Lưu ý: Logic này nên tách ra từng Controller riêng nếu game lớn
        var btnBack = nextPage.Q<Button>("BtnBack");
        if (btnBack != null) btnBack.clicked += () => NavigateBack(nextPage);

        // BƯỚC 3: Thêm vào cây UI
        _rootContainer.Add(nextPage);

        // BƯỚC 4: Kích hoạt Animation
        // LƯU Ý QUAN TRỌNG: Phải dùng schedule để đợi layout cập nhật xong mới chạy animation
        // Nếu không có dòng này, trang mới sẽ nhảy bụp vào giữa mà không trượt.
        nextPage.schedule.Execute(() => 
        {
            // Đẩy trang hiện tại sang trái
            _currentPage.RemoveFromClassList("page-center");
            _currentPage.AddToClassList("page-left");

            // Kéo trang mới từ phải vào giữa
            nextPage.RemoveFromClassList("page-right");
            nextPage.AddToClassList("page-center");

            // Cập nhật biến theo dõi (Lưu ý: Bạn có thể cần lưu _currentPage vào 1 Stack để Back)
            var oldPage = _currentPage;
            _currentPage = nextPage;
            
            // (Tùy chọn) Xóa trang cũ sau khi animation xong để tiết kiệm RAM
            // Dùng schedule đợi 500ms (bằng duration trong CSS)
            oldPage.schedule.Execute(() => 
            {
               // _rootContainer.Remove(oldPage); // Uncomment nếu muốn hủy trang cũ hoàn toàn
            }).ExecuteLater(500); 

        }).ExecuteLater(10); // Đợi khoảng 10ms hoặc 1 frame
    }

    // --- Hàm quay lại (Slide Back) ---
    public void NavigateBack(VisualElement pageToRemove)
    {
        // Logic quay lại: Đưa trang hiện tại sang phải, kéo trang cũ từ trái về
        // Để làm đơn giản demo này, tôi sẽ chỉ Reload lại Page A.
        // Trong thực tế, bạn nên dùng Stack<VisualElement> để lưu lịch sử.
        
        _rootContainer.Clear(); // Xóa sạch làm lại cho nhanh (Demo only)
        LoadPageA_Initial();
    }
}