using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace RecipeJoe.Api.Import;

/// <summary>Production <see cref="IPageFetcher"/>. SSRF protection lives in the handler's connect callback, see <see cref="ConnectGuarded"/>.</summary>
internal sealed class HttpPageFetcher(HttpClient client, IOptions<ImportOptions> options) : IPageFetcher
{
    internal const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private const int MaxRedirects = 5;

    private static readonly string[] HtmlMediaTypes = ["text/html", "application/xhtml+xml"];

    private sealed class ForbiddenAddressException : Exception;

    private readonly ImportOptions _options = options.Value;

    public static SocketsHttpHandler CreateHandler(ImportOptions options) =>
        new()
        {
            AllowAutoRedirect = false,
            UseProxy = false, // the connect callback must see the target's IP, not a proxy's
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = (context, ct) => ConnectGuarded(options, context, ct),
        };

    public static void ConfigureClient(HttpClient client)
    {
        client.Timeout = Timeout.InfiniteTimeSpan; // total timeout is enforced per fetch, across redirects
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE,de;q=0.9,en;q=0.8");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, br");
    }

    public async Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            return await FetchFollowingRedirectsAsync(url, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(ImportFailure.Unreachable); // our timeout, not the caller's cancellation
        }
        catch (HttpRequestException ex) when (IsForbiddenAddress(ex))
        {
            return Fail(ImportFailure.ForbiddenAddress);
        }
        catch (HttpRequestException)
        {
            return Fail(ImportFailure.Unreachable); // DNS, connection, TLS
        }
    }

    private async Task<Result<FetchedContent, ImportFailure>> FetchFollowingRedirectsAsync(Uri url, CancellationToken ct)
    {
        for (var redirects = 0; ; redirects++)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (IsRedirect(response.StatusCode))
            {
                if (redirects == MaxRedirects || response.Headers.Location is not { } location)
                {
                    return Fail(ImportFailure.BadResponse);
                }

                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
                {
                    return Fail(ImportFailure.BadResponse);
                }

                continue;
            }

            return await ReadAsync(response, ct);
        }
    }

    private async Task<Result<FetchedContent, ImportFailure>> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (IsChallenge(response))
        {
            return Fail(ImportFailure.Blocked);
        }

        switch ((int)response.StatusCode)
        {
            case 401 or 402 or 403 or 429:
                return Fail(ImportFailure.Blocked);
            case 404 or 410:
                return Fail(ImportFailure.NotFound);
            case 503 when await IsChallengePageAsync(response, ct):
                return Fail(ImportFailure.Blocked);
            case < 200 or >= 300:
                return Fail(ImportFailure.BadResponse);
        }

        var contentType = response.Content.Headers.ContentType;
        var mediaType = contentType?.MediaType;
        if (contentType is null || mediaType is null || !HtmlMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return Fail(ImportFailure.BadResponse);
        }

        if (response.Content.Headers.ContentLength > _options.MaxBytes)
        {
            return Fail(ImportFailure.BadResponse);
        }

        var bytes = await ReadCappedAsync(response.Content, ct);
        if (bytes is null)
        {
            return Fail(ImportFailure.BadResponse);
        }

        return ContainsChallengeMarker(bytes)
            ? Fail(ImportFailure.Blocked)
            : Result<FetchedContent, ImportFailure>.Ok(new FetchedContent(bytes, contentType.ToString()));
    }

    /// <summary>Streams the body; null when it exceeds the cap (Content-Length can be absent or a lie).</summary>
    private async Task<byte[]?> ReadCappedAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > _options.MaxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private async Task<bool> IsChallengePageAsync(HttpResponseMessage response, CancellationToken ct) =>
        await ReadCappedAsync(response.Content, ct) is { } body && ContainsChallengeMarker(body);

    private static bool IsChallenge(HttpResponseMessage response) =>
        response.Headers.TryGetValues("cf-mitigated", out var values)
        && values.Contains("challenge", StringComparer.OrdinalIgnoreCase);

    /// <summary>Cloudflare's interstitial is titled "Just a moment..."; it may come with a 200 or 503.</summary>
    private static bool ContainsChallengeMarker(byte[] html) =>
        Encoding.UTF8.GetString(html, 0, Math.Min(html.Length, 4096))
            .Contains("<title>Just a moment", StringComparison.OrdinalIgnoreCase);

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static bool IsForbiddenAddress(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is ForbiddenAddressException)
            {
                return true;
            }
        }

        return false;
    }

    private static Result<FetchedContent, ImportFailure> Fail(ImportFailure failure) =>
        Result<FetchedContent, ImportFailure>.Fail(failure);

    /// <summary>Connects like the default handler, but refuses non-public IPs unless the host is allowlisted. Checks the address actually connected to, so it covers redirects and DNS rebinding.</summary>
    private static async ValueTask<Stream> ConnectGuarded(
        ImportOptions options,
        SocketsHttpConnectionContext context,
        CancellationToken ct
    )
    {
        var host = context.DnsEndPoint.Host;
        var allowed = options.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        Exception? last = null;
        foreach (var address in addresses)
        {
            if (!allowed && !IpClassifier.IsPublic(address))
            {
                last = new ForbiddenAddressException();
                continue;
            }

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                socket.Dispose();
                last = ex;
            }
        }

        throw last ?? new SocketException((int)SocketError.HostNotFound);
    }
}
