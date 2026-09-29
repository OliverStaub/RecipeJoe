using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeJoe.Api;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class RecipeImageTests
{
    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void ClassCleanup() => _factory.Dispose();

    [TestCleanup]
    public async Task ResetDatabaseAsync() => await _factory.ResetDatabaseAsync();

    private static async Task<int> ImportAsync(HttpClient client, string page)
    {
        var response = await client.PostAsJsonAsync("/api/recipes/import", new { url = $"http://fixtures.test/recipes/{page}" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, page);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [TestMethod]
    [DataRow("apfelkuchen", "apfelkuchen.jpg", "image/jpeg")]
    [DataRow("brezeln", "brezeln.png", "image/png")]
    [DataRow("flammkuchen", "flammkuchen.webp", "image/webp")]
    [DataRow("fried-rice", "fried-rice.gif", "image/gif")]
    public async Task The_image_endpoint_serves_the_stored_bytes_type_and_a_long_lived_cache_header(
        string page,
        string file,
        string contentType
    )
    {
        var client = _factory.CreateClient();
        var id = await ImportAsync(client, $"{page}.html");

        var response = await client.GetAsync($"/api/recipes/{id}/image");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
        var expected = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "images", file));
        CollectionAssert.AreEqual(expected, await response.Content.ReadAsByteArrayAsync());
    }

    [TestMethod]
    public async Task The_recipe_points_at_its_image_when_it_has_one()
    {
        var client = _factory.CreateClient();
        var id = await ImportAsync(client, "apfelkuchen.html");

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{id}");

        Assert.AreEqual($"/api/recipes/{id}/image", fetched.GetProperty("imageUrl").GetString());
    }

    [TestMethod]
    public async Task The_import_response_points_at_the_image_too()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/recipes/import",
            new { url = "http://fixtures.test/recipes/apfelkuchen.html" }
        );

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual($"/api/recipes/{body.GetProperty("id").GetInt32()}/image", body.GetProperty("imageUrl").GetString());
    }

    [TestMethod]
    [DataRow("kartoffelsuppe.html", DisplayName = "no image")]
    [DataRow("haferkekse.html", DisplayName = "image url that 404s")]
    public async Task A_recipe_without_a_usable_image_is_still_imported_and_has_no_image(string page)
    {
        var client = _factory.CreateClient();
        var id = await ImportAsync(client, page);

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{id}");

        Assert.AreEqual(JsonValueKind.Null, fetched.GetProperty("imageUrl").ValueKind);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{id}/image")).StatusCode);
    }

    [TestMethod]
    public async Task The_image_endpoint_returns_404_for_an_unknown_recipe()
    {
        var response = await _factory.CreateClient().GetAsync("/api/recipes/9999/image");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Deleting_the_recipe_cascades_to_its_image()
    {
        var id = await ImportAsync(_factory.CreateClient(), "apfelkuchen.html");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeJoeDbContext>();
        Assert.AreEqual(1, await db.RecipeImages.CountAsync());

        await db.Recipes.Where(r => r.Id == id).ExecuteDeleteAsync();

        Assert.AreEqual(0, await db.RecipeImages.CountAsync());
    }
}
