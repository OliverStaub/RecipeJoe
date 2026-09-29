namespace RecipeJoe.Api.Import;

/// <summary>Placeholder production adapter until the HttpClient fetcher lands (ticket 12).</summary>
internal sealed class UnavailablePageFetcher : IPageFetcher
{
    public Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken) =>
        Task.FromResult(Result<FetchedContent, ImportFailure>.Fail(ImportFailure.Unreachable));
}
