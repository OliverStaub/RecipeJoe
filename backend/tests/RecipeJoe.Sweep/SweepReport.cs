using System.Globalization;
using System.Text;

namespace RecipeJoe.Sweep;

/// <summary>One row of the report: what a model scored and cost over all its saved runs.</summary>
internal sealed record ModelScore(string Model, int Runs, int Passed, int Total, decimal TotalCostUsd, IReadOnlyDictionary<string, int> Failures, IReadOnlyList<string> Providers)
{
    public double PassRate => Total == 0 ? 0 : (double)Passed / Total;

    public decimal CostPerRun => Runs == 0 ? 0 : TotalCostUsd / Runs;

    /// <summary>Money spent per passed case; null when nothing passed.</summary>
    public decimal? CostPerPass => Passed == 0 ? null : TotalCostUsd / Passed;
}

/// <summary>Turns saved results into the report. Pure: no network, so it can be regenerated or tested from files alone.</summary>
internal static class SweepReport
{
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
            [.. cases.Select(c => c.Provider).Where(p => !string.IsNullOrEmpty(p)).Distinct().Order(StringComparer.Ordinal)!]
        );
    }

    /// <summary>Models no other model beats on both axes: nobody is at least as good for less money (or better for the same money).</summary>
    public static IReadOnlyList<ModelScore> ParetoFrontier(IReadOnlyList<ModelScore> scores) =>
        [.. scores
            .Where(m => !scores.Any(o => o != m && o.PassRate >= m.PassRate && o.CostPerRun <= m.CostPerRun && (o.PassRate > m.PassRate || o.CostPerRun < m.CostPerRun)))
            .OrderBy(m => m.CostPerRun)];

    /// <summary>The cheapest frontier model that clears <paramref name="minPassRate"/>, judged only among the models with the most runs (the stage-2 survivors, when there are any); null when none clears it.</summary>
    public static ModelScore? Recommend(IReadOnlyList<ModelScore> scores, double minPassRate)
    {
        if (scores.Count == 0)
        {
            return null;
        }

        var deepest = scores.Max(s => s.Runs);
        var eligible = scores.Where(s => s.Runs == deepest).ToList();
        return ParetoFrontier(eligible).FirstOrDefault(m => m.PassRate >= minPassRate);
    }

    public static string Markdown(IReadOnlyList<ModelResult> results, SweepSummary? summary, double minPassRate = 0.9)
    {
        var scores = results.Select(Score).OrderByDescending(s => s.PassRate).ThenBy(s => s.CostPerRun).ThenBy(s => s.Model, StringComparer.Ordinal).ToList();
        var frontier = ParetoFrontier(scores);
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
            ? string.Create(CultureInfo.InvariantCulture, $"None: no model with the most runs reached a pass rate of {minPassRate:P0} on the Pareto frontier.")
            : string.Create(CultureInfo.InvariantCulture, $"`{recommended.Model}`: the cheapest frontier model with a pass rate of at least {minPassRate:P0} ({recommended.PassRate:P0} over {recommended.Runs} run(s), {Usd(recommended.CostPerRun)} per golden pass){(recommended.Runs == 1 ? ". **Unconfirmed:** only one run, no stage-2 repeats (the sweep may have stopped early)." : "")}");
        text.AppendLine(verdict);
        text.AppendLine();
        text.AppendLine("## Pareto frontier (score vs cost)");
        text.AppendLine();
        text.AppendLine("No other model passes at least as often for less money.");
        text.AppendLine();
        text.AppendLine("| Model | Pass rate | $ per golden pass |");
        text.AppendLine("|---|---:|---:|");
        foreach (var m in frontier)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| `{m.Model}` | {m.PassRate:P0} | {Usd(m.CostPerRun)} |");
        }

        text.AppendLine();
        text.AppendLine("## All models");
        text.AppendLine();
        text.AppendLine("Pass rate counts golden cases over all runs. Cost per pass is the money spent per passed case.");
        text.AppendLine();
        text.AppendLine("| Model | Runs | Pass rate | $ per golden pass | $ per pass | Failure kinds | Serving provider |");
        text.AppendLine("|---|---:|---:|---:|---:|---|---|");
        foreach (var m in scores)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| `{m.Model}` | {m.Runs} | {m.PassRate:P0} ({m.Passed}/{m.Total}) | {Usd(m.CostPerRun)} | {(m.CostPerPass is { } c ? Usd(c) : "-")} | {(m.Failures.Count == 0 ? "-" : string.Join(", ", m.Failures.Select(f => $"{f.Key}×{f.Value}")))} | {(m.Providers.Count == 0 ? "unknown" : string.Join(", ", m.Providers))} |");
        }

        return text.ToString();
    }

    private static string Usd(decimal value) => value.ToString("$0.0000", CultureInfo.InvariantCulture);
}
