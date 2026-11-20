using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItWebFrontend.Models;

// Tin tức / khuyến mãi do admin đăng
public class Post
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại"), StringLength(30)]
    public string Kind { get; set; } = PostKinds.Promotion;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề"), StringLength(200)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập nội dung"), StringLength(4000)]
    public string Content { get; set; } = "";

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ExpiresAt { get; set; }

    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public bool IsVisible => IsPublished && (ExpiresAt == null || ExpiresAt.Value.Date >= DateTime.Today);
}
