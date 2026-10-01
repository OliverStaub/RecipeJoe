using System.Globalization;
using System.Text.Json;

namespace RecipeJoe.Sweep;

/// <summary>One entry of OpenRouter's public model list, reduced to what candidate selection needs. Prices are USD per token, as the API sends them.</summary>
internal sealed record CatalogModel(
    string Id,
    string Name,
    IReadOnlyList<string> InputModalities,
    IReadOnlyList<string> OutputModalities,
    IReadOnlyList<string> SupportedParameters,
    decimal? PromptPerToken,
    decimal? CompletionPerToken,
    string? ExpirationDate
)
{
    public decimal? PromptPerMillion => PromptPerToken * 1_000_000m;

    public decimal? CompletionPerMillion => CompletionPerToken * 1_000_000m;
}

/// <summary>How many tokens one golden pass (all cases) is assumed to use, so a price list can be turned into a cost per pass. Replace by measured numbers once a run exists.</summary>
internal sealed record TokenAssumptions(int InputTokensPerPass = 15_500, int OutputTokensPerPass = 6_000);

internal sealed record CandidateFilter(decimal? MaxOutputPricePerMillion = null, string? NamePattern = null, IReadOnlyList<string>? Include = null)
{
    public bool Accepts(CatalogModel model)
    {
        if (Include?.Contains(model.Id, StringComparer.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return model.InputModalities.Contains("text")
            && model.OutputModalities.Contains("text")
            && model.SupportedParameters.Contains("structured_outputs")
            && !model.Id.StartsWith('~')
            && !model.Id.StartsWith("openrouter/", StringComparison.Ordinal)
            && !model.Id.Contains(":free", StringComparison.Ordinal)
            && !model.Id.Contains(":batch", StringComparison.Ordinal)
            && model.PromptPerToken is >= 0
            && model.CompletionPerToken is >= 0
            && model.ExpirationDate is null
            && (MaxOutputPricePerMillion is not { } max || model.CompletionPerMillion <= max)
            && (string.IsNullOrEmpty(NamePattern) || MatchesName(model));
    }

    private bool MatchesName(CatalogModel model) =>
        model.Id.Contains(NamePattern!, StringComparison.OrdinalIgnoreCase) || model.Name.Contains(NamePattern!, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A model that passed the filter, with the estimated cost of one golden pass.</summary>
internal sealed record Candidate(CatalogModel Model, decimal EstimatedCostPerPass);

internal static class ModelCatalog
{
    public static readonly Uri ModelsUrl = new("https://openrouter.ai/api/v1/models");

    /// <summary>The public list; needs no key.</summary>
    public static async Task<string> FetchAsync(HttpClient http, CancellationToken cancellationToken) =>
        await http.GetStringAsync(ModelsUrl, cancellationToken);

    public static IReadOnlyList<CatalogModel> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.GetProperty("data").EnumerateArray().Select(ParseModel)];
    }

    /// <summary>The API's per-token price strings ("0.0000001"); "-1" marks a router with a variable price and is kept as a negative number so the filter can drop it. Null when absent or unreadable.</summary>
    public static decimal? ParsePrice(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var price) ? price : null;

    public static IReadOnlyList<Candidate> Select(IEnumerable<CatalogModel> models, CandidateFilter filter, TokenAssumptions assumptions) =>
        [.. models
            .Where(filter.Accepts)
            .Select(m => new Candidate(m, EstimateCostPerPass(m, assumptions)))
            .OrderBy(c => c.EstimatedCostPerPass)
            .ThenBy(c => c.Model.Id, StringComparer.Ordinal)];

    public static decimal EstimateCostPerPass(CatalogModel model, TokenAssumptions assumptions) =>
        (model.PromptPerToken ?? 0m) * assumptions.InputTokensPerPass + (model.CompletionPerToken ?? 0m) * assumptions.OutputTokensPerPass;

    private static CatalogModel ParseModel(JsonElement model)
    {
        var architecture = model.TryGetProperty("architecture", out var a) ? a : default;
        var pricing = model.TryGetProperty("pricing", out var p) ? p : default;
        return new CatalogModel(
            model.GetProperty("id").GetString()!,
            Text(model, "name") ?? model.GetProperty("id").GetString()!,
            Strings(architecture, "input_modalities"),
            Strings(architecture, "output_modalities"),
            Strings(model, "supported_parameters"),
            ParsePrice(Text(pricing, "prompt")),
            ParsePrice(Text(pricing, "completion")),
            Text(model, "expiration_date")
        );
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Strings(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
            : [];
}
