using RecipeJoe.Api.Images;

namespace RecipeJoe.Api.Recipes;

internal sealed class Recipe
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Servings { get; set; }

    public TimeSpan? PrepTime { get; set; }

    public TimeSpan? CookTime { get; set; }

    public TimeSpan? TotalTime { get; set; }

    public required string SourceUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<IngredientLine> IngredientLines { get; } = [];

    public List<Step> Steps { get; } = [];

    /// <summary>Never loaded with the Recipe; query <see cref="RecipeJoeDbContext.RecipeImages"/> or project `Image != null`.</summary>
    public RecipeImage? Image { get; set; }
}

internal sealed class IngredientLine
{
    public int Position { get; set; }

    public required string Text { get; set; }
}

internal sealed class Step
{
    public int Position { get; set; }

    public required string Text { get; set; }
}
