using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Video;

/// <summary>The seam to wherever videos live: video URL → its text and thumbnail, or a typed failure (NotFound, NoCaptions, Blocked, Unreachable). Adapters: YouTube (prod), Fake (API/E2E tests).</summary>
internal interface IVideoSource
{
    Task<Result<VideoContent, ImportFailure>> LoadAsync(Uri url, CancellationToken cancellationToken);
}
