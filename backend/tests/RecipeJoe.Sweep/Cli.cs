using System.Globalization;
using Microsoft.Extensions.AI;
using RecipeJoe.Api.Video;

namespace RecipeJoe.Sweep;

/// <summary>Parsed command line: positional arguments plus <c>--name value</c> options and bare <c>--flag</c>s.</summary>
internal sealed class CliArguments
{
    private readonly Dictionary<string, string?> options = new(StringComparer.Ordinal);

    public CliArguments(IEnumerable<string> args)
    {
        var queue = new Queue<string>(args);
        while (queue.Count > 0)
        {
            var arg = queue.Dequeue();
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                Positional.Add(arg);
            }
            else if (queue.Count > 0 && !queue.Peek().StartsWith("--", StringComparison.Ordinal))
            {
                options[arg[2..]] = queue.Dequeue();
            }
            else
            {
                options[arg[2..]] = null;
            }
        }
    }

    public List<string> Positional { get; } = [];

    public bool Has(string name) => options.ContainsKey(name);

    public string? Get(string name) => options.GetValueOrDefault(name);

    public int? Int(string name) => Get(name) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : null;

    public decimal? Decimal(string name) => Get(name) is { } v ? decimal.Parse(v, CultureInfo.InvariantCulture) : null;
}

/// <summary>`sweep candidates | model | run | report`. The key is read from <c>Llm__ApiKey</c> and never printed.</summary>
internal static class Cli
{
    private const string Usage = """
        Usage: RecipeJoe.Sweep <command> [options]
          candidates  [--max-output-price N] [--name PATTERN] [--include ID,ID] [--tokens-in N] [--tokens-out N] [--out DIR]
          model <id>  [--provider NAME] [--repeats N] [--out DIR]
          run         [--max-models N] [--top N] [--repeats N] [--cap USD] [--concurrency N] [--dry-run]
                      [--max-output-price N] [--name PATTERN] [--include ID,ID] [--out DIR]
          report      [--min-pass-rate 0.95] [--out DIR]
        """;

    private const string DefaultOutput = "sweep-output";

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, Func<string, string?> env, CancellationToken cancellationToken)
    {
        try
        {
            var command = args.FirstOrDefault();
            var parsed = new CliArguments(args.Skip(1));
            switch (command)
            {
                case "candidates":
                    return await CandidatesAsync(parsed, output, cancellationToken);
                case "model":
                    return await ModelAsync(parsed, output, error, env, cancellationToken);
                case "run":
                    return await SweepAsync(parsed, output, error, env, cancellationToken);
                case "report":
                    return Report(parsed, output, error);
                default:
                    await error.WriteLineAsync(Usage);
                    return 2;
            }
        }
        catch (Exception ex) when (ex is FormatException or HttpRequestException or FileNotFoundException)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    private static async Task<int> CandidatesAsync(CliArguments args, TextWriter output, CancellationToken cancellationToken)
    {
        var (candidates, catalogSize, filter, assumptions) = await LoadCandidatesAsync(args, cancellationToken);
        var directory = args.Get("out") ?? DefaultOutput;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "candidates.md"), CandidateReport.Markdown(candidates, catalogSize, filter, assumptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "candidates.csv"), CandidateReport.Csv(candidates), cancellationToken);
        await output.WriteLineAsync($"{candidates.Count} candidates from {catalogSize} models; wrote {Path.Combine(directory, "candidates.md")} and candidates.csv");
        return 0;
    }

    private static async Task<int> ModelAsync(CliArguments args, TextWriter output, TextWriter error, Func<string, string?> env, CancellationToken cancellationToken)
    {
        if (args.Positional.Count != 1)
        {
            await error.WriteLineAsync("Usage: model <model-id> [--provider NAME] [--repeats N] [--out DIR]");
            return 2;
        }

        if (MissingKey(env, error) is { } missing)
        {
            return missing;
        }

        var options = Template(env);
        options.Model = args.Positional[0];
        options.PinnedProviders = args.Get("provider");
        var store = new ResultStore(Path.Combine(args.Get("out") ?? DefaultOutput, "results"));

        var result = await NewRunner().RunAsync(options, args.Int("repeats") ?? 1, cancellationToken);
        store.Save(result);
        var score = SweepReport.Score(result);
        await output.WriteLineAsync($"{result.Model}: {score.Passed}/{score.Total} cases passed, cost {result.CostUsd.ToString("$0.0000", CultureInfo.InvariantCulture)}; wrote {Path.Combine(store.Directory, ResultStore.FileName(result.Model))}");
        return 0;
    }

    private static async Task<int> SweepAsync(CliArguments args, TextWriter output, TextWriter error, Func<string, string?> env, CancellationToken cancellationToken)
    {
        var (candidates, _, _, assumptions) = await LoadCandidatesAsync(args, cancellationToken);
        var defaults = new SweepOptions();
        var options = new SweepOptions(args.Int("max-models"), args.Int("top") ?? defaults.TopN, args.Int("repeats") ?? defaults.Repeats, args.Decimal("cap") ?? defaults.SpendCapUsd, args.Int("concurrency") ?? defaults.Concurrency);
        var store = new ResultStore(Path.Combine(args.Get("out") ?? DefaultOutput, "results"));

        if (args.Has("dry-run"))
        {
            await output.WriteAsync(SweepPlan.Describe(candidates, options, store, assumptions));
            return 0;
        }

        if (MissingKey(env, error) is { } missing)
        {
            return missing;
        }

        var summary = await new SweepRunner(NewRunner(), store, output).RunAsync(candidates, options, Template(env), cancellationToken);
        var report = SweepReport.Markdown(store.LoadAll(), summary);
        var file = Path.Combine(args.Get("out") ?? DefaultOutput, "report.md");
        await File.WriteAllTextAsync(file, report, cancellationToken);
        await output.WriteLineAsync($"Spent {summary.SpentUsd.ToString("$0.00", CultureInfo.InvariantCulture)} of the {summary.SpendCapUsd.ToString("$0.00", CultureInfo.InvariantCulture)} cap{(summary.CapReached ? " (cap reached, sweep stopped early)" : "")}. Report: {file}");
        return 0;
    }

    private static int Report(CliArguments args, TextWriter output, TextWriter error)
    {
        var directory = args.Get("out") ?? DefaultOutput;
        var store = new ResultStore(Path.Combine(directory, "results"));
        var results = store.LoadAll();
        if (results.Count == 0)
        {
            error.WriteLine($"No results in {store.Directory}. Run `just sweep` first.");
            return 1;
        }

        var minPassRate = args.Get("min-pass-rate") is { } v ? double.Parse(v, CultureInfo.InvariantCulture) : SweepReport.DefaultMinPassRate;
        var file = Path.Combine(directory, "report.md");
        File.WriteAllText(file, SweepReport.Markdown(results, store.LoadSummary(), minPassRate));
        output.WriteLine($"Wrote {file}");
        return 0;
    }

    private static async Task<(IReadOnlyList<Candidate> Candidates, int CatalogSize, CandidateFilter Filter, TokenAssumptions Assumptions)> LoadCandidatesAsync(CliArguments args, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var catalog = ModelCatalog.Parse(await ModelCatalog.FetchAsync(http, cancellationToken));
        var filter = new CandidateFilter(args.Decimal("max-output-price"), args.Get("name"), args.Get("include")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var defaults = new TokenAssumptions();
        var assumptions = new TokenAssumptions(args.Int("tokens-in") ?? defaults.InputTokensPerPass, args.Int("tokens-out") ?? defaults.OutputTokensPerPass);
        return (ModelCatalog.Select(catalog, filter, assumptions), catalog.Count, filter, assumptions);
    }

    /// <summary>Fails fast and never echoes the key (or any part of it).</summary>
    private static int? MissingKey(Func<string, string?> env, TextWriter error)
    {
        if (!string.IsNullOrWhiteSpace(env("Llm__ApiKey")))
        {
            return null;
        }

        error.WriteLine("Llm__ApiKey is not set. Put it in .env (use a dedicated OpenRouter key with a spend limit); `just` loads it.");
        return 2;
    }

    private static LlmOptions Template(Func<string, string?> env) =>
        new() { ApiKey = env("Llm__ApiKey"), BaseUrl = env("Llm__BaseUrl") };

    private static ModelRunner NewRunner() =>
        new(options => LlmClientFactory.Create(options), GoldenCases.LoadAsync);
}
