using Microsoft.Extensions.AI;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;
using RecipeJoe.Sweep;

namespace RecipeJoe.UnitTests.Sweep;

[TestClass]
public sealed class ModelRunnerTests
{
    private const string OneRecipe = """{"recipes":[{"title":"Pasta","servings":null,"prepMinutes":null,"cookMinutes":null,"totalMinutes":null,"ingredientLines":["Nudeln"],"steps":["Kochen"]}]}""";

    private static string Recipes(int count) =>
        $$"""{"recipes":[{{string.Join(',', Enumerable.Range(1, count).Select(i => $$"""{"title":"Rezept {{i}}","servings":null,"prepMinutes":null,"cookMinutes":null,"totalMinutes":null,"ingredientLines":["Zutat"],"steps":["Schritt"]}"""))}}]}""";

    private static VideoText Recording(string videoId) => new(videoId, "", "");

    /// <summary>The model's answer per golden video: the pre-call says "recipe" unless <c>0</c> recipes are meant, the extraction call lists <c>count</c> recipes.</summary>
    private static StubOpenRouter Model(Func<string, int> recipesFor) =>
        new((body, _) =>
        {
            var video = GoldenCases.All.Single(c => body.Contains($"Titel: {c.VideoId}", StringComparison.Ordinal)).VideoId;
            var count = recipesFor(video);
            // Only the extraction call's schema has ingredient lines.
            return body.Contains("ingredientLines", StringComparison.Ordinal) && !body.Contains("containsRecipe", StringComparison.Ordinal)
                ? StubOpenRouter.Completion(Recipes(count))
                : StubOpenRouter.Completion($$"""{"containsRecipe":{{(count > 0 ? "true" : "false")}}}""");
        });

    private static ModelRunner RunnerFor(StubOpenRouter stub, List<TimeSpan>? delays = null) =>
        new(
            LlmClientFactory.Create,
            (id, _) => Task.FromResult(Recording(id)),
            (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            }
        );

    private static LlmOptions Options(StubOpenRouter stub) => new() { ApiKey = "sk-test", Model = "vendor/model", BaseUrl = stub.BaseUrl };

    private static int ExpectedFor(string video) => GoldenCases.All.Single(c => c.VideoId == video).ExpectedRecipes;

    [TestMethod]
    public async Task A_model_that_finds_the_expected_recipes_passes_every_case_with_tokens_cost_and_provider()
    {
        using var stub = Model(ExpectedFor);

        var run = await RunnerFor(stub).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.IsTrue(run.AllPassed);
        Assert.HasCount(GoldenCases.All.Count, run.Cases);
        // i84... and 6wR... make two calls (check + extraction), the no-recipe video one: five calls of 100 in / 10 out / $0.001.
        Assert.AreEqual(5 * 100, run.Cases.Sum(c => c.InputTokens));
        Assert.AreEqual(5 * 10, run.Cases.Sum(c => c.OutputTokens));
        Assert.AreEqual(0.005m, run.CostUsd);
        Assert.IsTrue(run.Cases.All(c => c.Provider == "StubProvider" && c.Failure is null));
        Assert.IsTrue(run.Cases.Select(c => c.RecipesFound).SequenceEqual([1, 5, null]));
        Assert.IsTrue(run.Cases.All(c => c.Seconds is >= 0));
        Assert.IsNotNull(run.Seconds);
    }

    [TestMethod]
    public async Task The_wrong_number_of_recipes_fails_the_case_as_WrongCount()
    {
        using var stub = Model(video => video == "6wR2T-PexT4" ? 2 : ExpectedFor(video));

        var run = await RunnerFor(stub).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        var wrong = run.Cases.Single(c => !c.Passed);
        Assert.AreEqual("6wR2T-PexT4", wrong.VideoId);
        Assert.AreEqual("WrongCount", wrong.Failure);
        Assert.AreEqual(2, wrong.RecipesFound);
    }

    [TestMethod]
    public async Task Recipes_found_in_a_video_without_any_fail_the_case()
    {
        using var stub = Model(video => video == "6tMZNYQkycI" ? 1 : ExpectedFor(video));

        var run = await RunnerFor(stub).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.AreEqual("WrongCount", run.Cases.Single(c => !c.Passed).Failure);
    }

    [TestMethod]
    public async Task A_bad_reply_is_recorded_as_its_failure_kind_not_thrown()
    {
        using var stub = new StubOpenRouter((_, _) => StubOpenRouter.Completion("not json at all"));

        var run = await RunnerFor(stub).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.IsTrue(run.Cases.All(c => !c.Passed && c.Failure == nameof(ImportFailure.LlmBadOutput)));
    }

    [TestMethod]
    public async Task A_provider_error_inside_a_200_reply_is_recorded_as_a_failure()
    {
        using var stub = new StubOpenRouter((_, _) => (200, """{"error":{"message":"Provider returned error","code":502},"provider":"Flaky"}"""));

        var run = await RunnerFor(stub).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.IsTrue(run.Cases.All(c => !c.Passed && c.Failure == nameof(ImportFailure.LlmUnavailable)));
    }

    [TestMethod]
    public async Task A_402_from_the_in_flight_budget_is_retried_with_growing_backoff_until_it_succeeds()
    {
        var delays = new List<TimeSpan>();
        using var stub = new StubOpenRouter((_, index) => index < 2
            ? (402, """{"error":{"message":"in flight budget exhausted","code":402}}""")
            : StubOpenRouter.Completion("""{"containsRecipe":false}"""));

        var run = await RunnerFor(stub, delays).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.IsTrue(delays.Take(2).SequenceEqual([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)]));
        // The model answers "no recipe" to everything: the first golden case (1 expected) fails, the no-recipe video passes.
        Assert.IsTrue(run.Cases.Single(c => c.VideoId == "6tMZNYQkycI").Passed);
    }

    [TestMethod]
    public async Task A_402_that_never_clears_ends_as_a_failed_case_after_a_bounded_number_of_retries()
    {
        var delays = new List<TimeSpan>();
        using var stub = new StubOpenRouter((_, _) => (402, """{"error":{"message":"in flight budget exhausted","code":402}}"""));

        var run = await RunnerFor(stub, delays).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.IsTrue(run.Cases.All(c => !c.Passed && c.Failure == nameof(ImportFailure.LlmUnavailable)));
        Assert.HasCount(5 * GoldenCases.All.Count, delays);
    }

    [TestMethod]
    public async Task Other_http_errors_are_not_retried()
    {
        var delays = new List<TimeSpan>();
        using var stub = new StubOpenRouter((_, _) => (429, """{"error":{"message":"slow down","code":429}}"""));

        var run = await RunnerFor(stub, delays).RunOnceAsync(Options(stub), stage: 1, CancellationToken.None);

        Assert.IsEmpty(delays);
        Assert.IsTrue(run.Cases.All(c => c.Failure == nameof(ImportFailure.LlmUnavailable)));
    }

    [TestMethod]
    public async Task The_pinned_provider_is_sent_with_every_request_and_kept_in_the_result()
    {
        var bodies = new List<string>();
        using var stub = new StubOpenRouter((body, _) =>
        {
            lock (bodies)
            {
                bodies.Add(body);
            }

            return StubOpenRouter.Completion("""{"containsRecipe":false}""");
        });
        var options = Options(stub);
        options.PinnedProviders = "Google AI Studio";

        var run = await RunnerFor(stub).RunOnceAsync(options, stage: 2, CancellationToken.None);

        Assert.AreEqual("Google AI Studio", run.PinnedProvider);
        Assert.AreEqual(2, run.Stage);
        Assert.IsTrue(bodies.All(b => b.Contains("\"allow_fallbacks\":false", StringComparison.Ordinal) && b.Contains("\"require_parameters\":true", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_repeats_the_golden_cases_and_names_the_model()
    {
        using var stub = Model(ExpectedFor);

        var result = await RunnerFor(stub).RunAsync(Options(stub), repeats: 2, CancellationToken.None);

        Assert.AreEqual("vendor/model", result.Model);
        Assert.HasCount(2, result.Runs);
        Assert.AreEqual(0.010m, result.CostUsd);
    }

    [TestMethod]
    public void Score_treats_NoRecipe_as_the_right_answer_only_when_no_recipe_is_expected()
    {
        var none = new GoldenCase("v", 0);
        var some = new GoldenCase("v", 2);
        var noRecipe = Result<IReadOnlyList<ParsedRecipe>, ImportFailure>.Fail(ImportFailure.NoRecipe);

        Assert.AreEqual((true, (string?)null, (int?)null), ModelRunner.Score(none, noRecipe));
        Assert.AreEqual((false, (string?)"NoRecipe", (int?)null), ModelRunner.Score(some, noRecipe));
    }

    [TestMethod]
    public async Task A_missing_recording_is_a_clear_error()
    {
        var error = await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => GoldenCases.LoadAsync("does-not-exist", CancellationToken.None));

        StringAssert.Contains(error.Message, "just golden");
    }

    [TestMethod]
    public async Task The_committed_recordings_can_be_loaded()
    {
        foreach (var golden in GoldenCases.All)
        {
            var video = await GoldenCases.LoadAsync(golden.VideoId, CancellationToken.None);

            Assert.IsFalse(string.IsNullOrWhiteSpace(video.Title), golden.VideoId);
        }
    }
}
