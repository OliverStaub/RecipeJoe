using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.UnitTests.Import;

[TestClass]
public sealed class ImporterTests
{
    private const string Apfelkuchen = "http://fixtures.test/recipes/apfelkuchen.html";

    private static readonly string ApfelkuchenFile = Path.Combine(AppContext.BaseDirectory, "fixtures", "recipes", "apfelkuchen.html");

    private static readonly int ApfelkuchenBytes = File.ReadAllBytes(ApfelkuchenFile).Length;

    private static Importer CreateImporter(IFetcher fetcher, int maxBytes = 5 * 1024 * 1024)
    {
        var policy = new FetchPolicy(fetcher, Options.Create(new ImportOptions { MaxBytes = maxBytes }));
        return new(policy, new ImageDownloader(policy, NullLogger<ImageDownloader>.Instance));
    }

    private const string PageUrl = "http://site.test/rezept";

    private const string ChallengePageHtml = "<html><head><title>Just a moment...</title></head></html>";

    /// <summary>Imports <see cref="PageUrl"/>, which answers with this response.</summary>
    private static Task<Result<RecipeDraft, ImportFailure>> ImportServedAsync(
        int status,
        string? contentType = "text/html",
        string body = "",
        string? challengeMitigation = null
    )
    {
        var fetcher = new FixtureFetcher();
        fetcher.Serve(PageUrl, status, contentType, body, challengeMitigation);
        return CreateImporter(fetcher).ImportAsync(PageUrl, CancellationToken.None);
    }

    [TestMethod]
    [DataRow("not a url")]
    [DataRow("/relative/path")]
    [DataRow("ftp://example.test/rezept")]
    public async Task Rejects_anything_that_is_not_an_absolute_http_url_without_fetching(string url)
    {
        var fetcher = new FixtureFetcher();

        var result = await CreateImporter(fetcher).ImportAsync(url, CancellationToken.None);

        Assert.AreEqual(ImportFailure.InvalidUrl, result.Failure);
        Assert.IsEmpty(fetcher.Requests);
    }

    [TestMethod]
    public async Task Passes_a_fetch_failure_through()
    {
        var result = await CreateImporter(new FixtureFetcher())
            .ImportAsync("http://fixtures.test/recipes/gibt-es-nicht.html", CancellationToken.None);

        Assert.AreEqual(ImportFailure.NotFound, result.Failure);
    }

    [TestMethod]
    public async Task Fails_with_NoRecipe_for_a_page_without_a_recipe()
    {
        var result = await ImportServedAsync(200, body: "<html></html>");

        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }

    [TestMethod]
    [DataRow(401)]
    [DataRow(402)]
    [DataRow(403)]
    [DataRow(429)]
    public async Task Fails_with_Blocked_when_the_site_refuses_us(int status)
    {
        Assert.AreEqual(ImportFailure.Blocked, (await ImportServedAsync(status)).Failure);
    }

    [TestMethod]
    [DataRow(200, "challenge")]
    [DataRow(200, "managed, Challenge")]
    [DataRow(404, "challenge")]
    public async Task Fails_with_Blocked_when_the_challenge_header_says_so_whatever_the_status(int status, string challengeMitigation)
    {
        var result = await ImportServedAsync(status, body: "<html></html>", challengeMitigation: challengeMitigation);

        Assert.AreEqual(ImportFailure.Blocked, result.Failure);
    }

    [TestMethod]
    [DataRow(503)]
    [DataRow(200)]
    public async Task Fails_with_Blocked_for_a_challenge_page(int status)
    {
        Assert.AreEqual(ImportFailure.Blocked, (await ImportServedAsync(status, body: ChallengePageHtml)).Failure);
    }

    [TestMethod]
    [DataRow(404)]
    [DataRow(410)]
    public async Task Fails_with_NotFound_when_the_page_is_gone(int status)
    {
        Assert.AreEqual(ImportFailure.NotFound, (await ImportServedAsync(status)).Failure);
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(503)]
    [DataRow(302)]
    public async Task Fails_with_BadResponse_for_any_other_error_status(int status)
    {
        Assert.AreEqual(ImportFailure.BadResponse, (await ImportServedAsync(status, body: "<html>Oops</html>")).Failure);
    }

    [TestMethod]
    [DataRow("application/json")]
    [DataRow("text/plain")]
    [DataRow(null)]
    public async Task Fails_with_BadResponse_for_a_page_that_is_not_html(string? contentType)
    {
        Assert.AreEqual(ImportFailure.BadResponse, (await ImportServedAsync(200, contentType, "<html></html>")).Failure);
    }

    [TestMethod]
    public async Task Imports_an_xhtml_page()
    {
        var result = await ImportServedAsync(200, "application/xhtml+xml; charset=utf-8", File.ReadAllText(ApfelkuchenFile));

        Assert.AreEqual("Einfacher Apfelkuchen", result.Value.Title);
    }

    [TestMethod]
    public async Task Imports_the_recipe_without_an_image_when_the_image_is_blocked()
    {
        var fetcher = new FixtureFetcher();
        fetcher.Serve("http://fixtures.test/images/apfelkuchen.jpg", 403);

        var result = await CreateImporter(fetcher).ImportAsync(Apfelkuchen, CancellationToken.None);

        Assert.AreEqual("Einfacher Apfelkuchen", result.Value.Title);
        Assert.IsNull(result.Value.Image);
    }

    [TestMethod]
    public async Task Imports_a_draft_with_its_image_from_the_page_it_was_redirected_to()
    {
        var result = await CreateImporter(new FixtureFetcher()).ImportAsync("http://short.test/apfelkuchen", CancellationToken.None);

        var draft = result.Value;
        Assert.AreEqual("Einfacher Apfelkuchen", draft.Title);
        Assert.AreEqual(new Uri("http://fixtures.test/recipes/apfelkuchen.html"), draft.Source);
        Assert.AreEqual("image/jpeg", draft.Image?.ContentType);
    }

    [TestMethod]
    public async Task Fails_with_BadResponse_for_a_page_over_the_cap_even_if_the_adapter_hands_over_the_whole_body()
    {
        var result = await CreateImporter(new FixtureFetcher(), maxBytes: ApfelkuchenBytes - 1)
            .ImportAsync(Apfelkuchen, CancellationToken.None);

        Assert.AreEqual(ImportFailure.BadResponse, result.Failure);
    }

    [TestMethod]
    public async Task Imports_a_page_exactly_at_the_cap()
    {
        var result = await CreateImporter(new FixtureFetcher(), maxBytes: ApfelkuchenBytes)
            .ImportAsync(Apfelkuchen, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
    }
}
