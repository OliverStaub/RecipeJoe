using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace RecipeJoe.Api.Import;

/// <param name="Url">Where the content was served from: after redirects, so relative links resolve against it.</param>
internal sealed record FetchedContent(byte[] Bytes, string? ContentType, Uri Url);

/// <summary>Every Import fetch goes through here: turns an adapter's raw response into content or an Import failure, the same way for every adapter.</summary>
internal sealed class FetchPolicy(IFetcher fetcher, IOptions<ImportOptions> options)
{
    private static readonly string[] HtmlMediaTypes = ["text/html", "application/xhtml+xml"];

    private readonly int _maxBytes = options.Value.MaxBytes;

    /// <summary>An HTML page.</summary>
    public Task<Result<FetchedContent, ImportFailure>> FetchPageAsync(Uri url, CancellationToken cancellationToken) =>
        FetchAsync(url, FetchKind.Page, cancellationToken);

    /// <summary>An image of any content type; the caller checks the bytes.</summary>
    public Task<Result<FetchedContent, ImportFailure>> FetchImageAsync(Uri url, CancellationToken cancellationToken) =>
        FetchAsync(url, FetchKind.Image, cancellationToken);

    private async Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, FetchKind kind, CancellationToken cancellationToken)
    {
        var fetched = await fetcher.FetchAsync(url, kind, cancellationToken);
        return fetched.IsSuccess ? Classify(fetched.Value, kind) : Fail(fetched.Failure);
    }

    private Result<FetchedContent, ImportFailure> Classify(RawResponse response, FetchKind kind)
    {
        if (IsChallenge(response.ChallengeMitigation))
        {
            return Fail(ImportFailure.Blocked);
        }

        // The cap applies whether or not the adapter enforced it.
        var body = response.Body is { } whole && whole.Length <= _maxBytes ? whole : null;

        switch (response.StatusCode)
        {
            case 401 or 402 or 403 or 429:
                return Fail(ImportFailure.Blocked);
            case 404 or 410:
                return Fail(ImportFailure.NotFound);
            case 503 when body is not null && ContainsChallengeMarker(body):
                return Fail(ImportFailure.Blocked);
            case < 200 or >= 300:
                return Fail(ImportFailure.BadResponse);
        }

        if (body is null)
        {
            return Fail(ImportFailure.BadResponse);
        }

        if (kind == FetchKind.Page)
        {
            if (!IsHtml(response.ContentType))
            {
                return Fail(ImportFailure.BadResponse);
            }

            if (ContainsChallengeMarker(body))
            {
                return Fail(ImportFailure.Blocked);
            }
        }

        return Result<FetchedContent, ImportFailure>.Ok(new FetchedContent(body, response.ContentType, response.Url));
    }

    private static bool IsChallenge(string? challengeMitigation) =>
        challengeMitigation?.Split(',', StringSplitOptions.TrimEntries).Contains("challenge", StringComparer.OrdinalIgnoreCase) == true;

    private static bool IsHtml(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var parsed)
        && HtmlMediaTypes.Contains(parsed.MediaType, StringComparer.OrdinalIgnoreCase);

    /// <summary>Cloudflare's interstitial is titled "Just a moment..."; it may come with a 200 or 503.</summary>
    private static bool ContainsChallengeMarker(byte[] html) =>
        Encoding.UTF8.GetString(html, 0, Math.Min(html.Length, 4096))
            .Contains("<title>Just a moment", StringComparison.OrdinalIgnoreCase);

    private static Result<FetchedContent, ImportFailure> Fail(ImportFailure failure) =>
        Result<FetchedContent, ImportFailure>.Fail(failure);
}
