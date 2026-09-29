using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeJoe.Api;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class DeleteRecipeTests
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

    private static async Task<int> CountAsync(Func<RecipeJoeDbContext, Task<int>> count)
    {
        using var scope = _factory.Services.CreateScope();
        return await count(scope.ServiceProvider.GetRequiredService<RecipeJoeDbContext>());
    }

    [TestMethod]
    public async Task Deleting_a_recipe_returns_204_and_it_is_gone_from_get_and_the_library()
    {
        var client = _factory.CreateClient();
        var id = await ImportAsync(client, "apfelkuchen.html");

        var response = await client.DeleteAsync($"/api/recipes/{id}");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{id}/image")).StatusCode);
        Assert.AreEqual(0, (await client.GetFromJsonAsync<JsonElement>("/api/recipes")).GetArrayLength());
    }

    [TestMethod]
    public async Task Deleting_removes_lines_steps_and_image_but_leaves_other_recipes()
    {
        var client = _factory.CreateClient();
        var doomed = await ImportAsync(client, "apfelkuchen.html");
        var kept = await ImportAsync(client, "brezeln.html");
        var keptRecipe = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{kept}");
        var keptLines = keptRecipe.GetProperty("ingredientLines").GetArrayLength();
        var keptSteps = keptRecipe.GetProperty("steps").GetArrayLength();

        await client.DeleteAsync($"/api/recipes/{doomed}");

        Assert.AreEqual(1, await CountAsync(db => db.Recipes.CountAsync()));
        Assert.AreEqual(1, await CountAsync(db => db.RecipeImages.CountAsync()));
        Assert.AreEqual(keptLines, await CountAsync(db => db.Database.SqlQuery<int>($"""SELECT "Position" AS "Value" FROM "IngredientLines" """).CountAsync()));
        Assert.AreEqual(keptSteps, await CountAsync(db => db.Database.SqlQuery<int>($"""SELECT "Position" AS "Value" FROM "Steps" """).CountAsync()));
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/api/recipes/{kept}/image")).StatusCode);
    }

    [TestMethod]
    public async Task Deleting_an_unknown_recipe_returns_404()
    {
        var response = await _factory.CreateClient().DeleteAsync("/api/recipes/9999");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
