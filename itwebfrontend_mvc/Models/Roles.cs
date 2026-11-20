namespace ItWebFrontend.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public static bool IsValid(string? role) => role == Admin || role == User;
}

public static class PostKinds
{
    public const string Promotion = "Khuyến mãi";
    public const string News = "Tin tức";
    public static readonly string[] All = { Promotion, News };
}
