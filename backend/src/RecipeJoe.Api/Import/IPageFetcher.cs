namespace RecipeJoe.Api.Import;

internal sealed record FetchedContent(byte[] Bytes, string? ContentType);

/// <summary>The only seam: all outbound HTTP (pages and images). Adapters: HttpClient (prod), fixtures (tests).</summary>
internal interface IPageFetcher
{
    /// <summary>An HTML page.</summary>
    Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>An image of any content type; the caller checks the bytes.</summary>
    Task<Result<FetchedContent, ImportFailure>> FetchImageAsync(Uri url, CancellationToken cancellationToken);
}
