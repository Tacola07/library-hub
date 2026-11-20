using ItWebFrontend.Helpers;
using ItWebFrontend.Models;

namespace ItWebFrontend.Data;

public static class DbSeeder
{
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin@123";

    // Chỉ nạp dữ liệu mẫu cho bảng còn trống
    public static void Seed(AppDbContext db)
    {
        if (!db.Readers.Any())
        {
            db.Readers.AddRange(
                new Reader { Name = "Quản trị viên", Email = AdminEmail, Phone = "0900000000", Role = Roles.Admin,
                             Address = "Thư viện trung tâm", PasswordHash = Passwords.Hash(AdminPassword) },
                new Reader { Name = "Nguyễn Minh Anh", Email = "minhanh@example.com", Phone = "0900000001",
                             PasswordHash = Passwords.Hash("User@123"),
                             Infos = { new ReaderInfo { Label = "Đơn vị", Value = "Sinh viên năm 2" } } },
                new Reader { Name = "Trần Gia Huy", Email = "giahuy@example.com", Phone = "0900000002",
                             PasswordHash = Passwords.Hash("User@123") });
            db.SaveChanges();
        }

        if (!db.Books.Any())
        {
            db.Books.AddRange(
                new Book { Title = "Atomic Habits",    Author = "James Clear",       Category = "Kỹ năng sống",        Quantity = 12, CoverUrl = "/assets/cover-1.webp" },
                new Book { Title = "Đắc Nhân Tâm",     Author = "Dale Carnegie",     Category = "Phát triển bản thân", Quantity = 8,  CoverUrl = "/assets/cover-2.webp" },
                new Book { Title = "Nhà Giả Kim",      Author = "Paulo Coelho",      Category = "Văn học",             Quantity = 10, CoverUrl = "/assets/cover-3.webp" },
                new Book { Title = "Tư Duy Phản Biện", Author = "Tom Chatfield",     Category = "Giáo dục",            Quantity = 6,  CoverUrl = "/assets/cover-4.webp" },
                new Book { Title = "Sapiens",          Author = "Yuval Noah Harari", Category = "Lịch sử",             Quantity = 7,  CoverUrl = "/assets/cover-5.webp" });
            db.SaveChanges();
        }

        if (!db.Loans.Any())
        {
            var book = db.Books.FirstOrDefault(x => x.Title == "Atomic Habits");
            var reader = db.Readers.FirstOrDefault(x => x.Email == "minhanh@example.com");
            if (book != null && reader != null)
            {
                db.Loans.Add(new Loan { BookId = book.Id, ReaderId = reader.Id,
                                        BorrowDate = new DateTime(2026, 11, 1), DueDate = new DateTime(2026, 11, 22) });
                db.SaveChanges();
            }
        }

        if (!db.Posts.Any())
        {
            db.Posts.AddRange(
                new Post { Kind = PostKinds.Promotion, Title = "Gia hạn mượn sách miễn phí 7 ngày",
                           Content = "Trong tháng 11, mọi phiếu mượn được gia hạn thêm 7 ngày không tính phí. Liên hệ thủ thư để được hỗ trợ." },
                new Post { Kind = PostKinds.News, Title = "Thư viện mở cửa thêm buổi tối",
                           Content = "Từ tuần này thư viện mở cửa đến 21:00 các ngày trong tuần." });
            db.SaveChanges();
        }
    }
}
