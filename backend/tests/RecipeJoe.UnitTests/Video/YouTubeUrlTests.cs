using RecipeJoe.Api.Video;

namespace RecipeJoe.UnitTests.Video;

[TestClass]
public sealed class YouTubeUrlTests
{
    [TestMethod]
    [DataRow("https://www.youtube.com/watch?v=i84Sc5uvQa8", "i84Sc5uvQa8")]
    [DataRow("https://youtube.com/watch?v=i84Sc5uvQa8&t=42s", "i84Sc5uvQa8")]
    [DataRow("https://m.youtube.com/watch?feature=share&v=i84Sc5uvQa8", "i84Sc5uvQa8")]
    [DataRow("http://www.youtube.com/watch?v=i84Sc5uvQa8", "i84Sc5uvQa8")]
    [DataRow("https://youtu.be/i84Sc5uvQa8", "i84Sc5uvQa8")]
    [DataRow("https://youtu.be/i84Sc5uvQa8?si=abc", "i84Sc5uvQa8")]
    [DataRow("https://www.youtube.com/shorts/i84Sc5uvQa8", "i84Sc5uvQa8")]
    [DataRow("https://WWW.YOUTUBE.COM/shorts/i84Sc5uvQa8?feature=share", "i84Sc5uvQa8")]
    public void Video_urls_yield_their_video_id(string url, string expectedId)
    {
        Assert.AreEqual(expectedId, YouTubeUrl.TryGetVideoId(new Uri(url)));
    }

    [TestMethod]
    [DataRow("https://www.youtube.com/playlist?list=PL123")]
    [DataRow("https://www.youtube.com/@channel")]
    [DataRow("https://www.youtube.com/watch")]
    [DataRow("https://www.youtube.com/watch?v=")]
    [DataRow("https://www.youtube.com/shorts/")]
    [DataRow("https://youtu.be/")]
    [DataRow("https://notyoutube.com/watch?v=i84Sc5uvQa8")]
    [DataRow("https://youtube.com.evil.test/watch?v=i84Sc5uvQa8")]
    [DataRow("https://example.test/rezept")]
    public void Anything_else_yields_no_video_id(string url)
    {
        Assert.IsNull(YouTubeUrl.TryGetVideoId(new Uri(url)));
    }
}
