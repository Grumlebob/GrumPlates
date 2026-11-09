using GrumPlates.Core.Features.Books.Domain;
using GrumPlates.Core.Features.Books.UseCases.GetBooks;
using GrumPlates.Infrastructure.Persistence;
using GrumPlates.Test.Features.Books.TestData;
using GrumPlates.Test.TestSupport.Integration;

namespace GrumPlates.Test.Features.Books.Api;

[Collection(TestCollectionNames.Integration)]
public sealed class SearchBooksTests : IAsyncLifetime, IDisposable
{
    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;
    private readonly Func<Task> _resetDatabase;
    private readonly IServiceScope _scope;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SearchBooksTests(WebAppFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _anonymousClient = factory.HttpClient;
        _resetDatabase = factory.ResetDatabaseAsync;
        _scope = factory.Services.CreateScope();
        _dbFactory = _scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    }

    [Fact]
    public async Task Search_Anonymous_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.GetAsync("/api/books/search");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_FiltersAndPaginatesOnlyCurrentUsersBooks()
    {
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            await BookTestUsers.EnsureAsync(db, BookTestUsers.DefaultUserId, BookTestUsers.OtherUserId);
            db.UserBooks.AddRange(
                UserBook("Personal Atlas", "Ada Lovelace"),
                UserBook("Field Notes", "Atlas Press"),
                UserBook("Private Atlas", "Other Owner", BookTestUsers.OtherUserId),
                UserBook("Completely Different", "Someone Else"));
            await db.SaveChangesAsync();
        }

        var firstPage = await _client.GetFromJsonAsync<SearchBooksResponse>(
            "/api/books/search?searchTerm=atlas&page=1&pageSize=1");
        var secondPage = await _client.GetFromJsonAsync<SearchBooksResponse>(
            "/api/books/search?searchTerm=atlas&page=2&pageSize=1");

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.Single(firstPage.Books);
        Assert.Equal("Personal Atlas", firstPage.Books[0].Title);
        Assert.Equal(2, secondPage!.Page);
        Assert.Single(secondPage.Books);
        Assert.Equal("Atlas Press", secondPage.Books[0].Author);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task Search_InvalidPagination_ReturnsValidationProblem(string query)
    {
        var response = await _client.GetAsync($"/api/books/search?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    public async ValueTask InitializeAsync() => await _resetDatabase();

    public async ValueTask DisposeAsync() => await _resetDatabase();

    public void Dispose()
    {
        _scope.Dispose();
        GC.SuppressFinalize(this);
    }

    private static UserBook UserBook(string title, string? author, string ownerUserId = BookTestUsers.DefaultUserId) => new()
    {
        OwnerUserId = ownerUserId,
        Book = new Book { Title = title, Author = author }
    };
}
