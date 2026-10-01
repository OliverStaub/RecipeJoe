using System.Net;
using RecipeJoe.Api.Import;
using RecipeJoe.Api.Video;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.ClosedCaptions;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class YouTubeCaptionsTests
{
    private static ClosedCaptionTrackInfo Track(string language, bool auto) =>
        new($"https://x.test/{language}-{auto}", new Language(language, language), auto);

    [TestMethod]
    public void A_manual_track_beats_an_auto_generated_one_even_when_listed_later()
    {
        var chosen = YouTubeCaptions.ChooseTrack([Track("en", auto: true), Track("de-DE", auto: false), Track("ja", auto: false)]);

        Assert.AreEqual("de-DE", chosen?.Language.Code);
    }

    [TestMethod]
    public void Without_manual_tracks_the_first_auto_track_is_the_original_language()
    {
        var chosen = YouTubeCaptions.ChooseTrack([Track("en", auto: true), Track("de", auto: true)]);

        Assert.AreEqual("en", chosen?.Language.Code);
    }

    [TestMethod]
    public void No_tracks_choose_nothing()
    {
        Assert.IsNull(YouTubeCaptions.ChooseTrack([]));
    }

    [TestMethod]
    public void Caption_texts_are_joined_by_lines_skipping_blanks()
    {
        Assert.AreEqual("Hallo\nWelt", YouTubeCaptions.ToTranscript(["Hallo", "  ", "Welt\n"]));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Forbidden, "Blocked")]
    [DataRow(HttpStatusCode.TooManyRequests, "Blocked")]
    [DataRow(HttpStatusCode.InternalServerError, "Unreachable")]
    [DataRow(null, "Unreachable")]
    public void Http_errors_map_to_blocked_or_unreachable(HttpStatusCode? status, string expected)
    {
        Assert.AreEqual(Enum.Parse<ImportFailure>(expected), YouTubeCaptions.MapFailure(new HttpRequestException("x", null, status)));
    }

    [TestMethod]
    public void Unavailable_unplayable_and_paid_videos_are_not_found()
    {
        Assert.AreEqual(ImportFailure.NotFound, YouTubeCaptions.MapFailure(new VideoUnavailableException("gone")));
        Assert.AreEqual(ImportFailure.NotFound, YouTubeCaptions.MapFailure(new VideoUnplayableException("private")));
        Assert.AreEqual(ImportFailure.NotFound, YouTubeCaptions.MapFailure(new VideoRequiresPurchaseException("paid", "i84Sc5uvQa8")));
    }

    [TestMethod]
    public void Rate_limiting_is_blocked()
    {
        Assert.AreEqual(ImportFailure.Blocked, YouTubeCaptions.MapFailure(new RequestLimitExceededException("slow down")));
    }

    [TestMethod]
    public void Other_library_errors_are_bad_responses()
    {
        Assert.AreEqual(ImportFailure.BadResponse, YouTubeCaptions.MapFailure(new YoutubeExplodeException("parse")));
    }

    [TestMethod]
    public void Unrelated_exceptions_are_not_mapped()
    {
        Assert.IsNull(YouTubeCaptions.MapFailure(new InvalidOperationException()));
    }
}
