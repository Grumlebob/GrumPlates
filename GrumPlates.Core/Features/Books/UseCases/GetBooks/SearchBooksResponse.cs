namespace GrumPlates.Core.Features.Books.UseCases.GetBooks;

public sealed class SearchBooksResponse
{
    public required List<BookListItemResponse> Books { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
