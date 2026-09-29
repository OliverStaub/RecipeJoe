using RecipeJoe.Api;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

/// <summary>Serves fixtures/ at http://fixtures.test/; any other host 404s. <c>http://short.test/{name}</c> redirects to <c>fixtures.test/recipes/{name}.html</c>, like a link shortener.</summary>
internal sealed class FixturePageFetcher : IPageFetcher
{
    private const string FixturesHost = "fixtures.test";
    private const string ShortenerHost = "short.test";

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "fixtures");

    public Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken) => Serve(url);

    public Task<Result<FetchedContent, ImportFailure>> FetchImageAsync(Uri url, CancellationToken cancellationToken) => Serve(url);

    private static Task<Result<FetchedContent, ImportFailure>> Serve(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Host == ShortenerHost)
        {
            url = new Uri($"http://{FixturesHost}/recipes{url.AbsolutePath}.html");
        }

        var file = Path.Combine(Root, url.AbsolutePath.TrimStart('/'));
        var result = url.Host == FixturesHost && File.Exists(file)
            ? Result<FetchedContent, ImportFailure>.Ok(new FetchedContent(File.ReadAllBytes(file), ContentTypeOf(file), url))
            : Result<FetchedContent, ImportFailure>.Fail(ImportFailure.NotFound);

        return Task.FromResult(result);
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
