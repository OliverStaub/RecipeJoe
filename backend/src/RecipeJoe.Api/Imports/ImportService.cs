using System.Threading.Channels;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;

namespace RecipeJoe.Api.Imports;

/// <summary>Start / List / Retry / Dismiss: the Imports module's one public seam. Starting enqueues the id for <see cref="ImportRunner"/>; everything else is synchronous against the in-memory <see cref="ImportStore"/>.</summary>
internal sealed class ImportService(ImportStore store, ChannelWriter<Guid> queue)
{
    public Result<Import, ImportFailure> Start(string url)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        )
        {
            return Result<Import, ImportFailure>.Fail(ImportFailure.InvalidUrl);
        }

        var kind = YouTubeUrl.TryGetVideoId(parsed) is null ? ImportKind.Web : ImportKind.Video;
        var import = store.Start(parsed, kind);
        queue.TryWrite(import.Id);
        return Result<Import, ImportFailure>.Ok(import);
    }

    public IReadOnlyList<Import> List() => store.List();

    public bool TryRetry(Guid id, out Import? import)
    {
        if (!store.TryRestart(id, out import))
        {
            return false;
        }

        queue.TryWrite(id);
        return true;
    }

    public bool TryDismiss(Guid id) => store.Remove(id);
}
