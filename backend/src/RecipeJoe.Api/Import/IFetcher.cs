namespace RecipeJoe.Api.Import;

internal enum FetchKind
{
    Page,
    Image,
}

/// <summary>What the server sent, unjudged. <see cref="FetchPolicy"/> decides what it means.</summary>
/// <param name="ChallengeMitigation">The <c>cf-mitigated</c> header, if any.</param>
/// <param name="Body">Null when the adapter stopped reading because the body is over the cap.</param>
/// <param name="Url">Where the response came from: after redirects, so relative links resolve against it.</param>
internal sealed record RawResponse(int StatusCode, string? ContentType, string? ChallengeMitigation, byte[]? Body, Uri Url);

/// <summary>The only seam: all outbound HTTP (pages and images), only moving bytes. Adapters: HttpClient (prod), fixtures (tests). Only <see cref="FetchPolicy"/> calls it.</summary>
internal interface IFetcher
{
    /// <summary>A response of any status, or a transport failure: Unreachable, ForbiddenAddress or BadResponse (broken redirects).</summary>
    Task<Result<RawResponse, ImportFailure>> FetchAsync(Uri url, FetchKind kind, CancellationToken cancellationToken);
}
