using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;

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
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();

        if (_respawner is null)
        {
            try
            {
                _respawner = await Respawner.CreateAsync(
                    connection,
                    new RespawnerOptions { DbAdapter = DbAdapter.Postgres, TablesToIgnore = ["__EFMigrationsHistory"] }
                );
            }
            catch (InvalidOperationException)
            {
                // No data tables exist yet (the skeleton's model has none); nothing to reset until real entities land.
                return;
            }
        }

        await _respawner.ResetAsync(connection);
    }
}
