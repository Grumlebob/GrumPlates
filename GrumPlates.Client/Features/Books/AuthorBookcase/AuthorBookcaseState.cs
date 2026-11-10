using GrumPlates.Core.Features.Books.Contracts;
using GrumPlates.Core.Features.Books.UseCases.GetAuthorBooks;

namespace GrumPlates.Client.Features.Books.AuthorBookcase;

public sealed class AuthorBookcaseState(
    IAuthorBooksApi authorBooks,
    ILogger<AuthorBookcaseState> logger)
{
    private readonly IAuthorBooksApi _authorBooks = authorBooks;
    private readonly ILogger<AuthorBookcaseState> _logger = logger;
    private int _version;

    public event Action? Changed;

    public bool IsLoading { get; private set; } = true;

    public string? Error { get; private set; }

    public IReadOnlyList<AuthorBookListItemResponse> Books { get; private set; } = [];

    public void ApplyLoadedBooks(IReadOnlyList<AuthorBookListItemResponse> books)
    {
        _version++;
        Books = books.OrderBy(book => book.Id).ToList();
        Error = null;
        IsLoading = false;
        NotifyChanged();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var version = ++_version;
        IsLoading = true;
        Error = null;
        NotifyChanged();

        try
        {
            var response = await _authorBooks.GetAsync(cancellationToken);
            if (version != _version)
            {
                return;
            }

            ApplyLoadedBooks(response.Books);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Component disposal cancels this request; keep expected cancellation out of the error state.
        }
        catch (Exception ex)
        {
            if (version != _version)
            {
                return;
            }

            _logger.LogWarning(ex, "Author bookcase load failed.");
            Error = "The public catalog could not be loaded.";
            IsLoading = false;
            NotifyChanged();
        }
    }

    private void NotifyChanged() => Changed?.Invoke();
}
