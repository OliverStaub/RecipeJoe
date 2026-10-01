using System.Globalization;
using System.Text;

namespace RecipeJoe.Sweep;

/// <summary>The candidate list as Markdown and CSV, sorted by estimated cost per golden pass, with the filters and token assumptions it was made with.</summary>
internal static class CandidateReport
{
    public static string Markdown(IReadOnlyList<Candidate> candidates, int catalogSize, CandidateFilter filter, TokenAssumptions assumptions)
    {
        var text = new StringBuilder();
        text.AppendLine("# Sweep candidates");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"{candidates.Count} candidates from {catalogSize} models in the OpenRouter list. No LLM calls were made.");
        text.AppendLine();
        text.AppendLine("## Filters");
        text.AppendLine();
        text.AppendLine("- text input and text output, `structured_outputs` supported");
        text.AppendLine("- dropped: `:free`, `:batch`, `~` aliases, routers (`openrouter/*`, negative prices), models with an expiry date");
        text.AppendLine(CultureInfo.InvariantCulture, $"- max output price per M tokens: {(filter.MaxOutputPricePerMillion is { } max ? Usd(max) : "none")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- name pattern: {(string.IsNullOrEmpty(filter.NamePattern) ? "none" : $"`{filter.NamePattern}`")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- always included: {(filter.Include is { Count: > 0 } include ? string.Join(", ", include.Select(i => $"`{i}`")) : "none")}");
        text.AppendLine();
        text.AppendLine("## Cost assumptions");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"One golden pass is assumed to use {assumptions.InputTokensPerPass} input and {assumptions.OutputTokensPerPass} output tokens. Replace them (`--tokens-in`, `--tokens-out`) with measured numbers once a run exists.");
        text.AppendLine();
        text.AppendLine("| Model | Input $/M | Output $/M | Est. $ per golden pass |");
        text.AppendLine("|---|---:|---:|---:|");
        foreach (var c in candidates)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| `{c.Model.Id}` | {Usd(c.Model.PromptPerMillion)} | {Usd(c.Model.CompletionPerMillion)} | {Usd(c.EstimatedCostPerPass)} |");
        }

        return text.ToString();
    }

    public static string Csv(IReadOnlyList<Candidate> candidates)
    {
        var text = new StringBuilder();
        text.AppendLine("model,input_usd_per_million,output_usd_per_million,estimated_usd_per_pass");
        foreach (var c in candidates)
        {
            text.AppendLine(string.Join(',', Quote(c.Model.Id), Number(c.Model.PromptPerMillion), Number(c.Model.CompletionPerMillion), Number(c.EstimatedCostPerPass)));
        }

        return text.ToString();
    }

    private static string Usd(decimal? value) => value is { } v ? v.ToString("0.####", CultureInfo.InvariantCulture) : "?";

    private static string Number(decimal? value) => value is { } v ? v.ToString("0.########", CultureInfo.InvariantCulture) : "";

    private static string Quote(string value) => value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
}
