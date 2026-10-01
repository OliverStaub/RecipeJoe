using System.Globalization;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;

namespace RecipeJoe.Sweep;

internal sealed record SweepOptions(
    int? MaxModels = null,
    int TopN = 10,
    int Repeats = 3,
    decimal SpendCapUsd = 5m,
    int Concurrency = 4
);

/// <summary>Written next to the results so the report can say the cap stopped the sweep.</summary>
internal sealed record SweepSummary(decimal SpendCapUsd, decimal SpentUsd, bool CapReached, IReadOnlyList<string> NotRun)
{
    public const string FileName = "sweep-summary.json";
}

/// <summary>
/// Stage 1: one run of every candidate. Stage 2: more runs of the cheapest survivors (all cases passed in stage 1), pinned to the provider that served them.
/// Results are saved per model as they finish, and finished work is never redone, so an interrupted sweep resumes where it stopped.
/// </summary>
internal sealed class SweepRunner(IModelRunner runner, ResultStore store, TextWriter log)
{
    private readonly object gate = new();
    private decimal spent;
    private decimal cap;
    private bool capReached;
    private readonly List<string> notRun = [];

    public async Task<SweepSummary> RunAsync(IReadOnlyList<Candidate> candidates, SweepOptions options, LlmOptions template, CancellationToken cancellationToken)
    {
        var models = candidates.Select(c => c.Model.Id).Take(options.MaxModels ?? int.MaxValue).ToList();
        spent = store.LoadAll().Sum(r => r.CostUsd);
        cap = options.SpendCapUsd;
        capReached = false;
        notRun.Clear();

        await ForEachAsync(models, options.Concurrency, model => Stage1Async(model, template, cancellationToken), cancellationToken);

        var survivors = models
            .Select(store.Load)
            .OfType<ModelResult>()
            .Where(r => r.Runs.Any(x => x.AllPassed))
            .OrderBy(r => r.Runs.First(x => x.AllPassed).CostUsd)
            .ThenBy(r => r.Model, StringComparer.Ordinal)
            .Take(options.TopN)
            .Select(r => r.Model)
            .ToList();
        log.WriteLine($"Stage 2: {survivors.Count} survivor(s), {options.Repeats} more run(s) each.");
        await ForEachAsync(survivors, options.Concurrency, model => Stage2Async(model, options.Repeats, template, cancellationToken), cancellationToken);

        var summary = new SweepSummary(options.SpendCapUsd, spent, capReached, [.. notRun.Order(StringComparer.Ordinal)]);
        store.SaveSummary(summary);
        return summary;
    }

    private Task ForEachAsync(IReadOnlyList<string> models, int concurrency, Func<string, Task> work, CancellationToken cancellationToken)
    {
        return Parallel.ForEachAsync(
            models,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, concurrency), CancellationToken = cancellationToken },
            async (model, _) =>
            {
                if (CapReached())
                {
                    MarkNotRun(model);
                    return;
                }

                await work(model);
            }
        );
    }

    private void MarkNotRun(string model)
    {
        lock (gate)
        {
            if (!notRun.Contains(model))
            {
                notRun.Add(model);
            }
        }
    }

    private bool CapReached()
    {
        lock (gate)
        {
            capReached |= spent >= cap;
            return capReached;
        }
    }

    private async Task Stage1Async(string model, LlmOptions template, CancellationToken cancellationToken)
    {
        if (store.Load(model) is { Runs.Count: > 0 })
        {
            log.WriteLine($"{model}: stage 1 already done, skipped.");
            return;
        }

        var run = await runner.RunOnceAsync(For(template, model, pin: null), 1, cancellationToken);
        var runs = new List<RunOutcome> { run };
        var cost = run.CostUsd;
        // One transient provider blip (429, timeout, 5xx) must not drop a good model: look again once.
        if (run.Cases.Any(c => c.Failure == nameof(ImportFailure.LlmUnavailable)) && !CapReached())
        {
            var again = await runner.RunOnceAsync(For(template, model, pin: null), 1, cancellationToken);
            runs.Add(again);
            cost += again.CostUsd;
            run = again;
        }

        Record(new ModelResult(model, runs), cost);
        log.WriteLine($"{model}: stage 1 {Passed(run)} for {Usd(cost)}");
    }

    private async Task Stage2Async(string model, int repeats, LlmOptions template, CancellationToken cancellationToken)
    {
        var existing = store.Load(model)!;
        var pin = SingleProvider(existing.Runs.First(r => r.AllPassed));
        var runs = existing.Runs.ToList();
        while (runs.Count < 1 + repeats)
        {
            if (CapReached())
            {
                MarkNotRun(model);
                break;
            }

            var run = await runner.RunOnceAsync(For(template, model, pin), 2, cancellationToken);
            runs.Add(run);
            Record(new ModelResult(model, runs), run.CostUsd);
            log.WriteLine($"{model}: stage 2 run {runs.Count - 1}/{repeats} {Passed(run)} for {Usd(run.CostUsd)}");
        }
    }

    private void Record(ModelResult result, decimal addedCost)
    {
        store.Save(result);
        lock (gate)
        {
            spent += addedCost;
        }
    }

    private static LlmOptions For(LlmOptions template, string model, string? pin) =>
        new()
        {
            Provider = template.Provider,
            BaseUrl = template.BaseUrl,
            ApiKey = template.ApiKey,
            Timeout = template.Timeout,
            Model = model,
            PinnedProviders = pin ?? template.PinnedProviders,
        };

    /// <summary>The one provider that served a whole stage-1 run, or null when it was several or unknown (then stage 2 stays unpinned).</summary>
    private static string? SingleProvider(RunOutcome run)
    {
        var providers = run.Cases.Select(c => c.Provider).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        return providers.Count == 1 && !providers[0]!.Contains(',', StringComparison.Ordinal) ? providers[0] : null;
    }

    private static string Passed(RunOutcome run) => $"{run.Cases.Count(c => c.Passed)}/{run.Cases.Count} cases";

    private static string Usd(decimal value) => value.ToString("$0.0000", CultureInfo.InvariantCulture);
}
