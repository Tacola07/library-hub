using ItWebFrontend.Data;
using ItWebFrontend.Models;
using ItWebFrontend.ViewModels;
using ItWebFrontend.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

public class LoansController : Controller
{
    private readonly AppDbContext _db;
    public LoansController(AppDbContext db) => _db = db;

    // Chỉ phiếu CHƯA trả
    public async Task<IActionResult> Index()
    {
        var loans = await MyLoans()
            .Where(l => l.ReturnedDate == null)
            .OrderBy(l => l.DueDate).ToListAsync();
        return View(loans);
    }

    // TẤT CẢ phiếu
    public async Task<IActionResult> History()
    {
        var loans = await MyLoans()
            .OrderByDescending(l => l.BorrowDate).ThenByDescending(l => l.Id).ToListAsync();
        return View(loans);
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create()
    {
        var vm = new LoanCreateViewModel();
        await FillListsAsync(vm);
        return View(vm);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LoanCreateViewModel vm)
    {
        if (vm.DueDate.Date < vm.BorrowDate.Date)
            ModelState.AddModelError(nameof(vm.DueDate), "Hạn trả không được trước ngày mượn");

        var book = await _db.Books.FindAsync(vm.BookId);
        if (book == null)
            ModelState.AddModelError(nameof(vm.BookId), "Sách không tồn tại");
        else
        {
            var borrowed = await _db.Loans.CountAsync(l => l.BookId == vm.BookId && l.ReturnedDate == null);
            if (book.Quantity - borrowed <= 0)
                ModelState.AddModelError(nameof(vm.BookId), "Sách đã hết, không thể cho mượn");
        }
        if (!await _db.Readers.AnyAsync(r => r.Id == vm.ReaderId))
            ModelState.AddModelError(nameof(vm.ReaderId), "Độc giả không tồn tại");

        if (!ModelState.IsValid)
        {
            await FillListsAsync(vm);
            return View(vm);
        }

        _db.Loans.Add(new Loan
        {
            BookId = vm.BookId,
            ReaderId = vm.ReaderId,
            BorrowDate = vm.BorrowDate.Date,
            DueDate = vm.DueDate.Date
        });
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã tạo phiếu mượn";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(int id)
    {
        var loan = await _db.Loans.FindAsync(id);
        if (loan == null) return NotFound();
        if (loan.ReturnedDate == null)
        {
            loan.ReturnedDate = DateTime.Today;
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Đã xác nhận trả sách";
        }
        return RedirectToAction(nameof(Index));
    }

    // Admin thấy tất cả phiếu; người dùng chỉ thấy phiếu của mình
    private IQueryable<Loan> MyLoans()
    {
        var q = _db.Loans.AsNoTracking().Include(l => l.Book).Include(l => l.Reader).AsQueryable();
        if (User.IsInRole(Roles.Admin)) return q;
        var uid = User.GetUserId();
        return q.Where(l => l.ReaderId == uid);
    }

    private async Task FillListsAsync(LoanCreateViewModel vm)
    {
        // Số bản còn lại = Quantity - số phiếu chưa trả
        var books = await _db.Books.AsNoTracking()
            .Select(b => new
            {
                b.Id, b.Title, b.Quantity,
                Borrowed = b.Loans.Count(l => l.ReturnedDate == null)
            })
            .OrderBy(b => b.Title).ToListAsync();

        vm.Books = books.Select(b => new SelectListItem(
            $"{b.Title} (còn {Math.Max(b.Quantity - b.Borrowed, 0)})", b.Id.ToString())).ToList();

        vm.Readers = await _db.Readers.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToListAsync();
    }
}
