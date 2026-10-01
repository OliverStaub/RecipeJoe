using RecipeJoe.Api.Import;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Imports;

/// <summary>URL + stage progress → the Recipe Drafts an Import produces, or why it failed. One implementation per Import kind; the runner picks it by URL.</summary>
internal interface IImportPath
{
    Task<Result<IReadOnlyList<RecipeDraft>, ImportFailure>> RunAsync(Uri url, IProgress<ImportStage> progress, CancellationToken cancellationToken);
}
