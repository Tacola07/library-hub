using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ItWebFrontend.Models;

// Reader = người dùng thư viện đồng thời là tài khoản đăng nhập (Role: Admin / User)
public class Reader
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

    [StringLength(20), ValidateNever]
    public string Role { get; set; } = Roles.User;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ValidateNever] public string PasswordHash { get; set; } = "";

    [ValidateNever] public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    [ValidateNever] public ICollection<ReaderInfo> Infos { get; set; } = new List<ReaderInfo>();

    [NotMapped] public bool IsAdmin => Role == Roles.Admin;
}
