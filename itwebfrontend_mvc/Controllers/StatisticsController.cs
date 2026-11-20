using ItWebFrontend.Data;
using ItWebFrontend.ViewModels;
using ItWebFrontend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

[Authorize(Roles = Roles.Admin)]
public class StatisticsController : Controller
{
    private readonly AppDbContext _db;
    public StatisticsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var vm = new StatisticsViewModel
        {
            Stats = await BuildStatsAsync(_db),
            ByCategory = await _db.Books.AsNoTracking()
                .GroupBy(b => b.Category)
                .Select(g => new CategoryStat { Category = g.Key, Copies = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.Category)
                .ToListAsync()
        };
        return View(vm);
    }

    // Dùng chung cho Trang chủ và Thống kê
    public static async Task<LibraryStats> BuildStatsAsync(AppDbContext db)
    {
        var today = DateTime.Today;
        return new LibraryStats
        {
            TotalBooks = await db.Books.SumAsync(b => (int?)b.Quantity) ?? 0,
            TotalReaders = await db.Readers.CountAsync(),
            Borrowing = await db.Loans.CountAsync(l => l.ReturnedDate == null),
            Overdue = await db.Loans.CountAsync(l => l.ReturnedDate == null && l.DueDate < today)
        };
    }
}
