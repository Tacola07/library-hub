using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ItWebFrontend.Data;
using ItWebFrontend.Helpers;
using ItWebFrontend.Models;
using ItWebFrontend.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Controllers;

// Trang "Nhập dữ liệu JSON" duy nhất cho admin: người dùng + sách + phiếu mượn trong MỘT file / MỘT lần gửi.
// Để giảm truy cập CSDL tối đa:
//   - bảng tra cứu (Readers, Books) chỉ được tải 1 lần cho cả lần nhập, và chỉ khi file có mục cần dùng;
//   - kiểm tra trùng / tồn kho / tham chiếu sách–người dùng đều làm trong bộ nhớ;
//   - ghi theo lô (1.000 bản ghi / lần SaveChanges) trong 1 transaction duy nhất (lỗi CSDL => hoàn tác tất cả).
[Authorize(Roles = Roles.Admin)]
public class ImportController : Controller
{
    private const int MaxRowsPerSection = 50_000;
    private const int BatchSize = 1000;
    private const int MaxErrorsShown = 100;
    private const string DefaultCover = "/assets/cover-1.webp";
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s.\-]{8,15}$");
    private static readonly EmailAddressAttribute EmailCheck = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "d/M/yyyy", "yyyy-MM-dd'T'HH:mm:ss" };

    private readonly AppDbContext _db;
    public ImportController(AppDbContext db) => _db = db;

    // ---------- DTO đọc từ JSON ----------
    private class UserDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; }
        public bool? IsActive { get; set; }
        public Dictionary<string, JsonElement>? Info { get; set; }
    }

    private class BookDto
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Category { get; set; }
        public int? Quantity { get; set; }
        public string? CoverUrl { get; set; }
    }

    private class LoanDto
    {
        public int? BookId { get; set; }
        public string? BookTitle { get; set; }
        public string? BookAuthor { get; set; }      // tùy chọn: phân biệt các sách trùng tên
        public int? ReaderId { get; set; }
        public string? ReaderEmail { get; set; }
        public string? BorrowDate { get; set; }       // yyyy-MM-dd hoặc dd/MM/yyyy (mặc định: hôm nay)
        public string? DueDate { get; set; }          // mặc định: ngày mượn + 14 ngày
        public string? ReturnedDate { get; set; }     // bỏ trống = chưa trả
    }

    // Bản tóm tắt sách trong bộ nhớ (sách có sẵn + sách mới nhập) để tra cứu & đếm tồn kho
    private class BookRef
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public int Quantity { get; set; }
        public int Borrowed { get; set; }              // số phiếu chưa trả (kể cả phiếu mới trong lần nhập này)
        public Book? NewEntity { get; set; }           // != null nếu là sách vừa nhập (Id có sau khi lưu)
    }

    public IActionResult Index() => View();

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000), RequestFormLimits(MultipartBodyLengthLimit = 50_000_000, ValueLengthLimit = 50_000_000)]
    public async Task<IActionResult> Index(IFormFile? file, string? json)
    {
        // ----- 1) Đọc nội dung -----
        string text;
        if (file != null && file.Length > 0)
        {
            if (file.Length > 20_000_000)
            {
                TempData["Toast"] = "File quá lớn (tối đa 20 MB). Hãy chia nhỏ file.";
                return RedirectToAction(nameof(Index));
            }
            using var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            text = await sr.ReadToEndAsync();
        }
        else if (!string.IsNullOrWhiteSpace(json)) text = json;
        else
        {
            TempData["Toast"] = "Vui lòng chọn file .json hoặc dán nội dung JSON";
            return RedirectToAction(nameof(Index));
        }

        // ----- 2) Phân tích JSON: object có các mảng "users", "books", "loans" (mục nào cũng tùy chọn) -----
        List<UserDto>? users;
        List<BookDto>? books;
        List<LoanDto>? loans;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                TempData["Toast"] = "JSON phải là một object có các mảng \"users\", \"books\", \"loans\"";
                return RedirectToAction(nameof(Index));
            }
            users = ReadSection<UserDto>(root, "users");
            books = ReadSection<BookDto>(root, "books");
            loans = ReadSection<LoanDto>(root, "loans");
        }
        catch (JsonException ex)
        {
            TempData["Toast"] = "JSON không hợp lệ: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }

        var nUsers = users?.Count ?? 0;
        var nBooks = books?.Count ?? 0;
        var nLoans = loans?.Count ?? 0;
        if (nUsers + nBooks + nLoans == 0)
        {
            TempData["Toast"] = "Không tìm thấy dữ liệu: cần ít nhất một mảng \"users\", \"books\" hoặc \"loans\" có phần tử";
            return RedirectToAction(nameof(Index));
        }
        if (nUsers > MaxRowsPerSection || nBooks > MaxRowsPerSection || nLoans > MaxRowsPerSection)
        {
            TempData["Toast"] = $"Tối đa {MaxRowsPerSection:N0} bản ghi mỗi mục trong một lần nhập";
            return RedirectToAction(nameof(Index));
        }

        var report = new ImportReportViewModel();
        report.Users.Present = users != null;
        report.Books.Present = books != null;
        report.Loans.Present = loans != null;

        // ----- 3) Xử lý trong 1 transaction -----
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Tra cứu: mỗi bảng tải đúng 1 lần, và chỉ khi cần
            var readerIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // email -> Id
            if (nUsers > 0 || nLoans > 0)
                readerIds = await _db.Readers.AsNoTracking().Select(r => new { r.Email, r.Id })
                    .ToDictionaryAsync(x => x.Email, x => x.Id, StringComparer.OrdinalIgnoreCase);

            var byId = new Dictionary<int, BookRef>();
            var byTitle = new Dictionary<string, List<BookRef>>(StringComparer.OrdinalIgnoreCase);
            var bookKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);             // "tên|tác giả"
            void RegisterBook(BookRef b)
            {
                if (b.Id > 0) byId[b.Id] = b;
                if (!byTitle.TryGetValue(b.Title, out var list)) byTitle[b.Title] = list = new List<BookRef>();
                list.Add(b);
                bookKeys.Add(b.Title + "|" + b.Author);
            }
            // Sách vừa nhập: bookKeys đã được thêm lúc kiểm tra trùng, ở đây chỉ bổ sung vào bảng tra cứu (sau khi có Id)
            void RegisterNewBook(BookRef b)
            {
                byId[b.Id] = b;
                if (!byTitle.TryGetValue(b.Title, out var list)) byTitle[b.Title] = list = new List<BookRef>();
                list.Add(b);
            }
            if (nBooks > 0 || nLoans > 0)
            {
                var rows = await _db.Books.AsNoTracking()
                    .Select(b => new
                    {
                        b.Id, b.Title, b.Author, b.Quantity,
                        Borrowed = b.Loans.Count(l => l.ReturnedDate == null)
                    }).ToListAsync();
                foreach (var r in rows)
                    RegisterBook(new BookRef { Id = r.Id, Title = r.Title, Author = r.Author, Quantity = r.Quantity, Borrowed = r.Borrowed });
            }

            // ===== NGƯỜI DÙNG =====
            if (nUsers > 0)
            {
                var sec = report.Users;
                sec.Total = nUsers;
                var emails = new HashSet<string>(readerIds.Keys, StringComparer.OrdinalIgnoreCase);
                var defaultHash = Passwords.Hash(Passwords.DefaultPassword);   // băm 1 lần cho mọi user không có mật khẩu riêng
                var usedDefault = 0;
                var pending = new List<Reader>();

                var rowsIn = users!;
                for (var i = 0; i < rowsIn.Count; i++)
                {
                    var r = rowsIn[i];
                    if (r == null) { Err(sec, i, null, "bản ghi rỗng"); continue; }
                    var name = r.Name?.Trim();
                    var email = r.Email?.Trim().ToLowerInvariant();
                    var phone = r.Phone?.Trim();
                    var role = string.IsNullOrWhiteSpace(r.Role) ? Roles.User : r.Role.Trim();
                    if (role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)) role = Roles.Admin;
                    else if (role.Equals(Roles.User, StringComparison.OrdinalIgnoreCase)) role = Roles.User;

                    if (string.IsNullOrEmpty(name) || name.Length > 150) { Err(sec, i, email, "thiếu hoặc quá dài trường name"); continue; }
                    if (string.IsNullOrEmpty(email) || email.Length > 200 || !EmailCheck.IsValid(email)) { Err(sec, i, email, "email không hợp lệ"); continue; }
                    if (string.IsNullOrEmpty(phone) || !PhoneRegex.IsMatch(phone)) { Err(sec, i, email, "phone không hợp lệ (cần chuỗi 8–15 chữ số)"); continue; }
                    if (!Roles.IsValid(role)) { Err(sec, i, email, "role phải là Admin hoặc User"); continue; }
                    if (r.Address is { Length: > 250 }) { Err(sec, i, email, "address quá dài"); continue; }
                    if (!string.IsNullOrEmpty(r.Password) && r.Password.Length < 6) { Err(sec, i, email, "password phải từ 6 ký tự"); continue; }
                    if (!emails.Add(email)) { Err(sec, i, email, "email đã tồn tại (trong hệ thống hoặc trùng trong file)"); continue; }

                    var hasOwnPassword = !string.IsNullOrEmpty(r.Password);
                    if (!hasOwnPassword) usedDefault++;

                    var reader = new Reader
                    {
                        Name = name, Email = email, Phone = phone,
                        Address = string.IsNullOrWhiteSpace(r.Address) ? null : r.Address.Trim(),
                        Role = role, IsActive = r.IsActive ?? true,
                        PasswordHash = hasOwnPassword ? Passwords.Hash(r.Password!) : defaultHash
                    };
                    if (r.Info != null)
                        foreach (var (label, el) in r.Info)
                        {
                            var l = label.Trim();
                            var v = (el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText())?.Trim();
                            if (l.Length is 0 or > 100 || string.IsNullOrEmpty(v) || v.Length > 500) continue;
                            reader.Infos.Add(new ReaderInfo { Label = l, Value = v });
                        }
                    pending.Add(reader);
                }

                await SaveInBatchesAsync(pending);
                foreach (var rd in pending) readerIds[rd.Email] = rd.Id;
                sec.Imported = pending.Count;
                if (usedDefault > 0)
                    sec.Note = $"{usedDefault} người dùng không có password nên dùng mật khẩu mặc định {Passwords.DefaultPassword}";
            }

            // ===== SÁCH =====
            if (nBooks > 0)
            {
                var sec = report.Books;
                sec.Total = nBooks;
                var pending = new List<Book>();
                var pendingRefs = new List<BookRef>();

                var rowsIn = books!;
                for (var i = 0; i < rowsIn.Count; i++)
                {
                    var r = rowsIn[i];
                    if (r == null) { Err(sec, i, null, "bản ghi rỗng"); continue; }
                    var title = r.Title?.Trim();
                    var author = r.Author?.Trim();
                    var category = r.Category?.Trim();
                    var qty = r.Quantity ?? 1;
                    var cover = string.IsNullOrWhiteSpace(r.CoverUrl) ? DefaultCover : r.CoverUrl.Trim();

                    if (string.IsNullOrEmpty(title) || title.Length > 200) { Err(sec, i, title, "thiếu hoặc quá dài trường title (≤ 200)"); continue; }
                    if (string.IsNullOrEmpty(author) || author.Length > 150) { Err(sec, i, title, "thiếu hoặc quá dài trường author (≤ 150)"); continue; }
                    if (string.IsNullOrEmpty(category) || category.Length > 100) { Err(sec, i, title, "thiếu hoặc quá dài trường category (≤ 100)"); continue; }
                    if (qty < 0 || qty > 100000) { Err(sec, i, title, "quantity phải từ 0 đến 100000"); continue; }
                    if (cover.Length > 300) { Err(sec, i, title, "coverUrl quá dài (≤ 300)"); continue; }
                    if (!bookKeys.Add(title + "|" + author)) { Err(sec, i, title, "sách (cùng tên + tác giả) đã tồn tại"); continue; }

                    var entity = new Book { Title = title, Author = author, Category = category, Quantity = qty, CoverUrl = cover };
                    pending.Add(entity);
                    pendingRefs.Add(new BookRef { Title = title, Author = author, Quantity = qty, NewEntity = entity });
                }

                await SaveInBatchesAsync(pending);
                foreach (var br in pendingRefs)
                {
                    br.Id = br.NewEntity!.Id;                 // Id do CSDL cấp sau khi lưu
                    RegisterNewBook(br);
                }
                sec.Imported = pending.Count;
            }

            // ===== PHIẾU MƯỢN =====
            if (nLoans > 0)
            {
                var sec = report.Loans;
                sec.Total = nLoans;
                var readerIdSet = new HashSet<int>(readerIds.Values);
                var pending = new List<Loan>();

                var rowsIn = loans!;
                for (var i = 0; i < rowsIn.Count; i++)
                {
                    var l = rowsIn[i];
                    if (l == null) { Err(sec, i, null, "bản ghi rỗng"); continue; }

                    // --- sách ---
                    BookRef? book;
                    string bookLabel;
                    if (l.BookId is int bid)
                    {
                        bookLabel = $"bookId {bid}";
                        if (!byId.TryGetValue(bid, out book)) { Err(sec, i, bookLabel, "không có sách với bookId này"); continue; }
                    }
                    else if (!string.IsNullOrWhiteSpace(l.BookTitle))
                    {
                        var title = l.BookTitle.Trim();
                        bookLabel = title;
                        if (!byTitle.TryGetValue(title, out var cands)) { Err(sec, i, bookLabel, "không tìm thấy sách theo bookTitle"); continue; }
                        var wantAuthor = l.BookAuthor?.Trim();
                        var matches = string.IsNullOrEmpty(wantAuthor)
                            ? cands
                            : cands.Where(c => c.Author.Equals(wantAuthor, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (matches.Count == 0) { Err(sec, i, bookLabel, "không có sách trùng cả bookTitle và bookAuthor"); continue; }
                        if (matches.Count > 1) { Err(sec, i, bookLabel, "có nhiều sách trùng tên — hãy thêm bookAuthor hoặc dùng bookId"); continue; }
                        book = matches[0];
                    }
                    else { Err(sec, i, null, "thiếu bookId hoặc bookTitle"); continue; }

                    // --- độc giả ---
                    int readerId;
                    if (l.ReaderId is int rid)
                    {
                        if (!readerIdSet.Contains(rid)) { Err(sec, i, bookLabel, $"không có người dùng với readerId {rid}"); continue; }
                        readerId = rid;
                    }
                    else if (!string.IsNullOrWhiteSpace(l.ReaderEmail))
                    {
                        if (!readerIds.TryGetValue(l.ReaderEmail.Trim(), out readerId))
                        { Err(sec, i, bookLabel, $"không có người dùng với email {l.ReaderEmail.Trim()}"); continue; }
                    }
                    else { Err(sec, i, bookLabel, "thiếu readerId hoặc readerEmail"); continue; }

                    // --- ngày ---
                    var borrow = DateTime.Today;
                    if (!string.IsNullOrWhiteSpace(l.BorrowDate) && !TryDate(l.BorrowDate, out borrow))
                    { Err(sec, i, bookLabel, "borrowDate không hợp lệ (dùng yyyy-MM-dd hoặc dd/MM/yyyy)"); continue; }
                    var due = borrow.AddDays(14);
                    if (!string.IsNullOrWhiteSpace(l.DueDate) && !TryDate(l.DueDate, out due))
                    { Err(sec, i, bookLabel, "dueDate không hợp lệ (dùng yyyy-MM-dd hoặc dd/MM/yyyy)"); continue; }
                    if (due.Date < borrow.Date) { Err(sec, i, bookLabel, "hạn trả không được trước ngày mượn"); continue; }

                    DateTime? returned = null;
                    if (!string.IsNullOrWhiteSpace(l.ReturnedDate))
                    {
                        if (!TryDate(l.ReturnedDate, out var rd)) { Err(sec, i, bookLabel, "returnedDate không hợp lệ"); continue; }
                        if (rd.Date < borrow.Date) { Err(sec, i, bookLabel, "ngày trả không được trước ngày mượn"); continue; }
                        returned = rd.Date;
                    }

                    // --- tồn kho: chỉ phiếu CHƯA trả mới chiếm 1 bản ---
                    if (returned == null)
                    {
                        if (book.Quantity - book.Borrowed <= 0) { Err(sec, i, bookLabel, "sách đã hết, không thể tạo phiếu đang mượn"); continue; }
                        book.Borrowed++;
                    }

                    pending.Add(new Loan
                    {
                        BookId = book.Id, ReaderId = readerId,
                        BorrowDate = borrow.Date, DueDate = due.Date, ReturnedDate = returned
                    });
                }

                await SaveInBatchesAsync(pending);
                sec.Imported = pending.Count;
            }

            await tx.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            // tx chưa Commit => tự hoàn tác toàn bộ
            TempData["Toast"] = "Không lưu được dữ liệu, đã hoàn tác toàn bộ lần nhập: " + (ex.InnerException?.Message ?? ex.Message);
            return RedirectToAction(nameof(Index));
        }

        return View("Result", report);
    }

    // Ghi theo lô để tiết kiệm bộ nhớ khi nhập số lượng lớn
    private async Task SaveInBatchesAsync<T>(List<T> items) where T : class
    {
        for (var i = 0; i < items.Count; i += BatchSize)
        {
            _db.Set<T>().AddRange(items.Skip(i).Take(BatchSize));
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }
    }

    private static void Err(ImportSectionResult s, int index, string? key, string msg)
    {
        if (s.Errors.Count < MaxErrorsShown)
            s.Errors.Add($"Dòng #{index + 1}{(string.IsNullOrWhiteSpace(key) ? "" : $" ({key})")}: {msg}");
        else s.ErrorsTruncated = true;
    }

    private static bool TryDate(string? s, out DateTime d)
    {
        d = default;
        return !string.IsNullOrWhiteSpace(s)
            && DateTime.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    }

    // Tìm thuộc tính (không phân biệt hoa thường) rồi đọc thành danh sách
    private static List<T>? ReadSection<T>(JsonElement root, string name)
    {
        foreach (var p in root.EnumerateObject())
        {
            if (!p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (p.Value.ValueKind == JsonValueKind.Null) return null;
            if (p.Value.ValueKind != JsonValueKind.Array)
                throw new JsonException($"\"{name}\" phải là một mảng");
            return p.Value.Deserialize<List<T>>(JsonOpts);
        }
        return null;
    }
}
