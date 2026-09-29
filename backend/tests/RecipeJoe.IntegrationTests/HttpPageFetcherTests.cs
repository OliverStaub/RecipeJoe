using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeJoe.Api.Import;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace RecipeJoe.IntegrationTests;

/// <summary>The real HttpClient adapter, wired through AddImport(), against WireMock on loopback.</summary>
[TestClass]
public sealed class HttpPageFetcherTests
{
    private WireMockServer _server = null!;

    [TestInitialize]
    public void Start() => _server = WireMockServer.Start();

    [TestCleanup]
    public void Stop() => _server.Dispose();

    private Uri Url(string path = "/page") => new($"{_server.Url}{path}");

    private static IPageFetcher CreateFetcher(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(config)
            .AddImport()
            .BuildServiceProvider()
            .GetRequiredService<IPageFetcher>();
    }

    private static IPageFetcher AllowLoopback(params (string Key, string Value)[] extra) =>
        CreateFetcher([("Import:AllowedHosts:0", "localhost"), ("Import:AllowedHosts:1", "127.0.0.1"), .. extra]);

    private void Respond(string path, IResponseBuilder response) =>
        _server.Given(Request.Create().WithPath(path)).RespondWith(response);

    private static IResponseBuilder Html(string body = "<html><body>ok</body></html>") =>
        Response.Create().WithStatusCode(200).WithHeader("Content-Type", "text/html; charset=utf-8").WithBody(body);

    [TestMethod]
    public async Task Fetches_html_and_sends_browser_headers()
    {
        Respond("/page", Html("<p>hello</p>"));

        var result = await AllowLoopback().FetchAsync(Url(), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("<p>hello</p>", Encoding.UTF8.GetString(result.Value.Bytes));
        Assert.AreEqual("text/html; charset=utf-8", result.Value.ContentType);

        var headers = _server.LogEntries.Single().RequestMessage!.Headers!;
        StringAssert.Contains(headers["User-Agent"].Single(), "Chrome/");
        Assert.AreEqual("de-DE, de; q=0.9, en; q=0.8", headers["Accept-Language"].Single());
        StringAssert.Contains(headers["Accept-Encoding"].Single(), "br");
    }

    [TestMethod]
    public async Task Fetches_an_image_of_any_content_type_asking_for_images()
    {
        Respond("/pic", Response.Create().WithHeader("Content-Type", "image/png").WithBody([0x89, 0x50, 0x4E, 0x47]));

        var result = await AllowLoopback().FetchImageAsync(Url("/pic"), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, result.Value.Bytes);
        Assert.AreEqual("image/png", result.Value.ContentType);
        StringAssert.StartsWith(_server.LogEntries.Single().RequestMessage!.Headers!["Accept"].Single(), "image/*");
    }

    [TestMethod]
    public async Task An_image_without_a_content_type_is_left_to_the_caller_to_judge()
    {
        Respond("/pic", Response.Create().WithBody([1, 2, 3]));

        var result = await AllowLoopback().FetchImageAsync(Url("/pic"), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task An_image_over_the_cap_is_a_bad_response()
    {
        Respond("/pic", Response.Create().WithHeader("Content-Type", "image/jpeg").WithBody(new byte[2048]));

        var result = await AllowLoopback(("Import:MaxBytes", "1024")).FetchImageAsync(Url("/pic"), CancellationToken.None);

        Assert.AreEqual(ImportFailure.BadResponse, result.Failure);
    }

    [TestMethod]
    public async Task A_missing_image_is_NotFound()
    {
        var result = await AllowLoopback().FetchImageAsync(Url("/nope"), CancellationToken.None);

        Assert.AreEqual(ImportFailure.NotFound, result.Failure);
    }

    [TestMethod]
    public async Task Images_get_the_same_address_guard_as_pages()
    {
        Respond("/pic", Response.Create().WithHeader("Content-Type", "image/jpeg").WithBody([0xFF, 0xD8, 0xFF]));

        var result = await CreateFetcher().FetchImageAsync(Url("/pic"), CancellationToken.None);

        Assert.AreEqual(ImportFailure.ForbiddenAddress, result.Failure);
    }

    [TestMethod]
    public async Task Accepts_xhtml()
    {
        Respond("/page", Response.Create().WithHeader("Content-Type", "application/xhtml+xml").WithBody("<html/>"));

        Assert.IsTrue((await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).IsSuccess);
    }

    [TestMethod]
    public async Task Decompresses_gzip()
    {
        Respond("/page", Html("<p>zipped</p>").WithHeader("Content-Encoding", "gzip").WithBody(Gzip("<p>zipped</p>")));

        var result = await AllowLoopback().FetchAsync(Url(), CancellationToken.None);

        Assert.AreEqual("<p>zipped</p>", Encoding.UTF8.GetString(result.Value.Bytes));
    }

    [TestMethod]
    [DataRow(401, "Blocked")]
    [DataRow(402, "Blocked")]
    [DataRow(403, "Blocked")]
    [DataRow(429, "Blocked")]
    [DataRow(404, "NotFound")]
    [DataRow(410, "NotFound")]
    [DataRow(500, "BadResponse")]
    [DataRow(503, "BadResponse")]
    [DataRow(400, "BadResponse")]
    public async Task Maps_status_codes(int status, string expectedKind)
    {
        Respond("/page", Response.Create().WithStatusCode(status).WithHeader("Content-Type", "text/html").WithBody("x"));

        var result = await AllowLoopback().FetchAsync(Url(), CancellationToken.None);

        Assert.AreEqual(Enum.Parse<ImportFailure>(expectedKind), result.Failure);
    }

    [TestMethod]
    public async Task Cloudflare_challenge_header_is_blocked()
    {
        Respond("/page", Html().WithHeader("cf-mitigated", "challenge"));

        Assert.AreEqual(ImportFailure.Blocked, (await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).Failure);
    }

    [TestMethod]
    public async Task Just_a_moment_page_is_blocked()
    {
        Respond("/page", Html("<html><head><title>Just a moment...</title></head></html>"));

        Assert.AreEqual(ImportFailure.Blocked, (await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).Failure);
    }

    [TestMethod]
    public async Task Just_a_moment_page_with_503_is_blocked()
    {
        Respond(
            "/page",
            Response.Create()
                .WithStatusCode(503)
                .WithHeader("Content-Type", "text/html")
                .WithBody("<html><head><title>Just a moment...</title></head></html>")
        );

        Assert.AreEqual(ImportFailure.Blocked, (await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).Failure);
    }

    [TestMethod]
    public async Task Non_html_is_a_bad_response()
    {
        Respond("/page", Response.Create().WithHeader("Content-Type", "application/json").WithBody("{}"));

        Assert.AreEqual(ImportFailure.BadResponse, (await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).Failure);
    }

    [TestMethod]
    public async Task Follows_relative_and_absolute_redirects()
    {
        Respond("/start", Response.Create().WithStatusCode(301).WithHeader("Location", "/middle"));
        Respond("/middle", Response.Create().WithStatusCode(302).WithHeader("Location", Url("/page").ToString()));
        Respond("/page", Html("<p>arrived</p>"));

        var result = await AllowLoopback().FetchAsync(Url("/start"), CancellationToken.None);

        Assert.AreEqual("<p>arrived</p>", Encoding.UTF8.GetString(result.Value.Bytes));
    }

    [TestMethod]
    public async Task Gives_up_after_five_redirects()
    {
        for (var i = 0; i < 7; i++)
        {
            Respond($"/r{i}", Response.Create().WithStatusCode(302).WithHeader("Location", $"/r{i + 1}"));
        }

        var result = await AllowLoopback().FetchAsync(Url("/r0"), CancellationToken.None);

        Assert.AreEqual(ImportFailure.BadResponse, result.Failure);
    }

    [TestMethod]
    public async Task Allows_exactly_five_redirects()
    {
        for (var i = 0; i < 5; i++)
        {
            Respond($"/r{i}", Response.Create().WithStatusCode(302).WithHeader("Location", $"/r{i + 1}"));
        }

        Respond("/r5", Html());

        Assert.IsTrue((await AllowLoopback().FetchAsync(Url("/r0"), CancellationToken.None)).IsSuccess);
    }

    [TestMethod]
    public async Task Redirect_to_a_non_http_scheme_is_a_bad_response()
    {
        Respond("/page", Response.Create().WithStatusCode(302).WithHeader("Location", "ftp://example.com/x"));

        Assert.AreEqual(ImportFailure.BadResponse, (await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).Failure);
    }

    [TestMethod]
    public async Task Redirect_without_location_is_a_bad_response()
    {
        Respond("/page", Response.Create().WithStatusCode(302));

        Assert.AreEqual(ImportFailure.BadResponse, (await AllowLoopback().FetchAsync(Url(), CancellationToken.None)).Failure);
    }

    [TestMethod]
    public async Task Body_over_the_cap_is_a_bad_response()
    {
        Respond("/page", Html(new string('a', 2000)));

        var result = await AllowLoopback(("Import:MaxBytes", "1000")).FetchAsync(Url(), CancellationToken.None);

        Assert.AreEqual(ImportFailure.BadResponse, result.Failure);
    }

    [TestMethod]
    public async Task Chunked_body_over_the_cap_is_a_bad_response()
    {
        // No Content-Length: only the streamed count can catch it.
        Respond(
            "/page",
            Response.Create()
                .WithHeader("Content-Type", "text/html")
                .WithHeader("Transfer-Encoding", "chunked")
                .WithBody(new string('a', 2000))
        );

        var result = await AllowLoopback(("Import:MaxBytes", "1000")).FetchAsync(Url(), CancellationToken.None);

        Assert.AreEqual(ImportFailure.BadResponse, result.Failure);
    }

    [TestMethod]
    public async Task Slow_response_times_out_as_unreachable()
    {
        Respond("/page", Html().WithDelay(TimeSpan.FromSeconds(5)));

        var result = await AllowLoopback(("Import:Timeout", "00:00:00.300")).FetchAsync(Url(), CancellationToken.None);

        Assert.AreEqual(ImportFailure.Unreachable, result.Failure);
    }

    [TestMethod]
    public async Task Caller_cancellation_propagates()
    {
        Respond("/page", Html().WithDelay(TimeSpan.FromSeconds(5)));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<TaskCanceledException>(() => AllowLoopback().FetchAsync(Url(), cts.Token));
    }

    [TestMethod]
    public async Task Refused_connection_is_unreachable()
    {
        var closed = new Uri($"http://127.0.0.1:{_server.Ports[0]}/");
        _server.Stop();

        var result = await AllowLoopback().FetchAsync(closed, CancellationToken.None);

        Assert.AreEqual(ImportFailure.Unreachable, result.Failure);
    }

    [TestMethod]
    public async Task Unknown_host_is_unreachable()
    {
        var result = await AllowLoopback().FetchAsync(new Uri("http://no-such-host.invalid/"), CancellationToken.None);

        Assert.AreEqual(ImportFailure.Unreachable, result.Failure);
    }

    [TestMethod]
    public async Task Loopback_without_the_allowlist_is_forbidden()
    {
        Respond("/page", Html());

        var result = await CreateFetcher().FetchAsync(Url(), CancellationToken.None);

        Assert.AreEqual(ImportFailure.ForbiddenAddress, result.Failure);
        Assert.IsEmpty(_server.LogEntries, "the request must never reach the server");
    }

    [TestMethod]
    public async Task Redirect_to_a_private_address_is_forbidden()
    {
        // Only 127.0.0.1 is allowlisted; the redirect target "localhost" resolves to loopback but is not.
        var target = new UriBuilder(Url()) { Host = "localhost" }.Uri;
        Respond("/start", Response.Create().WithStatusCode(302).WithHeader("Location", target.ToString()));
        Respond("/page", Html());
        var fetcher = CreateFetcher(("Import:AllowedHosts:0", "127.0.0.1"));

        var result = await fetcher.FetchAsync(Url("/start"), CancellationToken.None);

        Assert.AreEqual(ImportFailure.ForbiddenAddress, result.Failure);
    }

    private static byte[] Gzip(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }

        return output.ToArray();
    }
}
