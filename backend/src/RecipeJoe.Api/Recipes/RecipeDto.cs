namespace RecipeJoe.Api.Recipes;

internal sealed record RecipeDto(
    int Id,
    string Title,
    string? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    int? TotalMinutes,
    IReadOnlyList<string> IngredientLines,
    IReadOnlyList<string> Steps,
    string SourceUrl,
    string? ImageUrl,
    DateTimeOffset CreatedAt
)
{
    public static RecipeDto From(Recipe recipe, bool hasImage) =>
        new(
            recipe.Id,
            recipe.Title,
            recipe.Servings,
            Minutes(recipe.PrepTime),
            Minutes(recipe.CookTime),
            Minutes(recipe.TotalTime),
            [.. recipe.IngredientLines.OrderBy(l => l.Position).Select(l => l.Text)],
            [.. recipe.Steps.OrderBy(s => s.Position).Select(s => s.Text)],
            recipe.SourceUrl,
            hasImage ? $"/api/recipes/{recipe.Id}/image" : null,
            recipe.CreatedAt
        );

    private static int? Minutes(TimeSpan? time) => time is null ? null : (int)Math.Round(time.Value.TotalMinutes);
}
