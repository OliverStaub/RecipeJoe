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
                    var recipe = await db.Recipes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
                    return recipe is null ? Results.NotFound() : Results.Ok(RecipeDto.From(recipe));
                }
            )
            .WithName("GetRecipe")
            .Produces<RecipeDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
