using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace RecipeJoe.Api.Import;

/// <summary>Production <see cref="IFetcher"/>: transport only, the <see cref="FetchPolicy"/> judges responses. SSRF protection lives in the handler's connect callback, see <see cref="ConnectGuarded"/>.</summary>
internal sealed class HttpPageFetcher(HttpClient client, IOptions<ImportOptions> options) : IFetcher
{
    internal const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private const int MaxRedirects = 5;

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

    public async Task<Result<RawResponse, ImportFailure>> FetchAsync(Uri url, FetchKind kind, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        try
        {
            return await FetchFollowingRedirectsAsync(url, kind, timeout.Token);
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

    private async Task<Result<RawResponse, ImportFailure>> FetchFollowingRedirectsAsync(Uri url, FetchKind kind, CancellationToken ct)
    {
        for (var redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (kind == FetchKind.Image)
            {
                request.Headers.Accept.ParseAdd("image/*,*/*;q=0.5");
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

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

            return Result<RawResponse, ImportFailure>.Ok(await ReadAsync(response, url, ct));
        }
    }

    /// <summary>Reads the body of every status (the policy inspects error pages too), capped.</summary>
    private async Task<RawResponse> ReadAsync(HttpResponseMessage response, Uri url, CancellationToken ct)
    {
        var body = response.Content.Headers.ContentLength > _options.MaxBytes ? null : await ReadCappedAsync(response.Content, ct);
        var challengeMitigation = response.Headers.TryGetValues("cf-mitigated", out var values) ? string.Join(", ", values) : null;
        return new RawResponse(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            challengeMitigation,
            body,
            url
        );
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

    private static Result<RawResponse, ImportFailure> Fail(ImportFailure failure) =>
        Result<RawResponse, ImportFailure>.Fail(failure);

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
