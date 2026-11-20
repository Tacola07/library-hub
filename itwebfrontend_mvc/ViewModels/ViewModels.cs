using System.ComponentModel.DataAnnotations;
using ItWebFrontend.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ItWebFrontend.ViewModels;

// 4 thẻ thống kê dùng chung (partial _StatCards) — chỉ admin được xem
public class LibraryStats
{
    public int TotalBooks { get; set; }     // tổng số bản sách (SUM Quantity)
    public int TotalReaders { get; set; }
    public int Borrowing { get; set; }      // phiếu chưa trả (gồm cả quá hạn)
    public int Overdue { get; set; }
}

public class HomeViewModel
{
    public LibraryStats? Stats { get; set; }          // null nếu không phải admin
    public List<Book> NewBooks { get; set; } = new();
}

// Trang danh sách sách có bộ lọc thể loại / tác giả
public class BookListViewModel
{
    public List<Book> Items { get; set; } = new();
    public List<string> Categories { get; set; } = new();   // lựa chọn ô "Thể loại"
    public List<string> Authors { get; set; } = new();      // lựa chọn ô "Tác giả" (theo thể loại đang chọn)
    public string? Query { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public bool HasFilter => !string.IsNullOrEmpty(Query) || !string.IsNullOrEmpty(Category) || !string.IsNullOrEmpty(Author);
}

public class CategoryStat
{
    public string Category { get; set; } = "";
    public int Copies { get; set; }
}

public class StatisticsViewModel
{
    public LibraryStats Stats { get; set; } = new();
    public List<CategoryStat> ByCategory { get; set; } = new();
}

public class LoanCreateViewModel
{
    [Display(Name = "Sách")]
    public int BookId { get; set; }

    [Display(Name = "Độc giả")]
    public int ReaderId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày mượn")]
    [DataType(DataType.Date)]
    public DateTime BorrowDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Vui lòng chọn hạn trả")]
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);

    [ValidateNever] public List<SelectListItem> Books { get; set; } = new();
    [ValidateNever] public List<SelectListItem> Readers { get; set; } = new();
}

// ----- Tài khoản / người dùng -----
public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 ký tự")]
    public string NewPassword { get; set; } = "";

    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    public string ConfirmPassword { get; set; } = "";
}

public class UserDetailsViewModel
{
    public Reader Reader { get; set; } = null!;
    public List<Loan> Loans { get; set; } = new();
}

// Form thêm / sửa người dùng (admin)
public class ReaderFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(150)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(200)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"),
     RegularExpression(@"^\+?[0-9\s.\-]{8,15}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string Phone { get; set; } = "";

    [StringLength(250)]
    public string? Address { get; set; }

    [Required]
    public string Role { get; set; } = Roles.User;

    public bool IsActive { get; set; } = true;

    // Để trống: khi tạo mới dùng mật khẩu mặc định; khi sửa giữ nguyên mật khẩu cũ
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự")]
    public string? Password { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int Total { get; set; }
    public string? Query { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

// ----- Nhập JSON gộp (người dùng + sách + phiếu mượn) -----
public class ImportSectionResult
{
    public string Label { get; set; } = "";
    public bool Present { get; set; }          // file JSON có mục này không
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Skipped => Total - Imported;
    public string? Note { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool ErrorsTruncated { get; set; }
}

public class ImportReportViewModel
{
    public ImportSectionResult Users { get; set; } = new() { Label = "Người dùng" };
    public ImportSectionResult Books { get; set; } = new() { Label = "Sách" };
    public ImportSectionResult Loans { get; set; } = new() { Label = "Phiếu mượn" };
}

// ----- Người dùng tự chỉnh sửa hồ sơ -----
public class ProfileInfoItem
{
    [StringLength(100, ErrorMessage = "Tên thông tin tối đa 100 ký tự")]
    public string? Label { get; set; }

    [StringLength(500, ErrorMessage = "Giá trị tối đa 500 ký tự")]
    public string? Value { get; set; }
}

public class ProfileEditViewModel
{
    // Chỉ để hiển thị (email dùng để đăng nhập, không sửa ở đây)
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(150)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"),
     RegularExpression(@"^\+?[0-9\s.\-]{8,15}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string Phone { get; set; } = "";

    [StringLength(250)]
    public string? Address { get; set; }

    public List<ProfileInfoItem> Infos { get; set; } = new();
}
