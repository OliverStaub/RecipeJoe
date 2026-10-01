using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RecipeJoe.Api;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.UnitTests.ImportsModule;

[TestClass]
public sealed class ImportRunnerTests
{
    private static readonly Uri Url = new("http://site.test/rezept");

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private static (ImportRunner Runner, ImportStore Store, System.Threading.Channels.ChannelWriter<Guid> Queue, FakeImportPath Path, FakeDraftSaver Saver) CreateRunner(
        int maxConcurrent = 2
    )
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UnixEpoch);
        var store = new ImportStore(timeProvider);

        var webPath = new FakeImportPath();
        var saver = new FakeDraftSaver();

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IImportPath>(ImportKind.Web, webPath);
        services.AddSingleton<IDraftSaver>(saver);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var queue = System.Threading.Channels.Channel.CreateUnbounded<Guid>();
        var options = Options.Create(new ImportOptions { MaxConcurrent = maxConcurrent });
        var runner = new ImportRunner(queue.Reader, scopeFactory, store, options, NullLogger<ImportRunner>.Instance);
        return (runner, store, queue.Writer, webPath, saver);
    }

    private static RecipeDraft MakeDraft(string name = "rezept") =>
        new(
            $"Rezept {name}", null, null, null, null, ["Zutat"], ["Schritt"], new Uri($"http://site.test/{name}"), null
        );

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met within the timeout.");
            }

            await Task.Delay(10);
        }
    }

    [TestMethod]
    public async Task Runs_a_started_import_through_its_stages_and_removes_it_on_success()
    {
        var (runner, store, queue, webPath, saver) = CreateRunner();
        var import = store.Start(Url, ImportKind.Web);
        ImportStage? stageWhenPathRan = null;
        webPath.Behavior = (_, progress, _) =>
        {
            stageWhenPathRan = store.TryGet(import.Id, out var found) ? found!.Stage : null;
            progress.Report(ImportStage.Extracting);
            return Task.FromResult(Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([MakeDraft()]));
        };
        ImportStage? stageWhenSaverRan = null;
        saver.Outcome = _ =>
        {
            stageWhenSaverRan = store.TryGet(import.Id, out var found) ? found!.Stage : null;
            return true;
        };

        await runner.StartAsync(CancellationToken.None);
        queue.TryWrite(import.Id);
        await WaitUntilAsync(() => !store.TryGet(import.Id, out _));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ImportStage.Fetching, stageWhenPathRan);
        Assert.AreEqual(ImportStage.Saving, stageWhenSaverRan);
    }

    [TestMethod]
    public async Task Fails_the_import_with_the_paths_failure_kind()
    {
        var (runner, store, queue, webPath, _) = CreateRunner();
        webPath.Behavior = (_, _, _) => Task.FromResult(Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Fail(ImportFailure.NotFound));
        var import = store.Start(Url, ImportKind.Web);

        await runner.StartAsync(CancellationToken.None);
        queue.TryWrite(import.Id);
        await WaitUntilAsync(() => store.TryGet(import.Id, out var found) && found!.IsFailed);
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ImportFailure.NotFound, store.TryGet(import.Id, out var found) ? found!.Failure : null);
    }

    [TestMethod]
    public async Task Removes_the_import_once_at_least_one_draft_saves_even_if_another_does_not()
    {
        var (runner, store, queue, webPath, saver) = CreateRunner();
        var saved = MakeDraft("a");
        var unsaved = MakeDraft("b");
        webPath.Behavior = (_, _, _) => Task.FromResult(Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([saved, unsaved]));
        saver.Outcome = draft => draft == saved;
        var import = store.Start(Url, ImportKind.Web);

        await runner.StartAsync(CancellationToken.None);
        queue.TryWrite(import.Id);
        await WaitUntilAsync(() => !store.TryGet(import.Id, out _));
        await runner.StopAsync(CancellationToken.None);

        Assert.IsFalse(store.TryGet(import.Id, out _));
    }

    [TestMethod]
    public async Task Fails_with_SaveFailed_when_no_draft_can_be_saved()
    {
        var (runner, store, queue, webPath, saver) = CreateRunner();
        webPath.Behavior = (_, _, _) => Task.FromResult(Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([MakeDraft()]));
        saver.Outcome = _ => false;
        var import = store.Start(Url, ImportKind.Web);

        await runner.StartAsync(CancellationToken.None);
        queue.TryWrite(import.Id);
        await WaitUntilAsync(() => store.TryGet(import.Id, out var found) && found!.IsFailed);
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ImportFailure.SaveFailed, store.TryGet(import.Id, out var found) ? found!.Failure : null);
    }

    [TestMethod]
    public async Task Runs_no_more_than_MaxConcurrent_imports_at_once()
    {
        var (runner, store, queue, webPath, _) = CreateRunner(maxConcurrent: 2);
        var active = 0;
        var maxObserved = new int[1];
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        webPath.Behavior = async (_, _, _) =>
        {
            var current = Interlocked.Increment(ref active);
            UpdateMax(maxObserved, current);
            await gate.Task;
            Interlocked.Decrement(ref active);
            return Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([MakeDraft()]);
        };
        var imports = new[] { store.Start(Url, ImportKind.Web), store.Start(Url, ImportKind.Web), store.Start(Url, ImportKind.Web) };

        await runner.StartAsync(CancellationToken.None);
        foreach (var import in imports)
        {
            queue.TryWrite(import.Id);
        }

        await WaitUntilAsync(() => active == 2);
        await Task.Delay(50); // give a would-be third run a chance to start
        Assert.AreEqual(2, active);

        gate.SetResult();
        await WaitUntilAsync(() => imports.All(i => !store.TryGet(i.Id, out _)));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(2, maxObserved[0]);
    }

    [TestMethod]
    public async Task Retrying_a_failed_import_reruns_it_from_scratch_with_the_same_id()
    {
        var (runner, store, queue, webPath, _) = CreateRunner();
        var attempt = 0;
        webPath.Behavior = (_, _, _) =>
        {
            attempt++;
            return Task.FromResult(
                attempt == 1
                    ? Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Fail(ImportFailure.NotFound)
                    : Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok([MakeDraft()])
            );
        };
        var import = store.Start(Url, ImportKind.Web);

        await runner.StartAsync(CancellationToken.None);
        queue.TryWrite(import.Id);
        await WaitUntilAsync(() => store.TryGet(import.Id, out var found) && found!.IsFailed);

        Assert.IsTrue(store.TryRestart(import.Id, out _));
        queue.TryWrite(import.Id);
        await WaitUntilAsync(() => !store.TryGet(import.Id, out _));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(2, attempt);
    }

    [TestMethod]
    public async Task Dismissing_an_import_before_the_runner_picks_it_up_is_a_safe_no_op()
    {
        var (runner, store, queue, _, _) = CreateRunner();
        var import = store.Start(Url, ImportKind.Web);
        store.Remove(import.Id);
        queue.TryWrite(import.Id);

        await runner.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await runner.StopAsync(CancellationToken.None);

        Assert.IsFalse(store.TryGet(import.Id, out _));
    }

    private static void UpdateMax(int[] holder, int value)
    {
        int initial;
        do
        {
            initial = holder[0];
            if (value <= initial)
            {
                return;
            }
        } while (Interlocked.CompareExchange(ref holder[0], value, initial) != initial);
    }
}
