using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RecipeJoe.Api;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;
using RecipeJoe.Api.Recipes;
using RecipeJoe.Api.Video;

var isBuildTimeOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(args);

if (isBuildTimeOpenApiGeneration)
{
    // The doc generator boots the app without the secret; it never calls the LLM.
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Llm:ApiKey"] = "build-time-placeholder" });
}

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDbContext<RecipeJoeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Db")));

builder.Services.AddLibrary();
builder.Services.AddImages();
builder.Services.AddImport();
builder.Services.AddImports();
builder.Services.AddVideo();

builder.Services.AddOpenApi();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<RecipeJoeDbContext>();

var app = builder.Build();

if (!isBuildTimeOpenApiGeneration)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<RecipeJoeDbContext>().Database.Migrate();
}

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapImportsEndpoints();
app.MapRecipeEndpoints();
app.MapImageEndpoints();

app.Run();

// Exposed so RecipeJoe.IntegrationTests can boot the app via WebApplicationFactory<Program>.
public partial class Program;
