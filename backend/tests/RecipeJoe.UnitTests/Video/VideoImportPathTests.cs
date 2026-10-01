using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeJoe.Api;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Imports;
using RecipeJoe.Api.Video;
using RecipeJoe.UnitTests.Import;
using RecipeJoe.UnitTests.ImportsModule;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class VideoImportPathTests
{
    private static readonly Uri VideoUrl = new("https://www.youtube.com/watch?v=i84Sc5uvQa8");
    private static readonly Uri Thumbnail = new("http://fixtures.test/images/apfelkuchen.jpg");

    private const string TwoRecipes = """
        {"recipes":[
          {"title":"Apfelkuchen","ingredientLines":["Äpfel"],"steps":["Backen"]},
          {"title":"Birnenkuchen","ingredientLines":["Birnen"],"steps":["Backen"]}
        ]}
        """;

    private sealed class StubVideoSource(Result<VideoContent, ImportFailure> result) : IVideoSource
    {
        public Task<Result<VideoContent, ImportFailure>> LoadAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private static VideoImportPath CreatePath(Result<VideoContent, ImportFailure> video, FakeChatClient chat, FixtureFetcher? fetcher = null)
    {
        var policy = new FetchPolicy(fetcher ?? new FixtureFetcher(), Options.Create(new ImportOptions()));
        return new VideoImportPath(
            new StubVideoSource(video),
            new RecipeExtractor(chat, Options.Create(new LlmOptions()), NullLogger<RecipeExtractor>.Instance),
            new ImageDownloader(policy, NullLogger<ImageDownloader>.Instance)
        );
    }

    private static Result<VideoContent, ImportFailure> Video(Uri? thumbnail) =>
        Result<VideoContent, ImportFailure>.Ok(new VideoContent(new VideoText("Kuchen", "", "Transkript"), thumbnail));

    [TestMethod]
    public async Task One_video_becomes_one_draft_per_dish_all_with_the_video_as_Source_and_one_shared_thumbnail()
    {
        var fetcher = new FixtureFetcher();
        var progress = new RecordingProgress<ImportStage>();

        var result = await CreatePath(Video(Thumbnail), FakeChatClient.Replying(TwoRecipes), fetcher).RunAsync(VideoUrl, progress, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(2, result.Value);
        Assert.IsTrue(result.Value.All(d => d.Source == VideoUrl));
        Assert.IsNotNull(result.Value[0].Image);
        Assert.AreSame(result.Value[0].Image, result.Value[1].Image);
        Assert.AreEqual(1, fetcher.Requests.Count(r => r.Url == Thumbnail));
        Assert.IsTrue(progress.Reported.SequenceEqual([ImportStage.Fetching, ImportStage.Extracting]));
    }

    [TestMethod]
    public async Task A_failed_thumbnail_leaves_the_drafts_without_an_image()
    {
        var result = await CreatePath(Video(new Uri("http://fixtures.test/images/gibt-es-nicht.jpg")), FakeChatClient.Replying(TwoRecipes))
            .RunAsync(VideoUrl, new RecordingProgress<ImportStage>(), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(2, result.Value);
        Assert.IsTrue(result.Value.All(d => d.Image is null));
    }

    [TestMethod]
    public async Task A_video_without_a_thumbnail_leaves_the_drafts_without_an_image()
    {
        var result = await CreatePath(Video(null), FakeChatClient.Replying(TwoRecipes))
            .RunAsync(VideoUrl, new RecordingProgress<ImportStage>(), CancellationToken.None);

        Assert.IsTrue(result.Value.All(d => d.Image is null));
    }

    [TestMethod]
    public async Task A_video_source_failure_is_passed_through_before_the_LLM_is_asked()
    {
        var chat = FakeChatClient.Replying(TwoRecipes);
        var progress = new RecordingProgress<ImportStage>();

        var result = await CreatePath(Result<VideoContent, ImportFailure>.Fail(ImportFailure.NoCaptions), chat)
            .RunAsync(VideoUrl, progress, CancellationToken.None);

        Assert.AreEqual(ImportFailure.NoCaptions, result.Failure);
        Assert.IsEmpty(chat.Requests);
        Assert.IsTrue(progress.Reported.SequenceEqual([ImportStage.Fetching]));
    }

    [TestMethod]
    public async Task An_extractor_failure_is_passed_through()
    {
        var result = await CreatePath(Video(Thumbnail), FakeChatClient.Replying("""{"recipes":[]}"""))
            .RunAsync(VideoUrl, new RecordingProgress<ImportStage>(), CancellationToken.None);

        Assert.AreEqual(ImportFailure.NoRecipe, result.Failure);
    }
}
