using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;

namespace RecipeJoe.GoldenTests;

/// <summary>
/// Opt-in check of the extraction prompt against a real LLM (`just golden`); never part of `just test` or CI.
/// Each video is recorded once (in the FakeVideoSource shape) into Recordings/, then replayed, so a run judges the prompt, not YouTube.
/// Delete a recording to re-record it. Config comes from the same Llm__* variables as the API. The API key is required.
/// </summary>
[TestClass]
public sealed class GoldenCheckTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] GermanWords = [" und ", " die ", " der ", " das ", " in ", " mit ", " auf ", " zu "];

    public TestContext TestContext { get; set; } = null!;

    /// <summary>What a good extraction looks like for each video. Judged by the report below plus these hard checks: recipe count, German output, no invented numbers.</summary>
    public static IEnumerable<object[]> Videos =>
    [
        // id, expected recipe count (0 = NoRecipe)
        ["i84Sc5uvQa8", 1], // EN, one pasta dish; description states "serves 2-3"
        ["6wR2T-PexT4", 5], // long DE, noisy start (ads, Russian), chapters "Rezept 1" to "Rezept 5"
        ["6tMZNYQkycI", 0], // transcript is only "[Music]"; the ten recipes exist only as on-screen text
    ];

    [TestMethod]
    [DynamicData(nameof(Videos))]
    public async Task Extracts_the_expected_recipes(string videoId, int expectedRecipes)
    {
        var video = await LoadRecordingAsync(videoId);
        var options = LlmOptionsFromEnvironment();
        using var chat = LlmClientFactory.Create(options);
        var extractor = new RecipeExtractor(chat, Options.Create(options), new TestContextLogger<RecipeExtractor>(TestContext));

        var result = await extractor.ExtractAsync(video, TestContext.CancellationToken);

        if (expectedRecipes == 0)
        {
            Assert.IsFalse(result.IsSuccess, Report(videoId, result));
            Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
            return;
        }

        TestContext.WriteLine(Report(videoId, result));
        Assert.IsTrue(result.IsSuccess, Report(videoId, result));
        var recipes = result.Value;
        Assert.HasCount(expectedRecipes, recipes, Report(videoId, result));
        foreach (var recipe in recipes)
        {
            Assert.IsTrue(LooksGerman(string.Join(' ', recipe.Steps)), $"Steps of '{recipe.Title}' don't read as German.");
        }
    }

    private static string Report(string videoId, Result<IReadOnlyList<ParsedRecipe>, ImportFailure> result)
    {
        if (!result.IsSuccess)
        {
            return $"{videoId}: {result.Failure}";
        }

        var lines = result.Value.Select(r =>
            $"  - {r.Title}: {r.IngredientLines.Count} Zutaten, {r.Steps.Count} Schritte, Portionen={r.Servings ?? "null"}, prep={Minutes(r.PrepTime)}, cook={Minutes(r.CookTime)}, total={Minutes(r.TotalTime)}"
        );
        return $"{videoId}: {result.Value.Count} Rezept(e)\n{string.Join('\n', lines)}";
    }

    private static string Minutes(TimeSpan? time) => time is { } t ? $"{t.TotalMinutes:0}" : "null";

    /// <summary>Crude on purpose: German recipe steps contain at least a few of these very common words; English or Spanish ones almost never do.</summary>
    private static bool LooksGerman(string text) =>
        GermanWords.Count(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)) >= 3;

    private static LlmOptions LlmOptionsFromEnvironment()
    {
        var options = new LlmOptions
        {
            BaseUrl = Environment.GetEnvironmentVariable("Llm__BaseUrl"),
            Model = Environment.GetEnvironmentVariable("Llm__Model"),
            ApiKey = Environment.GetEnvironmentVariable("Llm__ApiKey"),
        };
        var providerName = Environment.GetEnvironmentVariable("Llm__Provider");
        if (providerName is not null)
        {
            Assert.IsTrue(Enum.TryParse<LlmProvider>(providerName, out var provider), $"Llm__Provider '{providerName}' is not a supported provider.");
            options.Provider = provider;
        }

        if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("Llm__Timeout"), out var timeout))
        {
            options.Timeout = timeout;
        }

        var validation = new LlmOptionsValidator().Validate(null, options);
        if (validation.Failed)
        {
            Assert.Fail($"Golden check needs the Llm__* config of the API: {validation.FailureMessage}");
        }

        return options;
    }

    private static async Task<VideoText> LoadRecordingAsync(string videoId)
    {
        var file = Path.Combine(RecordingsDirectory(), $"{videoId}.json");
        if (!File.Exists(file))
        {
            using var http = new HttpClient();
            var loaded = await new YoutubeExplodeVideoSource(http).LoadAsync(new Uri($"https://www.youtube.com/watch?v={videoId}"), CancellationToken.None);
            if (!loaded.IsSuccess)
            {
                Assert.Fail($"Could not record {videoId}: {loaded.Failure}");
            }

            var text = loaded.Value.Text;
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { text.Title, text.Description, text.Transcript }, Json));
        }

        var recorded = JsonSerializer.Deserialize<VideoText>(await File.ReadAllTextAsync(file), Json)!;
        return recorded;
    }

    /// <summary>The source directory, so a fresh recording lands next to the committed ones.</summary>
    private static string RecordingsDirectory([CallerFilePath] string thisFile = "") => Path.Combine(Path.GetDirectoryName(thisFile)!, "Recordings");

    /// <summary>Surfaces the extractor's own diagnostics (raw bad replies, skipped entries) in the test output.</summary>
    private sealed class TestContextLogger<T>(TestContext context) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            context.WriteLine($"[{logLevel}] {formatter(state, exception)}{(exception is null ? "" : $" {exception.Message}")}");
    }
}
