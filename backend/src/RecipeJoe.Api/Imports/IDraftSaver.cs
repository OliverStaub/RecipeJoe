using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Imports;

/// <summary>Saves one Recipe Draft in its own transaction; false when it couldn't be saved, so one bad Draft doesn't cost the others.</summary>
internal interface IDraftSaver
{
    Task<bool> TrySaveAsync(RecipeDraft draft, CancellationToken cancellationToken);
}
