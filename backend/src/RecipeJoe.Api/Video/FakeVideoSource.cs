using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Video;

/// <summary>Reads recorded videos from <c>{FakeDirectory}/{videoId}.json</c>, in our own shape: <c>{title, description, transcript, thumbnailUrl?}</c>, or <c>{failure}</c> naming an <see cref="ImportFailure"/> to return instead. An unknown video id is NotFound, like a removed video.</summary>
internal sealed class FakeVideoSource(IOptions<VideoSourceOptions> options) : IVideoSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<Result<VideoContent, ImportFailure>> LoadAsync(Uri url, CancellationToken cancellationToken)
    {
        var id = YouTubeUrl.TryGetVideoId(url);
        var directory = options.Value.FakeDirectory ?? throw new InvalidOperationException("VideoSource:FakeDirectory is not set.");
        var file = id is null ? null : Path.Combine(directory, $"{id}.json");
        if (file is null || !File.Exists(file))
        {
            return Result<VideoContent, ImportFailure>.Fail(ImportFailure.NotFound);
        }

        await using var stream = File.OpenRead(file);
        var recorded = await JsonSerializer.DeserializeAsync<RecordedVideo>(stream, Json, cancellationToken)
            ?? throw new InvalidOperationException($"{file} is empty.");

        if (recorded.Failure is { } failure)
        {
            return Result<VideoContent, ImportFailure>.Fail(failure);
        }

        var thumbnail = recorded.ThumbnailUrl is null ? null : new Uri(recorded.ThumbnailUrl);
        return Result<VideoContent, ImportFailure>.Ok(
            new VideoContent(new VideoText(recorded.Title ?? "", recorded.Description ?? "", recorded.Transcript ?? ""), thumbnail)
        );
    }

    private sealed record RecordedVideo(string? Title, string? Description, string? Transcript, string? ThumbnailUrl, ImportFailure? Failure);
}
