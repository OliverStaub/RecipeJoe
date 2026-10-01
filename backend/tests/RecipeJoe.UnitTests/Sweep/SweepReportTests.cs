using RecipeJoe.Sweep;

namespace RecipeJoe.UnitTests.Sweep;

[TestClass]
public sealed class SweepReportTests
{
    private static CaseOutcome Case(bool pass, decimal cost, string? failure = null, string? provider = "P1") =>
        new("v", 1, pass, pass ? null : failure ?? "WrongCount", pass ? 1 : null, 100, 10, cost, provider);

    private static ModelResult Model(string id, params (bool Pass, decimal Cost, string? Failure)[] cases) =>
        new(id, [new RunOutcome(1, null, [.. cases.Select(c => Case(c.Pass, c.Cost, c.Failure))])]);

    /// <summary>cheap: 1/2, $0.01. mid: 2/2, $0.04. dear: 2/2, $0.10 (dominated by mid). weak: 0/2, $0.02 (dominated by cheap).</summary>
    private static readonly IReadOnlyList<ModelResult> Results =
    [
        Model("cheap", (true, 0.005m, null), (false, 0.005m, "LlmBadOutput")),
        Model("mid", (true, 0.02m, null), (true, 0.02m, null)),
        Model("dear", (true, 0.05m, null), (true, 0.05m, null)),
        Model("weak", (false, 0.01m, "LlmUnavailable"), (false, 0.01m, "LlmUnavailable")),
    ];

    [TestMethod]
    public void A_model_is_scored_by_pass_rate_cost_failure_kinds_and_providers()
    {
        var score = SweepReport.Score(Model("m", (true, 0.01m, null), (false, 0.03m, "LlmBadOutput"), (false, 0.02m, "LlmBadOutput")));

        Assert.AreEqual(1, score.Passed);
        Assert.AreEqual(3, score.Total);
        Assert.AreEqual(1d / 3, score.PassRate, 1e-9);
        Assert.AreEqual(0.06m, score.CostPerRun);
        Assert.AreEqual(0.06m, score.CostPerPass);
        Assert.AreEqual(2, score.Failures["LlmBadOutput"]);
        Assert.IsTrue(score.Providers.SequenceEqual(["P1"]));
    }

    [TestMethod]
    public void Cost_per_pass_is_unknown_when_nothing_passed() =>
        Assert.IsNull(SweepReport.Score(Results.Single(r => r.Model == "weak")).CostPerPass);

    [TestMethod]
    public void Qualifying_models_clear_the_pass_bar_and_are_listed_cheapest_first()
    {
        var qualifying = SweepReport.Qualifying([.. Results.Select(SweepReport.Score)], 0.95);

        Assert.IsTrue(qualifying.Select(m => m.Model).SequenceEqual(["mid", "dear"]));
    }

    [TestMethod]
    public void The_recommendation_is_the_cheapest_model_above_the_pass_bar()
    {
        var scores = Results.Select(SweepReport.Score).ToList();

        Assert.AreEqual("mid", SweepReport.Recommend(scores, 0.95)?.Model);
        Assert.AreEqual("cheap", SweepReport.Recommend(scores, 0.5)?.Model);
        Assert.IsNull(SweepReport.Recommend([SweepReport.Score(Results.Single(r => r.Model == "weak"))], 0.95));
    }

    [TestMethod]
    public void Only_the_models_with_the_most_runs_are_recommended()
    {
        var oneRun = Model("lucky", (true, 0.001m, null), (true, 0.001m, null));
        var repeated = new ModelResult("proven", [.. Model("proven", (true, 0.02m, null), (true, 0.02m, null)).Runs, .. Model("proven", (true, 0.02m, null), (true, 0.02m, null)).Runs]);

        var recommended = SweepReport.Recommend([SweepReport.Score(oneRun), SweepReport.Score(repeated)], 0.95);

        Assert.AreEqual("proven", recommended?.Model);
    }

    [TestMethod]
    public void The_markdown_has_the_table_frontier_recommendation_and_total_spend()
    {
        var markdown = SweepReport.Markdown(Results, summary: null);

        StringAssert.Contains(markdown, "4 models, total actual spend **$0.1700**");
        StringAssert.Contains(markdown, "`mid`: the cheapest model with a pass rate of at least 95 %");
        StringAssert.Contains(markdown, "| `cheap` | 1 | 50 % (1/2) | $0.0100 | $0.0100 | - | LlmBadOutput×1 | P1 |");
        StringAssert.Contains(markdown, "| `weak` | 1 | 0 % (0/2) | $0.0200 | - | - | LlmUnavailable×2 | P1 |");
        StringAssert.Contains(markdown, "## Models with a pass rate of at least 95 %");
    }

    [TestMethod]
    public void A_recommendation_from_a_single_run_is_marked_unconfirmed()
    {
        var markdown = SweepReport.Markdown([Model("m", (true, 0.01m, null))], summary: null);

        StringAssert.Contains(markdown, "**Unconfirmed:**");
        var twice = new ModelResult("m", [.. Model("m", (true, 0.01m, null)).Runs, .. Model("m", (true, 0.01m, null)).Runs]);
        Assert.DoesNotContain("**Unconfirmed:**", SweepReport.Markdown([twice], null));
    }

    [TestMethod]
    public void Time_per_golden_pass_is_shown_as_median_and_worst_over_the_runs()
    {
        RunOutcome Run(double seconds) => new(1, null, [new CaseOutcome("v", 1, true, null, 1, 1, 1, 0.01m, "P", seconds / 2), new CaseOutcome("w", 1, true, null, 1, 1, 1, 0.01m, "P", seconds / 2)]);
        var result = new ModelResult("timed", [Run(10), Run(30), Run(20)]);

        var score = SweepReport.Score(result);

        Assert.AreEqual(20d, score.MedianSeconds);
        Assert.AreEqual(30d, score.WorstSeconds);
        StringAssert.Contains(SweepReport.Markdown([result], null), "| 20 s / 30 s |");
    }

    [TestMethod]
    public void Results_without_timings_show_no_time_and_do_not_fail()
    {
        var score = SweepReport.Score(Model("old", (true, 0.01m, null)));

        Assert.IsNull(score.MedianSeconds);
        Assert.IsNull(score.WorstSeconds);
    }

    [TestMethod]
    public void A_reached_cap_is_stated_in_the_report()
    {
        var summary = new SweepSummary(0.10m, 0.18m, CapReached: true, ["skipped/model"]);

        var markdown = SweepReport.Markdown(Results, summary);

        StringAssert.Contains(markdown, "Spend cap of $0.1000 reached");
        StringAssert.Contains(markdown, "`skipped/model`");
    }

    [TestMethod]
    public void No_cap_note_appears_when_the_cap_was_not_reached() =>
        Assert.DoesNotContain("Spend cap", SweepReport.Markdown(Results, new SweepSummary(5m, 0.18m, false, [])));

    [TestMethod]
    public void The_report_regenerates_from_saved_files_without_any_model_call()
    {
        var directory = Directory.CreateTempSubdirectory("sweep-report").FullName;
        try
        {
            var store = new ResultStore(directory);
            foreach (var result in Results)
            {
                store.Save(result);
            }

            store.SaveSummary(new SweepSummary(5m, 0.18m, false, []));

            Assert.AreEqual(SweepReport.Markdown(Results.OrderBy(r => r.Model, StringComparer.Ordinal).ToList(), store.LoadSummary()), SweepReport.Markdown(store.LoadAll(), store.LoadSummary()));
            Assert.HasCount(4, store.LoadAll());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void An_empty_result_set_recommends_nothing() =>
        Assert.IsNull(SweepReport.Recommend([], 0.95));
}
