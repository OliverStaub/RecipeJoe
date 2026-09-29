using Microsoft.EntityFrameworkCore;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

/// <summary>Failure paths only: they must return before touching the database. The success path runs against Postgres in the integration tests.</summary>
[TestClass]
public sealed class ImporterTests
{
    private static Importer CreateImporter(IPageFetcher fetcher)
    {
        // No connection is ever opened: any database access on a failure path would throw.
        var options = new DbContextOptionsBuilder<RecipeJoeDbContext>()
            .UseNpgsql("Host=unreachable.invalid;Database=none")
            .Options;
        return new Importer(fetcher, new RecipeJoeDbContext(options), TimeProvider.System);
    }

    private sealed class StubFetcher(Result<FetchedContent, ImportFailure>? result) : IPageFetcher
    {
        public int Calls { get; private set; }

        public Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result ?? throw new InvalidOperationException("Unexpected fetch."));
        }
    }

    [TestMethod]
    [DataRow("not a url")]
    [DataRow("/relative/path")]
    [DataRow("ftp://example.test/rezept")]
    public async Task Rejects_anything_that_is_not_an_absolute_http_url_without_fetching(string url)
    {
        var fetcher = new StubFetcher(null);

        var result = await CreateImporter(fetcher).ImportAsync(url, CancellationToken.None);

        Assert.AreEqual(ImportFailure.InvalidUrl, result.Failure);
        Assert.AreEqual(0, fetcher.Calls);
    }

    [TestMethod]
    public async Task Passes_a_fetch_failure_through()
    {
        var result = await CreateImporter(new FixturePageFetcher())
            .ImportAsync("http://fixtures.test/recipes/gibt-es-nicht.html", CancellationToken.None);

        Assert.AreEqual(ImportFailure.NotFound, result.Failure);
    }

    [TestMethod]
    public async Task Fails_with_NoRecipe_for_a_page_without_a_recipe()
    {
        var fetcher = new StubFetcher(
            Result<FetchedContent, ImportFailure>.Ok(new FetchedContent("<html></html>"u8.ToArray(), "text/html"))
        );

        var result = await CreateImporter(fetcher).ImportAsync("http://fixtures.test/x", CancellationToken.None);

        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }
}
