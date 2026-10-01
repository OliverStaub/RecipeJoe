using System.Threading.Channels;
using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Imports;

internal sealed record StartImportRequest(string Url);

internal enum ImportState
{
    Pending,
    Failed,
}

internal sealed record ImportDto(Guid Id, string Url, ImportKind Kind, ImportState State, ImportStage? Stage, ImportFailure? Failure)
{
    public static ImportDto From(Import import) =>
        new(import.Id, import.Url.AbsoluteUri, import.Kind, import.IsFailed ? ImportState.Failed : ImportState.Pending, import.Stage, import.Failure);
}

/// <summary>RFC 9457 problem body for the one synchronous Import failure; `kind` is always <see cref="ImportFailure.InvalidUrl"/>.</summary>
internal sealed record ImportsProblem(string Type, string Title, int Status, ImportFailure Kind);

internal static class ImportsEndpoints
{
    public static IServiceCollection AddImports(this IServiceCollection services)
    {
        var queue = Channel.CreateUnbounded<Guid>();
        services.AddSingleton(queue.Reader);
        services.AddSingleton(queue.Writer);
        services.AddSingleton<ImportStore>();
        services.AddSingleton<ImportService>();
        services.AddKeyedScoped<IImportPath, WebImportPath>(ImportKind.Web);
        services.AddScoped<IDraftSaver, LibraryDraftSaver>();
        services.AddHostedService<ImportRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapImportsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/imports", StartImportAsync)
            .WithName("StartImport")
            .Produces<ImportDto>(StatusCodes.Status202Accepted)
            .Produces<ImportsProblem>(StatusCodes.Status400BadRequest, "application/problem+json");

        app.MapGet("/api/imports", ListImports).WithName("ListImports").Produces<List<ImportDto>>();

        app.MapPost("/api/imports/{id:guid}/retry", RetryImport)
            .WithName("RetryImport")
            .Produces<ImportDto>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/imports/{id:guid}", DismissImport)
            .WithName("DismissImport")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult StartImportAsync(StartImportRequest request, ImportService imports)
    {
        var result = imports.Start(request.Url);
        if (!result.IsSuccess)
        {
            var problem = new ImportsProblem(
                "https://recipejoe/problems/import/InvalidUrl", "Import failed", StatusCodes.Status400BadRequest, result.Failure);
            return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
        }

        return Results.Accepted(value: ImportDto.From(result.Value));
    }

    private static List<ImportDto> ListImports(ImportService imports) => [.. imports.List().Select(ImportDto.From)];

    private static IResult RetryImport(Guid id, ImportService imports) =>
        imports.TryRetry(id, out var import) ? Results.Accepted(value: ImportDto.From(import!)) : Results.NotFound();

    private static IResult DismissImport(Guid id, ImportService imports) => imports.TryDismiss(id) ? Results.NoContent() : Results.NotFound();
}
