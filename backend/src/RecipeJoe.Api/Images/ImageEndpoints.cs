using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Images;

internal static class ImageEndpoints
{
    private const string CacheForever = "public, max-age=31536000, immutable";

    public static IServiceCollection AddImages(this IServiceCollection services) => services.AddScoped<ImageDownloader>();

    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/recipes/{id:int}/image",
                async (int id, HttpContext context, Library library, CancellationToken cancellationToken) =>
                {
                    var image = await library.GetImageAsync(id, cancellationToken);
                    if (image is null)
                    {
                        return Results.NotFound();
                    }

                    context.Response.Headers.CacheControl = CacheForever;
                    return Results.Bytes(image.Bytes, image.ContentType);
                }
            )
            .WithName("GetRecipeImage")
            .Produces<byte[]>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp", "image/gif")
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
