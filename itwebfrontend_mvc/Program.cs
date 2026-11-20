using System.Security.Claims;
using ItWebFrontend.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
// Dùng SQLite trên cloud (Render), SQL Server khi dev local
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
if (connectionString.Contains("localdb", StringComparison.OrdinalIgnoreCase) ||
    Environment.GetEnvironmentVariable("USE_SQLITE") == "true")
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "data", "library.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
}

// Đăng nhập bằng cookie. Mặc định mọi trang yêu cầu đăng nhập; chỉ các action gắn [AllowAnonymous]
// (Trang chủ, danh mục sách, tin tức & khuyến mãi, đăng nhập) cho khách xem ẩn danh.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;

        // Khách (chưa đăng nhập) vào trang cần đăng nhập: KHÔNG ép sang trang đăng nhập ngay,
        // mà quay về Trang chủ kèm ?needLogin=1 để hiện popup hỏi "Đăng nhập" hay "Tiếp tục xem ẩn danh".
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            var basePath = ctx.Request.PathBase.Value ?? "";
            var url = basePath + "/?needLogin=1";
            if (HttpMethods.IsGet(ctx.Request.Method))
            {
                var original = basePath + ctx.Request.Path.Value + ctx.Request.QueryString.Value;
                url += "&returnUrl=" + Uri.EscapeDataString(original);
            }
            ctx.Response.Redirect(url);
            return Task.CompletedTask;
        };

        // Mỗi request kiểm tra tài khoản còn hoạt động và vai trò chưa đổi
        // (khóa tài khoản / đổi quyền có hiệu lực ngay)
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = int.TryParse(id, out var uid)
                ? await db.Readers.AsNoTracking().Where(r => r.Id == uid)
                    .Select(r => new { r.IsActive, r.Role }).FirstOrDefaultAsync()
                : null;
            if (user == null || !user.IsActive || user.Role != role)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync();
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

// Tự tạo database + bảng (nếu chưa có) và nạp dữ liệu mẫu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
        DbSeeder.Seed(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không kết nối/khởi tạo được SQL Server. Kiểm tra ConnectionStrings:DefaultConnection trong appsettings.json (nếu DB LibraryHub cũ tạo từ bản trước, hãy xóa DB rồi chạy lại)");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
