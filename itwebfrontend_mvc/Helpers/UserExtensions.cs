using System.Security.Claims;

namespace ItWebFrontend.Helpers;

public static class UserExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
