using System.Collections.Generic;
using System.Linq;

namespace Game.Features.Settings
{
    /// <summary>FAQ entries of the support center, describing how this app actually behaves.</summary>
    public static class SupportContent
    {
        public struct Entry
        {
            public int Category;
            public string Question;
            public string Answer;
        }

        /// <summary>Chip labels; index 0 shows every entry.</summary>
        public static readonly string[] Categories = { "Tất cả", "Tài khoản", "Dẫn đường AR", "Trợ lý AI" };

        public static readonly Entry[] Entries =
        {
            new Entry
            {
                Category = 1, Question = "Làm sao để đặt lại mật khẩu khi quên?",
                Answer = "Ở màn hình đăng nhập, chọn “Quên mật khẩu” và nhập email đã đăng ký. Ứng dụng gửi mã OTP gồm 4 chữ số tới email đó; nhập mã rồi đặt mật khẩu mới."
            },
            new Entry
            {
                Category = 1, Question = "Tôi không nhận được mã OTP?",
                Answer = "Hãy kiểm tra thư mục Spam hoặc Quảng cáo. Mỗi mã có hiệu lực 5 phút và chỉ dùng được một lần; bạn có thể yêu cầu mã mới sau 60 giây. Nhập sai 5 lần thì mã bị hủy, khi đó hãy yêu cầu mã khác."
            },
            new Entry
            {
                Category = 1, Question = "Làm sao để đổi email đăng nhập?",
                Answer = "Vào Cài đặt › Hồ sơ › Thay đổi Email và nhập email mới. Mã OTP được gửi tới email mới để xác nhận bạn sở hữu địa chỉ đó; email chỉ được đổi sau khi bạn nhập đúng mã."
            },
            new Entry
            {
                Category = 1, Question = "“Ghi nhớ tôi” có lưu mật khẩu của tôi không?",
                Answer = "Không. Ứng dụng chỉ nhớ email của bạn. Phiên đăng nhập được duy trì bằng mã làm mới do máy chủ cấp và bị xóa khi bạn đăng xuất."
            },
            new Entry
            {
                Category = 2, Question = "Ứng dụng dẫn đường được đến những đâu?",
                Answer = "Ngoài trời, ứng dụng dẫn đến các tòa nhà có trong danh sách điểm đến của khuôn viên. Trong nhà, ứng dụng dẫn đến các phòng của tòa B9 và B10 – hai tòa đã có bản đồ định vị bằng hình ảnh."
            },
            new Entry
            {
                Category = 2, Question = "Điện thoại của tôi cần những gì để dùng AR?",
                Answer = "Điện thoại cần hỗ trợ ARCore (Android) hoặc ARKit (iOS), có kết nối mạng, và bạn cần cho phép ứng dụng dùng camera và vị trí."
            },
            new Entry
            {
                Category = 2, Question = "Khi bước vào tòa nhà tôi cần làm gì?",
                Answer = "Khi đến gần lối vào, hãy giơ điện thoại và quét camera chậm quanh hành lang vài giây để ứng dụng nhận ra vị trí trong nhà. Trong lúc chờ, ứng dụng tạm ước lượng vị trí theo bước chân của bạn."
            },
            new Entry
            {
                Category = 2, Question = "Vì sao đường dẫn AR đôi khi mờ đi hoặc chỉ còn chỉ hướng?",
                Answer = "Khi tín hiệu định vị kém tin cậy (GPS yếu, chưa nhận ra vị trí trong nhà), ứng dụng chủ động hiển thị thận trọng hơn thay vì vẽ một đường có thể sai. Hãy đi chậm và hướng camera về phía hành lang hoặc biển tên phòng để ứng dụng định vị lại."
            },
            new Entry
            {
                Category = 3, Question = "Trợ lý AI có thể giúp gì cho tôi?",
                Answer = "Bạn có thể hỏi bằng ngôn ngữ tự nhiên về khuôn viên và thông tin chung. Câu trả lời do AI tạo ra có thể chưa chính xác; hãy kiểm tra lại các thông tin quan trọng với phòng ban liên quan."
            },
            new Entry
            {
                Category = 3, Question = "Làm sao để xóa lịch sử trò chuyện?",
                Answer = "Mở Lịch sử, vuốt cuộc trò chuyện sang trái rồi nhấn biểu tượng thùng rác, hoặc dùng nút thùng rác ở trên cùng để xóa tất cả."
            },
        };

        /// <summary>Entries of one category; category 0 ("Tất cả") returns everything.</summary>
        public static List<Entry> For(int category)
        {
            return Entries.Where(entry => category <= 0 || entry.Category == category).ToList();
        }
    }
}
