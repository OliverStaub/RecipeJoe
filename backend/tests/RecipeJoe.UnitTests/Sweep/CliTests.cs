using RecipeJoe.Sweep;

namespace RecipeJoe.UnitTests.Sweep;

[TestClass]
public sealed class CliTests
{
    private static async Task<(int Code, string Output, string Error)> RunAsync(Func<string, string?> env, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await Cli.RunAsync(args, output, error, env, CancellationToken.None);
        return (code, output.ToString(), error.ToString());
    }

    [TestMethod]
    public async Task A_single_model_run_without_a_key_fails_fast_with_a_clear_message()
    {
        var (code, output, error) = await RunAsync(_ => null, "model", "vendor/model");

        Assert.AreEqual(2, code);
        Assert.AreEqual("", output);
        StringAssert.Contains(error, "Llm__ApiKey is not set");
    }

    [TestMethod]
    public async Task A_blank_key_counts_as_missing()
    {
        var (code, _, error) = await RunAsync(_ => "  ", "model", "vendor/model");

        Assert.AreEqual(2, code);
        StringAssert.Contains(error, "Llm__ApiKey");
    }

    [TestMethod]
    public async Task The_model_command_needs_exactly_one_model_id()
    {
        var (code, _, error) = await RunAsync(_ => "sk-secret", "model");

        Assert.AreEqual(2, code);
        StringAssert.Contains(error, "Usage");
        Assert.DoesNotContain("sk-secret", error);
    }

    [TestMethod]
    public async Task An_unknown_command_prints_usage()
    {
        var (code, _, error) = await RunAsync(_ => null, "nonsense");

        Assert.AreEqual(2, code);
        StringAssert.Contains(error, "Usage: RecipeJoe.Sweep");
    }

    [TestMethod]
    public async Task Report_without_results_says_so()
    {
        var directory = Directory.CreateTempSubdirectory("sweep-cli").FullName;
        try
        {
            var (code, _, error) = await RunAsync(_ => null, "report", "--out", directory);

            Assert.AreEqual(1, code);
            StringAssert.Contains(error, "No results");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Report_writes_the_markdown_from_saved_results()
    {
        var directory = Directory.CreateTempSubdirectory("sweep-cli").FullName;
        try
        {
            new ResultStore(Path.Combine(directory, "results")).Save(new ModelResult("vendor/model", [new RunOutcome(1, null, [new CaseOutcome("v", 1, true, null, 1, 10, 1, 0.01m, "P")])]));

            var (code, output, _) = await RunAsync(_ => null, "report", "--out", directory);

            Assert.AreEqual(0, code);
            StringAssert.Contains(output, "report.md");
            StringAssert.Contains(File.ReadAllText(Path.Combine(directory, "report.md")), "`vendor/model`");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Arguments_split_into_positionals_options_and_flags()
    {
        var args = new CliArguments(["vendor/model", "--repeats", "3", "--dry-run", "--cap", "1.5"]);

        Assert.IsTrue(args.Positional.SequenceEqual(["vendor/model"]));
        Assert.AreEqual(3, args.Int("repeats"));
        Assert.AreEqual(1.5m, args.Decimal("cap"));
        Assert.IsTrue(args.Has("dry-run"));
        Assert.IsNull(args.Get("dry-run"));
        Assert.IsNull(args.Int("top"));
    }
}
