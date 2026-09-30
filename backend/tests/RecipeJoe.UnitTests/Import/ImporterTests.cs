using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

[TestClass]
public sealed class ImporterTests
{
    private const string Apfelkuchen = "http://fixtures.test/recipes/apfelkuchen.html";

    private static readonly int ApfelkuchenBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "recipes", "apfelkuchen.html")
    ).Length;

    private static Importer CreateImporter(IPageFetcher fetcher, int maxBytes = 5 * 1024 * 1024)
    {
        var policy = new FetchPolicy(fetcher, Options.Create(new ImportOptions { MaxBytes = maxBytes }));
        return new(policy, new ImageDownloader(policy, NullLogger<ImageDownloader>.Instance));
    }

    private sealed class StubFetcher(Result<RawResponse, ImportFailure>? result) : IPageFetcher
    {
        public int Calls { get; private set; }

        public Task<Result<RawResponse, ImportFailure>> FetchAsync(Uri url, FetchKind kind, CancellationToken cancellationToken)
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
            Result<RawResponse, ImportFailure>.Ok(new RawResponse(200, "text/html", null, "<html></html>"u8.ToArray(), new Uri("http://fixtures.test/x")))
        );

        var result = await CreateImporter(fetcher).ImportAsync("http://fixtures.test/x", CancellationToken.None);

        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }

    [TestMethod]
    public async Task Imports_a_draft_with_its_image_from_the_page_it_was_redirected_to()
    {
        var result = await CreateImporter(new FixturePageFetcher()).ImportAsync("http://short.test/apfelkuchen", CancellationToken.None);

        var draft = result.Value;
        Assert.AreEqual("Einfacher Apfelkuchen", draft.Title);
        Assert.AreEqual(new Uri("http://fixtures.test/recipes/apfelkuchen.html"), draft.Source);
        Assert.AreEqual("image/jpeg", draft.Image?.ContentType);
    }

    [TestMethod]
    public async Task Fails_with_BadResponse_for_a_page_over_the_cap_even_if_the_adapter_hands_over_the_whole_body()
    {
        var result = await CreateImporter(new FixturePageFetcher(), maxBytes: ApfelkuchenBytes - 1)
            .ImportAsync(Apfelkuchen, CancellationToken.None);

        Assert.AreEqual(ImportFailure.BadResponse, result.Failure);
    }

    [TestMethod]
    public async Task Imports_a_page_exactly_at_the_cap()
    {
        var result = await CreateImporter(new FixturePageFetcher(), maxBytes: ApfelkuchenBytes)
            .ImportAsync(Apfelkuchen, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
    }
}
