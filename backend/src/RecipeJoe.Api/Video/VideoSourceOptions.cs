namespace RecipeJoe.Api.Video;

internal enum VideoSourceProvider
{
    YouTube,
    Fake,
}

/// <summary>Bound from the "VideoSource" configuration section.</summary>
internal sealed class VideoSourceOptions
{
    public VideoSourceProvider Provider { get; set; } = VideoSourceProvider.YouTube;

    /// <summary>Fake only: where the recorded <c>{videoId}.json</c> files are.</summary>
    public string? FakeDirectory { get; set; }
}
