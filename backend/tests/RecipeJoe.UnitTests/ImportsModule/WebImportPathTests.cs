using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;
using RecipeJoe.UnitTests.Import;

namespace RecipeJoe.UnitTests.ImportsModule;

[TestClass]
public sealed class WebImportPathTests
{
    private static readonly Uri Apfelkuchen = new("http://fixtures.test/recipes/apfelkuchen.html");

    private static WebImportPath CreatePath(IFetcher fetcher)
    {
        var policy = new FetchPolicy(fetcher, Options.Create(new ImportOptions()));
        var importer = new Importer(policy, new ImageDownloader(policy, NullLogger<ImageDownloader>.Instance));
        return new WebImportPath(importer);
    }

    [TestMethod]
    public async Task Reports_Fetching_then_Extracting_and_returns_one_draft_on_success()
    {
        var progress = new RecordingProgress<ImportStage>();

        var result = await CreatePath(new FixtureFetcher()).RunAsync(Apfelkuchen, progress, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(1, result.Value);
        Assert.AreEqual("Einfacher Apfelkuchen", result.Value[0].Title);
        CollectionAssert.AreEqual(new[] { ImportStage.Fetching, ImportStage.Extracting }, progress.Reported.ToArray());
    }

    [TestMethod]
    public async Task Reports_only_Fetching_and_passes_the_failure_through_when_the_page_cannot_be_imported()
    {
        var progress = new RecordingProgress<ImportStage>();

        var result = await CreatePath(new FixtureFetcher())
            .RunAsync(new Uri("http://fixtures.test/recipes/gibt-es-nicht.html"), progress, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(ImportFailure.NotFound, result.Failure);
        CollectionAssert.AreEqual(new[] { ImportStage.Fetching }, progress.Reported.ToArray());
    }
}
