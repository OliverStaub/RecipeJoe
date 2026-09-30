using RecipeJoe.Api;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

/// <summary>Raw responses only, so the <see cref="FetchPolicy"/> judges them as in production. Serves fixtures/ at http://fixtures.test/; any other host 404s. <c>http://short.test/{name}</c> redirects to <c>fixtures.test/recipes/{name}.html</c>, like a link shortener.</summary>
internal sealed class FixturePageFetcher : IFetcher
{
    private const string FixturesHost = "fixtures.test";
    private const string ShortenerHost = "short.test";

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "fixtures");

    public Task<Result<RawResponse, ImportFailure>> FetchAsync(Uri url, FetchKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Host == ShortenerHost)
        {
            url = new Uri($"http://{FixturesHost}/recipes{url.AbsolutePath}.html");
        }

        var file = Path.Combine(Root, url.AbsolutePath.TrimStart('/'));
        var response = url.Host == FixturesHost && File.Exists(file)
            ? new RawResponse(200, ContentTypeOf(file), null, File.ReadAllBytes(file), url)
            : new RawResponse(404, "text/html", null, [], url);

        return Task.FromResult(Result<RawResponse, ImportFailure>.Ok(response));
    }

    private static string ContentTypeOf(string file) =>
        Path.GetExtension(file).ToUpperInvariant() switch
        {
            ".JPG" => "image/jpeg",
            ".PNG" => "image/png",
            ".WEBP" => "image/webp",
            ".GIF" => "image/gif",
            _ => "text/html",
        };
}
