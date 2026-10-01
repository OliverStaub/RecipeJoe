using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Imports;

internal sealed partial class LibraryDraftSaver(Library library, ILogger<LibraryDraftSaver> logger) : IDraftSaver
{
    public async Task<bool> TrySaveAsync(RecipeDraft draft, CancellationToken cancellationToken)
    {
        try
        {
            await library.SaveAsync(draft, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveFailed(draft.Source, ex);
            return false;
        }
    }

    [LoggerMessage(LogLevel.Error, "Recipe Draft from {Source} could not be saved")]
    private partial void LogSaveFailed(Uri source, Exception exception);
}
