namespace RecipeJoe.Api.Import;

internal sealed record FetchedContent(byte[] Bytes, string? ContentType);

/// <summary>The only seam: all outbound HTTP (pages and images). Adapters: HttpClient (prod), fixtures (tests).</summary>
internal interface IPageFetcher
{
    Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken);
}
