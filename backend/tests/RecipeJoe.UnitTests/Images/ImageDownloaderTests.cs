using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;
using RecipeJoe.UnitTests.Import;

namespace RecipeJoe.UnitTests.Images;

[TestClass]
public sealed class ImageDownloaderTests
{
    private static readonly Uri PageUrl = new("http://fixtures.test/recipes/apfelkuchen.html");

    private static ImageDownloader Create(IPageFetcher fetcher, int maxBytes = 5 * 1024 * 1024) =>
        new(fetcher, Options.Create(new ImportOptions { MaxBytes = maxBytes }), NullLogger<ImageDownloader>.Instance);

    private static byte[] Padded(byte[] header, int length = 64)
    {
        var bytes = new byte[length];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private static readonly byte[] Jpeg = Padded([0xFF, 0xD8, 0xFF, 0xE0]);
    private static readonly byte[] Png = Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    private static readonly byte[] Gif87 = Padded("GIF87a"u8.ToArray());
    private static readonly byte[] Gif89 = Padded("GIF89a"u8.ToArray());
    private static readonly byte[] Webp = Padded([.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8]);

    private sealed class ImageStub(Result<FetchedContent, ImportFailure> result) : IPageFetcher
    {
        public Uri? Requested { get; private set; }

        public Task<Result<FetchedContent, ImportFailure>> FetchAsync(Uri url, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Images are fetched with FetchImageAsync.");

        public Task<Result<FetchedContent, ImportFailure>> FetchImageAsync(Uri url, CancellationToken cancellationToken)
        {
            Requested = url;
            return Task.FromResult(result);
        }
    }

    private static ImageStub Serving(byte[] bytes, string? contentType = "image/jpeg") =>
        new(Result<FetchedContent, ImportFailure>.Ok(new FetchedContent(bytes, contentType)));

    [TestMethod]
    public async Task Downloads_the_image_from_the_url_and_stores_it_as_is()
    {
        var fetcher = Serving(Jpeg);

        var image = await Create(fetcher).DownloadAsync(new Uri("http://cdn.test/a.jpg"), CancellationToken.None);

        Assert.IsNotNull(image);
        Assert.AreEqual("image/jpeg", image.ContentType);
        CollectionAssert.AreEqual(Jpeg, image.Bytes);
        Assert.AreEqual(new Uri("http://cdn.test/a.jpg"), fetcher.Requested);
    }

    [TestMethod]
    public async Task Downloads_the_fixture_images_of_every_supported_format()
    {
        var expected = new (string Path, string ContentType)[]
        {
            ("apfelkuchen.jpg", "image/jpeg"),
            ("brezeln.png", "image/png"),
            ("flammkuchen.webp", "image/webp"),
            ("fried-rice.gif", "image/gif"),
        };

        foreach (var (path, contentType) in expected)
        {
            var image = await Create(new FixturePageFetcher())
                .DownloadAsync(new Uri($"http://fixtures.test/images/{path}"), CancellationToken.None);

            Assert.AreEqual(contentType, image?.ContentType, path);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task Detects_the_type_by_magic_bytes_whatever_the_server_claims(byte[] bytes, string expectedType)
    {
        var image = await Create(Serving(bytes, "application/octet-stream"))
            .DownloadAsync(new Uri("http://cdn.test/x"), CancellationToken.None);

        Assert.AreEqual(expectedType, image?.ContentType);
    }

    public static IEnumerable<(byte[], string)> Formats =>
        [(Jpeg, "image/jpeg"), (Png, "image/png"), (Gif87, "image/gif"), (Gif89, "image/gif"), (Webp, "image/webp")];

    [TestMethod]
    public async Task Has_no_image_without_a_url_and_does_not_fetch()
    {
        var fetcher = Serving(Jpeg);

        Assert.IsNull(await Create(fetcher).DownloadAsync(null, CancellationToken.None));
        Assert.IsNull(fetcher.Requested);
    }

    [TestMethod]
    public async Task Has_no_image_when_the_download_fails()
    {
        var image = await Create(new FixturePageFetcher())
            .DownloadAsync(new Uri("http://fixtures.test/images/gibt-es-nicht.jpg"), CancellationToken.None);

        Assert.IsNull(image);
    }

    [TestMethod]
    public async Task Has_no_image_when_the_bytes_are_over_the_cap()
    {
        var image = await Create(Serving(Jpeg), maxBytes: Jpeg.Length - 1)
            .DownloadAsync(new Uri("http://cdn.test/a.jpg"), CancellationToken.None);

        Assert.IsNull(image);
    }

    [TestMethod]
    public async Task Keeps_an_image_exactly_at_the_cap()
    {
        var image = await Create(Serving(Jpeg), maxBytes: Jpeg.Length)
            .DownloadAsync(new Uri("http://cdn.test/a.jpg"), CancellationToken.None);

        Assert.IsNotNull(image);
    }

    [TestMethod]
    [DataRow("<html><body>Not an image</body></html>")]
    [DataRow("")]
    [DataRow("GIF")]
    [DataRow("RIFF\0\0\0\0WAVE")]
    public async Task Has_no_image_when_the_magic_bytes_are_wrong_even_if_the_server_says_image(string body)
    {
        var image = await Create(Serving(System.Text.Encoding.ASCII.GetBytes(body), "image/jpeg"))
            .DownloadAsync(new Uri("http://cdn.test/a.jpg"), CancellationToken.None);

        Assert.IsNull(image);
    }
}
