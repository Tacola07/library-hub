using ItWebFrontend.Data;
using ItWebFrontend.Models;
using ItWebFrontend.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    // Trang chủ: khách (chưa đăng nhập) cũng xem được. Tin tức & khuyến mãi nằm ở chuông trên header.
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            // Thống kê thư viện chỉ dành cho admin
            Stats = User.IsInRole(Roles.Admin) ? await StatisticsController.BuildStatsAsync(_db) : null,
            NewBooks = await _db.Books.AsNoTracking().OrderByDescending(b => b.Id).Take(5).ToListAsync()
        };
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => Content("Có lỗi xảy ra. Vui lòng thử lại.");
}
