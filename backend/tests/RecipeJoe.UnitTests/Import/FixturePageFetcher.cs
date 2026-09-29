using RecipeJoe.Api;
using RecipeJoe.Api.Import;

namespace RecipeJoe.UnitTests.Import;

/// <summary>Serves the committed /fixtures pages by URL path (e.g. http://fixtures.test/recipes/x.html). Also linked into the integration tests.</summary>
internal sealed class FixturePageFetcher : IPageFetcher
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "fixtures");

    public Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        var file = Path.Combine(Root, url.AbsolutePath.TrimStart('/'));
        var result = File.Exists(file)
            ? Result<FetchedContent, ImportFailure>.Ok(new FetchedContent(File.ReadAllBytes(file), "text/html"))
            : Result<FetchedContent, ImportFailure>.Fail(ImportFailure.NotFound);

        return Task.FromResult(result);
    }
}
