namespace RecipeJoe.Api.Import;

internal sealed record ParsedRecipe(
    string Title,
    string? Servings,
    TimeSpan? PrepTime,
    TimeSpan? CookTime,
    TimeSpan? TotalTime,
    IReadOnlyList<string> IngredientLines,
    IReadOnlyList<string> Steps
);
