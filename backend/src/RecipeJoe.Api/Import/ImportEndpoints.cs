using Microsoft.AspNetCore.Http.HttpResults;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Import;

internal sealed record ImportRequest(string Url);

/// <summary>RFC 9457 problem body; `kind` is the only failure detail the API exposes.</summary>
internal sealed record ImportProblem(string Type, string Title, int Status, ImportFailure Kind);

internal static class ImportEndpoints
{
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        services.AddSingleton<IPageFetcher, UnavailablePageFetcher>();
        services.AddScoped<Importer>();
        return services;
    }

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/recipes/import", ImportRecipeAsync)
            .WithName("ImportRecipe")
            .Produces<RecipeDto>(StatusCodes.Status201Created)
            .Produces<ImportProblem>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ImportProblem>(StatusCodes.Status422UnprocessableEntity, "application/problem+json");

        return app;
    }

    private static async Task<IResult> ImportRecipeAsync(
        ImportRequest request,
        Importer importer,
        CancellationToken cancellationToken
    )
    {
        var result = await importer.ImportAsync(request.Url, cancellationToken);
        if (result.IsSuccess)
        {
            var dto = RecipeDto.From(result.Value);
            return TypedResults.Created($"/api/recipes/{dto.Id}", dto);
        }

        var status =
            result.Failure == ImportFailure.InvalidUrl
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status422UnprocessableEntity;
        var problem = new ImportProblem(
            $"https://recipejoe/problems/import/{result.Failure}",
            "Import failed",
            status,
            result.Failure
        );
        return TypedResults.Json(problem, statusCode: status, contentType: "application/problem+json");
    }
}
