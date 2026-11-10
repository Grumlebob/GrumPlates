using System.Security.Claims;

namespace GrumPlates.Client.Features.Books;

internal static class BookUserIdentity
{
    internal static string? GetUserKey(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user.FindFirst(ClaimTypes.Name)?.Value ??
               user.Identity.Name;
    }
}
