using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Imports;

/// <summary>Reads started/retried Import ids from the queue and runs them, up to <see cref="ImportOptions.MaxConcurrent"/> at once. Each run gets its own DI scope.</summary>
internal sealed partial class ImportRunner(
    ChannelReader<Guid> queue,
    IServiceScopeFactory scopeFactory,
    ImportStore store,
    IOptions<ImportOptions> options,
    ILogger<ImportRunner> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var gate = new SemaphoreSlim(options.Value.MaxConcurrent);
        var running = new ConcurrentBag<Task>();

        await foreach (var id in queue.ReadAllAsync(stoppingToken))
        {
            await gate.WaitAsync(stoppingToken);
            running.Add(RunOneAsync(id, stoppingToken).ContinueWith(_ => gate.Release(), TaskScheduler.Default));
        }

        await Task.WhenAll(running);
    }

    private async Task RunOneAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!store.TryGet(id, out var import) || import is null)
        {
            return; // dismissed before the runner picked it up
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var path = scope.ServiceProvider.GetRequiredKeyedService<IImportPath>(import.Kind);
            var saver = scope.ServiceProvider.GetRequiredService<IDraftSaver>();
            var progress = new SynchronousProgress<ImportStage>(stage => store.SetStage(id, stage));

            var result = await path.RunAsync(import.Url, progress, cancellationToken);
            if (!result.IsSuccess)
            {
                store.Fail(id, result.Failure);
                return;
            }

            store.SetStage(id, ImportStage.Saving);

            var savedAny = false;
            foreach (var draft in result.Value)
            {
                savedAny |= await saver.TrySaveAsync(draft, cancellationToken);
            }

            if (savedAny)
            {
                store.Remove(id);
            }
            else
            {
                store.Fail(id, ImportFailure.SaveFailed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRunFailed(import.Url, ex);
            store.Fail(id, ImportFailure.SaveFailed);
        }
    }

    [LoggerMessage(LogLevel.Error, "Import of {Url} crashed")]
    private partial void LogRunFailed(Uri url, Exception exception);

    /// <summary>Unlike <see cref="Progress{T}"/>, calls the handler on the reporting thread instead of posting it — this runs with no SynchronizationContext, and tests need the store updated before <c>RunAsync</c> returns.</summary>
    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
