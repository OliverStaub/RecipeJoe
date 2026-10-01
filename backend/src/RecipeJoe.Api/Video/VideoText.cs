namespace RecipeJoe.Api.Video;

/// <summary>What the LLM reads of a video. Chapters aren't parsed: the raw description carries them.</summary>
internal sealed record VideoText(string Title, string Description, string Transcript);

/// <summary>A video as the video source returns it: its text and where its thumbnail is (null when it has none).</summary>
internal sealed record VideoContent(VideoText Text, Uri? ThumbnailUrl);
