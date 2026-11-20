# LIBRARY HUB — ASP.NET Core MVC (bản giao diện cho Backend)

Frontend đã tách giao diện thành **Razor View tĩnh có dữ liệu mẫu**. Backend chỉ cần thay dữ liệu mẫu bằng Model và viết Controller thật.

## Chạy thử
`dotnet restore` → `dotnet run` → mở địa chỉ in ra trong Terminal. (Cần .NET SDK 8.)

## Cấu trúc View
| URL | File | Ghi chú |
|---|---|---|
| `/` | `Views/Home/Index.cshtml` | Banner, 4 thẻ thống kê, 5 sách mới |
| `/Books` | `Views/Books/Index.cshtml` | Bảng sách, tìm `?q=`, lọc `?category=` (thể loại) và `?author=` (tác giả) |
| `/Books/Create`, `/Books/Edit/1` | `Views/Books/Create.cshtml`, `Edit.cshtml` | Form sách |
| `/Readers` (+ Create/Edit) | `Views/Readers/*.cshtml` | Độc giả |
| `/Loans` | `Views/Loans/Index.cshtml` | Phiếu đang mượn, nút Xác nhận trả |
| `/Loans/Create` | `Views/Loans/Create.cshtml` | Tạo phiếu mượn |
| `/Loans/History` | `Views/Loans/History.cshtml` | Lịch sử mượn |
| `/Statistics` | `Views/Statistics/Index.cshtml` | Thống kê theo thể loại |
| `/Import` | `Views/Import/Index.cshtml`, `Result.cshtml` | (Admin) Nhập JSON gộp người dùng + sách + phiếu mượn |
| `/Account/Profile`, `/Account/EditProfile` | `Views/Account/*.cshtml` | Hồ sơ của tôi; nút **Chỉnh sửa** mở form tự sửa hồ sơ |

Dùng chung: `Views/Shared/_Layout.cshtml` (topbar + menu, tự active theo Controller) và `Views/Shared/_StatCards.cshtml` (4 thẻ thống kê).

## Backend (đã hoàn thiện)
Stack: ASP.NET Core 8 MVC + Entity Framework Core 8 + SQL Server.

### Cài đặt & chạy
1. Cài **.NET SDK 8** và một SQL Server (mặc định dùng **LocalDB** có sẵn khi cài Visual Studio — chỉ Windows).
2. Kiểm tra chuỗi kết nối trong `appsettings.json` (`ConnectionStrings:DefaultConnection`):
   - LocalDB: `Server=(localdb)\\MSSQLLocalDB;Database=LibraryHub;Trusted_Connection=True;TrustServerCertificate=True`
   - SQL Server Express: `Server=.\\SQLEXPRESS;Database=LibraryHub;Trusted_Connection=True;TrustServerCertificate=True`
   - SQL Server (Docker / user-pass): `Server=localhost,1433;Database=LibraryHub;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True`
3. `dotnet restore` → `dotnet run` → mở địa chỉ in ra trong Terminal.
4. Lần chạy đầu app **tự tạo database `LibraryHub`, các bảng và nạp dữ liệu mẫu** (`Data/DbSeeder.cs`). Muốn làm lại từ đầu: xóa database rồi chạy lại.
   **Lưu ý:** nếu đã chạy bản trước (chưa có đăng nhập/tin tức), phải xóa database `LibraryHub` cũ (VD `DROP DATABASE LibraryHub;`) vì `EnsureCreated()` không tự cập nhật bảng đã tồn tại.

### Cấu trúc
| Thư mục | Nội dung |
|---|---|
| `Models/` | `Book`, `Reader`, `Loan` (có validation tiếng Việt) |
| `Data/` | `AppDbContext`, `DbSeeder` |
| `ViewModels/` | `HomeViewModel`, `StatisticsViewModel`, `LibraryStats`, `LoanCreateViewModel` |
| `Controllers/` | Books, Readers, Loans, Statistics, Home, Posts, Account, Import — CRUD + tìm kiếm `?q=` |

### Đăng nhập & phân quyền
**Vào web là Trang chủ, không bị ép đăng nhập.** Khách (ẩn danh) xem được Trang chủ, danh mục sách (có lọc) và tin tức & khuyến mãi (nút chuông). Khi khách bấm vào chức năng cần tài khoản (Mượn / Trả sách, Lịch sử mượn, hoặc gõ thẳng URL trang cần đăng nhập) thì **hiện popup** hỏi *Đăng nhập* hay *Tiếp tục xem ẩn danh*; chỉ khi chọn *Đăng nhập* mới sang trang đăng nhập (và quay lại đúng trang đang định vào sau khi đăng nhập). Cơ chế: `FallbackPolicy` vẫn yêu cầu đăng nhập, các action cho khách gắn `[AllowAnonymous]` (Home/Index, Books/Index, Posts/Index, Posts/Latest, Account/Login); `Program.cs` đổi `OnRedirectToLogin` để chuyển về `/?needLogin=1&returnUrl=...` thay vì thẳng tới trang đăng nhập.

Tài khoản mẫu:

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | `admin@example.com` | `Admin@123` |
| Người dùng | `minhanh@example.com` / `giahuy@example.com` | `User@123` |

| Chức năng | Khách | Người dùng | Admin |
|---|---|---|---|
| Trang chủ, danh mục sách + lọc, tin & khuyến mãi (chuông) | ✔ | ✔ | ✔ |
| Mượn/Trả & Lịch sử, chi tiết tài khoản, đổi mật khẩu | ✘ (hỏi đăng nhập) | ✔ (chỉ phiếu của mình) | ✔ (tất cả phiếu) |
| Tự chỉnh sửa hồ sơ (họ tên, SĐT, địa chỉ, thông tin bổ sung) | ✘ | ✔ | ✔ |
| Thống kê, quản lý người dùng, nhập JSON | ✘ | ✘ | ✔ |
| Thêm / sửa / xóa sách, tạo phiếu mượn, xác nhận trả | ✘ | ✘ | ✔ |
| Đăng / sửa / xóa tin tức & khuyến mãi | ✘ | ✘ | ✔ |

Khóa tài khoản (bỏ chọn "đang hoạt động") hoặc đổi vai trò có hiệu lực ngay ở request kế tiếp. Admin không thể tự khóa / tự hạ quyền chính mình.

### Tự chỉnh sửa hồ sơ
*Tài khoản của tôi* → nút **Chỉnh sửa** (`/Account/EditProfile`). Người dùng tự sửa họ tên, số điện thoại, địa chỉ và các dòng thông tin bổ sung (thêm / sửa / xóa). Email, vai trò, trạng thái hoạt động **không** sửa được ở đây (chỉ admin ở *Quản lý người dùng*). Lưu xong, tên trên header cập nhật ngay.

### Nhập dữ liệu JSON (người dùng + sách + phiếu mượn)
Admin → menu **Nhập dữ liệu JSON** (`/Import`). Một file / một lần gửi cho cả ba loại (file `.json` ≤ 20 MB hoặc dán trực tiếp, mỗi mục tối đa 50.000 bản ghi/lần). File mẫu: `wwwroot/samples/import-sample.json`. Các trang Người dùng / Sách / Phiếu mượn **không** còn nút nhập JSON riêng.

File là một object; mục nào không cần thì bỏ:
```json
{ "users": [ ... ], "books": [ ... ], "loans": [ ... ] }
```
- `users`: bắt buộc `name`, `email`, `phone`; tùy chọn `address`, `role` (`User`/`Admin`), `isActive`, `password` (≥ 6 ký tự; trống = `Thuvien@123`), `info` (cặp tên–giá trị). Email trùng bị bỏ qua.
- `books`: bắt buộc `title`, `author`, `category`; tùy chọn `quantity` (mặc định 1), `coverUrl`. Trùng cả tên + tác giả với sách đã có thì bị bỏ qua.
- `loans`: sách theo `bookId` hoặc `bookTitle` (+ `bookAuthor` nếu trùng tên); người mượn theo `readerId` hoặc `readerEmail`; tùy chọn `borrowDate` (mặc định hôm nay), `dueDate` (mặc định +14 ngày), `returnedDate` (trống = chưa trả). Ngày dạng `yyyy-MM-dd` hoặc `dd/MM/yyyy`. Phiếu chưa trả cho sách đã hết bản bị từ chối.
- Xử lý theo thứ tự users → books → loans, nên phiếu mượn có thể tham chiếu người dùng/sách vừa nhập trong cùng file. Trang kết quả liệt kê số đã nhập / bị bỏ qua và lý do từng dòng.

**Giảm truy cập CSDL:** mỗi bảng tra cứu (Readers, Books) chỉ tải 1 lần cho cả lần nhập (và chỉ khi file cần), kiểm tra trùng / tồn kho / tham chiếu làm trong bộ nhớ, ghi theo lô 1.000 bản ghi trong **một transaction** (lỗi CSDL thì hoàn tác toàn bộ).

### Tin tức & khuyến mãi
Không hiển thị ở Trang chủ nữa: bấm **nút chuông** trên header để mở cửa sổ nhỏ với tối đa 6 tin mới nhất (nội dung tải khi bấm lần đầu từ `/Posts/Latest`, nên các trang khác không tốn truy vấn). Cuối cửa sổ có *Xem tất cả* (`/Posts`); admin thấy thêm nút **+ Đăng tin / khuyến mãi** và có mục *Tin & khuyến mãi* trong menu để Sửa / Xóa. Bài hết hạn hoặc nháp tự ẩn với người dùng thường.

### Quy tắc nghiệp vụ
- Số bản còn lại của sách = `Quantity` − số phiếu chưa trả; hết sách thì không cho mượn.
- Hạn trả không được trước ngày mượn.
- Không xóa được sách / độc giả đã có phiếu mượn; email độc giả là duy nhất.
- Không cho giảm `Quantity` thấp hơn số bản đang được mượn.
- Trạng thái phiếu: *Đã trả* / *Quá hạn* (hạn trả < hôm nay) / *Đang mượn*.

## JS (`wwwroot/js/app.js`)
Chỉ lo giao diện: toast, Ctrl+K, hỏi xác nhận trước khi Xóa (`data-confirm`), lọc bảng tức thời (`data-filter`), cửa sổ chuông, popup hỏi đăng nhập cho khách, tự gửi form khi đổi ô lọc (`data-autosubmit`), thêm/xóa dòng thông tin hồ sơ. Không lưu dữ liệu bằng localStorage (chỉ cờ ẩn chấm đỏ của chuông trong sessionStorage).
