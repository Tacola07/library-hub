using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ItWebFrontend.Models;

// Thông tin bổ sung tùy ý của người dùng (admin thêm / sửa / xóa)
public class ReaderInfo
{
    public int Id { get; set; }
    public int ReaderId { get; set; }

    [Required, StringLength(100)] public string Label { get; set; } = "";
    [Required, StringLength(500)] public string Value { get; set; } = "";

    [ValidateNever] public Reader Reader { get; set; } = null!;
}
