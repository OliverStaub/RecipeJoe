using Testcontainers.PostgreSql;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public static class PostgresFixture
{
    private static PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
