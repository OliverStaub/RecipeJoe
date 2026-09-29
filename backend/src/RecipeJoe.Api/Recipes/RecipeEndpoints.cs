using Microsoft.EntityFrameworkCore;

namespace RecipeJoe.Api.Recipes;

internal static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/recipes",
                async (string? q, RecipeJoeDbContext db, CancellationToken cancellationToken) =>
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
                        .Select(r => new RecipeSummaryDto(r.Id, r.Title, r.SourceUrl, r.Image != null))
                        .ToListAsync(cancellationToken);
                }
            )
            .WithName("ListRecipes")
            .Produces<List<RecipeSummaryDto>>();

        app.MapGet(
                "/api/recipes/{id:int}",
                async (int id, RecipeJoeDbContext db, CancellationToken cancellationToken) =>
                {
                    var found = await db
                        .Recipes.AsNoTracking()
                        .Where(r => r.Id == id)
                        .Select(r => new { Recipe = r, HasImage = r.Image != null })
                        .FirstOrDefaultAsync(cancellationToken);
                    return found is null ? Results.NotFound() : Results.Ok(RecipeDto.From(found.Recipe, found.HasImage));
                }
            )
            .WithName("GetRecipe")
            .Produces<RecipeDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete(
                "/api/recipes/{id:int}",
                async (int id, RecipeJoeDbContext db, CancellationToken cancellationToken) =>
                    // Lines, Steps and the image cascade in the database.
                    await db.Recipes.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) == 0
                        ? Results.NotFound()
                        : Results.NoContent()
            )
            .WithName("DeleteRecipe")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static string[] Tokens(string? q) =>
        q is null ? [] : q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string EscapeLike(string token) =>
        token.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
