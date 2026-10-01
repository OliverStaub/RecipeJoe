using Microsoft.EntityFrameworkCore;
using RecipeJoe.Api.Images;

namespace RecipeJoe.Api.Recipes;

/// <summary>The saved Recipes. The only code that touches the Recipe tables: takes drafts, hands out DTOs, never entities. Image bytes are loaded by <see cref="GetImageAsync"/> only.</summary>
internal sealed class Library(RecipeJoeDbContext db, TimeProvider timeProvider)
{
    public async Task<RecipeDto> SaveAsync(RecipeDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var recipe = new Recipe
        {
            Title = draft.Title,
            Servings = draft.Servings,
            PrepTime = draft.PrepTime,
            CookTime = draft.CookTime,
            TotalTime = draft.TotalTime,
            SourceUrl = draft.Source.AbsoluteUri,
            CreatedAt = timeProvider.GetUtcNow(),
            Image = draft.Image is { } image ? new RecipeImage { ContentType = image.ContentType, Bytes = image.Bytes } : null,
        };
        recipe.IngredientLines.AddRange(draft.IngredientLines.Select((text, i) => new IngredientLine { Position = i, Text = text }));
        recipe.Steps.AddRange(draft.Steps.Select((text, i) => new Step { Position = i, Text = text }));

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);
        return RecipeDto.From(recipe, recipe.Image is not null);
    }

    /// <summary>Newest first. Every whitespace-separated token must match the title or an Ingredient Line, ignoring case; wildcards match literally.</summary>
    public async Task<List<RecipeSummaryDto>> SearchAsync(string? q, CancellationToken cancellationToken)
    {
        var recipes = db.Recipes.AsNoTracking();
        foreach (var token in Tokens(q))
        {
            var pattern = $"%{EscapeLike(token)}%";
            recipes = recipes.Where(r =>
                EF.Functions.ILike(r.Title, pattern, "\\")
                || r.IngredientLines.Any(l => EF.Functions.ILike(l.Text, pattern, "\\"))
            );
        }

        return await recipes
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new RecipeSummaryDto(r.Id, r.Title, r.SourceUrl, r.Image != null, r.SeenAt == null))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Marks the Recipe seen the first time it's fetched, clearing its "Neu" mark in the Library.</summary>
    public async Task<RecipeDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var found = await db
            .Recipes.Where(r => r.Id == id)
            .Select(r => new { Recipe = r, HasImage = r.Image != null })
            .FirstOrDefaultAsync(cancellationToken);
        if (found is null) return null;

        if (found.Recipe.SeenAt is null)
        {
            found.Recipe.SeenAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }

        return RecipeDto.From(found.Recipe, found.HasImage);
    }

    /// <summary>False when there is no such Recipe. Lines, Steps and the image cascade in the database.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await db.Recipes.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public Task<ImageFile?> GetImageAsync(int id, CancellationToken cancellationToken) =>
        db.RecipeImages.AsNoTracking()
            .Where(i => i.RecipeId == id)
            .Select(i => new ImageFile(i.ContentType, i.Bytes))
            .FirstOrDefaultAsync(cancellationToken);

    private static string[] Tokens(string? q) =>
        q is null ? [] : q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string EscapeLike(string token) =>
        token.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
