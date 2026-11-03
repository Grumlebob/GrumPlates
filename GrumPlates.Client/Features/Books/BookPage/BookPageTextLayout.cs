using GrumPlates.Client.Features.Books.Shared;

namespace GrumPlates.Client.Features.Books.BookPage;

public static class BookPageTextLayout
{
    private const int MaxAuthorLength = 28;

    // The page wraps titles exactly like the shelf cover.
    public static IReadOnlyList<string> TitleLines(string? title) => BookCoverTextLayout.TitleLines(title);

    public static string AuthorLine(string? author)
    {
        var clean = string.IsNullOrWhiteSpace(author) ? "Unknown author" : author.Trim();
        return clean.Length <= MaxAuthorLength ? clean : clean[..(MaxAuthorLength - 1)] + ".";
    }
}
