using System.Globalization;
using System.Text;

namespace RecipeJoe.Sweep;

/// <summary>What `--dry-run` prints: the runs a sweep would start and what they would roughly cost, without calling any model.</summary>
internal static class SweepPlan
{
    public static string Describe(IReadOnlyList<Candidate> candidates, SweepOptions options, ResultStore store, TokenAssumptions assumptions)
    {
        var chosen = candidates.Take(options.MaxModels ?? int.MaxValue).ToList();
        var todo = chosen.Where(c => store.Load(c.Model.Id) is not { Runs.Count: > 0 }).ToList();
        var stage1 = todo.Sum(c => c.EstimatedCostPerPass);
        // Survivors are unknown before stage 1, so the worst case is the TopN most expensive of the chosen models.
        var stage2 = chosen.OrderByDescending(c => c.EstimatedCostPerPass).Take(options.TopN).Sum(c => c.EstimatedCostPerPass) * options.Repeats;

        var text = new StringBuilder();
        text.AppendLine("Dry run: no model is called and no key is needed.");
        text.AppendLine(CultureInfo.InvariantCulture, $"Stage 1: {todo.Count} run(s) ({chosen.Count - todo.Count} already saved), about {Usd(stage1)}.");
        foreach (var c in todo)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  - {c.Model.Id}  ~{c.EstimatedCostPerPass.ToString("$0.0000", CultureInfo.InvariantCulture)}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Stage 2: up to {options.TopN} survivor(s) x {options.Repeats} run(s), at most about {Usd(stage2)}.");
        text.AppendLine(CultureInfo.InvariantCulture, $"Assumed tokens per golden pass: {assumptions.InputTokensPerPass} in, {assumptions.OutputTokensPerPass} out.");
        text.AppendLine(CultureInfo.InvariantCulture, $"Estimated total: {Usd(stage1 + stage2)}. Spend cap: {Usd(options.SpendCapUsd)}{(stage1 + stage2 > options.SpendCapUsd ? " (the cap would stop the sweep early)" : "")}.");
        return text.ToString();
    }

    private static string Usd(decimal value) => value.ToString("$0.00", CultureInfo.InvariantCulture);
}
