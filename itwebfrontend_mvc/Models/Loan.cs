using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ItWebFrontend.Models;

public class Loan
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int ReaderId { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnedDate { get; set; }

    [ValidateNever] public Book Book { get; set; } = null!;
    [ValidateNever] public Reader Reader { get; set; } = null!;

    [NotMapped] public bool IsReturned => ReturnedDate != null;
    [NotMapped] public bool IsOverdue => !IsReturned && DueDate.Date < DateTime.Today;
    [NotMapped] public string Status => IsReturned ? "Đã trả" : IsOverdue ? "Quá hạn" : "Đang mượn";
}
