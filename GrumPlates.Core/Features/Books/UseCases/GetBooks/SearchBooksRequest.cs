namespace GrumPlates.Core.Features.Books.UseCases.GetBooks;

public sealed class SearchBooksRequest
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;
    public const int MaximumPage = 1_000_000;
    public const int MaximumSearchLength = 200;

    public string? SearchTerm { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public int EffectivePage => Page ?? 1;

    public int EffectivePageSize => PageSize ?? DefaultPageSize;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (EffectivePage is < 1 or > MaximumPage)
        {
            errors[nameof(Page)] = [$"Page must be between 1 and {MaximumPage}."];
        }

        if (EffectivePageSize is < 1 or > MaximumPageSize)
        {
            errors[nameof(PageSize)] = [$"Page size must be between 1 and {MaximumPageSize}."];
        }

        if (SearchTerm?.Length > MaximumSearchLength)
        {
            errors[nameof(SearchTerm)] = [$"Search text must be {MaximumSearchLength} characters or fewer."];
        }

        return errors;
    }
}
