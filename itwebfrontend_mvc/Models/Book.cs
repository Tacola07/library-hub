using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ItWebFrontend.Models;

public class Book
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sách"), StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tác giả"), StringLength(150)]
    public string Author { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập thể loại"), StringLength(100)]
    public string Category { get; set; } = "";

    [Range(0, 100000, ErrorMessage = "Số lượng phải từ 0 trở lên")]
    public int Quantity { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập đường dẫn ảnh bìa"), StringLength(300)]
    public string CoverUrl { get; set; } = "/assets/cover-1.webp";

    [ValidateNever]
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
