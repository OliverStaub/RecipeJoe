using RecipeJoe.Sweep;

namespace RecipeJoe.UnitTests.Sweep;

[TestClass]
public sealed class ModelCatalogTests
{
    /// <summary>A trimmed copy of the real public list (2026-10-01): the must-include models plus a :free, a :batch, a ~alias, a router, an expiring model and one without structured outputs.</summary>
    private static IReadOnlyList<CatalogModel> Sample() => ModelCatalog.Parse(File.ReadAllText("Sweep/models-sample.json"));

    private static IEnumerable<string> Ids(IReadOnlyList<Candidate> candidates) => candidates.Select(c => c.Model.Id);

    [TestMethod]
    public void Prices_are_parsed_from_per_token_strings_and_shown_per_million()
    {
        var model = Sample().Single(m => m.Id == "deepseek/deepseek-v4-flash");

        Assert.AreEqual(0.00000004186m, model.PromptPerToken);
        Assert.AreEqual(0.04186m, model.PromptPerMillion);
        Assert.AreEqual(0.08372m, model.CompletionPerMillion);
    }

    [TestMethod]
    [DataRow("0.0000001", "0.0000001")]
    [DataRow("0", "0")]
    [DataRow("-1", "-1")]
    [DataRow("1e-7", "0.0000001")]
    public void ParsePrice_reads_invariant_decimals(string text, string expected) =>
        Assert.AreEqual(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ModelCatalog.ParsePrice(text));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("cheap")]
    public void ParsePrice_gives_null_for_missing_or_unreadable_prices(string? text) =>
        Assert.IsNull(ModelCatalog.ParsePrice(text));

    [TestMethod]
    public void Only_text_models_with_structured_outputs_remain_without_free_batch_routers_aliases_or_expiring_ones()
    {
        var candidates = ModelCatalog.Select(Sample(), new CandidateFilter(), new TokenAssumptions());

        Assert.IsTrue(Ids(candidates).SequenceEqual(["deepseek/deepseek-v4-flash"]));
    }

    [TestMethod]
    public void Included_models_pass_even_when_they_are_expiring()
    {
        var filter = new CandidateFilter(Include: ["google/gemini-2.5-flash", "google/gemini-2.5-flash-lite"]);

        var candidates = ModelCatalog.Select(Sample(), filter, new TokenAssumptions());

        Assert.IsTrue(Ids(candidates).ToHashSet().SetEquals(["deepseek/deepseek-v4-flash", "google/gemini-2.5-flash", "google/gemini-2.5-flash-lite"]));
    }

    [TestMethod]
    public void OpenRouter_routers_are_dropped_even_when_they_look_free()
    {
        var router = new CatalogModel("openrouter/free", "Free Models Router", ["text"], ["text"], ["structured_outputs"], 0m, 0m, null);

        Assert.IsFalse(new CandidateFilter().Accepts(router));
    }

    [TestMethod]
    public void A_model_without_text_output_is_dropped()
    {
        var image = new CatalogModel("x/image", "Image", ["text"], ["image"], ["structured_outputs"], 0.000001m, 0.000001m, null);

        Assert.IsFalse(new CandidateFilter().Accepts(image));
    }

    [TestMethod]
    public void Models_above_the_max_output_price_are_dropped()
    {
        var cheap = new CatalogModel("a/cheap", "Cheap", ["text"], ["text"], ["structured_outputs"], 0.0000001m, 0.0000005m, null);
        var dear = cheap with { Id = "a/dear", CompletionPerToken = 0.00001m };

        var candidates = ModelCatalog.Select([cheap, dear], new CandidateFilter(MaxOutputPricePerMillion: 1m), new TokenAssumptions());

        Assert.IsTrue(Ids(candidates).SequenceEqual(["a/cheap"]));
    }

    [TestMethod]
    public void The_name_pattern_matches_id_or_name_ignoring_case()
    {
        var a = new CatalogModel("a/gemini-x", "Alpha", ["text"], ["text"], ["structured_outputs"], 0m, 0m, null);
        var b = new CatalogModel("b/other", "Gemini Beta", ["text"], ["text"], ["structured_outputs"], 0m, 0m, null);
        var c = new CatalogModel("c/qwen", "Qwen", ["text"], ["text"], ["structured_outputs"], 0m, 0m, null);

        var candidates = ModelCatalog.Select([a, b, c], new CandidateFilter(NamePattern: "GEMINI"), new TokenAssumptions());

        Assert.IsTrue(Ids(candidates).ToHashSet().SetEquals(["a/gemini-x", "b/other"]));
    }

    [TestMethod]
    public void Cost_per_pass_is_estimated_from_the_assumed_tokens_and_candidates_are_sorted_by_it()
    {
        var cheap = new CatalogModel("a/cheap", "Cheap", ["text"], ["text"], ["structured_outputs"], 0.000001m, 0.000002m, null);
        var dear = cheap with { Id = "a/dear", PromptPerToken = 0.00001m, CompletionPerToken = 0.00002m };

        var candidates = ModelCatalog.Select([dear, cheap], new CandidateFilter(), new TokenAssumptions(InputTokensPerPass: 1000, OutputTokensPerPass: 500));

        Assert.IsTrue(Ids(candidates).SequenceEqual(["a/cheap", "a/dear"]));
        Assert.AreEqual(0.002m, candidates[0].EstimatedCostPerPass); // 1000 x 0.000001 + 500 x 0.000002
    }

    [TestMethod]
    public void The_report_lists_candidates_filters_assumptions_and_prices_per_million()
    {
        var filter = new CandidateFilter(MaxOutputPricePerMillion: 5m, NamePattern: "deepseek");
        var assumptions = new TokenAssumptions(1000, 500);
        var candidates = ModelCatalog.Select(Sample(), filter, assumptions);

        var markdown = CandidateReport.Markdown(candidates, catalogSize: 9, filter, assumptions);

        StringAssert.Contains(markdown, "1 candidates from 9 models");
        StringAssert.Contains(markdown, "max output price per M tokens: 5");
        StringAssert.Contains(markdown, "name pattern: `deepseek`");
        StringAssert.Contains(markdown, "1000 input and 500 output tokens");
        StringAssert.Contains(markdown, "| `deepseek/deepseek-v4-flash` | 0.0419 | 0.0837 |");
    }

    [TestMethod]
    public void The_csv_has_a_header_and_one_row_per_candidate()
    {
        var candidates = ModelCatalog.Select(Sample(), new CandidateFilter(), new TokenAssumptions(1000, 500));

        var lines = CandidateReport.Csv(candidates).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.AreEqual("model,input_usd_per_million,output_usd_per_million,estimated_usd_per_pass", lines[0]);
        Assert.HasCount(2, lines);
        StringAssert.StartsWith(lines[1], "deepseek/deepseek-v4-flash,0.04186,0.08372,");
    }
}
