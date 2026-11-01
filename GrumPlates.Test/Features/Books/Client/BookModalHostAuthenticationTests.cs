using System.Security.Claims;
using GrumPlates.Client.Features.Books.BookModal;
using GrumPlates.Client.Features.Books.UserBookcase;
using GrumPlates.Core.Features.Books.Contracts;
using GrumPlates.Core.Features.Books.UseCases.CreateBook;
using GrumPlates.Core.Features.Books.UseCases.DeleteBook;
using GrumPlates.Core.Features.Books.UseCases.GetAuthorBook;
using GrumPlates.Core.Features.Books.UseCases.GetAuthorBooks;
using GrumPlates.Core.Features.Books.UseCases.GetBook;
using GrumPlates.Core.Features.Books.UseCases.GetBooks;
using GrumPlates.Core.Features.Books.UseCases.UpdateBook;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GrumPlates.Test.Features.Books.Client;

public sealed class BookModalHostAuthenticationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedAuthenticationOnSameRoute_CancelsOldLoadAndHidesItsPrivateResult(bool signedOut)
    {
        var books = new ControlledBooksApi(blockFirstRead: true);
        var authentication = new MutableAuthenticationStateProvider();
        var navigation = new ModalNavigationManager();
        await using var services = CreateServices(books, authentication, navigation);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var rendered = await renderer.Dispatcher.InvokeAsync(() => renderer.BeginRenderingComponent<BookModalHost>());
        await books.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            await renderer.Dispatcher.InvokeAsync(() => authentication.ChangeTo(signedOut ? null : "second-user"));
            await books.SecondReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(books.FirstReadToken.IsCancellationRequested);

            books.FirstRead.TrySetResult(new GetBookResponse { Id = 1, Title = "First account private title" });
            await rendered.QuiescenceTask.WaitAsync(TimeSpan.FromSeconds(10));
            var html = await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString);

            Assert.Equal(2, books.ReadCount);
            Assert.DoesNotContain("First account private title", html);
            Assert.Contains("This book could not be found.", html);
            Assert.Equal(ModalNavigationManager.ModalUri, navigation.Uri);
            Assert.Empty(navigation.Navigations);
        }
        finally
        {
            // Release even the broken baseline so no renderer work survives a failed assertion.
            books.FirstRead.TrySetResult(null);
            await rendered.QuiescenceTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task InitialPrerender_LoadsTheModalOnceWithoutBrowserServices()
    {
        var books = new ControlledBooksApi(blockFirstRead: false);
        await using var services = CreateServices(books, new MutableAuthenticationStateProvider(), new ModalNavigationManager());
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<BookModalHost>();
            return rendered.ToHtmlString();
        });

        Assert.Equal(1, books.ReadCount);
        Assert.Contains("Current private title", html);
        Assert.Contains("data-testid=\"book-modal\"", html);
    }

    private static ServiceProvider CreateServices(
        IBooksApi books,
        AuthenticationStateProvider authentication,
        NavigationManager navigation) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton(books)
            .AddSingleton<IAuthorBooksApi>(new UnusedAuthorBooksApi())
            .AddSingleton(authentication)
            .AddSingleton(navigation)
            .AddScoped<UserBookcaseState>()
            .AddCascadingAuthenticationState()
            .BuildServiceProvider();

    private sealed class MutableAuthenticationStateProvider : AuthenticationStateProvider
    {
        private Task<AuthenticationState> _state = StateFor("first-user");

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state;

        public void ChangeTo(string? userId)
        {
            _state = StateFor(userId);
            NotifyAuthenticationStateChanged(_state);
        }

        private static Task<AuthenticationState> StateFor(string? userId) =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(userId is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "fixture"))));
    }

    private sealed class ModalNavigationManager : NavigationManager
    {
        public const string ModalUri = "https://example.test/books?bookId=1&bookMode=view";
        public List<string> Navigations { get; } = [];

        public ModalNavigationManager() => Initialize("https://example.test/", ModalUri);

        protected override void NavigateToCore(string uri, bool forceLoad) => Navigations.Add(uri);
    }

    private sealed class ControlledBooksApi(bool blockFirstRead) : IBooksApi
    {
        private int _readCount;
        public int ReadCount => Volatile.Read(ref _readCount);
        public TaskCompletionSource FirstReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<GetBookResponse?> FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken FirstReadToken { get; private set; }

        public Task<GetBookResponse?> GetByIdAsync(GetBookRequest request, CancellationToken cancellationToken = default)
        {
            var read = Interlocked.Increment(ref _readCount);
            if (read == 1)
            {
                FirstReadToken = cancellationToken;
                FirstReadStarted.TrySetResult();
                return blockFirstRead
                    ? FirstRead.Task
                    : Task.FromResult<GetBookResponse?>(new GetBookResponse { Id = request.Id, Title = "Current private title" });
            }

            SecondReadStarted.TrySetResult();
            return Task.FromResult<GetBookResponse?>(null);
        }

        public Task<GetBooksResponse> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CreateBookResponse> CreateAsync(CreateBookRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(UpdateBookRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(DeleteBookRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedAuthorBooksApi : IAuthorBooksApi
    {
        public Task<GetAuthorBookResponse?> GetByIdAsync(GetAuthorBookRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GetAuthorBooksResponse> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
