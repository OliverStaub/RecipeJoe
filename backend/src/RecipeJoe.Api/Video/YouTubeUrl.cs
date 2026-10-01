using System.Web;

namespace RecipeJoe.Api.Video;

/// <summary>Which URLs are videos: youtube.com/watch, youtu.be and /shorts/. No playlists or channels.</summary>
internal static class YouTubeUrl
{
    /// <summary>The video id, or null when <paramref name="url"/> isn't a YouTube video URL.</summary>
    public static string? TryGetVideoId(Uri url)
    {
        var id = url.Host.ToUpperInvariant() switch
        {
            "YOUTU.BE" => FirstSegment(url.AbsolutePath),
            "YOUTUBE.COM" or "WWW.YOUTUBE.COM" or "M.YOUTUBE.COM" => url.AbsolutePath.ToUpperInvariant() switch
            {
                "/WATCH" or "/WATCH/" => HttpUtility.ParseQueryString(url.Query)["v"],
                var path when path.StartsWith("/SHORTS/", StringComparison.Ordinal) => FirstSegment(url.AbsolutePath["/shorts/".Length..]),
                _ => null,
            },
            _ => null,
        };

        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    private static string FirstSegment(string path) => path.Trim('/').Split('/')[0];
}
