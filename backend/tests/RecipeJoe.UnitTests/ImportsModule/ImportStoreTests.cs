using NSubstitute;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;

namespace RecipeJoe.UnitTests.ImportsModule;

[TestClass]
public sealed class ImportStoreTests
{
    private static readonly Uri Url = new("http://site.test/rezept");

    private static ImportStore CreateStore()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UnixEpoch);
        return new ImportStore(timeProvider);
    }

    [TestMethod]
    public void Start_adds_a_Pending_Fetching_import()
    {
        var store = CreateStore();

        var import = store.Start(Url, ImportKind.Web);

        Assert.AreEqual(Url, import.Url);
        Assert.AreEqual(ImportKind.Web, import.Kind);
        Assert.AreEqual(ImportStage.Fetching, import.Stage);
        Assert.IsNull(import.Failure);
        Assert.IsFalse(import.IsFailed);
    }

    [TestMethod]
    public void SetStage_moves_a_pending_import_to_the_given_stage()
    {
        var store = CreateStore();
        var import = store.Start(Url, ImportKind.Web);

        store.SetStage(import.Id, ImportStage.Saving);

        Assert.IsTrue(store.TryGet(import.Id, out var found));
        Assert.AreEqual(ImportStage.Saving, found!.Stage);
    }

    [TestMethod]
    public void Fail_moves_a_pending_import_to_Failed_and_clears_its_stage()
    {
        var store = CreateStore();
        var import = store.Start(Url, ImportKind.Web);

        store.Fail(import.Id, ImportFailure.NoRecipe);

        Assert.IsTrue(store.TryGet(import.Id, out var found));
        Assert.IsTrue(found!.IsFailed);
        Assert.AreEqual(ImportFailure.NoRecipe, found.Failure);
        Assert.IsNull(found.Stage);
    }

    [TestMethod]
    public void TryRestart_puts_a_Failed_import_back_to_Pending_Fetching_with_the_same_id()
    {
        var store = CreateStore();
        var import = store.Start(Url, ImportKind.Web);
        store.Fail(import.Id, ImportFailure.NoRecipe);

        var restarted = store.TryRestart(import.Id, out var found);

        Assert.IsTrue(restarted);
        Assert.AreEqual(import.Id, found!.Id);
        Assert.AreEqual(ImportStage.Fetching, found.Stage);
        Assert.IsNull(found.Failure);
    }

    [TestMethod]
    public void TryRestart_returns_false_for_an_unknown_id()
    {
        Assert.IsFalse(CreateStore().TryRestart(Guid.NewGuid(), out _));
    }

    [TestMethod]
    public void Remove_deletes_the_import_and_reports_whether_it_existed()
    {
        var store = CreateStore();
        var import = store.Start(Url, ImportKind.Web);

        Assert.IsTrue(store.Remove(import.Id));
        Assert.IsFalse(store.TryGet(import.Id, out _));
        Assert.IsFalse(store.Remove(import.Id));
    }

    [TestMethod]
    public void SetStage_and_Fail_are_no_ops_once_the_import_is_gone()
    {
        var store = CreateStore();
        var import = store.Start(Url, ImportKind.Web);
        store.Remove(import.Id);

        store.SetStage(import.Id, ImportStage.Saving);
        store.Fail(import.Id, ImportFailure.NoRecipe);

        Assert.IsFalse(store.TryGet(import.Id, out _));
    }

    [TestMethod]
    public void List_returns_every_import_oldest_first()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1));
        var store = new ImportStore(timeProvider);
        var first = store.Start(Url, ImportKind.Web);
        var second = store.Start(new Uri("http://site.test/anderes-rezept"), ImportKind.Web);

        var listed = store.List();

        CollectionAssert.AreEqual(new[] { first.Id, second.Id }, listed.Select(i => i.Id).ToArray());
    }
}
