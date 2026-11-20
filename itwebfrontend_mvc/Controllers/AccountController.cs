using System.Security.Claims;
using ItWebFrontend.Data;
using ItWebFrontend.Helpers;
using ItWebFrontend.Models;
using ItWebFrontend.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    public AccountController(AppDbContext db) => _db = db;

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim().ToLowerInvariant();
        var user = await _db.Readers.FirstOrDefaultAsync(r => r.Email == email);
        if (user == null || !Passwords.Verify(user.PasswordHash, vm.Password))
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");
            return View(vm);
        }
        if (!user.IsActive)
        {
            ModelState.AddModelError("", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
            return View(vm);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = vm.RememberMe });

        return Url.IsLocalUrl(vm.ReturnUrl) ? Redirect(vm.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");   // về Trang chủ (xem ẩn danh)
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // Chi tiết tài khoản của chính mình (nút "Chỉnh sửa" mở EditProfile)
    public async Task<IActionResult> Profile()
    {
        var uid = User.GetUserId();
        var reader = await _db.Readers.AsNoTracking().Include(r => r.Infos).FirstOrDefaultAsync(r => r.Id == uid);
        if (reader == null) return NotFound();

        var loans = await _db.Loans.AsNoTracking().Include(l => l.Book).Include(l => l.Reader)
            .Where(l => l.ReaderId == uid)
            .OrderByDescending(l => l.BorrowDate).ThenByDescending(l => l.Id).Take(10).ToListAsync();
        return View(new UserDetailsViewModel { Reader = reader, Loans = loans });
    }

    // ---------- Tự chỉnh sửa hồ sơ ----------
    // Chỉ sửa được: họ tên, số điện thoại, địa chỉ, thông tin bổ sung. Email/vai trò/trạng thái KHÔNG sửa ở đây.
    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var uid = User.GetUserId();
        var r = await _db.Readers.AsNoTracking().Include(x => x.Infos).FirstOrDefaultAsync(x => x.Id == uid);
        if (r == null) return NotFound();
        return View(new ProfileEditViewModel
        {
            Email = r.Email, Name = r.Name, Phone = r.Phone, Address = r.Address,
            Infos = r.Infos.OrderBy(i => i.Label).Select(i => new ProfileInfoItem { Label = i.Label, Value = i.Value }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(ProfileEditViewModel vm)
    {
        var uid = User.GetUserId();
        var reader = await _db.Readers.Include(x => x.Infos).FirstOrDefaultAsync(x => x.Id == uid);   // 1 truy vấn
        if (reader == null) return NotFound();
        vm.Email = reader.Email;
        vm.Infos ??= new List<ProfileInfoItem>();

        // Chuẩn hóa các dòng thông tin bổ sung: bỏ dòng trống, báo lỗi dòng thiếu / trùng tên
        var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < vm.Infos.Count; i++)
        {
            var label = vm.Infos[i].Label?.Trim() ?? "";
            var value = vm.Infos[i].Value?.Trim() ?? "";
            if (label.Length == 0 && value.Length == 0) continue;
            if (label.Length == 0 || value.Length == 0)
            {
                ModelState.AddModelError("", $"Thông tin bổ sung dòng {i + 1}: cần nhập cả tên và giá trị");
                continue;
            }
            if (!wanted.TryAdd(label, value))
                ModelState.AddModelError("", $"Tên thông tin \"{label}\" bị trùng");
        }
        if (!ModelState.IsValid) return View(vm);

        reader.Name = vm.Name.Trim();
        reader.Phone = vm.Phone.Trim();
        reader.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();

        // Đồng bộ thông tin bổ sung: giữ + cập nhật dòng còn, xóa dòng bị bỏ, thêm dòng mới
        foreach (var old in reader.Infos.ToList())
        {
            if (wanted.TryGetValue(old.Label, out var v)) { old.Value = v; wanted.Remove(old.Label); }
            else _db.ReaderInfos.Remove(old);
        }
        foreach (var (label, value) in wanted)
            reader.Infos.Add(new ReaderInfo { Label = label, Value = value });

        await _db.SaveChangesAsync();

        // Làm mới cookie để tên hiển thị trên header đổi ngay (giữ nguyên "ghi nhớ đăng nhập")
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, reader.Id.ToString()),
            new(ClaimTypes.Name, reader.Name),
            new(ClaimTypes.Email, reader.Email),
            new(ClaimTypes.Role, reader.Role)
        };
        var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            current?.Properties);

        TempData["Toast"] = "Đã cập nhật hồ sơ";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        var user = await _db.Readers.FindAsync(User.GetUserId());
        if (user == null) return NotFound();
        if (!Passwords.Verify(user.PasswordHash, vm.CurrentPassword))
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Mật khẩu hiện tại không đúng");
        if (!ModelState.IsValid) return View(vm);

        user.PasswordHash = Passwords.Hash(vm.NewPassword);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã đổi mật khẩu";
        return RedirectToAction(nameof(Profile));
    }
}
