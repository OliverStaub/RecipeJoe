using Microsoft.EntityFrameworkCore;

namespace RecipeJoe.Api.Recipes;

internal static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
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

        return app;
    }
}
