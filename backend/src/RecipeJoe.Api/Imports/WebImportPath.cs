using RecipeJoe.Api.Import;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Imports;

/// <summary>The Web Import path: wraps the existing <see cref="Importer"/> unchanged, as a single Recipe Draft.</summary>
internal sealed class WebImportPath(Importer importer) : IImportPath
{
    public async Task<Result<IReadOnlyList<RecipeDraft>, ImportFailure>> RunAsync(
        Uri url,
        IProgress<ImportStage> progress,
        CancellationToken cancellationToken
    )
    {
        progress.Report(ImportStage.Fetching);
        var result = await importer.ImportAsync(url.AbsoluteUri, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Fail(result.Failure);
        }

        progress.Report(ImportStage.Extracting);
        return Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([result.Value]);
    }
}
