using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class RecipeExtractorTests
{
    private static readonly VideoText Video = new("Zwei Kuchen", "Backen mit Oma", "Erst Teig, dann Guss.");

    private static RecipeExtractor CreateExtractor(IChatClient client, TimeSpan? timeout = null) =>
        new(client, Options.Create(new LlmOptions { Timeout = timeout ?? TimeSpan.FromSeconds(30) }), NullLogger<RecipeExtractor>.Instance);

    [TestMethod]
    public async Task A_reply_with_several_recipes_becomes_one_ParsedRecipe_per_dish_with_minutes_as_durations()
    {
        var client = FakeChatClient.Replying("""
            {"recipes":[
              {"title":"Apfelkuchen","servings":"12 Stücke","prepMinutes":20,"cookMinutes":45,"totalMinutes":65,
               "ingredientLines":["500 g Äpfel","250 g Mehl"],"steps":["Teig kneten","Backen"]},
              {"title":"Schokokuchen","servings":null,"prepMinutes":null,"cookMinutes":null,"totalMinutes":null,
               "ingredientLines":["200 g Schokolade"],"steps":["Schmelzen"]}
            ]}
            """);

        var result = await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(2, result.Value);
        var apfel = result.Value[0];
        Assert.AreEqual("Apfelkuchen", apfel.Title);
        Assert.AreEqual("12 Stücke", apfel.Servings);
        Assert.AreEqual(TimeSpan.FromMinutes(20), apfel.PrepTime);
        Assert.AreEqual(TimeSpan.FromMinutes(45), apfel.CookTime);
        Assert.AreEqual(TimeSpan.FromMinutes(65), apfel.TotalTime);
        Assert.IsTrue(apfel.IngredientLines.SequenceEqual(["500 g Äpfel", "250 g Mehl"]));
        Assert.IsTrue(apfel.Steps.SequenceEqual(["Teig kneten", "Backen"]));
        Assert.IsNull(apfel.ImageUrl);
        Assert.AreEqual("Schokokuchen", result.Value[1].Title);
    }

    [TestMethod]
    public async Task Servings_and_timings_stay_null_when_the_reply_omits_them()
    {
        var client = FakeChatClient.Replying("""{"recipes":[{"title":"Toast","ingredientLines":["Brot"],"steps":["Rösten"]}]}""");

        var result = await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        var recipe = result.Value.Single();
        Assert.IsNull(recipe.Servings);
        Assert.IsNull(recipe.PrepTime);
        Assert.IsNull(recipe.CookTime);
        Assert.IsNull(recipe.TotalTime);
    }

    [TestMethod]
    public async Task Entries_without_a_title_ingredient_line_or_step_are_skipped_and_the_valid_ones_kept()
    {
        var client = FakeChatClient.Replying("""
            {"recipes":[
              {"title":"  ","ingredientLines":["Brot"],"steps":["Rösten"]},
              {"title":"Ohne Zutaten","ingredientLines":[],"steps":["Rösten"]},
              {"title":"Ohne Schritte","ingredientLines":["Brot"],"steps":[" "]},
              {"title":"Toast","ingredientLines":["Brot"],"steps":["Rösten"]}
            ]}
            """);

        var result = await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Toast", result.Value.Single().Title);
    }

    [TestMethod]
    public async Task A_reply_where_every_entry_is_invalid_is_LlmBadOutput()
    {
        var client = FakeChatClient.Replying("""{"recipes":[{"title":"Leer","ingredientLines":[],"steps":[]}]}""");

        var result = await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        Assert.AreEqual(ImportFailure.LlmBadOutput, result.Failure);
    }

    [TestMethod]
    public async Task An_empty_recipe_list_is_NoRecipe()
    {
        var client = FakeChatClient.Replying("""{"recipes":[]}""");

        var result = await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }

    [TestMethod]
    [DataRow("this is not json")]
    [DataRow("""{"recipes":"nope"}""")]
    [DataRow("{}")]
    public async Task Malformed_or_schema_violating_replies_are_LlmBadOutput(string reply)
    {
        var result = await CreateExtractor(FakeChatClient.Replying(reply)).ExtractAsync(Video, CancellationToken.None);

        Assert.AreEqual(ImportFailure.LlmBadOutput, result.Failure);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.NotFound, DisplayName = "Unknown model")]
    [DataRow(null, DisplayName = "Network error")]
    public async Task A_provider_error_is_LlmUnavailable(HttpStatusCode? status)
    {
        var client = FakeChatClient.Throwing(new HttpRequestException("boom", null, status));

        var result = await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        Assert.AreEqual(ImportFailure.LlmUnavailable, result.Failure);
    }

    [TestMethod]
    public async Task A_provider_slower_than_the_timeout_is_LlmUnavailable()
    {
        var result = await CreateExtractor(new SlowChatClient(), TimeSpan.FromMilliseconds(50)).ExtractAsync(Video, CancellationToken.None);

        Assert.AreEqual(ImportFailure.LlmUnavailable, result.Failure);
    }

    [TestMethod]
    public async Task Cancelling_the_caller_is_not_swallowed_as_a_failure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CreateExtractor(FakeChatClient.Replying("{}")).ExtractAsync(Video, cts.Token));
    }

    [TestMethod]
    public async Task The_request_carries_the_title_description_and_transcript()
    {
        var client = FakeChatClient.Replying("""{"recipes":[]}""");

        await CreateExtractor(client).ExtractAsync(Video, CancellationToken.None);

        var sent = string.Join("\n", client.Requests.Single().Select(m => m.Text));
        Assert.Contains("Zwei Kuchen", sent);
        Assert.Contains("Backen mit Oma", sent);
        Assert.Contains("Erst Teig, dann Guss.", sent);
    }

    private sealed class SlowChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new ChatResponse();
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
