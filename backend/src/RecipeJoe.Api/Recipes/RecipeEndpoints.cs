namespace RecipeJoe.Api.Recipes;

internal static class RecipeEndpoints
{
    public static IServiceCollection AddLibrary(this IServiceCollection services) => services.AddScoped<Library>();

    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/recipes",
                (string? q, Library library, CancellationToken cancellationToken) => library.SearchAsync(q, cancellationToken)
            )
            .WithName("ListRecipes")
            .Produces<List<RecipeSummaryDto>>();

        app.MapGet(
                "/api/recipes/{id:int}",
                async (int id, Library library, CancellationToken cancellationToken) =>
                    await library.GetAsync(id, cancellationToken) is { } recipe ? Results.Ok(recipe) : Results.NotFound()
            )
            .WithName("GetRecipe")
            .Produces<RecipeDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete(
                "/api/recipes/{id:int}",
                async (int id, Library library, CancellationToken cancellationToken) =>
                    await library.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound()
            )
            .WithName("DeleteRecipe")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
