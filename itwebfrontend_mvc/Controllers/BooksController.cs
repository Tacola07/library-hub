using ItWebFrontend.Data;
using ItWebFrontend.Models;
using ItWebFrontend.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

public class BooksController : Controller
{
    private readonly AppDbContext _db;
    public BooksController(AppDbContext db) => _db = db;

    // Khách xem ẩn danh được. Lọc theo từ khóa ?q=, thể loại ?category=, tác giả ?author=
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? q, string? category, string? author)
    {
        q = q?.Trim(); category = category?.Trim(); author = author?.Trim();

        var query = _db.Books.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(b => b.Title.Contains(q) || b.Author.Contains(q) || b.Category.Contains(q));
        if (!string.IsNullOrEmpty(category)) query = query.Where(b => b.Category == category);
        if (!string.IsNullOrEmpty(author)) query = query.Where(b => b.Author == author);

        // Một truy vấn duy nhất lấy các cặp (thể loại, tác giả) để dựng cả 2 ô lọc.
        // Khi đã chọn thể loại thì ô tác giả chỉ liệt kê tác giả thuộc thể loại đó.
        var pairs = await _db.Books.AsNoTracking().Select(b => new { b.Category, b.Author }).Distinct().ToListAsync();
        var categories = pairs.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        var authors = pairs
            .Where(p => string.IsNullOrEmpty(category) || string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Author).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();

        return View(new BookListViewModel
        {
            Items = await query.OrderBy(b => b.Id).ToListAsync(),
            Categories = categories,
            Authors = authors,
            Query = q, Category = category, Author = author
        });
    }

    [Authorize(Roles = Roles.Admin)]
    public IActionResult Create() => View(new Book());

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Author,Category,Quantity,CoverUrl")] Book book)
    {
        if (!ModelState.IsValid) return View(book);
        _db.Books.Add(book);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã lưu sách";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var book = await _db.Books.FindAsync(id);
        return book == null ? NotFound() : View(book);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Author,Category,Quantity,CoverUrl")] Book book)
    {
        if (id != book.Id) return NotFound();
        if (!await _db.Books.AnyAsync(b => b.Id == id)) return NotFound();

        // Không cho giảm số lượng thấp hơn số bản đang được mượn
        var borrowed = await _db.Loans.CountAsync(l => l.BookId == id && l.ReturnedDate == null);
        if (book.Quantity < borrowed)
            ModelState.AddModelError(nameof(Book.Quantity), $"Đang có {borrowed} bản được mượn, số lượng không thể nhỏ hơn.");

        if (!ModelState.IsValid) return View(book);
        _db.Update(book);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã lưu sách";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var book = await _db.Books.FindAsync(id);
        if (book == null) return NotFound();
        if (await _db.Loans.AnyAsync(l => l.BookId == id))
        {
            TempData["Toast"] = "Không thể xóa: sách đã có phiếu mượn";
            return RedirectToAction(nameof(Index));
        }
        _db.Books.Remove(book);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã xóa sách";
        return RedirectToAction(nameof(Index));
    }
}
