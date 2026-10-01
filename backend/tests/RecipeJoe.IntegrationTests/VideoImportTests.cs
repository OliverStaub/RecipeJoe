using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Matchers;

namespace RecipeJoe.IntegrationTests;

[TestClass]
public sealed class VideoImportTests
{
    private static ApiFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _factory = new ApiFactory();

    [ClassCleanup]
    public static void ClassCleanup() => _factory.Dispose();

    [TestCleanup]
    public async Task ResetAsync()
    {
        _factory.LlmStub.Reset();
        await _factory.ResetDatabaseAsync();
    }

    private const string TwoCakesReply = """
        {"recipes":[
          {"title":"Apfelkuchen","servings":"12 Stücke","prepMinutes":20,"cookMinutes":45,"ingredientLines":["500 g Äpfel","250 g Mehl"],"steps":["Teig kneten","Backen"]},
          {"title":"Birnenkuchen","ingredientLines":["4 Birnen"],"steps":["Backen"]}
        ]}
        """;

    /// <summary>OpenRouter answers any chat request mentioning <paramref name="videoTitle"/> with <paramref name="content"/> (an OpenAI chat completion wrapping it), or with <paramref name="status"/>.</summary>
    private static void StubLlm(string videoTitle, string content, int status = 200)
    {
        var completion = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 0,
            model = "test",
            choices = new[] { new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" } },
        });

        _factory.LlmStub
            .Given(Request.Create().WithPath("/v1/chat/completions").UsingPost().WithBody(new WildcardMatcher($"*{videoTitle}*")))
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(status == 200 ? completion : "{}"));
    }

    private static async Task<Guid> StartAsync(HttpClient client, string videoId)
    {
        var started = await client.PostAsJsonAsync("/api/imports", new { url = $"https://www.youtube.com/watch?v={videoId}" });
        Assert.AreEqual(HttpStatusCode.Accepted, started.StatusCode);
        var body = await started.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Video", body.GetProperty("kind").GetString());
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>Polls until the Import is gone (succeeded) or Failed; returns the failure kind, or null when it succeeded.</summary>
    private static async Task<string?> WaitForOutcomeAsync(HttpClient client, Guid id)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var list = await client.GetFromJsonAsync<JsonElement>("/api/imports");
            var found = list.EnumerateArray().FirstOrDefault(i => i.GetProperty("id").GetGuid() == id);
            if (found.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (found.GetProperty("state").GetString() == "Failed")
            {
                return found.GetProperty("failure").GetString();
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Import {id} did not finish in time.");
        throw new InvalidOperationException("unreachable");
    }

    [TestMethod]
    public async Task One_video_with_two_dishes_lands_two_recipes_in_the_library_each_with_the_video_as_source_and_the_thumbnail()
    {
        StubLlm("Zwei Kuchen für den Sonntag", TwoCakesReply);
        var client = _factory.CreateClient();

        var id = await StartAsync(client, "zwei-kuchen");

        Assert.IsNull(await WaitForOutcomeAsync(client, id));
        var library = (await client.GetFromJsonAsync<JsonElement>("/api/recipes")).EnumerateArray().ToList();
        Assert.HasCount(2, library);
        Assert.IsTrue(library.Select(r => r.GetProperty("title").GetString()).Order().SequenceEqual(["Apfelkuchen", "Birnenkuchen"]));
        Assert.IsTrue(library.All(r => r.GetProperty("sourceUrl").GetString() == "https://www.youtube.com/watch?v=zwei-kuchen"));
        Assert.IsTrue(library.All(r => r.GetProperty("hasImage").GetBoolean()));

        var apfel = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{library.Single(r => r.GetProperty("title").GetString() == "Apfelkuchen").GetProperty("id").GetInt32()}");
        Assert.AreEqual("12 Stücke", apfel.GetProperty("servings").GetString());
        Assert.AreEqual(20, apfel.GetProperty("prepMinutes").GetInt32());
        Assert.AreEqual(45, apfel.GetProperty("cookMinutes").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, apfel.GetProperty("totalMinutes").ValueKind);
    }

    [TestMethod]
    public async Task The_request_to_the_provider_asks_for_a_json_schema_and_requires_parameter_support()
    {
        StubLlm("Zwei Kuchen für den Sonntag", TwoCakesReply);
        var client = _factory.CreateClient();
        _factory.LlmStub.ResetLogEntries();

        await WaitForOutcomeAsync(client, await StartAsync(client, "zwei-kuchen"));

        var request = JsonDocument.Parse(_factory.LlmStub.LogEntries.Single().RequestMessage!.Body!).RootElement;
        Assert.AreEqual("json_schema", request.GetProperty("response_format").GetProperty("type").GetString());
        Assert.IsTrue(request.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
    }

    [TestMethod]
    [DataRow("ohne-untertitel", "NoCaptions")]
    [DataRow("gibt-es-nicht", "NotFound")]
    public async Task A_video_source_failure_ends_the_import_as_Failed_with_the_kind(string videoId, string expectedKind)
    {
        var client = _factory.CreateClient();

        Assert.AreEqual(expectedKind, await WaitForOutcomeAsync(client, await StartAsync(client, videoId)));
    }

    [TestMethod]
    public async Task A_video_without_a_recipe_fails_with_NoRecipe()
    {
        StubLlm("Mein Urlaub in Italien", """{"recipes":[]}""");
        var client = _factory.CreateClient();

        Assert.AreEqual("NoRecipe", await WaitForOutcomeAsync(client, await StartAsync(client, "kein-rezept")));
    }

    [TestMethod]
    public async Task A_provider_error_fails_with_LlmUnavailable()
    {
        StubLlm("Mein Urlaub in Italien", "", status: 503);
        var client = _factory.CreateClient();

        Assert.AreEqual("LlmUnavailable", await WaitForOutcomeAsync(client, await StartAsync(client, "kein-rezept")));
    }

    [TestMethod]
    public async Task A_malformed_reply_fails_with_LlmBadOutput()
    {
        StubLlm("Mein Urlaub in Italien", "Leider kein JSON.");
        var client = _factory.CreateClient();

        Assert.AreEqual("LlmBadOutput", await WaitForOutcomeAsync(client, await StartAsync(client, "kein-rezept")));
    }
}
