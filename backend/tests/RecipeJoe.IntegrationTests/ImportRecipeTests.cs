using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class ImportRecipeTests
{
    private const string KartoffelsuppeUrl = "http://fixtures.test/recipes/kartoffelsuppe.html";

    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void ClassCleanup() => _factory.Dispose();

    [TestCleanup]
    public async Task ResetDatabaseAsync() => await _factory.ResetDatabaseAsync();

    private static Task<HttpResponseMessage> ImportAsync(HttpClient client, string url) =>
        client.PostAsJsonAsync("/api/recipes/import", new { url });

    [TestMethod]
    public async Task Import_creates_a_recipe_that_GET_returns()
    {
        var client = _factory.CreateClient();

        var created = await ImportAsync(client, KartoffelsuppeUrl);

        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt32();
        Assert.AreEqual($"/api/recipes/{id}", created.Headers.Location?.OriginalString);

        var fetched = await client.GetAsync(created.Headers.Location);

        Assert.AreEqual(HttpStatusCode.OK, fetched.StatusCode);
        var recipe = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Kartoffelsuppe", recipe.GetProperty("title").GetString());
        Assert.AreEqual("4 Portionen", recipe.GetProperty("servings").GetString());
        Assert.AreEqual(15, recipe.GetProperty("prepMinutes").GetInt32());
        Assert.AreEqual(30, recipe.GetProperty("cookMinutes").GetInt32());
        Assert.AreEqual(45, recipe.GetProperty("totalMinutes").GetInt32());
        Assert.AreEqual(KartoffelsuppeUrl, recipe.GetProperty("sourceUrl").GetString());
        Assert.AreEqual(JsonValueKind.Null, recipe.GetProperty("imageUrl").ValueKind);
        Assert.AreEqual(4, recipe.GetProperty("ingredientLines").GetArrayLength());
        Assert.AreEqual("800 g Kartoffeln", recipe.GetProperty("ingredientLines")[0].GetString());
        Assert.AreEqual(3, recipe.GetProperty("steps").GetArrayLength());
        Assert.AreEqual("Pürieren und abschmecken.", recipe.GetProperty("steps")[2].GetString());
    }

    [TestMethod]
    public async Task GET_returns_404_for_an_unknown_recipe()
    {
        var response = await _factory.CreateClient().GetAsync("/api/recipes/9999");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    [DataRow("not a url", HttpStatusCode.BadRequest, "InvalidUrl")]
    [DataRow("http://fixtures.test/recipes/gibt-es-nicht.html", HttpStatusCode.UnprocessableEntity, "NotFound")]
    [DataRow("http://fixtures.test/e2e/placeholder.html", HttpStatusCode.UnprocessableEntity, "NoRecipe")]
    public async Task A_failed_import_returns_the_kind_as_problem_details_and_saves_nothing(
        string url,
        HttpStatusCode expectedStatus,
        string expectedKind
    )
    {
        var client = _factory.CreateClient();

        var response = await ImportAsync(client, url);

        Assert.AreEqual(expectedStatus, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(expectedKind, problem.GetProperty("kind").GetString());
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/api/recipes/1")).StatusCode);
    }
}
