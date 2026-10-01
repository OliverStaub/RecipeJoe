using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class ImportsEndpointsTests
{
    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void ClassCleanup() => _factory.Dispose();

    [TestCleanup]
    public async Task ResetDatabaseAsync() => await _factory.ResetDatabaseAsync();

    private static Task<HttpResponseMessage> StartImportAsync(HttpClient client, string url) =>
        client.PostAsJsonAsync("/api/imports", new { url });

    private static async Task<JsonElement[]> ListImportsAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/imports");
        return [.. list.EnumerateArray()];
    }

    private static Task WaitUntilGoneAsync(HttpClient client, Guid id) =>
        ImportsTestHelper.WaitUntilGoneAsync(client, id);

    private static async Task<JsonElement> WaitUntilFailedAsync(HttpClient client, Guid id)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var found = (await ListImportsAsync(client)).FirstOrDefault(i => i.GetProperty("id").GetGuid() == id);
            if (found.ValueKind != JsonValueKind.Undefined && found.GetProperty("state").GetString() == "Failed")
            {
                return found;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Import {id} did not fail in time.");
        throw new InvalidOperationException("unreachable");
    }

    [TestMethod]
    public async Task Starting_a_web_import_returns_202_then_lands_the_recipe_in_the_library_and_the_import_disappears()
    {
        const string url = "http://fixtures.test/recipes/kartoffelsuppe.html";
        var client = _factory.CreateClient();

        var started = await StartImportAsync(client, url);

        Assert.AreEqual(HttpStatusCode.Accepted, started.StatusCode);
        var body = await started.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.AreEqual(url, body.GetProperty("url").GetString());
        Assert.AreEqual("Web", body.GetProperty("kind").GetString());
        Assert.AreEqual("Pending", body.GetProperty("state").GetString());

        await WaitUntilGoneAsync(client, id);

        var recipes = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        var summary = recipes.EnumerateArray().First(r => r.GetProperty("sourceUrl").GetString() == url);
        var recipe = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{summary.GetProperty("id").GetInt32()}");
        Assert.AreEqual("Kartoffelsuppe", recipe.GetProperty("title").GetString());
        Assert.AreEqual("4 Portionen", recipe.GetProperty("servings").GetString());
        Assert.AreEqual(15, recipe.GetProperty("prepMinutes").GetInt32());
        Assert.AreEqual(30, recipe.GetProperty("cookMinutes").GetInt32());
        Assert.AreEqual(45, recipe.GetProperty("totalMinutes").GetInt32());
        Assert.AreEqual(url, recipe.GetProperty("sourceUrl").GetString());
        Assert.AreEqual(JsonValueKind.Null, recipe.GetProperty("imageUrl").ValueKind);
        Assert.AreEqual(4, recipe.GetProperty("ingredientLines").GetArrayLength());
        Assert.AreEqual("800 g Kartoffeln", recipe.GetProperty("ingredientLines")[0].GetString());
        Assert.AreEqual(3, recipe.GetProperty("steps").GetArrayLength());
        Assert.AreEqual("Pürieren und abschmecken.", recipe.GetProperty("steps")[2].GetString());
    }

    [TestMethod]
    public async Task Starting_an_invalid_url_fails_synchronously_as_a_problem_and_creates_no_import()
    {
        var client = _factory.CreateClient();

        var response = await StartImportAsync(client, "not a url");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("InvalidUrl", problem.GetProperty("kind").GetString());
    }

    [TestMethod]
    [DataRow("http://fixtures.test/recipes/gibt-es-nicht.html", "NotFound")]
    [DataRow("http://fixtures.test/e2e/placeholder.html", "NoRecipe")]
    public async Task A_failing_source_ends_the_import_as_Failed_with_the_kind(string url, string expectedKind)
    {
        var client = _factory.CreateClient();

        var started = await StartImportAsync(client, url);
        var id = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var failed = await WaitUntilFailedAsync(client, id);

        Assert.AreEqual(expectedKind, failed.GetProperty("failure").GetString());
        Assert.IsFalse(failed.TryGetProperty("stage", out var stage) && stage.ValueKind != JsonValueKind.Null);
    }

    [TestMethod]
    [DataRow(403, "text/html", "Blocked")]
    [DataRow(500, "text/html", "BadResponse")]
    [DataRow(200, "application/pdf", "BadResponse")]
    public async Task A_rejected_site_response_ends_the_import_as_Failed_with_the_kind(int fetchStatus, string contentType, string expectedKind)
    {
        var url = $"http://canned.test/imports/{fetchStatus}/{contentType}";
        _factory.Fetcher.Serve(url, fetchStatus, contentType);
        var client = _factory.CreateClient();

        var started = await StartImportAsync(client, url);
        var id = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var failed = await WaitUntilFailedAsync(client, id);

        Assert.AreEqual(expectedKind, failed.GetProperty("failure").GetString());
    }

    [TestMethod]
    public async Task Retrying_a_failed_import_puts_it_back_to_Pending_with_the_same_id_and_it_can_fail_again()
    {
        const string url = "http://fixtures.test/recipes/gibt-es-nicht.html";
        var client = _factory.CreateClient();
        var started = await StartImportAsync(client, url);
        var id = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await WaitUntilFailedAsync(client, id);

        var retried = await client.PostAsync($"/api/imports/{id}/retry", content: null);

        Assert.AreEqual(HttpStatusCode.Accepted, retried.StatusCode);
        var retriedBody = await retried.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(id, retriedBody.GetProperty("id").GetGuid());
        Assert.AreEqual("Pending", retriedBody.GetProperty("state").GetString());

        var failedAgain = await WaitUntilFailedAsync(client, id);
        Assert.AreEqual("NotFound", failedAgain.GetProperty("failure").GetString());
    }

    [TestMethod]
    public async Task Retrying_an_unknown_import_returns_404()
    {
        var response = await _factory.CreateClient().PostAsync($"/api/imports/{Guid.NewGuid()}/retry", content: null);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Dismissing_a_failed_import_removes_it()
    {
        const string url = "http://fixtures.test/recipes/gibt-es-nicht.html";
        var client = _factory.CreateClient();
        var started = await StartImportAsync(client, url);
        var id = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await WaitUntilFailedAsync(client, id);

        var dismissed = await client.DeleteAsync($"/api/imports/{id}");

        Assert.AreEqual(HttpStatusCode.NoContent, dismissed.StatusCode);
        Assert.IsFalse((await ListImportsAsync(client)).Any(i => i.GetProperty("id").GetGuid() == id));
    }

    [TestMethod]
    public async Task Dismissing_an_unknown_import_returns_404()
    {
        var response = await _factory.CreateClient().DeleteAsync($"/api/imports/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
