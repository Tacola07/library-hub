using ItWebFrontend.Models;
using Microsoft.EntityFrameworkCore;

namespace ItWebFrontend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<ReaderInfo> ReaderInfos => Set<ReaderInfo>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Reader>().HasIndex(r => r.Email).IsUnique();

        // Không cho xóa sách / người dùng đang có phiếu mượn
        b.Entity<Loan>().HasOne(l => l.Book).WithMany(x => x.Loans)
            .HasForeignKey(l => l.BookId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Loan>().HasOne(l => l.Reader).WithMany(x => x.Loans)
            .HasForeignKey(l => l.ReaderId).OnDelete(DeleteBehavior.Restrict);

        // Xóa người dùng thì xóa luôn thông tin bổ sung
        b.Entity<ReaderInfo>().HasOne(i => i.Reader).WithMany(r => r.Infos)
            .HasForeignKey(i => i.ReaderId).OnDelete(DeleteBehavior.Cascade);
    }
}
