using System.Net;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class HealthEndpointTests
{
    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void ClassCleanup() => _factory.Dispose();

    [TestCleanup]
    public async Task ResetDatabaseAsync() => await _factory.ResetDatabaseAsync();

    [TestMethod]
    public async Task Health_returns_ok_when_the_database_is_reachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
