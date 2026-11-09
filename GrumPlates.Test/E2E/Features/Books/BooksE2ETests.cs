using GrumPlates.Test.E2E.Support;
using GrumPlates.Test.TestSupport.Integration;
using Microsoft.Playwright;

namespace GrumPlates.Test.E2E.Features.Books;

[Collection(TestCollectionNames.E2E)]
public sealed class BooksE2ETests : BlazorE2ETestBase
{
    [Fact(Skip = "Set RUN_E2E=1 to run Playwright E2E tests.", SkipUnless = nameof(E2ETestGuard.IsEnabled), SkipType = typeof(E2ETestGuard))]
    [Trait("Category", "E2E")]
    public async Task Books_PublicCatalogAndPrivateCrudUseRoutablePages()
    {
        await RunWithFailureScreenshotAsync(async () =>
        {
            await Page.SetViewportSizeAsync(1440, 900);
            await GoHomeAndWaitForInteractivityAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Books", Exact = true })).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("author-book-list").GetByRole(AriaRole.Article).First).ToBeVisibleAsync();
            await AssertNoPageHorizontalOverflowAsync("anonymous Books page at 1440px");
            await Page.SetViewportSizeAsync(390, 844);
            await Expect(Page.GetByTestId("private-books-sign-in")).ToContainTextAsync("Sign in to create, edit, and remove books");
            await Expect(Page.GetByTestId("add-book")).ToHaveCountAsync(0);
            await Expect(Page.GetByTestId("user-book-list")).ToHaveCountAsync(0);
            await AssertNoPageHorizontalOverflowAsync("anonymous Books page at 390px");

            var publicSearch = Page.GetByTestId("author-book-search");
            await publicSearch.FillAsync("no-public-book-matches-this-text");
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Search", Exact = true }).ClickAsync();
            await Expect(Page.GetByTestId("author-book-no-results")).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Clear", Exact = true }).ClickAsync();
            await Expect(Page.GetByTestId("author-book-list").GetByRole(AriaRole.Article).First).ToBeVisibleAsync();

            var publicBookLink = Page.GetByTestId("author-book-list").GetByRole(AriaRole.Link).First;
            await publicBookLink.ClickAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            var publicTitle = await Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }).InnerTextAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = publicTitle, Exact = true })).ToBeVisibleAsync();
            await Expect(Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit book" })).ToHaveCountAsync(0);
            await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete book" })).ToHaveCountAsync(0);
            await Expect(Page.GetByText("Not provided", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            Assert.Contains("/books/catalog/", new Uri(Page.Url).AbsolutePath, StringComparison.Ordinal);
            await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            await Page.GoBackAsync();
            await Expect(Page.GetByTestId("author-book-list")).ToBeVisibleAsync();

            await GoToAsync("/books/author/ship");
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            Assert.Contains("/books/catalog/", new Uri(Page.Url).AbsolutePath, StringComparison.Ordinal);

            await GoHomeAndWaitForInteractivityAsync();
            await Page.GetByTestId("private-books-sign-in").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Sign in" }).ClickAsync();
            await Expect(Page.Locator("#Input\\.Email")).ToBeVisibleAsync();
            Assert.Contains("returnUrl=%2Fbooks", Page.Url, StringComparison.Ordinal);

            await GoToAsync("/books/new");
            await Expect(Page.Locator("#Input\\.Email")).ToBeVisibleAsync();
            await Page.SetViewportSizeAsync(1440, 900);
            await SignInAsSeededUserAsync();
            await GoHomeAndWaitForInteractivityAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Your books" })).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("add-book")).ToBeVisibleAsync();
            await AssertNoPageHorizontalOverflowAsync("signed-in Books page at 1440px");
            await Page.SetViewportSizeAsync(390, 844);

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var longTitle = string.Join(' ', Enumerable.Repeat("Long descriptive book title", 5));
            var title = $"E2E Book {suffix} {longTitle}";
            var updatedTitle = $"E2E Book Updated {suffix} {longTitle}";
            TrackCreatedBook(title);
            TrackCreatedBook(updatedTitle);

            await Page.GetByTestId("add-book").ClickAsync();
            await Expect(Page.GetByTestId("book-form")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("book-title")).ToHaveValueAsync(string.Empty);
            await Expect(Page.GetByTestId("book-author")).ToHaveValueAsync(string.Empty);
            await Expect(Page.GetByTestId("book-url")).ToHaveValueAsync(string.Empty);
            await Page.GetByTestId("book-title").FillAsync(string.Empty);
            await Page.GetByTestId("book-save").ClickAsync();
            await Expect(Page.GetByTestId("book-form").GetByText("The Title field is required.").First).ToBeVisibleAsync();
            await Page.GetByTestId("book-title").FillAsync(title);
            await Page.GetByTestId("book-url").FillAsync("not a URL");
            await Page.GetByTestId("book-save").ClickAsync();
            await Expect(Page.GetByTestId("book-form").GetByText("URL must be an absolute HTTP or HTTPS URL.").First).ToBeVisibleAsync();
            await Page.GetByTestId("book-url").FillAsync(string.Empty);

            await SkipIfBrowserApiRequestsAreNotObservableAsync("Book create failure and retry checks");
            var failNextCreate = true;
            await Page.RouteAsync("**/api/books", async route =>
            {
                if (route.Request.Method == "POST" && failNextCreate)
                {
                    failNextCreate = false;
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 500,
                        ContentType = "application/problem+json",
                        Body = "{\"title\":\"Synthetic save failure\"}"
                    });
                    return;
                }

                await route.ContinueAsync();
            });
            await Page.GetByTestId("book-url").PressAsync("Enter");
            await Expect(Page.GetByTestId("book-save-error")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("book-title")).ToHaveValueAsync(title);
            await Page.UnrouteAsync("**/api/books");
            await Page.GetByTestId("book-save").ClickAsync();

            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true })).ToBeVisibleAsync();
            await Expect(Page.GetByText("Not listed", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Expect(Page.GetByText("Not provided", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Page.SetViewportSizeAsync(1440, 900);
            await AssertNoPageHorizontalOverflowAsync("private book details at 1440px");
            await Page.SetViewportSizeAsync(390, 844);
            await ReloadAndWaitForInteractivityAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Back to your books" }).ClickAsync();

            var bookLink = PrivateBookLink(title);
            await Expect(bookLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            var privateSearch = Page.GetByTestId("user-book-search");
            var privateSearchButton = Page.GetByTestId("user-books-panel").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Search", Exact = true });
            await privateSearch.FillAsync("no-private-book-matches-this-text");
            await privateSearchButton.ClickAsync();
            await Expect(Page.GetByTestId("user-book-empty-state")).ToContainTextAsync("No books match that search.");
            await privateSearch.FillAsync($"E2E Book {suffix}");
            await privateSearchButton.ClickAsync();
            await Expect(PrivateBookLink(title)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Page.GetByTestId("user-books-panel").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Clear", Exact = true }).ClickAsync();
            await Expect(PrivateBookLink(title)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await ReloadAndWaitForInteractivityAsync();
            await Expect(PrivateBookLink(title)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await PrivateBookLink(title).ClickAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            await Page.GoBackAsync();
            await Expect(PrivateBookLink(title)).ToBeVisibleAsync();

            await PrivateBookLink(title).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit book" }).ClickAsync();
            await Expect(Page.GetByTestId("book-edit-page")).ToBeVisibleAsync();
            await Page.SetViewportSizeAsync(1440, 900);
            await AssertNoPageHorizontalOverflowAsync("book edit form at 1440px");
            await Page.SetViewportSizeAsync(390, 844);
            await Expect(Page.GetByTestId("book-title")).ToHaveValueAsync(title);
            await Page.GetByTestId("book-title").FillAsync($"Canceled {title}");
            await Page.GetByTestId("book-cancel").ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true })).ToBeVisibleAsync();

            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit book" }).ClickAsync();
            await Expect(Page.GetByTestId("book-title")).ToHaveValueAsync(title);
            await Page.GetByTestId("book-title").FillAsync(updatedTitle);
            await Page.GetByTestId("book-save").ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = updatedTitle, Exact = true }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            await Page.SetViewportSizeAsync(1440, 900);
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Logout", Exact = true }).ClickAsync();
            await Expect(Page.GetByTestId("private-books-sign-in")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Expect(Page.GetByTestId("user-book-list")).ToHaveCountAsync(0);
            await SignInAsSeededUserAsync();
            await GoHomeAndWaitForInteractivityAsync();
            await Expect(PrivateBookLink(updatedTitle)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await PrivateBookLink(updatedTitle).ClickAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Page.SetViewportSizeAsync(390, 844);

            var deleteRequestCount = 0;
            var failNextDelete = true;
            await Page.RouteAsync("**/api/books/*", async route =>
            {
                if (route.Request.Method == "DELETE")
                {
                    deleteRequestCount++;
                    if (failNextDelete)
                    {
                        failNextDelete = false;
                        await route.FulfillAsync(new RouteFulfillOptions
                        {
                            Status = 500,
                            ContentType = "application/problem+json",
                            Body = "{\"title\":\"Synthetic delete failure\"}"
                        });
                        return;
                    }
                }

                await route.ContinueAsync();
            });

            await Page.GetByTestId("book-delete-request").ClickAsync();
            await Expect(Page.GetByTestId("book-delete-confirmation")).ToContainTextAsync(updatedTitle);
            await Page.SetViewportSizeAsync(1440, 900);
            await AssertNoPageHorizontalOverflowAsync("delete confirmation at 1440px");
            await Page.SetViewportSizeAsync(390, 844);
            Assert.Equal(0, deleteRequestCount);
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();
            await Expect(Page.GetByTestId("book-delete-confirmation")).ToHaveCountAsync(0);
            Assert.Equal(0, deleteRequestCount);

            await Page.GetByTestId("book-delete-request").ClickAsync();
            await Page.GetByTestId("book-delete-confirm").ClickAsync();
            await Expect(Page.GetByTestId("book-operation-error")).ToContainTextAsync("could not be deleted");
            Assert.Equal(1, deleteRequestCount);

            await Page.GetByTestId("book-delete-request").ClickAsync();
            await Page.GetByTestId("book-delete-confirm").ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Your books" }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Page.UnrouteAsync("**/api/books/*");
            await ReloadAndWaitForInteractivityAsync();
            await Expect(PrivateBookLink(updatedTitle)).ToHaveCountAsync(0);

            await GoToAsync("/books/99999999");
            await Expect(Page.GetByTestId("user-book-not-found")).ToBeVisibleAsync();
            await AssertNoPageHorizontalOverflowAsync("private Books page at 390px");
        });
    }

    private ILocator PrivateBookLink(string title) =>
        Page.GetByTestId("user-book-list").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = title, Exact = true });
}
