using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using RecipeJoe.Api.Import;
using RecipeJoe.UnitTests.Import;

namespace RecipeJoe.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private Respawner? _respawner;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Db"] = PostgresFixture.ConnectionString }
            );
        });

        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IFetcher, FixtureFetcher>())
        );
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(
            connection,
            new RespawnerOptions { DbAdapter = DbAdapter.Postgres, TablesToIgnore = ["__EFMigrationsHistory"] }
        );

        await _respawner.ResetAsync(connection);
    }
}
