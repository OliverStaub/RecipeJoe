using RecipeJoe.Sweep;
using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Sweep;

[TestClass]
public sealed class SweepTests
{
    private string directory = "";

    [TestInitialize]
    public void CreateDirectory() => directory = Directory.CreateTempSubdirectory("sweep-test").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, recursive: true);

    private static Candidate Cand(string id, decimal estimate = 0.01m) =>
        new(new CatalogModel(id, id, ["text"], ["text"], ["structured_outputs"], 0m, 0m, null), estimate);

    private static RunOutcome Outcome(int stage, bool pass, decimal cost, string? provider = "P1", string? pinned = null) =>
        new(stage, pinned, [new CaseOutcome("v", 1, pass, pass ? null : "WrongCount", pass ? 1 : 2, 100, 10, cost, provider)]);

    /// <summary>Plays each model from a script: pass/fail and cost of every run, recording the options of each call.</summary>
    private sealed class FakeRunner(Func<string, int, RunOutcome> run) : IModelRunner
    {
        private readonly Dictionary<string, int> calls = [];

        public List<(string Model, int Stage, string? Pin)> Calls { get; } = [];

        public Task<RunOutcome> RunOnceAsync(LlmOptions options, int stage, CancellationToken cancellationToken)
        {
            int index;
            lock (calls)
            {
                index = calls.GetValueOrDefault(options.Model!);
                calls[options.Model!] = index + 1;
                Calls.Add((options.Model!, stage, options.PinnedProviders));
            }

            return Task.FromResult(run(options.Model!, index) with { Stage = stage, PinnedProvider = options.PinnedProviders });
        }
    }

    private SweepRunner Sweep(FakeRunner runner) => new(runner, new ResultStore(directory), TextWriter.Null);

    private static readonly LlmOptions Template = new() { ApiKey = "sk-test" };

    [TestMethod]
    public async Task Stage_one_runs_every_candidate_once_and_stage_two_repeats_only_the_cheapest_survivors()
    {
        // good-a / good-b pass, bad fails; good-a is cheaper than good-b; top 1 survives.
        var runner = new FakeRunner((model, _) => Outcome(1, model != "bad", model == "good-b" ? 0.02m : 0.01m));

        await Sweep(runner).RunAsync([Cand("good-a"), Cand("good-b"), Cand("bad")], new SweepOptions(TopN: 1, Repeats: 2, SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.AreEqual(1, runner.Calls.Count(c => c.Model == "bad"));
        Assert.AreEqual(1, runner.Calls.Count(c => c.Model == "good-b"));
        Assert.AreEqual(3, runner.Calls.Count(c => c.Model == "good-a"));
        Assert.AreEqual(2, runner.Calls.Count(c => c is { Model: "good-a", Stage: 2 }));
        var saved = new ResultStore(directory).Load("good-a")!;
        Assert.HasCount(3, saved.Runs);
    }

    [TestMethod]
    public async Task A_transient_provider_failure_in_stage_one_gets_one_more_look_and_does_not_drop_the_model()
    {
        var runner = new FakeRunner((_, index) => index == 0
            ? new RunOutcome(1, null, [new CaseOutcome("v", 1, false, "LlmUnavailable", null, 0, 0, 0m, null)])
            : Outcome(1, true, 0.01m));

        await Sweep(runner).RunAsync([Cand("m")], new SweepOptions(Repeats: 2, SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.IsTrue(runner.Calls.Select(c => c.Stage).SequenceEqual([1, 1, 2]));
    }

    [TestMethod]
    public async Task A_model_is_listed_once_when_the_cap_stops_it_in_both_places()
    {
        var runner = new FakeRunner((_, _) => Outcome(1, true, 0.03m));

        var summary = await Sweep(runner).RunAsync([Cand("m")], new SweepOptions(Repeats: 5, SpendCapUsd: 0.05m, Concurrency: 1), Template, CancellationToken.None);

        Assert.AreEqual(1, summary.NotRun.Count(m => m == "m"));
    }

    [TestMethod]
    public async Task Stage_two_pins_the_provider_that_served_stage_one()
    {
        var runner = new FakeRunner((_, _) => Outcome(1, true, 0.01m, provider: "Google AI Studio"));

        await Sweep(runner).RunAsync([Cand("m")], new SweepOptions(TopN: 5, Repeats: 1, SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.IsTrue(runner.Calls.SequenceEqual([("m", 1, (string?)null), ("m", 2, "Google AI Studio")]));
    }

    [TestMethod]
    public async Task Stage_two_stays_unpinned_when_stage_one_was_served_by_several_providers_or_unknown()
    {
        var runner = new FakeRunner((_, _) => Outcome(1, true, 0.01m, provider: null));

        await Sweep(runner).RunAsync([Cand("m")], new SweepOptions(TopN: 5, Repeats: 1, SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.IsNull(runner.Calls.Single(c => c.Stage == 2).Pin);
    }

    [TestMethod]
    public async Task The_model_cap_limits_stage_one_to_the_cheapest_candidates()
    {
        var runner = new FakeRunner((_, _) => Outcome(1, false, 0.01m));

        await Sweep(runner).RunAsync([Cand("a"), Cand("b"), Cand("c")], new SweepOptions(MaxModels: 2, SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.IsTrue(runner.Calls.Select(c => c.Model).SequenceEqual(["a", "b"]));
    }

    [TestMethod]
    public async Task The_spend_cap_stops_new_runs_and_the_summary_says_so()
    {
        var runner = new FakeRunner((_, _) => Outcome(1, false, 0.03m));

        var summary = await Sweep(runner).RunAsync([Cand("a"), Cand("b"), Cand("c"), Cand("d")], new SweepOptions(SpendCapUsd: 0.05m, Concurrency: 1), Template, CancellationToken.None);

        Assert.HasCount(2, runner.Calls);
        Assert.IsTrue(summary.CapReached);
        Assert.AreEqual(0.06m, summary.SpentUsd);
        Assert.IsTrue(summary.NotRun.SequenceEqual(["c", "d"]));
        Assert.IsTrue(new ResultStore(directory).LoadSummary()!.CapReached);
    }

    [TestMethod]
    public async Task The_cap_also_stops_stage_two_between_runs()
    {
        var runner = new FakeRunner((_, _) => Outcome(1, true, 0.03m));

        var summary = await Sweep(runner).RunAsync([Cand("m")], new SweepOptions(Repeats: 5, SpendCapUsd: 0.05m, Concurrency: 1), Template, CancellationToken.None);

        Assert.HasCount(2, runner.Calls); // stage 1 (0.03), one stage 2 run (0.06 >= cap), then stop
        Assert.IsTrue(summary.CapReached);
        Assert.Contains("m", summary.NotRun);
    }

    [TestMethod]
    public async Task A_resumed_sweep_skips_finished_models_and_counts_their_spend_against_the_cap()
    {
        var store = new ResultStore(directory);
        store.Save(new ModelResult("done", [Outcome(1, false, 0.04m)]));
        var runner = new FakeRunner((_, _) => Outcome(1, false, 0.01m));

        var summary = await Sweep(runner).RunAsync([Cand("done"), Cand("todo")], new SweepOptions(SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.IsTrue(runner.Calls.Select(c => c.Model).SequenceEqual(["todo"]));
        Assert.AreEqual(0.05m, summary.SpentUsd);
    }

    [TestMethod]
    public async Task A_resumed_sweep_does_not_repeat_stage_two_runs_that_are_saved()
    {
        var store = new ResultStore(directory);
        store.Save(new ModelResult("m", [Outcome(1, true, 0.01m), Outcome(2, true, 0.01m)]));
        var runner = new FakeRunner((_, _) => Outcome(1, true, 0.01m));

        await Sweep(runner).RunAsync([Cand("m")], new SweepOptions(Repeats: 3, SpendCapUsd: 100m, Concurrency: 1), Template, CancellationToken.None);

        Assert.HasCount(2, runner.Calls); // 4 runs wanted (1 + 3), 2 saved
        Assert.HasCount(4, store.Load("m")!.Runs);
    }

    [TestMethod]
    public async Task Concurrency_is_bounded()
    {
        var runner = new ConcurrencyProbe();

        await new SweepRunner(runner, new ResultStore(directory), TextWriter.Null).RunAsync(
            [.. Enumerable.Range(0, 8).Select(i => Cand($"m{i}"))],
            new SweepOptions(SpendCapUsd: 100m, Concurrency: 3),
            Template,
            CancellationToken.None
        );

        Assert.IsLessThanOrEqualTo(3, runner.Peak);
        Assert.IsGreaterThan(1, runner.Peak);
    }

    private sealed class ConcurrencyProbe : IModelRunner
    {
        private int running;

        public int Peak { get; private set; }

        public async Task<RunOutcome> RunOnceAsync(LlmOptions options, int stage, CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref running);
            lock (this)
            {
                Peak = Math.Max(Peak, now);
            }

            await Task.Delay(50, cancellationToken);
            Interlocked.Decrement(ref running);
            return Outcome(stage, false, 0.001m);
        }
    }

    [TestMethod]
    public void The_dry_run_lists_planned_runs_and_an_estimate_without_touching_the_store()
    {
        var store = new ResultStore(directory);
        store.Save(new ModelResult("saved", [Outcome(1, false, 0.01m)]));

        var plan = SweepPlan.Describe([Cand("saved", 0.5m), Cand("new-a", 0.1m), Cand("new-b", 0.2m)], new SweepOptions(TopN: 1, Repeats: 2, SpendCapUsd: 0.5m), store, new TokenAssumptions(1000, 500));

        StringAssert.Contains(plan, "Stage 1: 2 run(s) (1 already saved), about $0.30");
        StringAssert.Contains(plan, "new-a");
        Assert.DoesNotContain("  - saved", plan);
        StringAssert.Contains(plan, "up to 1 survivor(s) x 2 run(s), at most about $1.00");
        StringAssert.Contains(plan, "1000 in, 500 out");
        StringAssert.Contains(plan, "the cap would stop the sweep early");
        Assert.HasCount(1, store.LoadAll());
    }

    [TestMethod]
    public void Results_survive_a_round_trip_through_the_store()
    {
        var store = new ResultStore(directory);
        var result = new ModelResult("vendor/model:thinking", [Outcome(1, true, 0.0123m, pinned: "P1")]);

        store.Save(result);

        var loaded = store.Load("vendor/model:thinking")!;
        Assert.AreEqual(0.0123m, loaded.CostUsd);
        Assert.AreEqual("P1", loaded.Runs[0].PinnedProvider);
        Assert.IsTrue(File.Exists(Path.Combine(directory, "vendor_model_thinking.json")));
        Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
    }
}
