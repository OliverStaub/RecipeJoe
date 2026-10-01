using System.Globalization;
using System.Text;

namespace RecipeJoe.Sweep;

/// <summary>One row of the report: what a model scored and cost over all its saved runs.</summary>
internal sealed record ModelScore(string Model, int Runs, int Passed, int Total, decimal TotalCostUsd, IReadOnlyDictionary<string, int> Failures, IReadOnlyList<string> Providers, IReadOnlyList<double> RunSeconds)
{
    /// <summary>Median seconds of one golden pass; null when no run has timings. Includes any 402 backoff and, under concurrency, some queueing.</summary>
    public double? MedianSeconds => RunSeconds.Count == 0 ? null : Median(RunSeconds);

    public double? WorstSeconds => RunSeconds.Count == 0 ? null : RunSeconds.Max();

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.Order().ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    public double PassRate => Total == 0 ? 0 : (double)Passed / Total;

    public decimal CostPerRun => Runs == 0 ? 0 : TotalCostUsd / Runs;

    /// <summary>Money spent per passed case; null when nothing passed.</summary>
    public decimal? CostPerPass => Passed == 0 ? null : TotalCostUsd / Passed;
}

/// <summary>Turns saved results into the report. Pure: no network, so it can be regenerated or tested from files alone.</summary>
internal static class SweepReport
{
    /// <summary>A model must pass at least this share of golden cases to be recommended.</summary>
    public const double DefaultMinPassRate = 0.95;

    public static ModelScore Score(ModelResult result)
    {
        var cases = result.Runs.SelectMany(r => r.Cases).ToList();
        return new ModelScore(
            result.Model,
            result.Runs.Count,
            cases.Count(c => c.Passed),
            cases.Count,
            result.CostUsd,
            cases.Where(c => c.Failure is not null).GroupBy(c => c.Failure!).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            [.. cases.Select(c => c.Provider).Where(p => !string.IsNullOrEmpty(p)).Distinct().Order(StringComparer.Ordinal)!],
            [.. result.Runs.Select(r => r.Seconds).OfType<double>()]
        );
    }

    /// <summary>Models that clear the bar, cheapest first.</summary>
    public static IReadOnlyList<ModelScore> Qualifying(IReadOnlyList<ModelScore> scores, double minPassRate) =>
        [.. scores.Where(m => m.PassRate >= minPassRate).OrderBy(m => m.CostPerRun).ThenBy(m => m.Model, StringComparer.Ordinal)];

    /// <summary>The cheapest model that clears <paramref name="minPassRate"/>, judged only among the models with the most runs (the stage-2 survivors, when there are any); null when none clears it.</summary>
    public static ModelScore? Recommend(IReadOnlyList<ModelScore> scores, double minPassRate)
    {
        if (scores.Count == 0)
        {
            return null;
        }

        var deepest = scores.Max(s => s.Runs);
        var qualifying = Qualifying([.. scores.Where(s => s.Runs == deepest)], minPassRate);
        return qualifying.Count == 0 ? null : qualifying[0];
    }

    public static string Markdown(IReadOnlyList<ModelResult> results, SweepSummary? summary, double minPassRate = DefaultMinPassRate)
    {
        var scores = results.Select(Score).OrderByDescending(s => s.PassRate).ThenBy(s => s.CostPerRun).ThenBy(s => s.Model, StringComparer.Ordinal).ToList();
        var qualifying = Qualifying(scores, minPassRate);
        var recommended = Recommend(scores, minPassRate);
        var spent = results.Sum(r => r.CostUsd);

        var text = new StringBuilder();
        text.AppendLine("# Sweep report");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"{scores.Count} models, total actual spend **{Usd(spent)}**.");
        if (summary is { CapReached: true })
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"> **Spend cap of {Usd(summary.SpendCapUsd)} reached.** No new runs were launched after that; {summary.NotRun.Count} model(s) or run(s) were not done: {(summary.NotRun.Count == 0 ? "none listed" : string.Join(", ", summary.NotRun.Select(m => $"`{m}`")))}. Raise the cap and run `just sweep` again to continue.");
        }

        text.AppendLine();
        text.AppendLine("## Recommended model");
        text.AppendLine();
        var verdict = recommended is null
            ? string.Create(CultureInfo.InvariantCulture, $"None: no model with the most runs reached a pass rate of {minPassRate:P0}.")
            : string.Create(CultureInfo.InvariantCulture, $"`{recommended.Model}`: the cheapest model with a pass rate of at least {minPassRate:P0} ({recommended.PassRate:P0} over {recommended.Runs} run(s), {Usd(recommended.CostPerRun)} per golden pass){(recommended.Runs == 1 ? ". **Unconfirmed:** only one run, no stage-2 repeats (the sweep may have stopped early)." : "")}");
        text.AppendLine(verdict);
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"## Models with a pass rate of at least {minPassRate:P0}");
        text.AppendLine();
        text.AppendLine("Cheapest first.");
        text.AppendLine();
        text.AppendLine("| Model | Runs | Pass rate | $ per golden pass | Time per golden pass (median / worst) |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var m in qualifying)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| `{m.Model}` | {m.Runs} | {m.PassRate:P0} ({m.Passed}/{m.Total}) | {Usd(m.CostPerRun)} | {Time(m)} |");
        }

        if (qualifying.Count == 0)
        {
            text.AppendLine("| none | | | | |");
        }

        text.AppendLine();
        text.AppendLine("## All models");
        text.AppendLine();
        text.AppendLine("Pass rate counts golden cases over all runs. Cost per pass is the money spent per passed case. Time is wall-clock per golden pass (all cases); it includes retry backoff and, with concurrency above 1, some queueing, so use it to compare models, not as exact latency.");
        text.AppendLine();
        text.AppendLine("| Model | Runs | Pass rate | $ per golden pass | $ per pass | Time per golden pass (median / worst) | Failure kinds | Serving provider |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
        foreach (var m in scores)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| `{m.Model}` | {m.Runs} | {m.PassRate:P0} ({m.Passed}/{m.Total}) | {Usd(m.CostPerRun)} | {(m.CostPerPass is { } c ? Usd(c) : "-")} | {Time(m)} | {(m.Failures.Count == 0 ? "-" : string.Join(", ", m.Failures.Select(f => $"{f.Key}×{f.Value}")))} | {(m.Providers.Count == 0 ? "unknown" : string.Join(", ", m.Providers))} |");
        }

        return text.ToString();
    }

    private static string Time(ModelScore m) =>
        m.MedianSeconds is { } median ? string.Create(CultureInfo.InvariantCulture, $"{median:0.#} s / {m.WorstSeconds:0.#} s") : "-";

    private static string Usd(decimal value) => value.ToString("$0.0000", CultureInfo.InvariantCulture);
}
