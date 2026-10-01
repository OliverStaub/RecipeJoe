using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;

namespace RecipeJoe.Sweep;

/// <summary>What one golden case did in one run. <see cref="Failure"/> is an <see cref="ImportFailure"/> name, or <c>WrongCount</c> when the model answered but with the wrong number of Recipes.</summary>
internal sealed record CaseOutcome(
    string VideoId,
    int ExpectedRecipes,
    bool Passed,
    string? Failure,
    int? RecipesFound,
    long InputTokens,
    long OutputTokens,
    decimal CostUsd,
    string? Provider,
    double? Seconds = null
);

/// <summary>All golden cases once. Stage 1 is the cheap first pass, stage 2 the repeats on the survivors.</summary>
internal sealed record RunOutcome(int Stage, string? PinnedProvider, IReadOnlyList<CaseOutcome> Cases)
{
    [JsonIgnore]
    public decimal CostUsd => Cases.Sum(c => c.CostUsd);

    /// <summary>Wall-clock time of the whole pass; null when any case has no timing (older result files).</summary>
    [JsonIgnore]
    public double? Seconds => Cases.All(c => c.Seconds is not null) ? Cases.Sum(c => c.Seconds!.Value) : null;

    [JsonIgnore]
    public bool AllPassed => Cases.All(c => c.Passed);
}

/// <summary>Everything measured for one model; one JSON file each.</summary>
internal sealed record ModelResult(string Model, IReadOnlyList<RunOutcome> Runs)
{
    [JsonIgnore]
    public decimal CostUsd => Runs.Sum(r => r.CostUsd);
}

/// <summary>Counts tokens and cost over the calls of one extraction, and retries OpenRouter's transient 402 (in-flight budget exhausted) with backoff. Any other provider error passes through for the extractor to turn into a failure.</summary>
internal sealed class MeteringChatClient(IChatClient inner, Func<TimeSpan, CancellationToken, Task> delay, int maxBudgetRetries = 5) : DelegatingChatClient(inner)
{
    private static readonly TimeSpan FirstBackoff = TimeSpan.FromSeconds(2);

    private readonly List<string> providers = [];

    public long InputTokens { get; private set; }

    public long OutputTokens { get; private set; }

    public decimal CostUsd { get; private set; }

    public string? Provider => providers.Count == 0 ? null : string.Join(", ", providers.Distinct());

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = messages.ToList();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.GetResponseAsync(request, options, cancellationToken);
                InputTokens += response.Usage?.InputTokenCount ?? 0;
                OutputTokens += response.Usage?.OutputTokenCount ?? 0;
                CostUsd += LlmClientFactory.Cost(response) ?? 0m;
                if (LlmClientFactory.ServingProvider(response) is { } provider)
                {
                    providers.Add(provider);
                }

                return response;
            }
            catch (Exception ex) when (IsInFlightBudget(ex) && attempt < maxBudgetRetries)
            {
                await delay(FirstBackoff * Math.Pow(2, attempt), cancellationToken);
            }
        }
    }

    private static bool IsInFlightBudget(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is ClientResultException { Status: 402 })
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>The seam the sweep drives: one full pass over the golden cases for the model in <c>options</c>.</summary>
internal interface IModelRunner
{
    Task<RunOutcome> RunOnceAsync(LlmOptions options, int stage, CancellationToken cancellationToken);
}

/// <summary>Runs the golden cases against one model and scores them. The client factory and recordings are injected so tests need neither a network nor a key.</summary>
internal sealed class ModelRunner(Func<LlmOptions, IChatClient> createClient, Func<string, CancellationToken, Task<VideoText>> loadVideo, Func<TimeSpan, CancellationToken, Task>? delay = null) : IModelRunner
{
    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;

    /// <summary>One run: every golden case once. <paramref name="options"/> carries model, key and optional provider pin.</summary>
    public async Task<RunOutcome> RunOnceAsync(LlmOptions options, int stage, CancellationToken cancellationToken)
    {
        var cases = new List<CaseOutcome>();
        foreach (var golden in GoldenCases.All)
        {
            cases.Add(await RunCaseAsync(options, golden, cancellationToken));
        }

        return new RunOutcome(stage, options.PinnedProviders, cases);
    }

    public async Task<ModelResult> RunAsync(LlmOptions options, int repeats, CancellationToken cancellationToken)
    {
        var runs = new List<RunOutcome>();
        for (var i = 0; i < repeats; i++)
        {
            runs.Add(await RunOnceAsync(options, stage: 1, cancellationToken));
        }

        return new ModelResult(options.ResolvedModel, runs);
    }

    private async Task<CaseOutcome> RunCaseAsync(LlmOptions options, GoldenCase golden, CancellationToken cancellationToken)
    {
        var video = await loadVideo(golden.VideoId, cancellationToken);
        using var chat = new MeteringChatClient(createClient(options), delay);
        var extractor = new RecipeExtractor(chat, Options.Create(options), NullLogger<RecipeExtractor>.Instance);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await extractor.ExtractAsync(video, cancellationToken);
        clock.Stop();

        var (passed, failure, found) = Score(golden, result);
        return new CaseOutcome(golden.VideoId, golden.ExpectedRecipes, passed, failure, found, chat.InputTokens, chat.OutputTokens, chat.CostUsd, chat.Provider, clock.Elapsed.TotalSeconds);
    }

    /// <summary>Same bar as `just golden`: the expected number of Recipes, or NoRecipe when none are expected.</summary>
    public static (bool Passed, string? Failure, int? Found) Score(GoldenCase golden, Result<IReadOnlyList<ParsedRecipe>, ImportFailure> result)
    {
        if (!result.IsSuccess)
        {
            var noRecipeExpected = golden.ExpectedRecipes == 0 && result.Failure == ImportFailure.NoRecipe;
            return (noRecipeExpected, noRecipeExpected ? null : result.Failure.ToString(), null);
        }

        var found = result.Value.Count;
        var passed = found == golden.ExpectedRecipes;
        return (passed, passed ? null : "WrongCount", found);
    }
}

/// <summary>One JSON file per model in a directory, written atomically so an interrupted sweep never leaves half a file.</summary>
internal sealed class ResultStore(string directory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string Directory { get; } = directory;

    public static string FileName(string model) => model.Replace('/', '_').Replace(':', '_') + ".json";

    public ModelResult? Load(string model)
    {
        var file = Path.Combine(Directory, FileName(model));
        return File.Exists(file) ? JsonSerializer.Deserialize<ModelResult>(File.ReadAllText(file), Json) : null;
    }

    public IReadOnlyList<ModelResult> LoadAll() =>
        System.IO.Directory.Exists(Directory)
            ? [.. System.IO.Directory.EnumerateFiles(Directory, "*.json").Where(f => Path.GetFileName(f) != SweepSummary.FileName).Order(StringComparer.Ordinal).Select(f => JsonSerializer.Deserialize<ModelResult>(File.ReadAllText(f), Json)!)]
            : [];

    public void Save(ModelResult result)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var file = Path.Combine(Directory, FileName(result.Model));
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(result, Json));
        File.Move(temporary, file, overwrite: true);
    }

    public void SaveSummary(SweepSummary summary)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, SweepSummary.FileName), JsonSerializer.Serialize(summary, Json));
    }

    public SweepSummary? LoadSummary()
    {
        var file = Path.Combine(Directory, SweepSummary.FileName);
        return File.Exists(file) ? JsonSerializer.Deserialize<SweepSummary>(File.ReadAllText(file), Json) : null;
    }
}
