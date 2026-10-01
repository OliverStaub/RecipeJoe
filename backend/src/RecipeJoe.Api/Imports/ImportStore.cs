using System.Collections.Concurrent;
using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Imports;

/// <summary>Every Pending and Failed Import, in memory only. A restart loses them; the frontend treats a vanished Import as finished.</summary>
internal sealed class ImportStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, Import> _imports = new();

    public Import Start(Uri url, ImportKind kind)
    {
        var import = new Import(Guid.NewGuid(), url, kind, ImportStage.Fetching, null, timeProvider.GetUtcNow());
        _imports[import.Id] = import;
        return import;
    }

    public bool TryGet(Guid id, out Import? import) => _imports.TryGetValue(id, out import);

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<Import> List() => [.. _imports.Values.OrderBy(i => i.StartedAt)];

    public void SetStage(Guid id, ImportStage stage) => Update(id, existing => existing with { Stage = stage, Failure = null });

    public void Fail(Guid id, ImportFailure failure) => Update(id, existing => existing with { Stage = null, Failure = failure });

    /// <summary>Back to Pending, same id, as if just started. False when there's no such Import.</summary>
    public bool TryRestart(Guid id, out Import? import)
    {
        import = Update(id, existing => existing with { Stage = ImportStage.Fetching, Failure = null });
        return import is not null;
    }

    public bool Remove(Guid id) => _imports.TryRemove(id, out _);

    /// <summary>A no-op when <paramref name="id"/> has already been removed (e.g. dismissed while running).</summary>
    private Import? Update(Guid id, Func<Import, Import> change)
    {
        while (_imports.TryGetValue(id, out var existing))
        {
            var updated = change(existing);
            if (_imports.TryUpdate(id, updated, existing))
            {
                return updated;
            }
        }

        return null;
    }
}
