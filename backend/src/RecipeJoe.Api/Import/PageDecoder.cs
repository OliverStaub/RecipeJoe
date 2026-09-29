using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace RecipeJoe.Api.Import;

/// <summary>Bytes → text: charset from the Content-Type header, else the meta tag, else UTF-8.</summary>
internal static partial class PageDecoder
{
    static PageDecoder() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string Decode(FetchedContent content)
    {
        var encoding =
            FromName(HeaderCharset(content.ContentType))
            ?? FromName(MetaCharset(content.Bytes))
            ?? Encoding.UTF8;
        return encoding.GetString(content.Bytes);
    }

    private static string? HeaderCharset(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var parsed) ? parsed.CharSet : null;

    private static string? MetaCharset(byte[] bytes)
    {
        var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 2048));
        var match = MetaCharsetPattern().Match(head);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static Encoding? FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(name.Trim('"', '\'', ' '));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [GeneratedRegex("""<meta[^>]+charset\s*=\s*["']?([\w-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaCharsetPattern();
}
