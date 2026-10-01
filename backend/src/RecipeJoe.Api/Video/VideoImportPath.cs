using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api.Video;

/// <summary>The Video Import path: video source → extractor → one Recipe Draft per dish, each with the video as Source. The thumbnail is downloaded once and its image shared by every Draft; a failed thumbnail only leaves the Drafts without an image.</summary>
internal sealed class VideoImportPath(IVideoSource videos, RecipeExtractor extractor, ImageDownloader images) : IImportPath
{
    public async Task<Result<IReadOnlyList<RecipeDraft>, ImportFailure>> RunAsync(
        Uri url,
        IProgress<ImportStage> progress,
        CancellationToken cancellationToken
    )
    {
        progress.Report(ImportStage.Fetching);
        var video = await videos.LoadAsync(url, cancellationToken);
        if (!video.IsSuccess)
        {
            return Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Fail(video.Failure);
        }

        progress.Report(ImportStage.Extracting);
        var extracted = await extractor.ExtractAsync(video.Value.Text, cancellationToken);
        if (!extracted.IsSuccess)
        {
            return Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Fail(extracted.Failure);
        }

        var image = await images.DownloadAsync(video.Value.ThumbnailUrl, cancellationToken);
        return Result<IReadOnlyList<RecipeDraft>, ImportFailure>.Ok(
            [
                .. extracted.Value.Select(r => new RecipeDraft(
                    r.Title,
                    r.Servings,
                    r.PrepTime,
                    r.CookTime,
                    r.TotalTime,
                    r.IngredientLines,
                    r.Steps,
                    url,
                    image
                )),
            ]
        );
    }
}
