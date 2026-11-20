using ItWebFrontend.Models;
using Microsoft.AspNetCore.Identity;

namespace ItWebFrontend.Helpers;

public static class Passwords
{
    // Mật khẩu mặc định khi admin tạo / nhập JSON mà không cung cấp mật khẩu
    public const string DefaultPassword = "Thuvien@123";

    private static readonly PasswordHasher<Reader> Hasher = new();

    public static string Hash(string password) => Hasher.HashPassword(null!, password);

    public static bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        return Hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
    }
}
