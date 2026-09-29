using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RecipeJoe.Api;

var isBuildTimeOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.AddDbContext<RecipeJoeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Db")));

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

app.Run();

// Exposed so RecipeJoe.IntegrationTests can boot the app via WebApplicationFactory<Program>.
public partial class Program;
