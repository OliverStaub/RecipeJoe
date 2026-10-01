using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RecipeJoe.IntegrationTests;

/// <summary>Drives a Web Import to completion through the public Imports API, for tests that just need a Recipe in the Library.</summary>
internal static class ImportsTestHelper
{
    public static async Task<JsonElement> StartImportAsync(HttpClient client, string url)
    {
        var response = await client.PostAsJsonAsync("/api/imports", new { url });
        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode, url);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task WaitUntilGoneAsync(HttpClient client, Guid id)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var list = await client.GetFromJsonAsync<JsonElement>("/api/imports");
            if (!list.EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == id))
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Import {id} did not disappear in time.");
    }

    /// <summary>Starts a Web Import for <paramref name="url"/>, waits for it to succeed, and returns the resulting Recipe's id (the one that wasn't in the Library before; a redirect's Source differs from <paramref name="url"/>, so matching by id-diff rather than Source).</summary>
    public static async Task<int> ImportAsync(HttpClient client, string url)
    {
        var before = await IdsAsync(client);

        var started = await StartImportAsync(client, url);
        await WaitUntilGoneAsync(client, started.GetProperty("id").GetGuid());

        var after = await IdsAsync(client);
        return after.Except(before).Single();
    }

    private static async Task<HashSet<int>> IdsAsync(HttpClient client)
    {
        var recipes = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        return [.. recipes.EnumerateArray().Select(r => r.GetProperty("id").GetInt32())];
    }

    /// <summary>Convenience for the fixtures site: <paramref name="page"/> is a filename under <c>/recipes/</c>.</summary>
    public static Task<int> ImportFixtureAsync(HttpClient client, string page) =>
        ImportAsync(client, $"http://fixtures.test/recipes/{page}");
}
