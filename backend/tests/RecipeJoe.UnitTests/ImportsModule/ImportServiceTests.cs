using System.Threading.Channels;
using NSubstitute;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;

namespace RecipeJoe.UnitTests.ImportsModule;

[TestClass]
public sealed class ImportServiceTests
{
    private static (ImportService Service, ChannelReader<Guid> Queue) CreateService()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UnixEpoch);
        var store = new ImportStore(timeProvider);
        var queue = Channel.CreateUnbounded<Guid>();
        return (new ImportService(store, queue.Writer), queue.Reader);
    }

    [TestMethod]
    [DataRow("not a url")]
    [DataRow("ftp://example.test/rezept")]
    public void Start_rejects_anything_that_is_not_an_absolute_http_url(string url)
    {
        var (service, _) = CreateService();

        var result = service.Start(url);

        Assert.AreEqual(ImportFailure.InvalidUrl, result.Failure);
    }

    [TestMethod]
    public async Task Start_adds_a_Pending_import_and_enqueues_its_id_for_the_runner()
    {
        var (service, queue) = CreateService();

        var result = service.Start("http://site.test/rezept");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ImportStage.Fetching, result.Value.Stage);
        var queued = await queue.ReadAsync();
        Assert.AreEqual(result.Value.Id, queued);
    }

    [TestMethod]
    public void Start_marks_every_import_as_Web_for_now()
    {
        var (service, _) = CreateService();

        Assert.AreEqual(ImportKind.Web, service.Start("http://site.test/rezept").Value.Kind);
    }

    [TestMethod]
    public async Task TryRetry_restarts_a_known_import_and_requeues_it()
    {
        var (service, queue) = CreateService();
        var started = service.Start("http://site.test/rezept").Value;
        await queue.ReadAsync();

        var retried = service.TryRetry(started.Id, out var import);

        Assert.IsTrue(retried);
        Assert.AreEqual(started.Id, import!.Id);
        Assert.AreEqual(ImportStage.Fetching, import.Stage);
        var requeued = await queue.ReadAsync();
        Assert.AreEqual(started.Id, requeued);
    }

    [TestMethod]
    public void TryRetry_returns_false_for_an_unknown_id()
    {
        var (service, _) = CreateService();

        Assert.IsFalse(service.TryRetry(Guid.NewGuid(), out _));
    }

    [TestMethod]
    public async Task TryDismiss_removes_a_known_import_and_returns_false_for_an_unknown_one()
    {
        var (service, queue) = CreateService();
        var started = service.Start("http://site.test/rezept").Value;
        await queue.ReadAsync();

        Assert.IsTrue(service.TryDismiss(started.Id));
        Assert.IsFalse(service.TryDismiss(started.Id));
    }

    [TestMethod]
    public async Task List_returns_started_imports()
    {
        var (service, queue) = CreateService();
        var started = service.Start("http://site.test/rezept").Value;
        await queue.ReadAsync();

        var listed = service.List();

        Assert.HasCount(1, listed);
        Assert.AreEqual(started.Id, listed[0].Id);
    }
}
