using GrumPlates.Client.Features.Books.BookPage;
using GrumPlates.Client.Features.Books.Shared;

namespace GrumPlates.Test.Features.Books.Client;

// The book page and the shelf cover must wrap a title the same way.
public sealed class BookTitleLayoutTests
{
    public static TheoryData<string?, string[]> Titles => new()
    {
        { null, ["Untitled"] },
        { "   ", ["Untitled"] },
        { "  The Hobbit  ", ["The Hobbit"] },
        { "The Lord of the Rings", ["The Lord of", "the Rings"] },
        { "Supercalifragilistic", ["Supercalifr."] },
        { "One Two Three Four Five Six Seven", ["One Two", "Three Four", "Five"] },
    };

    [Theory]
    [MemberData(nameof(Titles))]
    public void TitleLines_WrapTheSameOnThePageAndTheCover(string? title, string[] expected)
    {
        Assert.Equal(expected, BookPageTextLayout.TitleLines(title));
        Assert.Equal(expected, BookCoverTextLayout.TitleLines(title));
    }

    [Theory]
    [InlineData(null, "Unknown author")]
    [InlineData("  J. R. R. Tolkien ", "J. R. R. Tolkien")]
    [InlineData("Alexandra Konstantinopoulou-Smith", "Alexandra Konstantinopoulou.")]
    public void AuthorLine_FallsBackAndTruncatesToTheAuthorWidth(string? author, string expected)
    {
        Assert.Equal(expected, BookPageTextLayout.AuthorLine(author));
    }
}
