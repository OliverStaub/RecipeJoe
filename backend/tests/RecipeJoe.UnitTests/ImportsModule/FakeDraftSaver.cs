using RecipeJoe.Api.Imports;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.UnitTests.ImportsModule;

/// <summary>Saves nothing for real; <see cref="Outcome"/> decides whether each Draft "saved".</summary>
internal sealed class FakeDraftSaver : IDraftSaver
{
    public Func<RecipeDraft, bool> Outcome { get; set; } = _ => true;

    public Task<bool> TrySaveAsync(RecipeDraft draft, CancellationToken cancellationToken) => Task.FromResult(Outcome(draft));
}
