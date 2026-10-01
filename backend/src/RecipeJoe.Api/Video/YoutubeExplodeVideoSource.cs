using RecipeJoe.Api.Import;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace RecipeJoe.Api.Video;

/// <summary>Production <see cref="IVideoSource"/>: YoutubeExplode over the injected <see cref="HttpClient"/>. Decisions live in <see cref="YouTubeCaptions"/>; this class is the network glue and is verified by hand against real videos, never in CI.</summary>
internal sealed class YoutubeExplodeVideoSource(HttpClient http) : IVideoSource
{
    public async Task<Result<VideoContent, ImportFailure>> LoadAsync(Uri url, CancellationToken cancellationToken)
    {
        if (YouTubeUrl.TryGetVideoId(url) is not { } rawId || VideoId.TryParse(rawId) is not { } id)
        {
            return Result<VideoContent, ImportFailure>.Fail(ImportFailure.NotFound);
        }

        using var youtube = new YoutubeClient(http);
        try
        {
            var video = await youtube.Videos.GetAsync(id, cancellationToken);
            var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(id, cancellationToken);
            if (YouTubeCaptions.ChooseTrack(manifest.Tracks) is not { } track)
            {
                return Result<VideoContent, ImportFailure>.Fail(ImportFailure.NoCaptions);
            }

            var captions = await youtube.Videos.ClosedCaptions.GetAsync(track, cancellationToken);
            var transcript = YouTubeCaptions.ToTranscript(captions.Captions.Select(c => c.Text));
            if (transcript.Length == 0)
            {
                // A listed track that comes back empty is YouTube withholding it (PO token / bot check), not a video without captions.
                return Result<VideoContent, ImportFailure>.Fail(ImportFailure.Blocked);
            }

            var thumbnail = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url;
            return Result<VideoContent, ImportFailure>.Ok(
                new VideoContent(new VideoText(video.Title, video.Description, transcript), thumbnail is null ? null : new Uri(thumbnail))
            );
        }
        catch (Exception e) when (YouTubeCaptions.MapFailure(e) is { } failure)
        {
            return Result<VideoContent, ImportFailure>.Fail(failure);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<VideoContent, ImportFailure>.Fail(ImportFailure.Unreachable); // HttpClient timeout
        }
    }
}
