using System.Collections.Concurrent;
using System.Text;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

/// <summary>Raw responses only, so the <see cref="FetchPolicy"/> judges them as in production. Serves fixtures/ at http://fixtures.test/; any other host 404s. <c>http://short.test/{name}</c> redirects to <c>fixtures.test/recipes/{name}.html</c>, like a link shortener. <see cref="Serve(string, int, string?, byte[], string?)"/> overrides any URL with a canned response.</summary>
internal sealed class FixtureFetcher : IFetcher
{
    private const string FixturesHost = "fixtures.test";
    private const string ShortenerHost = "short.test";

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "fixtures");

    private readonly ConcurrentDictionary<Uri, RawResponse> _canned = new();
    private readonly ConcurrentQueue<(Uri Url, FetchKind Kind)> _requests = new();

    /// <summary>Every fetch, in order.</summary>
    public IReadOnlyCollection<(Uri Url, FetchKind Kind)> Requests => _requests;

    /// <summary>From now on, <paramref name="url"/> answers with this response instead of a fixture.</summary>
    public void Serve(string url, int status, string? contentType, byte[] body, string? challengeMitigation = null) =>
        _canned[new Uri(url)] = new RawResponse(status, contentType, challengeMitigation, body, new Uri(url));

    /// <inheritdoc cref="Serve(string, int, string?, byte[], string?)"/>
    public void Serve(string url, int status, string? contentType = "text/html", string body = "", string? challengeMitigation = null) =>
        Serve(url, status, contentType, Encoding.UTF8.GetBytes(body), challengeMitigation);

    public Task<Result<RawResponse, ImportFailure>> FetchAsync(Uri url, FetchKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        _requests.Enqueue((url, kind));

        if (_canned.TryGetValue(url, out var canned))
        {
            return Task.FromResult(Result<RawResponse, ImportFailure>.Ok(canned));
        }

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
