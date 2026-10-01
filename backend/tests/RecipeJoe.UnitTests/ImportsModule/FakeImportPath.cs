using RecipeJoe.Api;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.UnitTests.ImportsModule;

/// <summary>An <see cref="IImportPath"/> whose behaviour the test controls completely.</summary>
internal sealed class FakeImportPath : IImportPath
{
    public Func<Uri, IProgress<ImportStage>, CancellationToken, Task<Result<IReadOnlyList<RecipeDraft>, ImportFailure>>> Behavior { get; set; } =
        (_, _, _) => Task.FromResult(Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([]));

    public Task<Result<IReadOnlyList<RecipeDraft>, ImportFailure>> RunAsync(Uri url, IProgress<ImportStage> progress, CancellationToken cancellationToken) =>
        Behavior(url, progress, cancellationToken);
}
