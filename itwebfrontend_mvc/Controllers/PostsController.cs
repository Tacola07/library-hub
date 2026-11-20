using ItWebFrontend.Data;
using ItWebFrontend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

// Tin tức & khuyến mãi: mọi người xem, chỉ admin đăng / sửa / xóa
public class PostsController : Controller
{
    private readonly AppDbContext _db;
    public PostsController(AppDbContext db) => _db = db;

    // Khách xem được
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var query = _db.Posts.AsNoTracking().AsQueryable();
        if (!User.IsInRole(Roles.Admin))
        {
            var today = DateTime.Today;
            query = query.Where(p => p.IsPublished && (p.ExpiresAt == null || p.ExpiresAt >= today));
        }
        return View(await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync());
    }

    // Danh sách rút gọn cho cửa sổ nhỏ của nút chuông (header). Trả HTML partial, JS chỉ tải khi bấm chuông.
    [AllowAnonymous]
    public async Task<IActionResult> Latest()
    {
        var today = DateTime.Today;
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && (p.ExpiresAt == null || p.ExpiresAt >= today))
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(6).ToListAsync();
        return PartialView("_BellList", posts);
    }

    [Authorize(Roles = Roles.Admin)]
    public IActionResult Create() => View(new Post());

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Kind,Title,Content,ImageUrl,ExpiresAt,IsPublished")] Post post)
    {
        if (!PostKinds.All.Contains(post.Kind)) ModelState.AddModelError(nameof(post.Kind), "Loại không hợp lệ");
        if (!ModelState.IsValid) return View(post);

        post.CreatedAt = DateTime.Now;
        post.ImageUrl = string.IsNullOrWhiteSpace(post.ImageUrl) ? null : post.ImageUrl.Trim();
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã đăng";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        return post == null ? NotFound() : View(post);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Kind,Title,Content,ImageUrl,ExpiresAt,IsPublished")] Post input)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null) return NotFound();
        if (!PostKinds.All.Contains(input.Kind)) ModelState.AddModelError(nameof(input.Kind), "Loại không hợp lệ");
        if (!ModelState.IsValid) { input.Id = id; return View(input); }

        post.Kind = input.Kind;
        post.Title = input.Title;
        post.Content = input.Content;
        post.ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl.Trim();
        post.ExpiresAt = input.ExpiresAt;
        post.IsPublished = input.IsPublished;
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã lưu";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null) return NotFound();
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã xóa";
        return RedirectToAction(nameof(Index));
    }
}
