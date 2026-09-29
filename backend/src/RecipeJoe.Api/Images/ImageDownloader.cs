using Microsoft.Extensions.Options;
using RecipeJoe.Api.Import;

namespace RecipeJoe.Api.Images;

/// <summary>Downloads and validates a Recipe's image. Every failure is logged and yields no image, never an Import failure.</summary>
internal sealed partial class ImageDownloader(IPageFetcher fetcher, IOptions<ImportOptions> options, ILogger<ImageDownloader> logger)
{
    public async Task<ImageFile?> DownloadAsync(Uri? url, CancellationToken cancellationToken)
    {
        if (url is null)
        {
            return null;
        }

        var fetched = await fetcher.FetchImageAsync(url, cancellationToken);
        if (!fetched.IsSuccess)
        {
            LogDownloadFailed(url, fetched.Failure);
            return null;
        }

        var bytes = fetched.Value.Bytes;
        if (bytes.Length > options.Value.MaxBytes)
        {
            LogTooLarge(url, bytes.Length, options.Value.MaxBytes);
            return null;
        }

        if (ContentTypeOf(bytes) is not { } contentType)
        {
            LogUnsupportedFormat(url);
            return null;
        }

        return new ImageFile(contentType, bytes);
    }

    /// <summary>The Content-Type the server claims is ignored: only the magic bytes count.</summary>
    private static string? ContentTypeOf(ReadOnlySpan<byte> bytes) =>
        bytes switch
        {
            [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
            [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'7' or (byte)'9', (byte)'a', ..] => "image/gif",
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
            _ => null,
        };

    [LoggerMessage(LogLevel.Warning, "Image {Url} skipped: download failed with {Failure}")]
    private partial void LogDownloadFailed(Uri url, ImportFailure failure);

    [LoggerMessage(LogLevel.Warning, "Image {Url} skipped: {Length} bytes is over the {Max} byte cap")]
    private partial void LogTooLarge(Uri url, int length, int max);

    [LoggerMessage(LogLevel.Warning, "Image {Url} skipped: not a jpeg, png, webp or gif")]
    private partial void LogUnsupportedFormat(Uri url);
}
