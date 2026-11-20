using ItWebFrontend.Data;
using ItWebFrontend.Helpers;
using ItWebFrontend.Models;
using ItWebFrontend.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

// Quản lý người dùng: chỉ admin (nhập JSON nằm ở trang "Nhập dữ liệu" riêng – ImportController)
[Authorize(Roles = Roles.Admin)]
public class ReadersController : Controller
{
    private const int PageSize = 50;

    private readonly AppDbContext _db;
    public ReadersController(AppDbContext db) => _db = db;

    private static string NormEmail(string email) => email.Trim().ToLowerInvariant();

    // ---------- Danh sách (có phân trang cho số lượng lớn) ----------
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = _db.Readers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(r => r.Name.Contains(q) || r.Email.Contains(q) || r.Phone.Contains(q));
        }
        page = Math.Max(1, page);
        var total = await query.CountAsync();
        var items = await query.OrderBy(r => r.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(new PagedResult<Reader> { Items = items, Page = page, PageSize = PageSize, Total = total, Query = q });
    }

    // ---------- Chi tiết + thêm/bớt thông tin ----------
    public async Task<IActionResult> Details(int id)
    {
        var reader = await _db.Readers.AsNoTracking().Include(r => r.Infos).FirstOrDefaultAsync(r => r.Id == id);
        if (reader == null) return NotFound();
        var loans = await _db.Loans.AsNoTracking().Include(l => l.Book).Include(l => l.Reader)
            .Where(l => l.ReaderId == id)
            .OrderByDescending(l => l.BorrowDate).ThenByDescending(l => l.Id).Take(10).ToListAsync();
        return View(new UserDetailsViewModel { Reader = reader, Loans = loans });
    }

    // Thêm thông tin; nếu nhãn đã có thì cập nhật giá trị
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInfo(int id, string? label, string? value)
    {
        label = label?.Trim(); value = value?.Trim();
        if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value))
        {
            TempData["Toast"] = "Vui lòng nhập cả tên thông tin và giá trị";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (label.Length > 100 || value.Length > 500)
        {
            TempData["Toast"] = "Nội dung quá dài (tên ≤ 100, giá trị ≤ 500 ký tự)";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!await _db.Readers.AnyAsync(r => r.Id == id)) return NotFound();

        var existing = await _db.ReaderInfos.FirstOrDefaultAsync(i => i.ReaderId == id && i.Label == label);
        if (existing != null) existing.Value = value;
        else _db.ReaderInfos.Add(new ReaderInfo { ReaderId = id, Label = label, Value = value });
        await _db.SaveChangesAsync();
        TempData["Toast"] = existing != null ? "Đã cập nhật thông tin" : "Đã thêm thông tin";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveInfo(int id, int infoId)
    {
        var info = await _db.ReaderInfos.FirstOrDefaultAsync(i => i.Id == infoId && i.ReaderId == id);
        if (info == null) return NotFound();
        _db.ReaderInfos.Remove(info);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã xóa thông tin";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Thêm ----------
    public IActionResult Create() => View(new ReaderFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReaderFormViewModel vm)
    {
        var email = NormEmail(vm.Email ?? "");
        if (!Roles.IsValid(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Vai trò không hợp lệ");
        if (await _db.Readers.AnyAsync(r => r.Email == email))
            ModelState.AddModelError(nameof(vm.Email), "Email này đã tồn tại");
        if (!ModelState.IsValid) return View(vm);

        var reader = new Reader
        {
            Name = vm.Name.Trim(), Email = email, Phone = vm.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim(),
            Role = vm.Role, IsActive = vm.IsActive,
            PasswordHash = Passwords.Hash(string.IsNullOrEmpty(vm.Password) ? Passwords.DefaultPassword : vm.Password)
        };
        _db.Readers.Add(reader);
        await _db.SaveChangesAsync();
        TempData["Toast"] = string.IsNullOrEmpty(vm.Password)
            ? $"Đã tạo người dùng (mật khẩu mặc định: {Passwords.DefaultPassword})" : "Đã tạo người dùng";
        return RedirectToAction(nameof(Details), new { id = reader.Id });
    }

    // ---------- Sửa ----------
    public async Task<IActionResult> Edit(int id)
    {
        var r = await _db.Readers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        return View(new ReaderFormViewModel
        {
            Id = r.Id, Name = r.Name, Email = r.Email, Phone = r.Phone,
            Address = r.Address, Role = r.Role, IsActive = r.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ReaderFormViewModel vm)
    {
        var reader = await _db.Readers.FindAsync(id);
        if (reader == null) return NotFound();

        var email = NormEmail(vm.Email ?? "");
        if (!Roles.IsValid(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Vai trò không hợp lệ");
        if (await _db.Readers.AnyAsync(r => r.Email == email && r.Id != id))
            ModelState.AddModelError(nameof(vm.Email), "Email này đã tồn tại");
        // Tránh admin tự khóa / tự hạ quyền chính mình
        if (id == User.GetUserId() && (vm.Role != Roles.Admin || !vm.IsActive))
            ModelState.AddModelError(nameof(vm.Role), "Không thể tự hạ quyền hoặc khóa chính tài khoản đang đăng nhập");
        if (!ModelState.IsValid) { vm.Id = id; return View(vm); }

        reader.Name = vm.Name.Trim();
        reader.Email = email;
        reader.Phone = vm.Phone.Trim();
        reader.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();
        reader.Role = vm.Role;
        reader.IsActive = vm.IsActive;
        if (!string.IsNullOrEmpty(vm.Password)) reader.PasswordHash = Passwords.Hash(vm.Password);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã lưu thông tin người dùng";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Xóa ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var reader = await _db.Readers.FindAsync(id);
        if (reader == null) return NotFound();
        if (id == User.GetUserId())
        {
            TempData["Toast"] = "Không thể xóa tài khoản đang đăng nhập";
            return RedirectToAction(nameof(Index));
        }
        if (await _db.Loans.AnyAsync(l => l.ReaderId == id))
        {
            TempData["Toast"] = "Không thể xóa: người dùng đã có phiếu mượn (hãy khóa tài khoản thay vì xóa)";
            return RedirectToAction(nameof(Index));
        }
        _db.Readers.Remove(reader);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã xóa người dùng";
        return RedirectToAction(nameof(Index));
    }
}
