using System.Security.Cryptography;
using System.Text;
using GrumPlates.Infrastructure.Hosting.CacheInvalidation;

namespace GrumPlates.Features.Books.Caching;

public static class BooksCacheKeys
{
    public const string Scope = "books";
    public const string AllTag = "books";

    public static string List(string userId) => $"books:user:{userId}:list";

    public static string SearchList(string userId, string searchTerm, int page, int pageSize)
    {
        var termHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(searchTerm.ToUpperInvariant())));
        return $"books:user:{userId}:search:{termHash}:page:{page}:size:{pageSize}";
    }

    public static string ListTag(string userId) => $"books:user:{userId}:list";

    public static string Item(string userId, int id) => $"books:user:{userId}:item:{id}";

    public static string ItemTag(string userId, int id) => $"books:user:{userId}:item:{id}";

    internal static CacheInvalidationRequest ForChangedBook(string userId, int id) =>
        new(Scope, [List(userId), Item(userId, id)], [ListTag(userId), ItemTag(userId, id)]);
}
