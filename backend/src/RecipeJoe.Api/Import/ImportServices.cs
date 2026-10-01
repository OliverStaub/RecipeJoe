using Microsoft.Extensions.Options;

namespace RecipeJoe.Api.Import;

internal static class ImportServices
{
    /// <summary>The Web Import building blocks (fetch policy + parser); consumed by the Imports module's Web path.</summary>
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        services.AddOptions<ImportOptions>().BindConfiguration("Import");
        services
            .AddHttpClient<IFetcher, HttpFetcher>(HttpFetcher.ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(sp =>
                HttpFetcher.CreateHandler(sp.GetRequiredService<IOptions<ImportOptions>>().Value)
            );
        services.AddScoped<FetchPolicy>();
        services.AddScoped<Importer>();
        return services;
    }
}
