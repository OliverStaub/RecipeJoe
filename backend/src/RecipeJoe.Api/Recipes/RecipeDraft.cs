using RecipeJoe.Api.Images;

namespace RecipeJoe.Api.Recipes;

/// <summary>A Recipe not yet in the Library: what an Import produces and <see cref="Library.SaveAsync"/> takes.</summary>
internal sealed record RecipeDraft(
    string Title,
    string? Servings,
    TimeSpan? PrepTime,
    TimeSpan? CookTime,
    TimeSpan? TotalTime,
    IReadOnlyList<string> IngredientLines,
    IReadOnlyList<string> Steps,
    Uri Source,
    ImageFile? Image
);
