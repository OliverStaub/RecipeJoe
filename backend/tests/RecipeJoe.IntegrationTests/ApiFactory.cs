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
using WireMock.Server;

namespace RecipeJoe.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private Respawner? _respawner;

    /// <summary>The app's fetch adapter; <c>Serve</c> canned responses on it to shape what an Import sees. Shared by every test using this factory, so use a unique URL per test.</summary>
    internal FixtureFetcher Fetcher { get; } = new();

    /// <summary>In-process stand-in for OpenRouter (the app's real HTTP adapter talks to it). Stub per test by matching on the video's title, which the request carries.</summary>
    internal WireMockServer LlmStub { get; } = WireMockServer.Start();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Db"] = PostgresFixture.ConnectionString,
                    ["VideoSource:Provider"] = "Fake",
                    ["VideoSource:FakeDirectory"] = Path.Combine(AppContext.BaseDirectory, "fixtures", "videos"),
                    ["Llm:Provider"] = "OpenRouter",
                    ["Llm:ApiKey"] = "test-key",
                    ["Llm:BaseUrl"] = $"{LlmStub.Url}/v1",
                    ["Llm:Timeout"] = "00:00:05",
                }
            );
        });

        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IFetcher>(Fetcher))
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            LlmStub.Dispose();
        }

        base.Dispose(disposing);
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
